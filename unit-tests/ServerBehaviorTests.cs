using System.IO;
using System.Net.Sockets;
using Newtonsoft.Json.Linq;
using testprog.messenger;
using testprog.server;

namespace unit_tests;

public class ServerBehaviorTests
{
    [Test]
    public void Whitelist_UnknownStudent_IsRejectedDuringHandshake()
    {
        Assert.That(async () =>
        {
            await using TestServerHostHarness host = await TestServerHostHarness.StartAsync(
                TestHelpers.CreateSumSuite(),
                studentIdWhitelist: new[] { "allowed-student" });

            _ = await TestProgClient.ConnectAsync(TestHelpers.CreateClientOptions(host.TcpPort));
        }, Throws.TypeOf<InvalidOperationException>());
    }

    [Test]
    public async Task NormalizedText_Comparison_CanPass()
    {
        TestSuiteDefinition suite = new()
        {
            Groups = new[]
            {
                new TestGroupDefinition
                {
                    GroupId = "text",
                    DisplayName = "Text",
                    TestCases = new[]
                    {
                        new TestCaseDefinition
                        {
                            TestCaseId = "text-001",
                            Input = JObject.FromObject(new { a = 2, b = 3 }),
                            ExpectedOutput = JObject.FromObject(new { value = "5" }),
                            ComparisonMode = TestCaseComparisonMode.NormalizedText
                        }
                    }
                }
            }
        };

        await using TestServerHostHarness host = await TestServerHostHarness.StartAsync(suite);
        await using ITestProgClient client = await TestProgClient.ConnectAsync(TestHelpers.CreateClientOptions(host.TcpPort));

        TestRunSummary summary = await client.RunAsync(_ => " 5 \n");
        Assert.That(summary.PassedCount, Is.EqualTo(1));
        Assert.That(summary.FailedCount, Is.EqualTo(0));
    }

    [Test]
    public async Task ScalarOutput_Comparison_CanPass()
    {
        TestSuiteDefinition suite = new()
        {
            Groups = new[]
            {
                new TestGroupDefinition
                {
                    GroupId = "scalar",
                    DisplayName = "Scalar",
                    TestCases = new[]
                    {
                        new TestCaseDefinition
                        {
                            TestCaseId = "scalar-001",
                            Input = JObject.FromObject(new { a = 2, b = 3 }),
                            ExpectedOutput = new JValue(5),
                            ComparisonMode = TestCaseComparisonMode.StrictJson
                        }
                    }
                }
            }
        };

        await using TestServerHostHarness host = await TestServerHostHarness.StartAsync(suite);
        await using ITestProgClient client = await TestProgClient.ConnectAsync(TestHelpers.CreateClientOptions(host.TcpPort));

        TestRunSummary summary = await client.RunAsync(input => input.GetInt("a") + input.GetInt("b"));
        Assert.That(summary.PassedCount, Is.EqualTo(1));
        Assert.That(summary.FailedCount, Is.EqualTo(0));
    }

    [Test]
    public async Task GoldenStandard_Comparison_CanPass()
    {
        string sourcePath = CreateTempGoldenSourceFile();

        try
        {
            TestSuiteDefinition suite = new()
            {
                Groups = new[]
                {
                    new TestGroupDefinition
                    {
                        GroupId = "golden",
                        DisplayName = "Golden",
                        TestCases = new[]
                        {
                            new TestCaseDefinition
                            {
                                TestCaseId = "golden-001",
                                Input = JObject.FromObject(new { a = 2, b = 3 }),
                                GoldenStandard = new GoldenStandardDefinition
                                {
                                    SourceFilePath = sourcePath
                                },
                                ComparisonMode = TestCaseComparisonMode.StrictJson
                            }
                        }
                    }
                }
            };

            await using TestServerHostHarness host = await TestServerHostHarness.StartAsync(suite);
            await using ITestProgClient client = await TestProgClient.ConnectAsync(TestHelpers.CreateClientOptions(host.TcpPort));

            TestRunSummary summary = await client.RunAsync(input =>
            {
                int a = input.GetInt("a");
                int b = input.GetInt("b");
                return new { result = a + b };
            });

            Assert.That(summary.PassedCount, Is.EqualTo(1));
            Assert.That(summary.FailedCount, Is.EqualTo(0));
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
    public async Task RandomizedGroup_DefaultGeneratorAndGolden_CanPass()
    {
        string sourcePath = CreateTempGoldenSourceFile();

        try
        {
            TestSuiteDefinition suite = new()
            {
                Groups = new[]
                {
                    new TestGroupDefinition
                    {
                        GroupId = "random-default",
                        DisplayName = "Random default",
                        Randomized = new RandomTestGroupDefinition
                        {
                            Count = 5,
                            TestCaseIdPrefix = "rnd-",
                            Seed = 42,
                            ComparisonMode = TestCaseComparisonMode.StrictJson,
                            GoldenStandard = new GoldenStandardDefinition
                            {
                                SourceFilePath = sourcePath
                            },
                            InputGenerator = new RandomInputGeneratorDefinition
                            {
                                Mode = RandomInputGeneratorMode.Default,
                                Default = new DefaultRandomInputGeneratorDefinition
                                {
                                    IntFields = new[]
                                    {
                                        new RandomIntFieldDefinition { Name = "a", MinValue = -20, MaxValue = 20 },
                                        new RandomIntFieldDefinition { Name = "b", MinValue = -20, MaxValue = 20 }
                                    }
                                }
                            }
                        }
                    }
                }
            };

            await using TestServerHostHarness host = await TestServerHostHarness.StartAsync(suite);
            await using ITestProgClient client = await TestProgClient.ConnectAsync(TestHelpers.CreateClientOptions(host.TcpPort));

            TestRunSummary summary = await client.RunAsync(input =>
            {
                int a = input.GetInt("a");
                int b = input.GetInt("b");
                return new { result = a + b };
            });

            Assert.That(summary.TestCaseCount, Is.EqualTo(5));
            Assert.That(summary.PassedCount, Is.EqualTo(5));
            Assert.That(summary.FailedCount, Is.EqualTo(0));
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
    public async Task RandomizedGroup_SourceFileGeneratorAndGolden_CanPass()
    {
        string goldenSourcePath = CreateTempGoldenSourceFile();
        string randomGeneratorPath = CreateTempRandomGeneratorSourceFile();

        try
        {
            TestSuiteDefinition suite = new()
            {
                Groups = new[]
                {
                    new TestGroupDefinition
                    {
                        GroupId = "random-source",
                        DisplayName = "Random source",
                        Randomized = new RandomTestGroupDefinition
                        {
                            Count = 4,
                            TestCaseIdPrefix = "src-",
                            Seed = 24,
                            ComparisonMode = TestCaseComparisonMode.StrictJson,
                            GoldenStandard = new GoldenStandardDefinition
                            {
                                SourceFilePath = goldenSourcePath
                            },
                            InputGenerator = new RandomInputGeneratorDefinition
                            {
                                Mode = RandomInputGeneratorMode.SourceFile,
                                SourceFile = new SourceFileRandomInputGeneratorDefinition
                                {
                                    SourceFilePath = randomGeneratorPath,
                                    TypeName = "RandomInputGenerator",
                                    MethodName = "Generate"
                                }
                            }
                        }
                    }
                }
            };

            await using TestServerHostHarness host = await TestServerHostHarness.StartAsync(suite);
            await using ITestProgClient client = await TestProgClient.ConnectAsync(TestHelpers.CreateClientOptions(host.TcpPort));

            TestRunSummary summary = await client.RunAsync(input =>
            {
                int a = input.GetInt("a");
                int b = input.GetInt("b");
                return new { result = a + b };
            });

            Assert.That(summary.TestCaseCount, Is.EqualTo(4));
            Assert.That(summary.PassedCount, Is.EqualTo(4));
            Assert.That(summary.FailedCount, Is.EqualTo(0));
        }
        finally
        {
            if (File.Exists(goldenSourcePath))
            {
                File.Delete(goldenSourcePath);
            }

            if (File.Exists(randomGeneratorPath))
            {
                File.Delete(randomGeneratorPath);
            }
        }
    }

    [Test]
    public async Task UnexpectedClientMessage_ResultsInErrorEnvelope()
    {
        await using TestServerHostHarness host = await TestServerHostHarness.StartAsync(TestHelpers.CreateSumSuite());
        await using FramedTcpChannel channel = await FramedTcpChannel.ConnectAsync("127.0.0.1", host.TcpPort, CancellationToken.None);

        await SendClientHelloAsync(channel, CancellationToken.None);

        ProtocolEnvelope serverHello = await channel.ReceiveAsync(CancellationToken.None);
        string token = ProtocolSerializer.DeserializePayload<ServerHelloPayload>(serverHello).SessionToken;

        // Consume test-begin, testgroup-start and testcase.
        _ = await channel.ReceiveAsync(CancellationToken.None);
        _ = await channel.ReceiveAsync(CancellationToken.None);
        _ = await channel.ReceiveAsync(CancellationToken.None);

        await channel.SendAsync(new ProtocolEnvelope
        {
            Type = MessageTypes.TestBegin,
            SessionToken = token,
            Payload = new JObject()
        }, CancellationToken.None);

        ProtocolEnvelope error = await TestHelpers.ReceiveWithTimeoutAsync(channel, TimeSpan.FromSeconds(2));
        Assert.That(error.Type, Is.EqualTo(MessageTypes.Error));
    }

    [Test]
    public async Task WrongTestcaseId_ReturnsFailedResult()
    {
        await using TestServerHostHarness host = await TestServerHostHarness.StartAsync(TestHelpers.CreateSumSuite());
        await using FramedTcpChannel channel = await FramedTcpChannel.ConnectAsync("127.0.0.1", host.TcpPort, CancellationToken.None);

        await SendClientHelloAsync(channel, CancellationToken.None);
        ProtocolEnvelope serverHello = await channel.ReceiveAsync(CancellationToken.None);
        string token = ProtocolSerializer.DeserializePayload<ServerHelloPayload>(serverHello).SessionToken;

        _ = await channel.ReceiveAsync(CancellationToken.None); // test-begin
        _ = await channel.ReceiveAsync(CancellationToken.None); // testgroup-start
        _ = await channel.ReceiveAsync(CancellationToken.None); // testcase

        await channel.SendAsync(new ProtocolEnvelope
        {
            Type = MessageTypes.TestCaseSolved,
            SessionToken = token,
            Payload = ProtocolSerializer.ToPayloadObject(new TestCaseSolvedPayload
            {
                TestCaseId = "wrong-id",
                Output = JObject.FromObject(new { result = 5 })
            })
        }, CancellationToken.None);

        ProtocolEnvelope resultEnvelope = await TestHelpers.ReceiveWithTimeoutAsync(channel, TimeSpan.FromSeconds(2));
        Assert.That(resultEnvelope.Type, Is.EqualTo(MessageTypes.TestCaseResult));

        TestCaseResultPayload result = ProtocolSerializer.DeserializePayload<TestCaseResultPayload>(resultEnvelope);
        Assert.That(result.Status, Is.EqualTo(TestCaseResultStatuses.Failed));
        Assert.That(result.ReasonCode, Is.EqualTo(StopReasonCodes.InvalidAnswer));
    }

    [Test]
    public async Task WrongSessionToken_ResultsInErrorEnvelope()
    {
        await using TestServerHostHarness host = await TestServerHostHarness.StartAsync(TestHelpers.CreateSumSuite());
        await using FramedTcpChannel channel = await FramedTcpChannel.ConnectAsync("127.0.0.1", host.TcpPort, CancellationToken.None);

        await SendClientHelloAsync(channel, CancellationToken.None);
        _ = await channel.ReceiveAsync(CancellationToken.None); // server-hello
        _ = await channel.ReceiveAsync(CancellationToken.None); // test-begin
        _ = await channel.ReceiveAsync(CancellationToken.None); // testgroup-start
        _ = await channel.ReceiveAsync(CancellationToken.None); // testcase

        await channel.SendAsync(new ProtocolEnvelope
        {
            Type = MessageTypes.TestCaseSolved,
            SessionToken = "bad-token",
            Payload = ProtocolSerializer.ToPayloadObject(new TestCaseSolvedPayload
            {
                TestCaseId = "sum-001",
                Output = JObject.FromObject(new { result = 5 })
            })
        }, CancellationToken.None);

        ProtocolEnvelope error = await TestHelpers.ReceiveWithTimeoutAsync(channel, TimeSpan.FromSeconds(2));
        Assert.That(error.Type, Is.EqualTo(MessageTypes.Error));
    }

    [Test]
    public async Task NullOutput_DoesNotCrashAndReturnsFailedResult()
    {
        await using TestServerHostHarness host = await TestServerHostHarness.StartAsync(TestHelpers.CreateSumSuite());
        await using FramedTcpChannel channel = await FramedTcpChannel.ConnectAsync("127.0.0.1", host.TcpPort, CancellationToken.None);

        await SendClientHelloAsync(channel, CancellationToken.None);
        ProtocolEnvelope serverHello = await channel.ReceiveAsync(CancellationToken.None);
        string token = ProtocolSerializer.DeserializePayload<ServerHelloPayload>(serverHello).SessionToken;

        _ = await channel.ReceiveAsync(CancellationToken.None); // test-begin
        _ = await channel.ReceiveAsync(CancellationToken.None); // testgroup-start
        ProtocolEnvelope testcaseEnvelope = await channel.ReceiveAsync(CancellationToken.None);
        TestCasePayload testcase = ProtocolSerializer.DeserializePayload<TestCasePayload>(testcaseEnvelope);

        await channel.SendAsync(new ProtocolEnvelope
        {
            Type = MessageTypes.TestCaseSolved,
            SessionToken = token,
            Payload = new JObject
            {
                ["testcaseId"] = testcase.TestCaseId,
                ["output"] = JValue.CreateNull()
            }
        }, CancellationToken.None);

        ProtocolEnvelope resultEnvelope = await TestHelpers.ReceiveWithTimeoutAsync(channel, TimeSpan.FromSeconds(2));
        TestCaseResultPayload result = ProtocolSerializer.DeserializePayload<TestCaseResultPayload>(resultEnvelope);

        Assert.That(result.Status, Is.EqualTo(TestCaseResultStatuses.Failed));
    }

    [Test]
    public async Task ClientResponseTimeout_SendsStopTimeout()
    {
        await using TestServerHostHarness host = await TestServerHostHarness.StartAsync(
            TestHelpers.CreateSumSuite(),
            responseTimeout: TimeSpan.FromMilliseconds(300));

        await using FramedTcpChannel channel = await FramedTcpChannel.ConnectAsync("127.0.0.1", host.TcpPort, CancellationToken.None);
        await SendClientHelloAsync(channel, CancellationToken.None);

        ProtocolEnvelope serverHello = await channel.ReceiveAsync(CancellationToken.None);
        string token = ProtocolSerializer.DeserializePayload<ServerHelloPayload>(serverHello).SessionToken;

        _ = await channel.ReceiveAsync(CancellationToken.None); // test-begin
        _ = await channel.ReceiveAsync(CancellationToken.None); // testgroup-start
        _ = await channel.ReceiveAsync(CancellationToken.None); // testcase

        ProtocolEnvelope stopEnvelope = await TestHelpers.ReceiveWithTimeoutAsync(channel, TimeSpan.FromSeconds(2));
        Assert.That(stopEnvelope.Type, Is.EqualTo(MessageTypes.Stop));
        Assert.That(stopEnvelope.SessionToken, Is.EqualTo(token));

        StopPayload stop = ProtocolSerializer.DeserializePayload<StopPayload>(stopEnvelope);
        Assert.That(stop.ReasonCode, Is.EqualTo(StopReasonCodes.Timeout));
    }

    [Test]
    public async Task ClientStop_EndsSessionWithoutFurtherResults()
    {
        await using TestServerHostHarness host = await TestServerHostHarness.StartAsync(TestHelpers.CreateSumSuite());
        await using FramedTcpChannel channel = await FramedTcpChannel.ConnectAsync("127.0.0.1", host.TcpPort, CancellationToken.None);

        await SendClientHelloAsync(channel, CancellationToken.None);
        ProtocolEnvelope serverHello = await channel.ReceiveAsync(CancellationToken.None);
        string token = ProtocolSerializer.DeserializePayload<ServerHelloPayload>(serverHello).SessionToken;

        _ = await channel.ReceiveAsync(CancellationToken.None); // test-begin
        _ = await channel.ReceiveAsync(CancellationToken.None); // testgroup-start
        _ = await channel.ReceiveAsync(CancellationToken.None); // testcase

        await channel.SendAsync(new ProtocolEnvelope
        {
            Type = MessageTypes.Stop,
            SessionToken = token,
            Payload = ProtocolSerializer.ToPayloadObject(new StopPayload
            {
                ReasonCode = StopReasonCodes.ClientStop,
                ReasonDetail = "cancelled by test"
            })
        }, CancellationToken.None);

        Assert.That(
            async () => await TestHelpers.ReceiveWithTimeoutAsync(channel, TimeSpan.FromMilliseconds(300)),
            Throws.TypeOf<OperationCanceledException>().Or.TypeOf<TimeoutException>().Or.TypeOf<IOException>());
    }

    private static Task SendClientHelloAsync(FramedTcpChannel channel, CancellationToken cancellationToken)
    {
        return channel.SendAsync(new ProtocolEnvelope
        {
            Type = MessageTypes.ClientHello,
            Payload = ProtocolSerializer.ToPayloadObject(new ClientHelloPayload
            {
                StudentId = "novakj",
                DisplayName = "Jan Novak",
                ClientVersion = "1.0.0"
            })
        }, cancellationToken);
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

    private static string CreateTempRandomGeneratorSourceFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"testprog-random-generator-{Guid.NewGuid():N}.cs");

        const string source = """
        using System;

        public static class RandomInputGenerator
        {
            public static object Generate(Random random, int testcaseIndex)
            {
                int a = random.Next(-10, 11);
                int b = random.Next(-10, 11);
                return new { a, b };
            }
        }
        """;

        File.WriteAllText(path, source);
        return path;
    }
}
