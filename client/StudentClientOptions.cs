using testprog.messenger;

namespace testprog.client;

public sealed class StudentClientOptions
{
    public string StudentId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;

    public string? ServerHost { get; init; }
    public int ServerPort { get; init; } = 5000;

    public string DiscoveryMulticastAddress { get; init; } = "239.0.0.222";
    public int DiscoveryPort { get; init; } = 11000;
    public TimeSpan DiscoveryTimeout { get; init; } = TimeSpan.FromSeconds(3);

    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(8);
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
