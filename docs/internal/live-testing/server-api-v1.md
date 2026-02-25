## Server API v1 draft

### Goal
Teacher defines test groups + testcases and runs one host service.

### Example
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
                    ExpectedOutput = JObject.FromObject(new { result = 5 })
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

### Comparison modes
- `StrictJson`: deep JSON equality (`JToken.DeepEquals`)
- `NormalizedText`: compares minified JSON after recursively trimming all string values

### Golden standard testcases
- testcase can use `ExpectedOutput` or `GoldenStandard` (exactly one)
- golden standard source is compiled by the server at startup
- required signature: `public static object Solve(JObject input)`

### Randomized groups
- group can use `Randomized` instead of `TestCases`
- randomized group generates `Count` testcase inputs at runtime
- expected output is always resolved via group `GoldenStandard`
- input generator modes:
  - `Default`: configured list of integer fields with min/max range
  - `SourceFile`: compiled method `public static object Generate(Random random, int testcaseIndex)`

### Runtime behavior
- UDP multicast discovery (`server-wanted` -> `server-available`)
- TCP session with length-prefixed JSON envelopes
- per testcase flow: `testcase` -> `testcase-solved` -> `testcase-result`
- fail-fast on timeout / invalid protocol messages
