# Instructor Guide: Operating `server-cli`

This guide assumes you have a ready-to-run `server-cli` binary.

## 1. Start with the binary (recommended)

```bash
./server-cli --config ./server-config.json
```

Optional log file:

```bash
./server-cli --config ./server-config.json --log-file ./logs/course-a.log
```

## 2. Development alternative: `dotnet run`

```bash
dotnet run --project server-cli -- --config ./server-config.json
```

## 3. What appears after startup

- server identity and ports
- live student dashboard
- session status (Running/Completed/Rejected/...)
- pass/fail counters

## 4. During class operations

- keep the dashboard visible for all active sessions
- when incidents happen, capture `studentId`, `reasonCode`, and timestamp
- use the log file for post-class analysis

## 5. Stopping the server

- `Ctrl+C` triggers graceful shutdown
- active sessions are closed cleanly

## 6. Pre-class sanity check

1. start server with production config
2. connect one test client
3. verify at least one group executes end-to-end
4. verify log output is being written
