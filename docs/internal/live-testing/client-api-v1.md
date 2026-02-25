## Client API v1 draft

### Goal
Keep student integration as simple as possible. Student provides only a solver function.

### Student-friendly sync console API (recommended)
```csharp
using testprog.client;
using testprog.messenger;

StudentClientOptions options = new()
{
    StudentId = "novakj",
    DisplayName = "Jan Novak"
};

int exitCode = StudentConsoleTestRunner.RunWithExitCode(options, input =>
{
    int a = input.GetInt("a");
    int b = input.GetInt("b");
    return new { result = a + b };
});

return exitCode;
```

This prints live progress to console:
- test run start
- each test group start/end
- each testcase result (pass/fail)
- final summary

### Advanced async API (low-level)
```csharp
await using ITestProgClient client = await TestProgClient.ConnectAsync(new TestProgClientOptions
{
    StudentId = "novakj",
    DisplayName = "Jan Novak"
});

TestRunSummary summary = await client.RunAsync(
    input => new { result = input.GetInt("a") + input.GetInt("b") },
    progress => Console.WriteLine($"{progress.Kind} {progress.TestCaseId}"));
```

### Input helpers
- `GetInt(string key)`
- `GetString(string key)`
- `GetBool(string key)`
- `GetDouble(string key)`
- `GetFirstInt()`
- `GetFirstString()`
- `Parse<T>()`

### Behavior
- Discovery: UDP multicast
- Session: TCP
- Auth/session correlation: server-issued `sessionToken`
- Optional progress callback on each group/testcase transition
- TCP wire format: 4-byte length prefix + UTF-8 JSON envelope
- Connection loss: fail-fast
