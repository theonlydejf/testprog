using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Newtonsoft.Json.Linq;

namespace testprog.server;

internal static class RandomInputGeneratorCompiler
{
    private static readonly ConcurrentDictionary<string, Func<Random, int, JObject>> Cache =
        new(StringComparer.Ordinal);

    public static Func<Random, int, JObject> GetOrCreateGenerator(SourceFileRandomInputGeneratorDefinition definition)
    {
        string fullPath = Path.GetFullPath(definition.SourceFilePath);
        string key = $"{fullPath}|{definition.TypeName}|{definition.MethodName}";

        return Cache.GetOrAdd(key, _ => CompileGenerator(fullPath, definition.TypeName, definition.MethodName));
    }

    private static Func<Random, int, JObject> CompileGenerator(string sourceFilePath, string typeName, string methodName)
    {
        if (!File.Exists(sourceFilePath))
        {
            throw new ArgumentException($"Random input generator source file was not found: '{sourceFilePath}'.");
        }

        string source = File.ReadAllText(sourceFilePath);
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);

        string assemblyName = $"testprog_random_generator_{Guid.NewGuid():N}";
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { syntaxTree },
            BuildMetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using MemoryStream stream = new();
        EmitResult emitResult = compilation.Emit(stream);
        if (!emitResult.Success)
        {
            string diagnostics = string.Join(
                Environment.NewLine,
                emitResult.Diagnostics
                    .Where(static d => d.Severity == DiagnosticSeverity.Error)
                    .Select(static d => d.ToString()));

            throw new ArgumentException(
                $"Random input generator compile failed for '{sourceFilePath}':{Environment.NewLine}{diagnostics}");
        }

        stream.Position = 0;
        Assembly assembly = Assembly.Load(stream.ToArray());

        Type? type = assembly.GetType(typeName, throwOnError: false, ignoreCase: false);
        if (type is null)
        {
            throw new ArgumentException(
                $"Random input generator type '{typeName}' was not found in '{sourceFilePath}'.");
        }

        MethodInfo? method = type.GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(Random), typeof(int) },
            modifiers: null);

        if (method is null)
        {
            throw new ArgumentException(
                $"Random input generator method '{typeName}.{methodName}(Random,int)' was not found in '{sourceFilePath}'.");
        }

        if (method.ReturnType == typeof(void))
        {
            throw new ArgumentException(
                $"Random input generator method '{typeName}.{methodName}' cannot return void.");
        }

        return (random, testcaseIndex) =>
        {
            object? rawResult = method.Invoke(null, new object?[] { random, testcaseIndex });
            if (rawResult is null)
            {
                throw new InvalidOperationException(
                    $"Random input generator '{typeName}.{methodName}' returned null for testcase index {testcaseIndex}.");
            }

            if (rawResult is JObject objectResult)
            {
                return (JObject)objectResult.DeepClone();
            }

            if (rawResult is JToken tokenResult)
            {
                if (tokenResult is not JObject objectToken)
                {
                    throw new InvalidOperationException(
                        $"Random input generator '{typeName}.{methodName}' must return a JSON object.");
                }

                return (JObject)objectToken.DeepClone();
            }

            JToken converted = JToken.FromObject(rawResult);
            if (converted is not JObject convertedObject)
            {
                throw new InvalidOperationException(
                    $"Random input generator '{typeName}.{methodName}' must return a JSON object.");
            }

            return (JObject)convertedObject.DeepClone();
        };
    }

    private static IReadOnlyList<MetadataReference> BuildMetadataReferences()
    {
        Dictionary<string, MetadataReference> references = new(StringComparer.OrdinalIgnoreCase);

        string? trustedAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (!string.IsNullOrWhiteSpace(trustedAssemblies))
        {
            foreach (string path in trustedAssemblies.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!references.ContainsKey(path))
                {
                    references[path] = MetadataReference.CreateFromFile(path);
                }
            }
        }

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic || string.IsNullOrWhiteSpace(assembly.Location))
            {
                continue;
            }

            if (!references.ContainsKey(assembly.Location))
            {
                references[assembly.Location] = MetadataReference.CreateFromFile(assembly.Location);
            }
        }

        return references.Values.ToArray();
    }
}
