# Client API v1

Client integration is intentionally minimal for student usage while still exposing a lower-level async API.

## Recommended student API

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

Behavior of `StudentConsoleTestRunner`:

- connects to server (auto discovery or direct host)
- prints progress for groups and testcases
- prints final pass/fail summary
- returns process-friendly exit code

## Advanced async API

```csharp
await using ITestProgClient client = await TestProgClient.ConnectAsync(new TestProgClientOptions
{
    StudentId = "novakj",
    DisplayName = "Jan Novak"
});

TestRunSummary summary = await client.RunAsync(
    input => new { result = input.GetInt("a") + input.GetInt("b") },
    progress => Console.WriteLine($"{progress.Kind}: {progress.TestCaseId}"));
```

## Input helper methods (`TestInput`)

- `GetInt(string key)`
- `GetString(string key)`
- `GetBool(string key)`
- `GetDouble(string key)`
- `GetFirstInt()`
- `GetFirstString()`
- `Parse<T>()`

## Discovery and connection

- discovery mode `Auto`: UDP multicast endpoint resolution
- discovery mode `DirectTcp`: direct host/port connection
- handshake returns `sessionToken`, required for subsequent messages

## Related references

- [Communication Protocol v2](comm-protocol.md)
- [Messaging Overview](messaging/message-list.md)
- [API Reference](../../api/index.md)
