using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json.Linq;
using testprog.messenger;

namespace unit_tests;

public class FramedTcpChannelTests
{
    [Test]
    public async Task SendReceive_MultipleFrames_WorksBothDirections()
    {
        int port = TestHelpers.GetFreeTcpPort();
        TcpListener listener = new(IPAddress.Loopback, port);
        listener.Start();
        try
        {
            Task<TcpClient> acceptedTask = listener.AcceptTcpClientAsync();
            await using FramedTcpChannel client = await FramedTcpChannel.ConnectAsync("127.0.0.1", port, CancellationToken.None);
            using TcpClient serverClient = await acceptedTask;
            await using FramedTcpChannel server = FramedTcpChannel.FromAcceptedClient(serverClient);

            await client.SendAsync(new ProtocolEnvelope
            {
                Type = MessageTypes.TestCase,
                SessionToken = "s1",
                Payload = JObject.FromObject(new { x = 1 })
            }, CancellationToken.None);

            await client.SendAsync(new ProtocolEnvelope
            {
                Type = MessageTypes.TestCase,
                SessionToken = "s1",
                Payload = JObject.FromObject(new { x = 2 })
            }, CancellationToken.None);

            ProtocolEnvelope first = await server.ReceiveAsync(CancellationToken.None);
            ProtocolEnvelope second = await server.ReceiveAsync(CancellationToken.None);
            Assert.That(first.Payload["x"]?.Value<int>(), Is.EqualTo(1));
            Assert.That(second.Payload["x"]?.Value<int>(), Is.EqualTo(2));

            await server.SendAsync(new ProtocolEnvelope
            {
                Type = MessageTypes.Ping,
                SessionToken = "s1",
                Payload = new JObject()
            }, CancellationToken.None);

            ProtocolEnvelope reply = await client.ReceiveAsync(CancellationToken.None);
            Assert.That(reply.Type, Is.EqualTo(MessageTypes.Ping));
        }
        finally
        {
            listener.Stop();
        }
    }

    [Test]
    public async Task Receive_WhenRemoteClosed_ThrowsIOException()
    {
        int port = TestHelpers.GetFreeTcpPort();
        TcpListener listener = new(IPAddress.Loopback, port);
        listener.Start();
        try
        {
            Task<TcpClient> acceptedTask = listener.AcceptTcpClientAsync();
            await using FramedTcpChannel client = await FramedTcpChannel.ConnectAsync("127.0.0.1", port, CancellationToken.None);

            using TcpClient serverClient = await acceptedTask;
            serverClient.Dispose();

            Assert.That(
                async () => await client.ReceiveAsync(CancellationToken.None),
                Throws.TypeOf<IOException>());
        }
        finally
        {
            listener.Stop();
        }
    }

    [Test]
    public async Task Receive_WhenFrameLengthIsInvalid_Throws()
    {
        int port = TestHelpers.GetFreeTcpPort();
        TcpListener listener = new(IPAddress.Loopback, port);
        listener.Start();
        try
        {
            Task<TcpClient> acceptedTask = listener.AcceptTcpClientAsync();
            using TcpClient rawClient = new();
            await rawClient.ConnectAsync("127.0.0.1", port);
            using TcpClient serverClient = await acceptedTask;

            await using FramedTcpChannel channel = FramedTcpChannel.FromAcceptedClient(serverClient);
            using NetworkStream stream = rawClient.GetStream();

            byte[] header = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(header, 2 * 1024 * 1024);
            await stream.WriteAsync(header);
            await stream.FlushAsync();

            Assert.That(
                async () => await channel.ReceiveAsync(CancellationToken.None),
                Throws.TypeOf<InvalidOperationException>());
        }
        finally
        {
            listener.Stop();
        }
    }

    [Test]
    public async Task Send_WhenPayloadTooLarge_Throws()
    {
        int port = TestHelpers.GetFreeTcpPort();
        TcpListener listener = new(IPAddress.Loopback, port);
        listener.Start();
        try
        {
            Task<TcpClient> acceptedTask = listener.AcceptTcpClientAsync();
            await using FramedTcpChannel client = await FramedTcpChannel.ConnectAsync("127.0.0.1", port, CancellationToken.None);
            using TcpClient serverClient = await acceptedTask;

            ProtocolEnvelope hugeEnvelope = new()
            {
                Type = MessageTypes.TestCase,
                SessionToken = "s1",
                Payload = JObject.FromObject(new
                {
                    data = new string('a', 1_200_000)
                })
            };

            Assert.That(
                async () => await client.SendAsync(hugeEnvelope, CancellationToken.None),
                Throws.TypeOf<InvalidOperationException>());
        }
        finally
        {
            listener.Stop();
        }
    }
}
