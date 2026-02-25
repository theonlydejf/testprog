using testprog.messenger;

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

}
