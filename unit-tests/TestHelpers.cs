using System.Net;
using System.Net.Sockets;
using testprog.messenger;
using testprog.server;

namespace unit_tests;

internal static class TestHelpers
{
    public static int GetFreeTcpPort()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        try
        {
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            return port;
        }
        finally
        {
            listener.Stop();
        }
    }

    public static int GetFreeUdpPort()
    {
        using UdpClient udpClient = new(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)udpClient.Client.LocalEndPoint!).Port;
    }

    public static TestSuiteDefinition CreateSumSuite(
        int expectedResult = 5,
        TestCaseComparisonMode comparisonMode = TestCaseComparisonMode.StrictJson,
        TimeSpan? testcaseResponseTimeout = null)
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
                            TestCaseId = "sum-001",
                            Input = Newtonsoft.Json.Linq.JObject.FromObject(new { a = 2, b = 3 }),
                            ExpectedOutput = Newtonsoft.Json.Linq.JObject.FromObject(new { result = expectedResult }),
                            ComparisonMode = comparisonMode,
                            ResponseTimeout = testcaseResponseTimeout
                        }
                    }
                }
            }
        };
    }

    public static TestServerOptions CreateServerOptions(
        int tcpPort,
        int discoveryPort,
        int maxConcurrentSessions = 8,
        TimeSpan? responseTimeout = null,
        IReadOnlyList<string>? studentIdWhitelist = null)
    {
        return new TestServerOptions
        {
            ServerId = "test-server",
            AdvertiseHost = "127.0.0.1",
            DiscoveryMulticastAddress = "239.0.0.222",
            DiscoveryPort = discoveryPort,
            TcpPort = tcpPort,
            MaxConcurrentSessions = maxConcurrentSessions,
            ClientResponseTimeout = responseTimeout ?? TimeSpan.FromSeconds(3),
            StudentIdWhitelist = studentIdWhitelist ?? Array.Empty<string>()
        };
    }

    public static TestProgClientOptions CreateClientOptions(int tcpPort)
    {
        return new TestProgClientOptions
        {
            StudentId = "novakj",
            DisplayName = "Jan Novak",
            ConnectTimeout = TimeSpan.FromSeconds(4),
            HeartbeatTimeout = TimeSpan.FromSeconds(4),
            Discovery = new DiscoveryOptions
            {
                Mode = DiscoveryMode.DirectTcp,
                DirectServerHost = "127.0.0.1",
                DirectServerPort = tcpPort,
                DiscoveryTimeout = TimeSpan.FromSeconds(1)
            }
        };
    }

    public static async Task<ProtocolEnvelope> ReceiveWithTimeoutAsync(
        FramedTcpChannel channel,
        TimeSpan timeout)
    {
        using CancellationTokenSource cts = new(timeout);
        return await channel.ReceiveAsync(cts.Token);
    }

    public static async Task<UdpReceiveResult> ReceiveUdpWithTimeoutAsync(
        UdpClient client,
        TimeSpan timeout)
    {
        using CancellationTokenSource cts = new(timeout);
        Task<UdpReceiveResult> receiveTask = client.ReceiveAsync();
        Task waitTask = Task.Delay(Timeout.InfiniteTimeSpan, cts.Token);
        Task completed = await Task.WhenAny(receiveTask, waitTask);
        if (completed == receiveTask)
        {
            return await receiveTask;
        }

        throw new TimeoutException("Timed out waiting for UDP packet.");
    }
}
