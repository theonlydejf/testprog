# Server CLI v1

`server-cli` is the operational entry point for instructors.

## Command line

```bash
dotnet run --project server-cli -- --config ./server-config.json
```

Optional log output file:

```bash
dotnet run --project server-cli -- --config ./server-config.json --log-file ./logs/server.log
```

## Runtime capabilities

- live dashboard with per-student state
- pass/fail counters per active session
- group-level progress visibility
- whitelist enforcement via server config
- structured runtime event logging

## Student states shown in dashboard

- `Running`
- `Completed`
- `Completed (partial)`
- `Rejected`
- `Stopped`
- `Error`

## Log record content

Each runtime event can include:

- UTC timestamp
- student identity
- remote endpoint
- session token
- group/testcase identifiers
- reason code/detail for failure or stop events

## Operational notes

- `Ctrl+C` triggers graceful shutdown
- active sessions are drained before full process exit

## Related references

- [Server Configuration v1](server-config-v1.md)
- [Sum Smoke Test](sum-smoke-test.md)
