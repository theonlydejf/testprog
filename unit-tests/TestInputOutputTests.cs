using Newtonsoft.Json.Linq;
using testprog.messenger;

namespace unit_tests;

public class TestInputOutputTests
{
    [Test]
    public void FromJson_And_Getters_Work()
    {
        TestInput input = TestInput.FromJson("""
        {
          "a": 7,
          "name": "jan",
          "flag": true
        }
        """);

        Assert.That(input.GetInt("a"), Is.EqualTo(7));
        Assert.That(input.GetString("name"), Is.EqualTo("jan"));
        Assert.That(input.GetBool("flag"), Is.True);
        Assert.That(input.GetFirstInt(), Is.EqualTo(7));
        Assert.That(input.GetFirstString(), Is.EqualTo("jan"));
    }

    [Test]
    public void GetInt_MissingKey_Throws()
    {
        TestInput input = TestInput.FromJson("""{ "a": 1 }""");
        Assert.That(() => input.GetInt("missing"), Throws.TypeOf<KeyNotFoundException>());
    }

    [Test]
    public void GetFirstInt_NoInteger_Throws()
    {
        TestInput input = TestInput.FromJson("""{ "name": "x" }""");
        Assert.That(() => input.GetFirstInt(), Throws.TypeOf<KeyNotFoundException>());
    }

    [Test]
    public void Parse_GenericObject_Works()
    {
        TestInput input = TestInput.FromJson("""{ "a": 10, "b": 20 }""");
        SumInput parsed = input.Parse<SumInput>();

        Assert.That(parsed.A, Is.EqualTo(10));
        Assert.That(parsed.B, Is.EqualTo(20));
    }

    [Test]
    public void TestOutput_FromNull_ReturnsEmptyObject()
    {
        TestOutput output = TestOutput.FromObject(null);
        Assert.That(output.Payload.Properties(), Is.Empty);
    }

    [Test]
    public void TestOutput_FromScalar_WrapsValue()
    {
        TestOutput output = TestOutput.FromObject(15);
        Assert.That(output.Payload["value"]?.Value<int>(), Is.EqualTo(15));
    }

    [Test]
    public void TestOutput_FromObject_KeepsProperties()
    {
        TestOutput output = TestOutput.FromObject(new { result = 123 });
        Assert.That(output.Payload["result"]?.Value<int>(), Is.EqualTo(123));
    }

    private sealed class SumInput
    {
        public int A { get; init; }
        public int B { get; init; }
    }
}
