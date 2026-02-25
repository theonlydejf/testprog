using Newtonsoft.Json.Linq;
using testprog.messenger;

namespace unit_tests;

public class ClientProtocolNegativeTests
{
    [Test]
    public async Task ConnectAsync_WhenServerRepliesWithWrongType_Throws()
    {
        await using ScriptedTcpServerHarness server = await ScriptedTcpServerHarness.StartAsync(async (channel, ct) =>
        {
            _ = await channel.ReceiveAsync(ct);
            await channel.SendAsync(new ProtocolEnvelope
            {
                Type = MessageTypes.TestBegin,
                Payload = new JObject()
            }, ct);
        });

        TestProgClientOptions options = TestHelpers.CreateClientOptions(server.Port);
        Assert.That(
            async () => await TestProgClient.ConnectAsync(options),
            Throws.TypeOf<InvalidOperationException>());
    }

    [Test]
    public async Task RunAsync_WhenSessionTokenMismatch_Throws()
    {
        await using ScriptedTcpServerHarness server = await ScriptedTcpServerHarness.StartAsync(async (channel, ct) =>
        {
            _ = await channel.ReceiveAsync(ct);
            await channel.SendAsync(new ProtocolEnvelope
            {
                Type = MessageTypes.ServerHello,
                Payload = ProtocolSerializer.ToPayloadObject(new ServerHelloPayload
                {
                    SessionToken = "expected-token",
                    HeartbeatSeconds = 5
                })
            }, ct);

            await channel.SendAsync(new ProtocolEnvelope
            {
                Type = MessageTypes.TestBegin,
                SessionToken = "wrong-token",
                Payload = new JObject()
            }, ct);
        });

        await using ITestProgClient client = await TestProgClient.ConnectAsync(TestHelpers.CreateClientOptions(server.Port));
        Assert.That(
            async () => await client.RunAsync(_ => new { result = 0 }),
            Throws.TypeOf<InvalidOperationException>());
    }

    [Test]
    public async Task RunAsync_WhenServerSendsError_Throws()
    {
        await using ScriptedTcpServerHarness server = await ScriptedTcpServerHarness.StartAsync(async (channel, ct) =>
        {
            _ = await channel.ReceiveAsync(ct);
            await channel.SendAsync(new ProtocolEnvelope
            {
                Type = MessageTypes.ServerHello,
                Payload = ProtocolSerializer.ToPayloadObject(new ServerHelloPayload
                {
                    SessionToken = "s1",
                    HeartbeatSeconds = 5
                })
            }, ct);

            await channel.SendAsync(new ProtocolEnvelope
            {
                Type = MessageTypes.Error,
                SessionToken = "s1",
                Payload = ProtocolSerializer.ToPayloadObject(new ErrorPayload
                {
                    ReasonCode = StopReasonCodes.InternalServerError,
                    ReasonDetail = "boom"
                })
            }, ct);
        });

        await using ITestProgClient client = await TestProgClient.ConnectAsync(TestHelpers.CreateClientOptions(server.Port));
        Assert.That(
            async () => await client.RunAsync(_ => new { result = 0 }),
            Throws.TypeOf<InvalidOperationException>());
    }

    [Test]
    public async Task RunAsync_WhenServerSendsStop_ReturnsSummaryWithStopReason()
    {
        await using ScriptedTcpServerHarness server = await ScriptedTcpServerHarness.StartAsync(async (channel, ct) =>
        {
            _ = await channel.ReceiveAsync(ct);
            await channel.SendAsync(new ProtocolEnvelope
            {
                Type = MessageTypes.ServerHello,
                Payload = ProtocolSerializer.ToPayloadObject(new ServerHelloPayload
                {
                    SessionToken = "s1",
                    HeartbeatSeconds = 5
                })
            }, ct);

            await channel.SendAsync(new ProtocolEnvelope
            {
                Type = MessageTypes.Stop,
                SessionToken = "s1",
                Payload = ProtocolSerializer.ToPayloadObject(new StopPayload
                {
                    ReasonCode = StopReasonCodes.ServerStop,
                    ReasonDetail = "teacher ended run"
                })
            }, ct);
        });

        await using ITestProgClient client = await TestProgClient.ConnectAsync(TestHelpers.CreateClientOptions(server.Port));
        TestRunSummary summary = await client.RunAsync(_ => new { result = 0 });

        Assert.That(summary.Completed, Is.False);
        Assert.That(summary.StopReasonCode, Is.EqualTo(StopReasonCodes.ServerStop));
        Assert.That(summary.StopReasonDetail, Is.EqualTo("teacher ended run"));
    }

    [Test]
    public async Task RunAsync_WhenServerSilent_HeartbeatTimeoutThrows()
    {
        await using ScriptedTcpServerHarness server = await ScriptedTcpServerHarness.StartAsync(async (channel, ct) =>
        {
            _ = await channel.ReceiveAsync(ct);
            await channel.SendAsync(new ProtocolEnvelope
            {
                Type = MessageTypes.ServerHello,
                Payload = ProtocolSerializer.ToPayloadObject(new ServerHelloPayload
                {
                    SessionToken = "s1",
                    HeartbeatSeconds = 1
                })
            }, ct);

            await Task.Delay(TimeSpan.FromSeconds(3), ct);
        });

        TestProgClientOptions options = TestHelpers.CreateClientOptions(server.Port);
        options = new TestProgClientOptions
        {
            StudentId = options.StudentId,
            DisplayName = options.DisplayName,
            Discovery = options.Discovery,
            ConnectTimeout = options.ConnectTimeout,
            HeartbeatTimeout = TimeSpan.FromMilliseconds(200)
        };

        await using ITestProgClient client = await TestProgClient.ConnectAsync(options);
        Assert.That(
            async () => await client.RunAsync(_ => new { result = 0 }),
            Throws.TypeOf<TimeoutException>());
    }
}
