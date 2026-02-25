# Sum Smoke Test

This scenario verifies end-to-end server/client integration on a basic `a + b` task.

## Assets

- config: `docs/internal/live-testing/examples/sum-smoke/server-config.v1.json`
- golden standard: `docs/internal/live-testing/examples/sum-smoke/sum-golden-standard.cs`
- random input generator: `docs/internal/live-testing/examples/sum-smoke/sum-random-input-generator.cs`
- sample client: `examples/sum-student-client/Program.cs`

The suite includes:

- static testcase with `expectedOutput`
- static testcase with `goldenStandard`
- random group with `default` input generator
- random group with `source-file` input generator

## Execution

Start server:

```bash
dotnet run --project server-cli -- \
  --config docs/internal/live-testing/examples/sum-smoke/server-config.v1.json
```

Run sample client:

```bash
dotnet run --project examples/sum-student-client -- novakj "Jan Novak" 127.0.0.1 15000
```

## Expected result

Client output should contain only passing testcases and final summary `Failed: 0`.

## Typical issues

- occupied TCP/discovery ports
- mismatch between configured server port and client port argument
- stale host override when server is running on another address
