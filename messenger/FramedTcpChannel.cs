using System.Buffers.Binary;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace testprog.messenger;

internal sealed class FramedTcpChannel : IAsyncDisposable
{
    private const int HeaderLength = 4;
    private const int MaxFrameBytes = 1024 * 1024;

    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private bool _disposed;

    private FramedTcpChannel(TcpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
        _stream = client.GetStream();
    }

    internal static FramedTcpChannel FromAcceptedClient(TcpClient client)
    {
        return new FramedTcpChannel(client);
    }

    public static async Task<FramedTcpChannel> ConnectAsync(
        string host,
        int port,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new ArgumentException("Host is required.", nameof(host));
        }

        if (port is <= 0 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535.");
        }

        TcpClient client = new();
        try
        {
            Task connectTask = client.ConnectAsync(host, port);
            Task waitTask = Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            Task completed = await Task.WhenAny(connectTask, waitTask).ConfigureAwait(false);

            if (completed != connectTask)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            await connectTask.ConfigureAwait(false);
            return new FramedTcpChannel(client);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    public async Task SendAsync(ProtocolEnvelope envelope, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(envelope);

        string json = ProtocolSerializer.Serialize(envelope);
        byte[] payloadBytes = Encoding.UTF8.GetBytes(json);
        if (payloadBytes.Length is <= 0 or > MaxFrameBytes)
        {
            throw new InvalidOperationException($"Message size must be between 1 and {MaxFrameBytes} bytes.");
        }

        byte[] header = new byte[HeaderLength];
        BinaryPrimitives.WriteInt32BigEndian(header, payloadBytes.Length);

        await _stream.WriteAsync(header, 0, header.Length, cancellationToken).ConfigureAwait(false);
        await _stream.WriteAsync(payloadBytes, 0, payloadBytes.Length, cancellationToken).ConfigureAwait(false);
        await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProtocolEnvelope> ReceiveAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        byte[] header = new byte[HeaderLength];
        await ReadExactlyAsync(_stream, header, cancellationToken).ConfigureAwait(false);

        int payloadLength = BinaryPrimitives.ReadInt32BigEndian(header);
        if (payloadLength is <= 0 or > MaxFrameBytes)
        {
            throw new InvalidOperationException($"Incoming message size is invalid: {payloadLength}.");
        }

        byte[] payloadBytes = new byte[payloadLength];
        await ReadExactlyAsync(_stream, payloadBytes, cancellationToken).ConfigureAwait(false);

        string payloadJson = Encoding.UTF8.GetString(payloadBytes);
        return ProtocolSerializer.Deserialize(payloadJson);
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    private static async Task ReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer, offset, buffer.Length - offset, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new IOException("Connection closed by remote host.");
            }

            offset += read;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(FramedTcpChannel));
        }
    }
}
