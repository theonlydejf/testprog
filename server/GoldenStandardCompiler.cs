using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Newtonsoft.Json.Linq;

namespace testprog.server;

internal static class GoldenStandardCompiler
{
    private static readonly ConcurrentDictionary<string, Func<JObject, JToken>> Cache =
        new(StringComparer.Ordinal);

    public static Func<JObject, JToken> GetOrCreateEvaluator(GoldenStandardDefinition definition)
    {
        string fullPath = Path.GetFullPath(definition.SourceFilePath);
        string key = $"{fullPath}|{definition.TypeName}|{definition.MethodName}";

        return Cache.GetOrAdd(key, _ => CompileEvaluator(fullPath, definition.TypeName, definition.MethodName));
    }

    private static Func<JObject, JToken> CompileEvaluator(string sourceFilePath, string typeName, string methodName)
    {
        if (!File.Exists(sourceFilePath))
        {
            throw new ArgumentException($"Golden standard source file was not found: '{sourceFilePath}'.");
        }

        string source = File.ReadAllText(sourceFilePath);
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);

        string assemblyName = $"testprog_golden_{Guid.NewGuid():N}";
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
                $"Golden standard compile failed for '{sourceFilePath}':{Environment.NewLine}{diagnostics}");
        }

        stream.Position = 0;
        Assembly assembly = Assembly.Load(stream.ToArray());

        Type? type = assembly.GetType(typeName, throwOnError: false, ignoreCase: false);
        if (type is null)
        {
            throw new ArgumentException(
                $"Golden standard type '{typeName}' was not found in '{sourceFilePath}'.");
        }

        MethodInfo? method = type.GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(JObject) },
            modifiers: null);

        if (method is null)
        {
            throw new ArgumentException(
                $"Golden standard method '{typeName}.{methodName}(JObject)' was not found in '{sourceFilePath}'.");
        }

        if (method.ReturnType == typeof(void))
        {
            throw new ArgumentException(
                $"Golden standard method '{typeName}.{methodName}' cannot return void.");
        }

        return input =>
        {
            object? rawResult = method.Invoke(null, new object?[] { (JObject)input.DeepClone() });
            if (rawResult is null)
            {
                return JValue.CreateNull();
            }

            if (rawResult is JToken token)
            {
                return token.DeepClone();
            }

            return JToken.FromObject(rawResult);
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
