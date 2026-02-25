using System.Net;
using System.Net.Sockets;
using testprog.messenger;

namespace unit_tests;

internal sealed class ScriptedTcpServerHarness : IAsyncDisposable
{
    private readonly CancellationTokenSource _shutdown;
    private readonly TcpListener _listener;
    private readonly Task _acceptTask;

    private ScriptedTcpServerHarness(
        TcpListener listener,
        CancellationTokenSource shutdown,
        Task acceptTask,
        int port)
    {
        _listener = listener;
        _shutdown = shutdown;
        _acceptTask = acceptTask;
        Port = port;
    }

    public int Port { get; }

    public static async Task<ScriptedTcpServerHarness> StartAsync(
        Func<FramedTcpChannel, CancellationToken, Task> onSession)
    {
        int port = TestHelpers.GetFreeTcpPort();
        TcpListener listener = new(IPAddress.Loopback, port);
        listener.Start();

        CancellationTokenSource shutdown = new(TimeSpan.FromSeconds(20));
        Task acceptTask = Task.Run(async () =>
        {
            try
            {
                using TcpClient tcpClient = await listener.AcceptTcpClientAsync(shutdown.Token);
                await using FramedTcpChannel channel = FramedTcpChannel.FromAcceptedClient(tcpClient);
                await onSession(channel, shutdown.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        });

        await Task.Delay(50);
        return new ScriptedTcpServerHarness(listener, shutdown, acceptTask, port);
    }

    public async ValueTask DisposeAsync()
    {
        _shutdown.Cancel();
        _listener.Stop();

        try
        {
            await _acceptTask;
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _shutdown.Dispose();
        }
    }
}
