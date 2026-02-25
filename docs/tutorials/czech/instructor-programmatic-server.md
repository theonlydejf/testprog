# Vyučující: Vlastní server programově

Pokud potřebujete vlastní orchestraci (např. integraci do interního systému), můžete server spouštět přes `testprog.server` API.

## Kdy to dává smysl

- chcete vlastní UI místo CLI
- potřebujete napojení na interní autentizaci
- chcete programově sestavovat suite nebo runtime eventy

## Minimalní programové spuštění

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

## Programově z JSON konfigurace

```csharp
LoadedServerConfiguration loaded = TestServerConfigurationLoader.LoadFromFile("./server-config.json");

await using TestServerHost host = new(loaded.ServerOptions, loaded.Suite);
await host.RunAsync(CancellationToken.None);
```

## Co řešit navíc oproti `server-cli`

- lifecycle aplikace (signal handling, shutdown)
- vlastní logování a rotace logů
- bezpečné zacházení s chybami v callbacku runtime eventů

Související reference:

- [Server API v1](../../internal/live-testing/server-api-v1.md)
- [Server CLI v1](../../internal/live-testing/server-cli-v1.md)
