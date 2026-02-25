using testprog.messenger;

namespace unit_tests;

public class ClientOptionsValidationTests
{
    [Test]
    public void ConnectAsync_NullOptions_Throws()
    {
        Assert.That(
            async () => await TestProgClient.ConnectAsync(null!),
            Throws.TypeOf<ArgumentNullException>());
    }

    [Test]
    public void ConnectAsync_MissingStudentId_Throws()
    {
        TestProgClientOptions options = new()
        {
            StudentId = "",
            DisplayName = "Jan"
        };

        Assert.That(
            async () => await TestProgClient.ConnectAsync(options),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void ConnectAsync_MissingDisplayName_Throws()
    {
        TestProgClientOptions options = new()
        {
            StudentId = "novakj",
            DisplayName = ""
        };

        Assert.That(
            async () => await TestProgClient.ConnectAsync(options),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void ConnectAsync_InvalidConnectTimeout_Throws()
    {
        TestProgClientOptions options = new()
        {
            StudentId = "novakj",
            DisplayName = "Jan",
            ConnectTimeout = TimeSpan.Zero
        };

        Assert.That(
            async () => await TestProgClient.ConnectAsync(options),
            Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void ConnectAsync_DirectModeMissingHost_Throws()
    {
        TestProgClientOptions options = new()
        {
            StudentId = "novakj",
            DisplayName = "Jan",
            Discovery = new DiscoveryOptions
            {
                Mode = DiscoveryMode.DirectTcp,
                DirectServerHost = "",
                DirectServerPort = 5000
            }
        };

        Assert.That(
            async () => await TestProgClient.ConnectAsync(options),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void ConnectAsync_InvalidMulticastAddress_Throws()
    {
        TestProgClientOptions options = new()
        {
            StudentId = "novakj",
            DisplayName = "Jan",
            Discovery = new DiscoveryOptions
            {
                Mode = DiscoveryMode.Auto,
                MulticastAddress = "1.1.1.1",
                MulticastPort = 11000
            }
        };

        Assert.That(
            async () => await TestProgClient.ConnectAsync(options),
            Throws.TypeOf<ArgumentException>());
    }
}
