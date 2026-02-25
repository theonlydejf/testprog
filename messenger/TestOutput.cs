using Newtonsoft.Json.Linq;

namespace testprog.messenger;

public sealed class TestOutput
{
    private TestOutput(JObject payload)
    {
        Payload = payload;
    }

    public JObject Payload { get; }

    public static TestOutput FromObject(object? value)
    {
        if (value is TestOutput output)
        {
            return output;
        }

        return new TestOutput(ProtocolSerializer.ToPayloadObject(value));
    }
}
