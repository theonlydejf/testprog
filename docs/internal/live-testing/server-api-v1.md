# Server API v1

The server API is centered around `TestServerHost`, `TestServerOptions`, and `TestSuiteDefinition`.

## Main usage

```csharp
using Newtonsoft.Json.Linq;
using testprog.server;

TestSuiteDefinition suite = new()
{
    Groups = new List<TestGroupDefinition>
    {
        new()
        {
            GroupId = "basic",
            DisplayName = "Basic tests",
            TestCases = new List<TestCaseDefinition>
            {
                new()
                {
                    TestCaseId = "sum-001",
                    Input = JObject.FromObject(new { a = 2, b = 3 }),
                    ExpectedOutput = new JValue(5)
                },
                new()
                {
                    TestCaseId = "sum-002",
                    Input = JObject.FromObject(new { a = 10, b = -4 }),
                    GoldenStandard = new GoldenStandardDefinition
                    {
                        SourceFilePath = "golden/SumGolden.cs",
                        TypeName = "GoldenStandard",
                        MethodName = "Solve"
                    }
                }
            }
        }
    }
};

TestServerOptions options = new()
{
    ServerId = "teacher-pc-01",
    TcpPort = 5000
};

await using TestServerHost host = new(options, suite);
await host.RunAsync(cancellationToken);
```

## Key model constraints

- a group must define exactly one source: `TestCases` or `Randomized`
- a testcase must define exactly one expected source: `ExpectedOutput` or `GoldenStandard`
- random groups require both `GoldenStandard` and `InputGenerator`

## Comparison modes

- `StrictJson`: deep token equality
- `NormalizedText`: compares normalized text representation

## Runtime behavior

- compiles source-file contracts at startup (golden standards and source generators)
- uses UDP discovery plus TCP sessions
- enforces timeouts and protocol validity
- emits runtime events via `Action<TestServerRuntimeEvent>` callback

## Related references

- [Server Configuration v1](server-config-v1.md)
- [Server CLI v1](server-cli-v1.md)
- [API Reference](../../api/index.md)
