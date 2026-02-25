## Sum Smoke Test (end-to-end)

This is a minimal end-to-end example to verify server + client integration on a simple task:
add two integers (`a + b`).
In this example, student solver returns a plain integer output.

### Files used
- server config: `docs/internal/live-testing/examples/sum-smoke/server-config.v1.json`
- golden standard: `docs/internal/live-testing/examples/sum-smoke/sum-golden-standard.cs`
- random input generator script: `docs/internal/live-testing/examples/sum-smoke/sum-random-input-generator.cs`
- student sample client: `examples/sum-student-client/Program.cs`

The config includes:
- testcase with `expectedOutput`
- testcase with `goldenStandard`
- random group with generated testcases (`default` input generator + `goldenStandard`)
- random group with generated testcases (`source-file` input generator + `goldenStandard`)

### 1. Start the server
Run from repository root:

```bash
dotnet run --project server-cli -- --config docs/internal/live-testing/examples/sum-smoke/server-config.v1.json
```

Optional with explicit log file:

```bash
dotnet run --project server-cli -- --config docs/internal/live-testing/examples/sum-smoke/server-config.v1.json --log-file logs/sum-smoke.log
```

### 2. Start the student client
Open a second terminal and run:

```bash
dotnet run --project examples/sum-student-client
```

Optional arguments:
- arg1: `studentId`
- arg2: `displayName`
- arg3: `serverHost`
- arg4: `serverPort`

Example:

```bash
dotnet run --project examples/sum-student-client -- novakj "Jan Novak" 127.0.0.1 15000
```

### 3. Expected result
Client should report:
- `PASS sum-expected-001`
- `PASS sum-golden-001`
- `PASS sum-random-1`
- `PASS sum-random-2`
- `PASS sum-random-3`
- `PASS sum-random-script-1`
- `PASS sum-random-script-2`
- `PASS sum-random-script-3`
- final summary with `Failed: 0`

### Troubleshooting
- `Fatal error: Address already in use` means TCP or discovery port is occupied.
- Update `tcpPort` and `discoveryPort` in `docs/internal/live-testing/examples/sum-smoke/server-config.v1.json`.
- If you change `tcpPort`, pass the same port as client arg4 (or update default in `examples/sum-student-client/Program.cs`).

### 4. Stop the server
Use `Ctrl+C` in the server terminal.
