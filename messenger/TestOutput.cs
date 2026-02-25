using Newtonsoft.Json.Linq;

namespace testprog.messenger;

/// <summary>
/// Wrapper for normalized testcase output payload.
/// </summary>
public sealed class TestOutput
{
    private TestOutput(JObject payload)
    {
        Payload = payload;
    }

    /// <summary>
    /// Output payload represented as JSON object.
    /// </summary>
    public JObject Payload { get; }

    /// <summary>
    /// Creates normalized output wrapper from arbitrary object.
    /// </summary>
    /// <param name="value">Output object, scalar value, or existing <see cref="TestOutput"/>.</param>
    /// <returns>Normalized output wrapper.</returns>
    public static TestOutput FromObject(object? value)
    {
        if (value is TestOutput output)
        {
            return output;
        }

        return new TestOutput(ProtocolSerializer.ToPayloadObject(value));
    }
}
