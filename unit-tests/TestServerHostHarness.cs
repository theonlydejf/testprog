using testprog.server;

namespace unit_tests;

internal sealed class TestServerHostHarness : IAsyncDisposable
{
    private readonly CancellationTokenSource _shutdown;
    private readonly TestServerHost _host;
    private readonly Task _hostTask;

    private TestServerHostHarness(
        TestServerHost host,
        Task hostTask,
        CancellationTokenSource shutdown,
        int tcpPort,
        int discoveryPort)
    {
        _host = host;
        _hostTask = hostTask;
        _shutdown = shutdown;
        TcpPort = tcpPort;
        DiscoveryPort = discoveryPort;
    }

    public int TcpPort { get; }
    public int DiscoveryPort { get; }

    public static async Task<TestServerHostHarness> StartAsync(
        TestSuiteDefinition suite,
        int? tcpPort = null,
        int? discoveryPort = null,
        int maxConcurrentSessions = 8,
        TimeSpan? responseTimeout = null,
        IReadOnlyList<string>? studentIdWhitelist = null)
    {
        int realTcpPort = tcpPort ?? TestHelpers.GetFreeTcpPort();
        int realDiscoveryPort = discoveryPort ?? TestHelpers.GetFreeUdpPort();

        TestServerOptions options = TestHelpers.CreateServerOptions(
            realTcpPort,
            realDiscoveryPort,
            maxConcurrentSessions,
            responseTimeout,
            studentIdWhitelist);

        TestServerHost host = new(options, suite);
        CancellationTokenSource shutdown = new(TimeSpan.FromSeconds(20));
        Task hostTask = host.RunAsync(shutdown.Token);

        await Task.Delay(100);
        return new TestServerHostHarness(host, hostTask, shutdown, realTcpPort, realDiscoveryPort);
    }

    public async ValueTask DisposeAsync()
    {
        _shutdown.Cancel();

        try
        {
            await _hostTask;
        }
        catch (OperationCanceledException)
        {
        }

        await _host.DisposeAsync();
        _shutdown.Dispose();
    }
}
