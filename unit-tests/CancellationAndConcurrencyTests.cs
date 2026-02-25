using System.Net.Sockets;
using testprog.messenger;
using testprog.server;

namespace unit_tests;

public class CancellationAndConcurrencyTests
{
    [Test]
    public async Task HostRunAsync_CanBeCancelled()
    {
        int tcpPort = TestHelpers.GetFreeTcpPort();
        int discoveryPort = TestHelpers.GetFreeUdpPort();
        TestServerOptions options = TestHelpers.CreateServerOptions(tcpPort, discoveryPort);
        TestServerHost host = new(options, TestHelpers.CreateSumSuite());

        using CancellationTokenSource cts = new();
        Task runTask = host.RunAsync(cts.Token);
        await Task.Delay(100);
        cts.Cancel();

        try
        {
            await runTask;
        }
        catch (OperationCanceledException)
        {
        }

        await host.DisposeAsync();
    }

    [Test]
    public async Task MaxConcurrentSessions_SecondClientWaitsForSlot()
    {
        await using TestServerHostHarness host = await TestServerHostHarness.StartAsync(
            TestHelpers.CreateSumSuite(),
            maxConcurrentSessions: 1,
            responseTimeout: TimeSpan.FromSeconds(3));

        await using FramedTcpChannel client1 = await FramedTcpChannel.ConnectAsync("127.0.0.1", host.TcpPort, CancellationToken.None);
        await SendClientHelloAsync(client1);
        _ = await client1.ReceiveAsync(CancellationToken.None); // server-hello
        _ = await client1.ReceiveAsync(CancellationToken.None); // test-begin

        await using FramedTcpChannel client2 = await FramedTcpChannel.ConnectAsync("127.0.0.1", host.TcpPort, CancellationToken.None);
        await SendClientHelloAsync(client2);

        Exception? waitException = null;
        try
        {
            _ = await TestHelpers.ReceiveWithTimeoutAsync(client2, TimeSpan.FromMilliseconds(300));
        }
        catch (Exception ex)
        {
            waitException = ex;
        }

        Assert.That(waitException, Is.Not.Null);
        Assert.That(waitException, Is.TypeOf<OperationCanceledException>().Or.TypeOf<TimeoutException>());

        await client1.DisposeAsync();

        ProtocolEnvelope hello2 = await TestHelpers.ReceiveWithTimeoutAsync(client2, TimeSpan.FromSeconds(2));
        Assert.That(hello2.Type, Is.EqualTo(MessageTypes.ServerHello));
    }

    [Test]
    public async Task ClientRunAsync_RespectsCancellationToken()
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
                    HeartbeatSeconds = 30
                })
            }, ct);

            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        });

        await using ITestProgClient client = await TestProgClient.ConnectAsync(TestHelpers.CreateClientOptions(server.Port));

        using CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(100));
        Assert.That(
            async () => await client.RunAsync(_ => new { result = 0 }, cts.Token),
            Throws.TypeOf<OperationCanceledException>());
    }

    private static Task SendClientHelloAsync(FramedTcpChannel channel)
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
        }, CancellationToken.None);
    }
}
