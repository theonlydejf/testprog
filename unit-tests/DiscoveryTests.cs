using System.Net;
using System.Net.Sockets;
using System.Text;
using testprog.messenger;

namespace unit_tests;

public class DiscoveryTests
{
    [Test]
    public async Task ServerWanted_ReturnsServerAvailable()
    {
        await using TestServerHostHarness host = await TestServerHostHarness.StartAsync(TestHelpers.CreateSumSuite());

        using UdpClient client = new();
        IPEndPoint multicastEndpoint = new(IPAddress.Parse("239.0.0.222"), host.DiscoveryPort);

        ProtocolEnvelope wanted = new()
        {
            Type = MessageTypes.ServerWanted,
            Payload = ProtocolSerializer.ToPayloadObject(new ServerWantedPayload
            {
                StudentId = "novakj",
                DisplayName = "Jan Novak"
            })
        };

        byte[] payload = Encoding.UTF8.GetBytes(ProtocolSerializer.Serialize(wanted));
        await client.SendAsync(payload, payload.Length, multicastEndpoint);

        UdpReceiveResult response = await TestHelpers.ReceiveUdpWithTimeoutAsync(client, TimeSpan.FromSeconds(2));
        ProtocolEnvelope envelope = ProtocolSerializer.Deserialize(Encoding.UTF8.GetString(response.Buffer));

        Assert.That(envelope.Type, Is.EqualTo(MessageTypes.ServerAvailable));
        ServerAvailablePayload available = ProtocolSerializer.DeserializePayload<ServerAvailablePayload>(envelope);
        Assert.That(available.ServerPort, Is.EqualTo(host.TcpPort));
    }

    [Test]
    public async Task InvalidDiscoveryMessage_IsIgnored()
    {
        await using TestServerHostHarness host = await TestServerHostHarness.StartAsync(TestHelpers.CreateSumSuite());

        using UdpClient client = new();
        IPEndPoint endpoint = new(IPAddress.Loopback, host.DiscoveryPort);

        byte[] payload = Encoding.UTF8.GetBytes("{not-valid-json");
        await client.SendAsync(payload, payload.Length, endpoint);

        Assert.That(
            async () => await TestHelpers.ReceiveUdpWithTimeoutAsync(client, TimeSpan.FromMilliseconds(300)),
            Throws.TypeOf<TimeoutException>());
    }

    [Test]
    public async Task DiscoveryMessageWithWrongType_IsIgnored()
    {
        await using TestServerHostHarness host = await TestServerHostHarness.StartAsync(TestHelpers.CreateSumSuite());

        using UdpClient client = new();
        IPEndPoint endpoint = new(IPAddress.Parse("239.0.0.222"), host.DiscoveryPort);

        ProtocolEnvelope wrongType = new()
        {
            Type = MessageTypes.TestBegin,
            Payload = new Newtonsoft.Json.Linq.JObject()
        };

        byte[] payload = Encoding.UTF8.GetBytes(ProtocolSerializer.Serialize(wrongType));
        await client.SendAsync(payload, payload.Length, endpoint);

        Assert.That(
            async () => await TestHelpers.ReceiveUdpWithTimeoutAsync(client, TimeSpan.FromMilliseconds(300)),
            Throws.TypeOf<TimeoutException>());
    }
}
