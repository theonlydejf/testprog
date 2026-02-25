using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace testprog.messenger;

public sealed class TestInput
{
    private readonly JObject _root;

    public TestInput(JObject root)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
    }

    public static TestInput FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("Input JSON cannot be empty.", nameof(json));
        }

        JObject? root = JsonConvert.DeserializeObject<JObject>(json);
        if (root is null)
        {
            throw new JsonException("Input JSON object could not be parsed.");
        }

        return new TestInput(root);
    }

    public int GetInt(string key)
    {
        return GetRequiredToken(key).Value<int>();
    }

    public string GetString(string key)
    {
        return GetRequiredToken(key).Value<string>()
            ?? throw new InvalidOperationException($"Property '{key}' must be a string.");
    }

    public bool GetBool(string key)
    {
        return GetRequiredToken(key).Value<bool>();
    }

    public double GetDouble(string key)
    {
        return GetRequiredToken(key).Value<double>();
    }

    public int GetFirstInt()
    {
        foreach (JProperty property in _root.Properties())
        {
            if (property.Value.Type == JTokenType.Integer)
            {
                return property.Value.Value<int>();
            }
        }

        throw new KeyNotFoundException("Input does not contain any integer value.");
    }

    public string GetFirstString()
    {
        foreach (JProperty property in _root.Properties())
        {
            if (property.Value.Type == JTokenType.String)
            {
                return property.Value.Value<string>()!;
            }
        }

        throw new KeyNotFoundException("Input does not contain any string value.");
    }

    public T Parse<T>()
    {
        T? parsed = _root.ToObject<T>();
        if (parsed is null)
        {
            throw new JsonException($"Input cannot be parsed into {typeof(T).Name}.");
        }

        return parsed;
    }

    public JObject ToJsonObject()
    {
        return (JObject)_root.DeepClone();
    }

    private JToken GetRequiredToken(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key cannot be empty.", nameof(key));
        }

        if (!_root.TryGetValue(key, StringComparison.Ordinal, out JToken? token))
        {
            throw new KeyNotFoundException($"Input does not contain key '{key}'.");
        }

        return token;
    }
}
