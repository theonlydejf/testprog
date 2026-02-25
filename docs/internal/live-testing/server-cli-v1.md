## Server CLI v1

### Usage
```bash
dotnet run --project server-cli -- --config ./server-config.json
dotnet run --project server-cli -- --config ./server-config.json --log-file ./logs/server.log
```

### Features
- live dashboard of students currently attempting tests
- per-student status (`Running`, `Completed`, `Completed (partial)`, `Rejected`, `Stopped`, `Error`)
- per-student counters (`passed/failed/total`)
- shows current group name for each student
- optional whitelist support from config (`server.studentIdWhitelist`)
- runtime event logging into a log file

### Controls
- `Ctrl+C` triggers graceful stop

### Log format
- one line per runtime event with timestamp, student id, session token, group, testcase and reason fields

### Quick smoke test
- end-to-end example (server config + golden standard + student client): `docs/internal/live-testing/sum-smoke-test.md`
