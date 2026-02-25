using System.Net;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;

namespace testprog.messenger;

internal readonly record struct ServerEndpoint(string Host, int Port);

internal static class ServerDiscovery
{
    public static Task<ServerEndpoint> ResolveAsync(
        TestProgClientOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Discovery.Mode == DiscoveryMode.DirectTcp)
        {
            return Task.FromResult(new ServerEndpoint(
                options.Discovery.DirectServerHost!,
                options.Discovery.DirectServerPort));
        }

        return ResolveViaUdpMulticastAsync(options, cancellationToken);
    }

    private static async Task<ServerEndpoint> ResolveViaUdpMulticastAsync(
        TestProgClientOptions options,
        CancellationToken cancellationToken)
    {
        using UdpClient udpClient = new();
        IPAddress multicastAddress = ParseMulticastAddress(options.Discovery.MulticastAddress);
        IPEndPoint multicastEndPoint = new(multicastAddress, options.Discovery.MulticastPort);

        ProtocolEnvelope wantedEnvelope = new()
        {
            Type = MessageTypes.ServerWanted,
            Payload = ProtocolSerializer.ToPayloadObject(new ServerWantedPayload
            {
                StudentId = options.StudentId,
                DisplayName = options.DisplayName
            })
        };

        string wantedJson = ProtocolSerializer.Serialize(wantedEnvelope);
        byte[] wantedBytes = Encoding.UTF8.GetBytes(wantedJson);
        await udpClient.SendAsync(wantedBytes, wantedBytes.Length, multicastEndPoint).ConfigureAwait(false);

        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(options.Discovery.DiscoveryTimeout);

        while (true)
        {
            UdpReceiveResult response = await ReceiveAsync(udpClient, timeoutCts.Token).ConfigureAwait(false);
            string responseJson = Encoding.UTF8.GetString(response.Buffer);

            ProtocolEnvelope envelope;
            try
            {
                envelope = ProtocolSerializer.Deserialize(responseJson);
            }
            catch (JsonException)
            {
                continue;
            }

            if (!string.Equals(envelope.Type, MessageTypes.ServerAvailable, StringComparison.Ordinal))
            {
                continue;
            }

            ServerAvailablePayload payload;
            try
            {
                payload = ProtocolSerializer.DeserializePayload<ServerAvailablePayload>(envelope);
            }
            catch (JsonException)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(payload.ServerHost))
            {
                continue;
            }

            if (payload.ServerPort is <= 0 or > 65535)
            {
                continue;
            }

            return new ServerEndpoint(payload.ServerHost, payload.ServerPort);
        }
    }

    private static IPAddress ParseMulticastAddress(string value)
    {
        if (!IPAddress.TryParse(value, out IPAddress? parsed))
        {
            throw new ArgumentException("Discovery multicast address is not a valid IP.", nameof(value));
        }

        if (parsed.AddressFamily != AddressFamily.InterNetwork)
        {
            throw new ArgumentException("Discovery multicast address must be an IPv4 address.", nameof(value));
        }

        byte firstByte = parsed.GetAddressBytes()[0];
        if (firstByte < 224 || firstByte > 239)
        {
            throw new ArgumentException("Discovery multicast address must be in 224.0.0.0/4 range.", nameof(value));
        }

        return parsed;
    }

    private static async Task<UdpReceiveResult> ReceiveAsync(UdpClient client, CancellationToken cancellationToken)
    {
        Task<UdpReceiveResult> receiveTask = client.ReceiveAsync();
        Task waitTask = Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        Task completed = await Task.WhenAny(receiveTask, waitTask).ConfigureAwait(false);

        if (completed == receiveTask)
        {
            return await receiveTask.ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        throw new OperationCanceledException(cancellationToken);
    }
}
