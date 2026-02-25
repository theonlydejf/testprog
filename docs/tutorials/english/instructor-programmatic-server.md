# Instructor Guide: Programmatic server hosting

If you need custom orchestration (for example integration with internal systems), host the server directly through `testprog.server` APIs.

## When to use this

- custom UI instead of CLI dashboard
- integration with existing authentication/authorization
- dynamic suite generation or event routing

## Minimal programmatic host

```csharp
using Newtonsoft.Json.Linq;
using testprog.server;

TestSuiteDefinition suite = new()
{
    Groups = new[]
    {
        new TestGroupDefinition
        {
            GroupId = "basic",
            DisplayName = "Basic tests",
            TestCases = new[]
            {
                new TestCaseDefinition
                {
                    TestCaseId = "sum-001",
                    Input = JObject.FromObject(new { a = 2, b = 3 }),
                    ExpectedOutput = new JValue(5),
                    ComparisonMode = TestCaseComparisonMode.StrictJson
                }
            }
        }
    }
};

TestServerOptions options = new()
{
    ServerId = "course-a",
    AdvertiseHost = "127.0.0.1",
    DiscoveryPort = 15001,
    TcpPort = 15000,
    MaxConcurrentSessions = 64,
    ClientResponseTimeout = TimeSpan.FromSeconds(10)
};

void OnEvent(TestServerRuntimeEvent runtimeEvent)
{
    Console.WriteLine($"[{runtimeEvent.Kind}] student={runtimeEvent.StudentId} testcase={runtimeEvent.TestCaseId} reason={runtimeEvent.ReasonCode}");
}

await using TestServerHost host = new(options, suite, OnEvent);
await host.RunAsync(CancellationToken.None);
```

## Start from JSON config programmatically

```csharp
LoadedServerConfiguration loaded = TestServerConfigurationLoader.LoadFromFile("./server-config.json");

await using TestServerHost host = new(loaded.ServerOptions, loaded.Suite);
await host.RunAsync(CancellationToken.None);
```

## Additional responsibilities vs `server-cli`

- application lifecycle and signal handling
- logging strategy and log rotation
- robust exception handling in runtime event callbacks

Related references:

- [Server API v1](../../internal/live-testing/server-api-v1.md)
- [Server CLI v1](../../internal/live-testing/server-cli-v1.md)
