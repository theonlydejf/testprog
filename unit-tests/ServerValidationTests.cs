using Newtonsoft.Json.Linq;
using testprog.server;

namespace unit_tests;

public class ServerValidationTests
{
    [Test]
    public void Constructor_NullOptions_Throws()
    {
        Assert.That(() => new TestServerHost(null!, ValidSuite()), Throws.TypeOf<ArgumentNullException>());
    }

    [Test]
    public void Constructor_NullSuite_Throws()
    {
        Assert.That(() => new TestServerHost(ValidOptions(), null!), Throws.TypeOf<ArgumentNullException>());
    }

    [Test]
    public void Constructor_EmptyGroups_Throws()
    {
        TestSuiteDefinition suite = new()
        {
            Groups = Array.Empty<TestGroupDefinition>()
        };

        Assert.That(() => new TestServerHost(ValidOptions(), suite), Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void Constructor_DuplicateGroupId_Throws()
    {
        TestSuiteDefinition suite = new()
        {
            Groups = new[]
            {
                ValidGroup("basic"),
                ValidGroup("basic")
            }
        };

        Assert.That(() => new TestServerHost(ValidOptions(), suite), Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void Constructor_DuplicateTestcaseId_Throws()
    {
        TestSuiteDefinition suite = new()
        {
            Groups = new[]
            {
                new TestGroupDefinition
                {
                    GroupId = "basic",
                    DisplayName = "Basic",
                    TestCases = new[]
                    {
                        ValidCase("t1"),
                        ValidCase("t1")
                    }
                }
            }
        };

        Assert.That(() => new TestServerHost(ValidOptions(), suite), Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void Constructor_GroupWithBothStaticAndRandom_Throws()
    {
        TestSuiteDefinition suite = new()
        {
            Groups = new[]
            {
                new TestGroupDefinition
                {
                    GroupId = "mixed",
                    DisplayName = "Mixed",
                    TestCases = new[] { ValidCase("t1") },
                    Randomized = ValidRandomGroup()
                }
            }
        };

        Assert.That(() => new TestServerHost(ValidOptions(), suite), Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void Constructor_RandomGroupMissingGolden_Throws()
    {
        TestSuiteDefinition suite = new()
        {
            Groups = new[]
            {
                new TestGroupDefinition
                {
                    GroupId = "rand",
                    DisplayName = "Random",
                    Randomized = new RandomTestGroupDefinition
                    {
                        Count = 3,
                        TestCaseIdPrefix = "r-",
                        InputGenerator = new RandomInputGeneratorDefinition
                        {
                            Mode = RandomInputGeneratorMode.Default,
                            Default = new DefaultRandomInputGeneratorDefinition
                            {
                                IntFields = new[]
                                {
                                    new RandomIntFieldDefinition { Name = "a", MinValue = 0, MaxValue = 10 }
                                }
                            }
                        }
                    }
                }
            }
        };

        Assert.That(() => new TestServerHost(ValidOptions(), suite), Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void Constructor_ValidRandomGroup_DoesNotThrow()
    {
        TestSuiteDefinition suite = new()
        {
            Groups = new[]
            {
                new TestGroupDefinition
                {
                    GroupId = "rand",
                    DisplayName = "Random",
                    Randomized = ValidRandomGroup()
                }
            }
        };

        Assert.That(() => new TestServerHost(ValidOptions(), suite), Throws.Nothing);
    }

    [Test]
    public void Constructor_InvalidServerPort_Throws()
    {
        TestServerOptions options = ValidOptions();
        options = new TestServerOptions
        {
            ServerId = options.ServerId,
            AdvertiseHost = options.AdvertiseHost,
            DiscoveryMulticastAddress = options.DiscoveryMulticastAddress,
            DiscoveryPort = options.DiscoveryPort,
            TcpPort = 0,
            MaxConcurrentSessions = options.MaxConcurrentSessions,
            ClientResponseTimeout = options.ClientResponseTimeout
        };
        Assert.That(() => new TestServerHost(options, ValidSuite()), Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void Constructor_InvalidMulticastAddress_Throws()
    {
        TestServerOptions options = ValidOptions();
        options = new TestServerOptions
        {
            ServerId = options.ServerId,
            AdvertiseHost = options.AdvertiseHost,
            DiscoveryMulticastAddress = "127.0.0.1",
            DiscoveryPort = options.DiscoveryPort,
            TcpPort = options.TcpPort,
            MaxConcurrentSessions = options.MaxConcurrentSessions,
            ClientResponseTimeout = options.ClientResponseTimeout
        };
        Assert.That(() => new TestServerHost(options, ValidSuite()), Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void Constructor_WhitelistDuplicateStudent_Throws()
    {
        TestServerOptions options = ValidOptions();
        options = new TestServerOptions
        {
            ServerId = options.ServerId,
            AdvertiseHost = options.AdvertiseHost,
            DiscoveryMulticastAddress = options.DiscoveryMulticastAddress,
            DiscoveryPort = options.DiscoveryPort,
            TcpPort = options.TcpPort,
            MaxConcurrentSessions = options.MaxConcurrentSessions,
            ClientResponseTimeout = options.ClientResponseTimeout,
            StudentIdWhitelist = new[] { "novakj", "NOVAKJ" }
        };

        Assert.That(() => new TestServerHost(options, ValidSuite()), Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void Constructor_TestCaseWithoutExpectedSource_Throws()
    {
        TestSuiteDefinition suite = new()
        {
            Groups = new[]
            {
                new TestGroupDefinition
                {
                    GroupId = "basic",
                    DisplayName = "Basic",
                    TestCases = new[]
                    {
                        new TestCaseDefinition
                        {
                            TestCaseId = "t1",
                            Input = JObject.FromObject(new { a = 1, b = 2 }),
                            ExpectedOutput = null
                        }
                    }
                }
            }
        };

        Assert.That(() => new TestServerHost(ValidOptions(), suite), Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void Constructor_TestCaseWithBothExpectedSources_Throws()
    {
        TestSuiteDefinition suite = new()
        {
            Groups = new[]
            {
                new TestGroupDefinition
                {
                    GroupId = "basic",
                    DisplayName = "Basic",
                    TestCases = new[]
                    {
                        new TestCaseDefinition
                        {
                            TestCaseId = "t1",
                            Input = JObject.FromObject(new { a = 1, b = 2 }),
                            ExpectedOutput = JObject.FromObject(new { result = 3 }),
                            GoldenStandard = new GoldenStandardDefinition
                            {
                                SourceFilePath = "golden.cs"
                            }
                        }
                    }
                }
            }
        };

        Assert.That(() => new TestServerHost(ValidOptions(), suite), Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void Constructor_GoldenStandardFileMissing_Throws()
    {
        TestSuiteDefinition suite = new()
        {
            Groups = new[]
            {
                new TestGroupDefinition
                {
                    GroupId = "basic",
                    DisplayName = "Basic",
                    TestCases = new[]
                    {
                        new TestCaseDefinition
                        {
                            TestCaseId = "t1",
                            Input = JObject.FromObject(new { a = 1, b = 2 }),
                            ExpectedOutput = null,
                            GoldenStandard = new GoldenStandardDefinition
                            {
                                SourceFilePath = Path.Combine(Path.GetTempPath(), $"missing-golden-{Guid.NewGuid():N}.cs")
                            }
                        }
                    }
                }
            }
        };

        Assert.That(() => new TestServerHost(ValidOptions(), suite), Throws.TypeOf<ArgumentException>());
    }

    private static TestSuiteDefinition ValidSuite()
    {
        return new TestSuiteDefinition
        {
            Groups = new[] { ValidGroup("basic") }
        };
    }

    private static TestGroupDefinition ValidGroup(string groupId)
    {
        return new TestGroupDefinition
        {
            GroupId = groupId,
            DisplayName = "Group",
            TestCases = new[] { ValidCase("t1") }
        };
    }

    private static TestCaseDefinition ValidCase(string caseId)
    {
        return new TestCaseDefinition
        {
            TestCaseId = caseId,
            Input = JObject.FromObject(new { a = 1, b = 2 }),
            ExpectedOutput = JObject.FromObject(new { result = 3 })
        };
    }

    private static RandomTestGroupDefinition ValidRandomGroup()
    {
        string goldenPath = Path.Combine(Path.GetTempPath(), "testprog-valid-random-golden.cs");
        if (!File.Exists(goldenPath))
        {
            File.WriteAllText(goldenPath, """
            using Newtonsoft.Json.Linq;

            public static class GoldenStandard
            {
                public static object Solve(JObject input)
                {
                    return new { result = input.Value<int>("a") + input.Value<int>("b") };
                }
            }
            """);
        }

        return new RandomTestGroupDefinition
        {
            Count = 3,
            TestCaseIdPrefix = "r-",
            Seed = 123,
            ComparisonMode = TestCaseComparisonMode.StrictJson,
            GoldenStandard = new GoldenStandardDefinition
            {
                SourceFilePath = goldenPath
            },
            InputGenerator = new RandomInputGeneratorDefinition
            {
                Mode = RandomInputGeneratorMode.Default,
                Default = new DefaultRandomInputGeneratorDefinition
                {
                    IntFields = new[]
                    {
                        new RandomIntFieldDefinition { Name = "a", MinValue = 0, MaxValue = 10 },
                        new RandomIntFieldDefinition { Name = "b", MinValue = -5, MaxValue = 5 }
                    }
                }
            }
        };
    }

    private static TestServerOptions ValidOptions()
    {
        return new TestServerOptions
        {
            ServerId = "srv1",
            AdvertiseHost = "127.0.0.1",
            DiscoveryMulticastAddress = "239.0.0.222",
            DiscoveryPort = 11000,
            TcpPort = 5000,
            MaxConcurrentSessions = 1,
            ClientResponseTimeout = TimeSpan.FromSeconds(2)
        };
    }
}
