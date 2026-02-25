using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace testprog.messenger;

public static class ProtocolSerializer
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        MissingMemberHandling = MissingMemberHandling.Ignore,
        NullValueHandling = NullValueHandling.Ignore
    };

    public static string Serialize(ProtocolEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return JsonConvert.SerializeObject(envelope, Settings);
    }

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
