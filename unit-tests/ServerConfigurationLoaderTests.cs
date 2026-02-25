using testprog.server;

namespace unit_tests;

public class ServerConfigurationLoaderTests
{
    [Test]
    public void LoadFromJson_ValidConfig_ParsesSuccessfully()
    {
        LoadedServerConfiguration loaded = TestServerConfigurationLoader.LoadFromJson(ValidConfigJson());

        Assert.That(loaded.ServerOptions.ServerId, Is.EqualTo("teacher-pc-01"));
        Assert.That(loaded.ServerOptions.TcpPort, Is.EqualTo(5000));
        Assert.That(loaded.ServerOptions.ClientResponseTimeout, Is.EqualTo(TimeSpan.FromSeconds(6)));

        Assert.That(loaded.Suite.Groups.Count, Is.EqualTo(1));
        Assert.That(loaded.Suite.Groups[0].GroupId, Is.EqualTo("basic"));
        Assert.That(loaded.Suite.Groups[0].TestCases.Count, Is.EqualTo(1));
        Assert.That(loaded.Suite.Groups[0].TestCases[0].TestCaseId, Is.EqualTo("sum-001"));
        Assert.That(loaded.Suite.Groups[0].TestCases[0].ComparisonMode, Is.EqualTo(TestCaseComparisonMode.StrictJson));
    }

    [Test]
    public void LoadFromJson_MissingOptionalValues_UsesDefaults()
    {
        const string json = """
        {
          "server": {},
          "suite": {
            "groups": [
              {
                "id": "g1",
                "name": "Group",
                "testcases": [
                  {
                    "id": "t1",
                    "input": { "mode": "inline", "value": { "a": 1 } },
                    "expectedOutput": { "result": 1 }
                  }
                ]
              }
            ]
          }
        }
        """;

        LoadedServerConfiguration loaded = TestServerConfigurationLoader.LoadFromJson(json);

        Assert.That(loaded.ServerOptions.DiscoveryPort, Is.EqualTo(11000));
        Assert.That(loaded.ServerOptions.TcpPort, Is.EqualTo(5000));
        Assert.That(loaded.ServerOptions.MaxConcurrentSessions, Is.EqualTo(32));
        Assert.That(loaded.ServerOptions.ClientResponseTimeout, Is.EqualTo(TimeSpan.FromSeconds(10)));
        Assert.That(loaded.ServerOptions.StudentIdWhitelist, Is.Empty);
    }

    [Test]
    public void LoadFromJson_Whitelist_ParsesSuccessfully()
    {
        string json = ValidConfigJson().Replace(
            "\"clientResponseTimeoutSeconds\": 6",
            """
            "clientResponseTimeoutSeconds": 6,
                "studentIdWhitelist": ["novakj", "svobodaa"]
            """,
            StringComparison.Ordinal);

        LoadedServerConfiguration loaded = TestServerConfigurationLoader.LoadFromJson(json);
        Assert.That(loaded.ServerOptions.StudentIdWhitelist, Is.EqualTo(new[] { "novakj", "svobodaa" }));
    }

    [Test]
    public void LoadFromJson_WhitelistMustBeArray_Throws()
    {
        string json = ValidConfigJson().Replace(
            "\"clientResponseTimeoutSeconds\": 6",
            "\"clientResponseTimeoutSeconds\": 6, \"studentIdWhitelist\": \"novakj\"",
            StringComparison.Ordinal);

        Assert.That(
            () => TestServerConfigurationLoader.LoadFromJson(json),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void LoadFromFile_ValidConfig_ParsesSuccessfully()
    {
        string path = Path.Combine(Path.GetTempPath(), $"testprog-config-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, ValidConfigJson());

        try
        {
            LoadedServerConfiguration loaded = TestServerConfigurationLoader.LoadFromFile(path);
            Assert.That(loaded.Suite.Groups[0].TestCases[0].TestCaseId, Is.EqualTo("sum-001"));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Test]
    public void LoadFromFile_GoldenStandardRelativePath_ResolvesAgainstConfigDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"testprog-config-dir-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        string sourcePath = Path.Combine(directory, "SumGolden.cs");
        File.WriteAllText(sourcePath, """
        using Newtonsoft.Json.Linq;
        public static class GoldenStandard
        {
            public static object Solve(JObject input) => new { result = input.Value<int>("a") + input.Value<int>("b") };
        }
        """);

        string configPath = Path.Combine(directory, "server-config.json");
        string configJson = """
        {
          "server": {},
          "suite": {
            "groups": [
              {
                "id": "g1",
                "name": "Group",
                "testcases": [
                  {
                    "id": "t1",
                    "input": { "mode": "inline", "value": { "a": 2, "b": 3 } },
                    "goldenStandard": {
                      "sourceFile": "SumGolden.cs"
                    }
                  }
                ]
              }
            ]
          }
        }
        """;
        File.WriteAllText(configPath, configJson);

        try
        {
            LoadedServerConfiguration loaded = TestServerConfigurationLoader.LoadFromFile(configPath);
            string resolvedPath = loaded.Suite.Groups[0].TestCases[0].GoldenStandard!.SourceFilePath;
            Assert.That(resolvedPath, Is.EqualTo(Path.GetFullPath(sourcePath)));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Test]
    public void LoadFromJson_Empty_Throws()
    {
        Assert.That(
            () => TestServerConfigurationLoader.LoadFromJson(""),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void LoadFromJson_InvalidJson_Throws()
    {
        Assert.That(
            () => TestServerConfigurationLoader.LoadFromJson("{not-json"),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void LoadFromJson_MissingServerSection_Throws()
    {
        const string json = """
        {
          "suite": { "groups": [] }
        }
        """;

        Assert.That(
            () => TestServerConfigurationLoader.LoadFromJson(json),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void LoadFromJson_MissingSuiteSection_Throws()
    {
        const string json = """
        {
          "server": {}
        }
        """;

        Assert.That(
            () => TestServerConfigurationLoader.LoadFromJson(json),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void LoadFromJson_ServerSectionMustBeObject_Throws()
    {
        const string json = """
        {
          "server": [],
          "suite": { "groups": [] }
        }
        """;

        Assert.That(
            () => TestServerConfigurationLoader.LoadFromJson(json),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void LoadFromJson_GroupsMustBeArray_Throws()
    {
        const string json = """
        {
          "server": {},
          "suite": { "groups": {} }
        }
        """;

        Assert.That(
            () => TestServerConfigurationLoader.LoadFromJson(json),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void LoadFromJson_GroupMissingId_Throws()
    {
        const string json = """
        {
          "server": {},
          "suite": {
            "groups": [
              {
                "name": "Group",
                "testcases": [
                  {
                    "id": "t1",
                    "input": { "mode": "inline", "value": { "a": 1 } },
                    "expectedOutput": { "result": 1 }
                  }
                ]
              }
            ]
          }
        }
        """;

        Assert.That(
            () => TestServerConfigurationLoader.LoadFromJson(json),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void LoadFromJson_TestCaseMissingExpectedOutput_Throws()
    {
        const string json = """
        {
          "server": {},
          "suite": {
            "groups": [
              {
                "id": "g1",
                "name": "Group",
                "testcases": [
                  {
                    "id": "t1",
                    "input": { "mode": "inline", "value": { "a": 1 } }
                  }
                ]
              }
            ]
          }
        }
        """;

        Assert.That(
            () => TestServerConfigurationLoader.LoadFromJson(json),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void LoadFromJson_TestCaseWithBothExpectedOutputAndGolden_Throws()
    {
        string json = ValidConfigJson().Replace(
            "\"expectedOutput\": { \"result\": 5 }",
            """
            "expectedOutput": { "result": 5 },
                    "goldenStandard": { "sourceFile": "golden/SumGolden.cs" }
            """,
            StringComparison.Ordinal);

        Assert.That(
            () => TestServerConfigurationLoader.LoadFromJson(json),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void LoadFromJson_GoldenStandard_ParsesSuccessfully()
    {
        string sourcePath = CreateTempGoldenSourceFile();
        string json = GoldenConfigJson(sourcePath);

        try
        {
            LoadedServerConfiguration loaded = TestServerConfigurationLoader.LoadFromJson(json);
            TestCaseDefinition testcase = loaded.Suite.Groups[0].TestCases[0];

            Assert.That(testcase.ExpectedOutput, Is.Null);
            Assert.That(testcase.GoldenStandard, Is.Not.Null);
            Assert.That(testcase.GoldenStandard!.SourceFilePath, Is.EqualTo(Path.GetFullPath(sourcePath)));
            Assert.That(testcase.GoldenStandard.TypeName, Is.EqualTo("GoldenStandard"));
            Assert.That(testcase.GoldenStandard.MethodName, Is.EqualTo("Solve"));
        }
        finally
        {
            if (File.Exists(sourcePath))
            {
                File.Delete(sourcePath);
            }
        }
    }

    [Test]
    public void LoadFromJson_GoldenStandardMissingSourceFile_Throws()
    {
        const string json = """
        {
          "server": {},
          "suite": {
            "groups": [
              {
                "id": "g1",
                "name": "Group",
                "testcases": [
                  {
                    "id": "t1",
                    "input": { "mode": "inline", "value": { "a": 1 } },
                    "goldenStandard": {}
                  }
                ]
              }
            ]
          }
        }
        """;

        Assert.That(
            () => TestServerConfigurationLoader.LoadFromJson(json),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void LoadFromJson_RandomGroupWithDefaultGenerator_ParsesSuccessfully()
    {
        string sourcePath = CreateTempGoldenSourceFile();
        string escapedPath = sourcePath.Replace("\\", "\\\\", StringComparison.Ordinal);

        string json = $$"""
        {
          "server": {},
          "suite": {
            "groups": [
              {
                "id": "g-random",
                "name": "Random",
                "random": {
                  "count": 4,
                  "testCaseIdPrefix": "rnd-",
                  "seed": 123,
                  "comparisonMode": "strict-json",
                  "goldenStandard": {
                    "sourceFile": "{{escapedPath}}"
                  },
                  "inputGenerator": {
                    "mode": "default",
                    "intFields": [
                      { "name": "a", "min": -5, "max": 5 },
                      { "name": "b", "min": 0, "max": 10 }
                    ]
                  }
                }
              }
            ]
          }
        }
        """;

        try
        {
            LoadedServerConfiguration loaded = TestServerConfigurationLoader.LoadFromJson(json);
            TestGroupDefinition group = loaded.Suite.Groups[0];

            Assert.That(group.TestCases.Count, Is.EqualTo(0));
            Assert.That(group.Randomized, Is.Not.Null);
            Assert.That(group.Randomized!.Count, Is.EqualTo(4));
            Assert.That(group.Randomized.TestCaseIdPrefix, Is.EqualTo("rnd-"));
            Assert.That(group.Randomized.Seed, Is.EqualTo(123));
            Assert.That(group.Randomized.GoldenStandard!.SourceFilePath, Is.EqualTo(Path.GetFullPath(sourcePath)));
            Assert.That(group.Randomized.InputGenerator!.Mode, Is.EqualTo(RandomInputGeneratorMode.Default));
            Assert.That(group.Randomized.InputGenerator.Default!.IntFields.Count, Is.EqualTo(2));
        }
        finally
        {
            if (File.Exists(sourcePath))
            {
                File.Delete(sourcePath);
            }
        }
    }

    [Test]
    public void LoadFromFile_RandomGeneratorRelativePath_ResolvesAgainstConfigDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"testprog-random-config-dir-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        string goldenPath = Path.Combine(directory, "SumGolden.cs");
        File.WriteAllText(goldenPath, """
        using Newtonsoft.Json.Linq;
        public static class GoldenStandard
        {
            public static object Solve(JObject input) => new { result = input.Value<int>("a") + input.Value<int>("b") };
        }
        """);

        string randomGeneratorPath = Path.Combine(directory, "RandomGenerator.cs");
        File.WriteAllText(randomGeneratorPath, """
        using System;
        using Newtonsoft.Json.Linq;
        public static class RandomInputGenerator
        {
            public static object Generate(Random random, int testcaseIndex)
            {
                return new { a = random.Next(0, 10), b = random.Next(0, 10) };
            }
        }
        """);

        string configPath = Path.Combine(directory, "server-config.json");
        File.WriteAllText(configPath, """
        {
          "server": {},
          "suite": {
            "groups": [
              {
                "id": "g1",
                "name": "Random Group",
                "random": {
                  "count": 2,
                  "goldenStandard": { "sourceFile": "SumGolden.cs" },
                  "inputGenerator": {
                    "mode": "source-file",
                    "sourceFile": "RandomGenerator.cs"
                  }
                }
              }
            ]
          }
        }
        """);

        try
        {
            LoadedServerConfiguration loaded = TestServerConfigurationLoader.LoadFromFile(configPath);
            TestGroupDefinition group = loaded.Suite.Groups[0];

            Assert.That(group.Randomized, Is.Not.Null);
            Assert.That(
                group.Randomized!.InputGenerator!.SourceFile!.SourceFilePath,
                Is.EqualTo(Path.GetFullPath(randomGeneratorPath)));
            Assert.That(
                group.Randomized.GoldenStandard!.SourceFilePath,
                Is.EqualTo(Path.GetFullPath(goldenPath)));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Test]
    public void LoadFromJson_GroupWithBothStaticAndRandom_Throws()
    {
        const string json = """
        {
          "server": {},
          "suite": {
            "groups": [
              {
                "id": "g1",
                "name": "Mixed",
                "testcases": [
                  {
                    "id": "t1",
                    "input": { "mode": "inline", "value": { "a": 1, "b": 2 } },
                    "expectedOutput": { "result": 3 }
                  }
                ],
                "random": {
                  "count": 1,
                  "goldenStandard": { "sourceFile": "missing.cs" },
                  "inputGenerator": {
                    "mode": "default",
                    "intFields": [
                      { "name": "a", "min": 0, "max": 1 }
                    ]
                  }
                }
              }
            ]
          }
        }
        """;

        Assert.That(
            () => TestServerConfigurationLoader.LoadFromJson(json),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void LoadFromJson_UnsupportedRandomInputGeneratorMode_Throws()
    {
        string sourcePath = CreateTempGoldenSourceFile();
        string escapedPath = sourcePath.Replace("\\", "\\\\", StringComparison.Ordinal);

        string json = $$"""
        {
          "server": {},
          "suite": {
            "groups": [
              {
                "id": "g1",
                "name": "Random",
                "random": {
                  "count": 1,
                  "goldenStandard": { "sourceFile": "{{escapedPath}}" },
                  "inputGenerator": {
                    "mode": "xml",
                    "intFields": [
                      { "name": "a", "min": 0, "max": 1 }
                    ]
                  }
                }
              }
            ]
          }
        }
        """;

        try
        {
            Assert.That(
                () => TestServerConfigurationLoader.LoadFromJson(json),
                Throws.TypeOf<ArgumentException>());
        }
        finally
        {
            if (File.Exists(sourcePath))
            {
                File.Delete(sourcePath);
            }
        }
    }

    [Test]
    public void LoadFromJson_UnsupportedInputMode_Throws()
    {
        string json = ValidConfigJson().Replace(
            "\"mode\": \"inline\"",
            "\"mode\": \"random-int\"",
            StringComparison.Ordinal);

        Assert.That(
            () => TestServerConfigurationLoader.LoadFromJson(json),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void LoadFromJson_InputInlineValueMustBeObject_Throws()
    {
        string json = ValidConfigJson().Replace(
            "\"value\": { \"a\": 2, \"b\": 3 }",
            "\"value\": 42",
            StringComparison.Ordinal);

        Assert.That(
            () => TestServerConfigurationLoader.LoadFromJson(json),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void LoadFromJson_UnsupportedComparisonMode_Throws()
    {
        string json = ValidConfigJson().Replace(
            "\"comparisonMode\": \"strict-json\"",
            "\"comparisonMode\": \"xml\"",
            StringComparison.Ordinal);

        Assert.That(
            () => TestServerConfigurationLoader.LoadFromJson(json),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void LoadFromJson_NormalizedTextComparison_ParsesEnum()
    {
        string json = ValidConfigJson().Replace(
            "\"comparisonMode\": \"strict-json\"",
            "\"comparisonMode\": \"normalized-text\"",
            StringComparison.Ordinal);

        LoadedServerConfiguration loaded = TestServerConfigurationLoader.LoadFromJson(json);

        Assert.That(
            loaded.Suite.Groups[0].TestCases[0].ComparisonMode,
            Is.EqualTo(TestCaseComparisonMode.NormalizedText));
    }

    [Test]
    public void LoadFromJson_DuplicateGroupId_Throws()
    {
        const string json = """
        {
          "server": {},
          "suite": {
            "groups": [
              {
                "id": "g1",
                "name": "A",
                "testcases": [
                  { "id": "t1", "input": { "mode": "inline", "value": { "a": 1 } }, "expectedOutput": { "x": 1 } }
                ]
              },
              {
                "id": "g1",
                "name": "B",
                "testcases": [
                  { "id": "t2", "input": { "mode": "inline", "value": { "a": 2 } }, "expectedOutput": { "x": 2 } }
                ]
              }
            ]
          }
        }
        """;

        Assert.That(
            () => TestServerConfigurationLoader.LoadFromJson(json),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void LoadFromJson_DuplicateTestCaseIdInGroup_Throws()
    {
        const string json = """
        {
          "server": {},
          "suite": {
            "groups": [
              {
                "id": "g1",
                "name": "A",
                "testcases": [
                  { "id": "t1", "input": { "mode": "inline", "value": { "a": 1 } }, "expectedOutput": { "x": 1 } },
                  { "id": "t1", "input": { "mode": "inline", "value": { "a": 2 } }, "expectedOutput": { "x": 2 } }
                ]
              }
            ]
          }
        }
        """;

        Assert.That(
            () => TestServerConfigurationLoader.LoadFromJson(json),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void LoadFromJson_InvalidPort_Throws()
    {
        string json = ValidConfigJson().Replace(
            "\"tcpPort\": 5000",
            "\"tcpPort\": 0",
            StringComparison.Ordinal);

        Assert.That(
            () => TestServerConfigurationLoader.LoadFromJson(json),
            Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    private static string ValidConfigJson()
    {
        return """
        {
          "server": {
            "serverId": "teacher-pc-01",
            "advertiseHost": "127.0.0.1",
            "discoveryMulticastAddress": "239.0.0.222",
            "discoveryPort": 11000,
            "tcpPort": 5000,
            "maxConcurrentSessions": 10,
            "clientResponseTimeoutSeconds": 6
          },
          "suite": {
            "groups": [
              {
                "id": "basic",
                "name": "Basic tests",
                "testcases": [
                  {
                    "id": "sum-001",
                    "comparisonMode": "strict-json",
                    "input": {
                      "mode": "inline",
                      "value": { "a": 2, "b": 3 }
                    },
                    "expectedOutput": { "result": 5 }
                  }
                ]
              }
            ]
          }
        }
        """;
    }

    private static string GoldenConfigJson(string sourcePath)
    {
        string escapedPath = sourcePath.Replace("\\", "\\\\", StringComparison.Ordinal);

        return $$"""
        {
          "server": {},
          "suite": {
            "groups": [
              {
                "id": "golden",
                "name": "Golden",
                "testcases": [
                  {
                    "id": "sum-golden-001",
                    "input": {
                      "mode": "inline",
                      "value": { "a": 2, "b": 3 }
                    },
                    "goldenStandard": {
                      "sourceFile": "{{escapedPath}}",
                      "typeName": "GoldenStandard",
                      "methodName": "Solve"
                    }
                  }
                ]
              }
            ]
          }
        }
        """;
    }

    private static string CreateTempGoldenSourceFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"testprog-golden-{Guid.NewGuid():N}.cs");

        const string source = """
        using Newtonsoft.Json.Linq;

        public static class GoldenStandard
        {
            public static object Solve(JObject input)
            {
                int a = input.Value<int>("a");
                int b = input.Value<int>("b");
                return new { result = a + b };
            }
        }
        """;

        File.WriteAllText(path, source);
        return path;
    }
}
