using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace testprog.server;

/// <summary>
/// Result of parsing a server configuration document.
/// </summary>
public sealed class LoadedServerConfiguration
{
    /// <summary>
    /// Parsed and validated server options.
    /// </summary>
    public required TestServerOptions ServerOptions { get; init; }

    /// <summary>
    /// Parsed and validated test suite definition.
    /// </summary>
    public required TestSuiteDefinition Suite { get; init; }
}

/// <summary>
/// Loads and validates server configuration from JSON sources.
/// </summary>
public static class TestServerConfigurationLoader
{
    /// <summary>
    /// Loads configuration from a JSON file path.
    /// </summary>
    /// <param name="path">Path to configuration JSON file.</param>
    /// <returns>Parsed and validated configuration object.</returns>
    public static LoadedServerConfiguration LoadFromFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Config file path is required.", nameof(path));
        }

        string json = File.ReadAllText(path);
        string? baseDirectory = Path.GetDirectoryName(Path.GetFullPath(path));
        return LoadFromJson(json, baseDirectory);
    }

    /// <summary>
    /// Loads configuration directly from JSON text.
    /// </summary>
    /// <param name="json">Configuration JSON.</param>
    /// <returns>Parsed and validated configuration object.</returns>
    public static LoadedServerConfiguration LoadFromJson(string json)
    {
        return LoadFromJson(json, baseDirectory: null);
    }

    private static LoadedServerConfiguration LoadFromJson(string json, string? baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("Config JSON cannot be empty.", nameof(json));
        }

        JObject root;
        try
        {
            root = JObject.Parse(json);
        }
        catch (JsonReaderException ex)
        {
            throw new ArgumentException("Config JSON is invalid.", nameof(json), ex);
        }

        JObject serverNode = GetRequiredObject(root, "server");
        JObject suiteNode = GetRequiredObject(root, "suite");

        TestServerOptions options = ParseServerOptions(serverNode);
        TestSuiteDefinition suite = ParseSuite(suiteNode, baseDirectory);

        options.Validate();
        suite.Validate();

        return new LoadedServerConfiguration
        {
            ServerOptions = options,
            Suite = suite
        };
    }

    private static TestServerOptions ParseServerOptions(JObject node)
    {
        return new TestServerOptions
        {
            ServerId = GetOptionalString(node, "serverId", Environment.MachineName),
            AdvertiseHost = GetOptionalString(node, "advertiseHost", string.Empty),
            DiscoveryMulticastAddress = GetOptionalString(node, "discoveryMulticastAddress", "239.0.0.222"),
            DiscoveryPort = GetOptionalInt(node, "discoveryPort", 11000),
            TcpPort = GetOptionalInt(node, "tcpPort", 5000),
            MaxConcurrentSessions = GetOptionalInt(node, "maxConcurrentSessions", 32),
            ClientResponseTimeout = TimeSpan.FromSeconds(
                GetOptionalDouble(node, "clientResponseTimeoutSeconds", 10)),
            StudentIdWhitelist = GetOptionalStringArray(node, "studentIdWhitelist")
        };
    }

    private static TestSuiteDefinition ParseSuite(JObject node, string? baseDirectory)
    {
        JArray groupsArray = GetRequiredArray(node, "groups");
        List<TestGroupDefinition> groups = new(groupsArray.Count);

        foreach (JToken groupToken in groupsArray)
        {
            if (groupToken is not JObject groupNode)
            {
                throw new ArgumentException("Each group must be a JSON object.");
            }

            groups.Add(ParseGroup(groupNode, baseDirectory));
        }

        return new TestSuiteDefinition
        {
            Groups = groups
        };
    }

    private static TestGroupDefinition ParseGroup(JObject node, string? baseDirectory)
    {
        bool hasTestCases = node.TryGetValue("testcases", StringComparison.Ordinal, out JToken? testcasesToken);
        bool hasRandom = node.TryGetValue("random", StringComparison.Ordinal, out JToken? randomToken);

        if (hasTestCases == hasRandom)
        {
            throw new ArgumentException(
                "Each group must define exactly one source: 'testcases' or 'random'.");
        }

        IReadOnlyList<TestCaseDefinition> testcases = Array.Empty<TestCaseDefinition>();
        RandomTestGroupDefinition? randomized = null;

        if (hasTestCases)
        {
            if (testcasesToken is not JArray testcasesArray)
            {
                throw new ArgumentException("Property 'testcases' must be a JSON array.");
            }

            List<TestCaseDefinition> parsedCases = new(testcasesArray.Count);
            foreach (JToken testcaseToken in testcasesArray)
            {
                if (testcaseToken is not JObject testcaseNode)
                {
                    throw new ArgumentException("Each testcase must be a JSON object.");
                }

                parsedCases.Add(ParseTestCase(testcaseNode, baseDirectory));
            }

            testcases = parsedCases;
        }
        else
        {
            if (randomToken is not JObject randomNode)
            {
                throw new ArgumentException("Property 'random' must be a JSON object.");
            }

            randomized = ParseRandomizedGroup(randomNode, baseDirectory);
        }

        return new TestGroupDefinition
        {
            GroupId = GetRequiredString(node, "id"),
            DisplayName = GetRequiredString(node, "name"),
            TestCases = testcases,
            Randomized = randomized
        };
    }

    private static TestCaseDefinition ParseTestCase(JObject node, string? baseDirectory)
    {
        JObject inputNode = GetRequiredObject(node, "input");
        string inputMode = GetRequiredString(inputNode, "mode");
        if (!string.Equals(inputMode, "inline", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Unsupported input mode '{inputMode}'. Supported modes: inline.");
        }

        JToken rawInput = GetRequiredToken(inputNode, "value");
        if (rawInput is not JObject inputObject)
        {
            throw new ArgumentException("input.value must be a JSON object for mode 'inline'.");
        }

        bool hasExpectedOutput = node.TryGetValue("expectedOutput", StringComparison.Ordinal, out JToken? expectedOutputToken);
        bool hasGoldenStandard = node.TryGetValue("goldenStandard", StringComparison.Ordinal, out JToken? goldenStandardToken);

        if (hasExpectedOutput == hasGoldenStandard)
        {
            throw new ArgumentException(
                "Each testcase must define exactly one of: 'expectedOutput' or 'goldenStandard'.");
        }

        JToken? expectedOutput = hasExpectedOutput ? expectedOutputToken!.DeepClone() : null;
        GoldenStandardDefinition? goldenStandard = hasGoldenStandard
            ? ParseGoldenStandard(goldenStandardToken!, baseDirectory)
            : null;

        TestCaseComparisonMode comparisonMode = ParseComparisonMode(
            GetOptionalString(node, "comparisonMode", "strict-json"));

        return new TestCaseDefinition
        {
            TestCaseId = GetRequiredString(node, "id"),
            Input = (JObject)inputObject.DeepClone(),
            ExpectedOutput = expectedOutput,
            GoldenStandard = goldenStandard,
            ComparisonMode = comparisonMode
        };
    }

    private static GoldenStandardDefinition ParseGoldenStandard(JToken token, string? baseDirectory)
    {
        if (token is not JObject goldenObject)
        {
            throw new ArgumentException("Property 'goldenStandard' must be a JSON object.");
        }

        string configuredSourcePath = GetRequiredString(goldenObject, "sourceFile");
        string resolvedSourcePath = ResolveSourcePath(configuredSourcePath, baseDirectory);

        return new GoldenStandardDefinition
        {
            SourceFilePath = resolvedSourcePath,
            TypeName = GetOptionalString(goldenObject, "typeName", "GoldenStandard"),
            MethodName = GetOptionalString(goldenObject, "methodName", "Solve")
        };
    }

    private static RandomTestGroupDefinition ParseRandomizedGroup(JObject node, string? baseDirectory)
    {
        JObject inputGeneratorNode = GetRequiredObject(node, "inputGenerator");
        JObject goldenStandardNode = GetRequiredObject(node, "goldenStandard");

        TestCaseComparisonMode comparisonMode = ParseComparisonMode(
            GetOptionalString(node, "comparisonMode", "strict-json"));

        return new RandomTestGroupDefinition
        {
            Count = GetOptionalInt(node, "count", 10),
            TestCaseIdPrefix = GetOptionalString(node, "testCaseIdPrefix", "random-"),
            Seed = GetOptionalNullableInt(node, "seed"),
            ComparisonMode = comparisonMode,
            GoldenStandard = ParseGoldenStandard(goldenStandardNode, baseDirectory),
            InputGenerator = ParseRandomInputGenerator(inputGeneratorNode, baseDirectory)
        };
    }

    private static RandomInputGeneratorDefinition ParseRandomInputGenerator(JObject node, string? baseDirectory)
    {
        string mode = GetOptionalString(node, "mode", "default");
        return mode switch
        {
            "default" => new RandomInputGeneratorDefinition
            {
                Mode = RandomInputGeneratorMode.Default,
                Default = ParseDefaultRandomInputGenerator(node)
            },
            "source-file" => new RandomInputGeneratorDefinition
            {
                Mode = RandomInputGeneratorMode.SourceFile,
                SourceFile = ParseSourceFileRandomInputGenerator(node, baseDirectory)
            },
            _ => throw new ArgumentException(
                $"Unsupported random input generator mode '{mode}'. Supported modes: default, source-file.")
        };
    }

    private static DefaultRandomInputGeneratorDefinition ParseDefaultRandomInputGenerator(JObject node)
    {
        JArray intFieldsArray = GetRequiredArray(node, "intFields");
        List<RandomIntFieldDefinition> intFields = new(intFieldsArray.Count);

        foreach (JToken fieldToken in intFieldsArray)
        {
            if (fieldToken is not JObject fieldObject)
            {
                throw new ArgumentException("Each random int field must be a JSON object.");
            }

            intFields.Add(new RandomIntFieldDefinition
            {
                Name = GetRequiredString(fieldObject, "name"),
                MinValue = GetOptionalInt(fieldObject, "min", 0),
                MaxValue = GetOptionalInt(fieldObject, "max", 100)
            });
        }

        return new DefaultRandomInputGeneratorDefinition
        {
            IntFields = intFields
        };
    }

    private static SourceFileRandomInputGeneratorDefinition ParseSourceFileRandomInputGenerator(
        JObject node,
        string? baseDirectory)
    {
        string configuredSourcePath = GetRequiredString(node, "sourceFile");
        string resolvedSourcePath = ResolveSourcePath(configuredSourcePath, baseDirectory);

        return new SourceFileRandomInputGeneratorDefinition
        {
            SourceFilePath = resolvedSourcePath,
            TypeName = GetOptionalString(node, "typeName", "RandomInputGenerator"),
            MethodName = GetOptionalString(node, "methodName", "Generate")
        };
    }

    private static string ResolveSourcePath(string configuredPath, string? baseDirectory)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        string resolvedBase = string.IsNullOrWhiteSpace(baseDirectory)
            ? Environment.CurrentDirectory
            : baseDirectory;

        return Path.GetFullPath(Path.Combine(resolvedBase, configuredPath));
    }

    private static TestCaseComparisonMode ParseComparisonMode(string value)
    {
        return value switch
        {
            "strict-json" => TestCaseComparisonMode.StrictJson,
            "normalized-text" => TestCaseComparisonMode.NormalizedText,
            _ => throw new ArgumentException(
                $"Unsupported comparisonMode '{value}'. Supported values: strict-json, normalized-text.")
        };
    }

    private static JObject GetRequiredObject(JObject node, string propertyName)
    {
        JToken token = GetRequiredToken(node, propertyName);
        if (token is not JObject obj)
        {
            throw new ArgumentException($"Property '{propertyName}' must be a JSON object.");
        }

        return obj;
    }

    private static JArray GetRequiredArray(JObject node, string propertyName)
    {
        JToken token = GetRequiredToken(node, propertyName);
        if (token is not JArray array)
        {
            throw new ArgumentException($"Property '{propertyName}' must be a JSON array.");
        }

        return array;
    }

    private static JToken GetRequiredToken(JObject node, string propertyName)
    {
        if (!node.TryGetValue(propertyName, StringComparison.Ordinal, out JToken? token))
        {
            throw new ArgumentException($"Missing required property '{propertyName}'.");
        }

        return token;
    }

    private static string GetRequiredString(JObject node, string propertyName)
    {
        JToken token = GetRequiredToken(node, propertyName);
        if (token.Type != JTokenType.String)
        {
            throw new ArgumentException($"Property '{propertyName}' must be a string.");
        }

        string? value = token.Value<string>();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"Property '{propertyName}' cannot be empty.");
        }

        return value;
    }

    private static string GetOptionalString(JObject node, string propertyName, string defaultValue)
    {
        if (!node.TryGetValue(propertyName, StringComparison.Ordinal, out JToken? token))
        {
            return defaultValue;
        }

        if (token.Type != JTokenType.String)
        {
            throw new ArgumentException($"Property '{propertyName}' must be a string.");
        }

        string? value = token.Value<string>();
        return value ?? defaultValue;
    }

    private static int GetOptionalInt(JObject node, string propertyName, int defaultValue)
    {
        if (!node.TryGetValue(propertyName, StringComparison.Ordinal, out JToken? token))
        {
            return defaultValue;
        }

        if (token.Type != JTokenType.Integer)
        {
            throw new ArgumentException($"Property '{propertyName}' must be an integer.");
        }

        return token.Value<int>();
    }

    private static int? GetOptionalNullableInt(JObject node, string propertyName)
    {
        if (!node.TryGetValue(propertyName, StringComparison.Ordinal, out JToken? token))
        {
            return null;
        }

        if (token.Type == JTokenType.Null)
        {
            return null;
        }

        if (token.Type != JTokenType.Integer)
        {
            throw new ArgumentException($"Property '{propertyName}' must be an integer.");
        }

        return token.Value<int>();
    }

    private static double GetOptionalDouble(JObject node, string propertyName, double defaultValue)
    {
        if (!node.TryGetValue(propertyName, StringComparison.Ordinal, out JToken? token))
        {
            return defaultValue;
        }

        if (token.Type is not JTokenType.Float and not JTokenType.Integer)
        {
            throw new ArgumentException($"Property '{propertyName}' must be a number.");
        }

        return token.Value<double>();
    }

    private static IReadOnlyList<string> GetOptionalStringArray(JObject node, string propertyName)
    {
        if (!node.TryGetValue(propertyName, StringComparison.Ordinal, out JToken? token))
        {
            return Array.Empty<string>();
        }

        if (token is not JArray array)
        {
            throw new ArgumentException($"Property '{propertyName}' must be a JSON array.");
        }

        List<string> values = new(array.Count);
        foreach (JToken item in array)
        {
            if (item.Type != JTokenType.String)
            {
                throw new ArgumentException($"Property '{propertyName}' must contain only strings.");
            }

            string? value = item.Value<string>();
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"Property '{propertyName}' cannot contain empty values.");
            }

            values.Add(value);
        }

        return values;
    }
}
