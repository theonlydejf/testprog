# Tutorial (English): Student + Instructor

This is a compact end-to-end tutorial for both roles: student and instructor.

## 1. Instructor: start the test server

Use the prepared config:

```bash
dotnet run --project server-cli -- \
  --config docs/internal/live-testing/examples/sum-smoke/server-config.v1.json
```

Optional log file:

```bash
dotnet run --project server-cli -- \
  --config docs/internal/live-testing/examples/sum-smoke/server-config.v1.json \
  --log-file logs/sum-smoke.log
```

## 2. Student: connect your solver

Minimal client example:

```csharp
using testprog.client;

StudentClientOptions options = new()
{
    StudentId = "novakj",
    DisplayName = "Jan Novak",
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

Run the sample client without editing code:

```bash
dotnet run --project examples/sum-student-client -- novakj "Jan Novak" 127.0.0.1 15000
```

## 3. Expected result

- Student sees live progress (`PASS`/`FAIL`) and final summary.
- Instructor sees student status in the server dashboard.
- Successful run ends with `Failed: 0`.

## 4. Common issues

- `timeout`: solver responds too slowly.
- `unauthorized`: `studentId` is not in server whitelist.
- `invalid-answer`: output JSON shape does not match testcase expectation.

## 5. More docs

- Server config: [server-config-v1](../internal/live-testing/server-config-v1.md)
- Wire protocol: [comm-protocol](../internal/live-testing/comm-protocol.md)
- API reference: [API](../api/index.md)
