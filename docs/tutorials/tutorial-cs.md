# Tutorial (Čeština): Student + Vyučující

Tento tutorial je krátký end-to-end průchod pro oba role: studenta i vyučujícího.

## 1. Vyučující: spusťte testovací server

Použijte připravenou konfiguraci:

```bash
dotnet run --project server-cli -- \
  --config docs/internal/live-testing/examples/sum-smoke/server-config.v1.json
```

Volitelně se souborem logu:

```bash
dotnet run --project server-cli -- \
  --config docs/internal/live-testing/examples/sum-smoke/server-config.v1.json \
  --log-file logs/sum-smoke.log
```

## 2. Student: připojte solver

Minimální ukázka klienta:

```csharp
using testprog.client;

StudentClientOptions options = new()
{
    StudentId = "novakj",
    DisplayName = "Jan Novák",
    ServerHost = "127.0.0.1",
    ServerPort = 15000,
    DiscoveryPort = 15001
};

int exitCode = StudentConsoleTestRunner.RunWithExitCode(options, input =>
{
    int a = input.GetInt("a");
    int b = input.GetInt("b");
    return a + b;
});

return exitCode;
```

Spuštění ukázkového klienta bez úprav kódu:

```bash
dotnet run --project examples/sum-student-client -- novakj "Jan Novák" 127.0.0.1 15000
```

## 3. Co očekávat

- Student vidí průběh testů (`PASS`/`FAIL`) a finální souhrn.
- Vyučující vidí dashboard se stavem studenta.
- Úspěšný běh končí `Failed: 0`.

## 4. Nejčastější chyby

- `timeout`: student vrací odpověď pozdě.
- `unauthorized`: `studentId` není na whitelistu.
- `invalid-answer`: tvar výstupu neodpovídá očekávání testcase.

## 5. Další dokumentace

- Konfigurace serveru: [server-config-v1](../internal/live-testing/server-config-v1.md)
- Wire protokol: [comm-protocol](../internal/live-testing/comm-protocol.md)
- API reference: [API](../api/index.md)
