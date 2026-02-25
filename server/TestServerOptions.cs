using System.Net;

namespace testprog.server;

public sealed class TestServerOptions
{
    public string ServerId { get; init; } = Environment.MachineName;
    public string AdvertiseHost { get; init; } = string.Empty;
    public string DiscoveryMulticastAddress { get; init; } = "239.0.0.222";
    public int DiscoveryPort { get; init; } = 11000;
    public int TcpPort { get; init; } = 5000;
    public int MaxConcurrentSessions { get; init; } = 32;
    public TimeSpan ClientResponseTimeout { get; init; } = TimeSpan.FromSeconds(10);
    public IReadOnlyList<string> StudentIdWhitelist { get; init; } = Array.Empty<string>();

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(ServerId))
        {
            throw new ArgumentException("ServerId is required.", nameof(ServerId));
        }

        if (DiscoveryPort is <= 0 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(DiscoveryPort), "DiscoveryPort must be between 1 and 65535.");
        }

        if (TcpPort is <= 0 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(TcpPort), "TcpPort must be between 1 and 65535.");
        }

        if (MaxConcurrentSessions <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxConcurrentSessions), "MaxConcurrentSessions must be greater than zero.");
        }

        if (ClientResponseTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ClientResponseTimeout), "ClientResponseTimeout must be greater than zero.");
        }

        if (StudentIdWhitelist is null)
        {
            throw new ArgumentException("StudentIdWhitelist cannot be null.", nameof(StudentIdWhitelist));
        }

        HashSet<string> uniqueIds = new(StringComparer.OrdinalIgnoreCase);
        foreach (string studentId in StudentIdWhitelist)
        {
            if (string.IsNullOrWhiteSpace(studentId))
            {
                throw new ArgumentException("StudentIdWhitelist cannot contain empty values.", nameof(StudentIdWhitelist));
            }

            if (!uniqueIds.Add(studentId))
            {
                throw new ArgumentException($"StudentIdWhitelist contains duplicate id '{studentId}'.", nameof(StudentIdWhitelist));
            }
        }

        ParseMulticastAddress(DiscoveryMulticastAddress);
    }

    internal bool IsStudentAllowed(string studentId)
    {
        if (StudentIdWhitelist.Count == 0)
        {
            return true;
        }

        return StudentIdWhitelist.Any(allowed =>
            string.Equals(allowed, studentId, StringComparison.OrdinalIgnoreCase));
    }

    internal string ResolveAdvertiseHost()
    {
        if (!string.IsNullOrWhiteSpace(AdvertiseHost))
        {
            return AdvertiseHost;
        }

        IPAddress? bestAddress = Dns.GetHostAddresses(Dns.GetHostName())
            .FirstOrDefault(static address =>
                address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                !IPAddress.IsLoopback(address));

        return bestAddress?.ToString() ?? "127.0.0.1";
    }

    internal static IPAddress ParseMulticastAddress(string value)
    {
        if (!IPAddress.TryParse(value, out IPAddress? parsed))
        {
            throw new ArgumentException("Discovery multicast address is not a valid IP.", nameof(value));
        }

        if (parsed.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
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
}
