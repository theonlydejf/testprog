using Newtonsoft.Json.Linq;
using testprog.messenger;

namespace unit_tests;

public class ProtocolSerializerTests
{
    [Test]
    public void Serialize_And_Deserialize_Roundtrip_Works()
    {
        ProtocolEnvelope input = new()
        {
            Type = MessageTypes.TestCase,
            SessionToken = "session-123",
            RequestId = "req-1",
            Payload = JObject.FromObject(new { a = 2, b = 3 })
        };

        string json = ProtocolSerializer.Serialize(input);
        ProtocolEnvelope output = ProtocolSerializer.Deserialize(json);

        Assert.That(output.Version, Is.EqualTo(2));
        Assert.That(output.Type, Is.EqualTo(MessageTypes.TestCase));
        Assert.That(output.SessionToken, Is.EqualTo("session-123"));
        Assert.That(output.RequestId, Is.EqualTo("req-1"));
        Assert.That(output.Payload["a"]?.Value<int>(), Is.EqualTo(2));
        Assert.That(output.Payload["b"]?.Value<int>(), Is.EqualTo(3));
    }

    [Test]
    public void Deserialize_EmptyJson_Throws()
    {
        Assert.That(
            () => ProtocolSerializer.Deserialize(""),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void ToPayloadObject_WithScalar_WrapsToValueProperty()
    {
        JObject payload = ProtocolSerializer.ToPayloadObject(42);
        Assert.That(payload["value"]?.Value<int>(), Is.EqualTo(42));
    }

    [Test]
    public void ToPayloadObject_WithJObject_ReturnsClone()
    {
        JObject original = JObject.FromObject(new { name = "abc" });
        JObject payload = ProtocolSerializer.ToPayloadObject(original);

        payload["name"] = "changed";
        Assert.That(original["name"]?.Value<string>(), Is.EqualTo("abc"));
    }
}
