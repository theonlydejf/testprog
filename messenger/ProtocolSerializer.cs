using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace testprog.messenger;

/// <summary>
/// Helpers for serializing and deserializing protocol envelopes and payload objects.
/// </summary>
public static class ProtocolSerializer
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        MissingMemberHandling = MissingMemberHandling.Ignore,
        NullValueHandling = NullValueHandling.Ignore
    };

    /// <summary>
    /// Serializes a protocol envelope to a JSON string.
    /// </summary>
    /// <param name="envelope">Envelope to serialize.</param>
    /// <returns>JSON string representation of <paramref name="envelope"/>.</returns>
    public static string Serialize(ProtocolEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return JsonConvert.SerializeObject(envelope, Settings);
    }

    /// <summary>
    /// Deserializes a protocol envelope from a JSON string.
    /// </summary>
    /// <param name="json">Envelope JSON.</param>
    /// <returns>Parsed <see cref="ProtocolEnvelope"/> instance.</returns>
    public static ProtocolEnvelope Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("JSON cannot be empty.", nameof(json));
        }

        ProtocolEnvelope? envelope = JsonConvert.DeserializeObject<ProtocolEnvelope>(json, Settings);
        if (envelope is null)
        {
            throw new JsonException("Protocol envelope could not be parsed.");
        }

        return envelope;
    }

    /// <summary>
    /// Converts arbitrary value to payload <see cref="JObject"/>.
    /// </summary>
    /// <param name="payload">Source payload value.</param>
    /// <returns>
    /// A deep-cloned <see cref="JObject"/> when object payload is supplied,
    /// an empty object for <see langword="null"/>,
    /// or a wrapped object with <c>value</c> property for scalar tokens.
    /// </returns>
    public static JObject ToPayloadObject(object? payload)
    {
        if (payload is null)
        {
            return new JObject();
        }

        if (payload is JObject jObject)
        {
            return (JObject)jObject.DeepClone();
        }

        JToken token = JToken.FromObject(payload);
        if (token is JObject tokenObject)
        {
            return tokenObject;
        }

        return new JObject(new JProperty("value", token));
    }

    /// <summary>
    /// Deserializes typed payload object from envelope payload JSON.
    /// </summary>
    /// <typeparam name="T">Expected payload type.</typeparam>
    /// <param name="envelope">Envelope containing payload.</param>
    /// <returns>Typed payload instance.</returns>
    public static T DeserializePayload<T>(ProtocolEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        T? parsed = envelope.Payload.ToObject<T>();
        if (parsed is null)
        {
            throw new JsonException($"Payload cannot be parsed into {typeof(T).Name}.");
        }

        return parsed;
    }
}
