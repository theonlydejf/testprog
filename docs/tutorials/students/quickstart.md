# Tutorial pro studenty: Quickstart

Cil: spustit vlastni reseni proti testovacimu serveru.

## 1. Minimalni klient

Pouzij API z `client` projektu:

```csharp
using testprog.client;

StudentClientOptions options = new()
{
    StudentId = "novakj",
    DisplayName = "Jan Novak"
};

int exitCode = StudentConsoleTestRunner.RunWithExitCode(options, input =>
{
    int a = input.GetInt("a");
    int b = input.GetInt("b");
    return a + b;
});

return exitCode;
```

## 2. Spusteni

```bash
dotnet run --project examples/sum-student-client -- novakj "Jan Novak" 127.0.0.1 15000
```

Argumenty:

- `studentId`
- `displayName`
- `serverHost`
- `serverPort`

## 3. Jak cist vysledky

Konzole zobrazuje:

- start behu testu
- start a konec skupin
- `PASS`/`FAIL` pro kazdy testcase
- finalni souhrn (`Passed`, `Failed`)

Exit code:

- `0`: vse proslo
- `1`: chyba pripojeni, stop runu, nebo aspon jeden neuspesny testcase
