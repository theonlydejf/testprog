using Newtonsoft.Json.Linq;
using testprog.messenger;
using testprog.server;

namespace unit_tests;

public class EndToEndFlowTests
{
    [Test]
    public async Task RunAsync_PassingCase_ReturnsPassedSummary()
    {
        await using TestServerHostHarness harness = await TestServerHostHarness.StartAsync(TestHelpers.CreateSumSuite(expectedResult: 5));
        await using ITestProgClient client = await TestProgClient.ConnectAsync(TestHelpers.CreateClientOptions(harness.TcpPort));

        TestRunSummary summary = await client.RunAsync(input =>
        {
            int a = input.GetInt("a");
            int b = input.GetInt("b");
            return new { result = a + b };
        });

        Assert.That(summary.Completed, Is.True);
        Assert.That(summary.TestGroupCount, Is.EqualTo(1));
        Assert.That(summary.TestCaseCount, Is.EqualTo(1));
        Assert.That(summary.PassedCount, Is.EqualTo(1));
        Assert.That(summary.FailedCount, Is.EqualTo(0));
        Assert.That(summary.StopReasonCode, Is.Null);
    }

    [Test]
    public async Task RunAsync_FailingCase_ReturnsFailedSummary()
    {
        await using TestServerHostHarness harness = await TestServerHostHarness.StartAsync(TestHelpers.CreateSumSuite(expectedResult: 999));
        await using ITestProgClient client = await TestProgClient.ConnectAsync(TestHelpers.CreateClientOptions(harness.TcpPort));

        TestRunSummary summary = await client.RunAsync(input =>
        {
            int a = input.GetInt("a");
            int b = input.GetInt("b");
            return new { result = a + b };
        });

        Assert.That(summary.Completed, Is.True);
        Assert.That(summary.TestCaseCount, Is.EqualTo(1));
        Assert.That(summary.PassedCount, Is.EqualTo(0));
        Assert.That(summary.FailedCount, Is.EqualTo(1));
    }

    [Test]
    public async Task RunAsync_CalledTwice_Throws()
    {
        await using TestServerHostHarness harness = await TestServerHostHarness.StartAsync(TestHelpers.CreateSumSuite(expectedResult: 5));
        await using ITestProgClient client = await TestProgClient.ConnectAsync(TestHelpers.CreateClientOptions(harness.TcpPort));

        _ = await client.RunAsync(input => new { result = input.GetInt("a") + input.GetInt("b") });

        Assert.That(
            async () => await client.RunAsync(input => new { result = 0 }),
            Throws.TypeOf<InvalidOperationException>());
    }

    [Test]
    public async Task RunAsync_WhenSolverThrows_ContinuesWithRemainingTestcases()
    {
        await using TestServerHostHarness harness = await TestServerHostHarness.StartAsync(CreateTwoCaseSuite());
        await using ITestProgClient client = await TestProgClient.ConnectAsync(TestHelpers.CreateClientOptions(harness.TcpPort));

        TestRunSummary summary = await client.RunAsync(input =>
        {
            int value = input.GetInt("a");
            if (value == 1)
            {
                throw new InvalidOperationException("boom");
            }

            return new { result = value };
        });

        Assert.That(summary.Completed, Is.True);
        Assert.That(summary.TestCaseCount, Is.EqualTo(2));
        Assert.That(summary.PassedCount, Is.EqualTo(1));
        Assert.That(summary.FailedCount, Is.EqualTo(1));
    }

    [Test]
    public async Task RunAsync_WhenSolverCancelsInternally_ContinuesWithRemainingTestcases()
    {
        await using TestServerHostHarness harness = await TestServerHostHarness.StartAsync(CreateTwoCaseSuite());
        await using ITestProgClient client = await TestProgClient.ConnectAsync(TestHelpers.CreateClientOptions(harness.TcpPort));

        TestRunSummary summary = await client.RunAsync(input =>
        {
            int value = input.GetInt("a");
            if (value == 1)
            {
                throw new OperationCanceledException("solver timeout");
            }

            return new { result = value };
        });

        Assert.That(summary.Completed, Is.True);
        Assert.That(summary.TestCaseCount, Is.EqualTo(2));
        Assert.That(summary.PassedCount, Is.EqualTo(1));
        Assert.That(summary.FailedCount, Is.EqualTo(1));
    }

    [Test]
    public async Task RunAsync_WhenServerTimesOutDuringSolve_ReturnsStoppedSummary()
    {
        await using TestServerHostHarness harness = await TestServerHostHarness.StartAsync(
            TestHelpers.CreateSumSuite(testcaseResponseTimeout: TimeSpan.FromMilliseconds(150)),
            responseTimeout: TimeSpan.FromSeconds(3));
        await using ITestProgClient client = await TestProgClient.ConnectAsync(TestHelpers.CreateClientOptions(harness.TcpPort));

        TestRunSummary summary = await client.RunAsync(async input =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(400));
            return new { result = input.GetInt("a") + input.GetInt("b") };
        });

        Assert.That(summary.Completed, Is.False);
        Assert.That(summary.StopReasonCode, Is.EqualTo(StopReasonCodes.Timeout));
    }

    private static TestSuiteDefinition CreateTwoCaseSuite()
    {
        return new TestSuiteDefinition
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
                            Input = JObject.FromObject(new { a = 1 }),
                            ExpectedOutput = JObject.FromObject(new { result = 1 })
                        },
                        new TestCaseDefinition
                        {
                            TestCaseId = "t2",
                            Input = JObject.FromObject(new { a = 2 }),
                            ExpectedOutput = JObject.FromObject(new { result = 2 })
                        }
                    }
                }
            }
        };
    }
}
