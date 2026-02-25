using testprog.messenger;

namespace testprog.client;

/// <summary>
/// High-level options used by students to connect to a test server.
/// </summary>
public sealed class StudentClientOptions
{
    /// <summary>
    /// Unique student identifier used for authorization and reporting.
    /// </summary>
    public string StudentId { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable student name shown in server dashboards and logs.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Optional direct server host. When omitted, UDP discovery is used.
    /// </summary>
    public string? ServerHost { get; init; }

    /// <summary>
    /// Direct TCP port used together with <see cref="ServerHost"/>.
    /// </summary>
    public int ServerPort { get; init; } = 15001;

    /// <summary>
    /// Multicast address used for UDP server discovery.
    /// </summary>
    public string DiscoveryMulticastAddress { get; init; } = "239.0.0.222";

    /// <summary>
    /// UDP discovery port used to locate the server.
    /// </summary>
    public int DiscoveryPort { get; init; } = 11000;

    /// <summary>
    /// Maximum time allowed for UDP discovery.
    /// </summary>
    public TimeSpan DiscoveryTimeout { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Maximum time allowed to establish transport connection and complete handshake.
    /// </summary>
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(8);

    /// <summary>
    /// Maximum allowed interval between server messages before treating the session as stalled.
    /// </summary>
    public TimeSpan HeartbeatTimeout { get; init; } = TimeSpan.FromSeconds(10);

    internal TestProgClientOptions ToCoreOptions()
    {
        Validate();

        DiscoveryOptions discovery = string.IsNullOrWhiteSpace(ServerHost)
            ? new DiscoveryOptions
            {
                Mode = DiscoveryMode.Auto,
                MulticastAddress = DiscoveryMulticastAddress,
                MulticastPort = DiscoveryPort,
                DiscoveryTimeout = DiscoveryTimeout
            }
            : new DiscoveryOptions
            {
                Mode = DiscoveryMode.DirectTcp,
                DirectServerHost = ServerHost,
                DirectServerPort = ServerPort,
                MulticastAddress = DiscoveryMulticastAddress,
                MulticastPort = DiscoveryPort,
                DiscoveryTimeout = DiscoveryTimeout
            };

        return new TestProgClientOptions
        {
            StudentId = StudentId,
            DisplayName = DisplayName,
            ConnectTimeout = ConnectTimeout,
            HeartbeatTimeout = HeartbeatTimeout,
            Discovery = discovery
        };
    }

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(StudentId))
        {
            throw new ArgumentException("StudentId is required.", nameof(StudentId));
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            throw new ArgumentException("DisplayName is required.", nameof(DisplayName));
        }

        if (ServerPort is <= 0 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(ServerPort), "ServerPort must be between 1 and 65535.");
        }

        if (DiscoveryPort is <= 0 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(DiscoveryPort), "DiscoveryPort must be between 1 and 65535.");
        }

        if (DiscoveryTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(DiscoveryTimeout), "DiscoveryTimeout must be greater than zero.");
        }

        if (ConnectTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ConnectTimeout), "ConnectTimeout must be greater than zero.");
        }

        if (HeartbeatTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(HeartbeatTimeout), "HeartbeatTimeout must be greater than zero.");
        }
    }
}
