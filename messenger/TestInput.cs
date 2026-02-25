using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace testprog.messenger;

/// <summary>
/// Typed accessor wrapper around testcase input JSON object.
/// </summary>
public sealed class TestInput
{
    private readonly JObject _root;

    /// <summary>
    /// Initializes input wrapper from an existing JSON object.
    /// </summary>
    /// <param name="root">Root input object.</param>
    public TestInput(JObject root)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
    }

    /// <summary>
    /// Parses input wrapper from JSON object text.
    /// </summary>
    /// <param name="json">Input JSON object text.</param>
    /// <returns>Parsed <see cref="TestInput"/> instance.</returns>
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

    /// <summary>
    /// Reads required integer value by key.
    /// </summary>
    /// <param name="key">Input property key.</param>
    /// <returns>Property value converted to <see cref="int"/>.</returns>
    public int GetInt(string key)
    {
        return GetRequiredToken(key).Value<int>();
    }

    /// <summary>
    /// Reads required string value by key.
    /// </summary>
    /// <param name="key">Input property key.</param>
    /// <returns>Property value converted to <see cref="string"/>.</returns>
    public string GetString(string key)
    {
        return GetRequiredToken(key).Value<string>()
            ?? throw new InvalidOperationException($"Property '{key}' must be a string.");
    }

    /// <summary>
    /// Reads required boolean value by key.
    /// </summary>
    /// <param name="key">Input property key.</param>
    /// <returns>Property value converted to <see cref="bool"/>.</returns>
    public bool GetBool(string key)
    {
        return GetRequiredToken(key).Value<bool>();
    }

    /// <summary>
    /// Reads required floating-point value by key.
    /// </summary>
    /// <param name="key">Input property key.</param>
    /// <returns>Property value converted to <see cref="double"/>.</returns>
    public double GetDouble(string key)
    {
        return GetRequiredToken(key).Value<double>();
    }

    /// <summary>
    /// Returns first integer property found in input object.
    /// </summary>
    /// <returns>First integer value in property iteration order.</returns>
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

    /// <summary>
    /// Returns first string property found in input object.
    /// </summary>
    /// <returns>First string value in property iteration order.</returns>
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

    /// <summary>
    /// Parses full input object into a custom CLR type.
    /// </summary>
    /// <typeparam name="T">Target type.</typeparam>
    /// <returns>Deserialized instance of <typeparamref name="T"/>.</returns>
    public T Parse<T>()
    {
        T? parsed = _root.ToObject<T>();
        if (parsed is null)
        {
            throw new JsonException($"Input cannot be parsed into {typeof(T).Name}.");
        }

        return parsed;
    }

    /// <summary>
    /// Returns a deep-cloned JSON object representation of the input.
    /// </summary>
    /// <returns>Deep clone of root JSON object.</returns>
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
