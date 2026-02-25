## Communication Protocol v2 (draft)

### Transport
- UDP multicast: service discovery only (`server-wanted`, `server-available`)
- TCP: full test session and all test messages
- Session behavior: fail-fast on connection loss

### TCP framing
- Each TCP message is a single JSON envelope encoded in UTF-8
- Frame format: 4-byte signed big-endian message length, followed by JSON bytes
- Max frame size on client side: 1 MiB

### Envelope
All TCP messages share one envelope:

```json
{
  "v": 2,
  "type": "testcase",
  "sessionToken": "optional-before-server-hello",
  "requestId": "uuid",
  "sentAtUtc": "2026-02-17T12:00:00Z",
  "payload": {}
}
```

### Session flow
1. `client -> broadcast`: `server-wanted`
2. `server -> client`: `server-available` (contains TCP host and port)
3. `client -> server (TCP)`: `client-hello` (`studentId`, `displayName`)
4. `server -> client`: `server-hello` (issues `sessionToken`)
5. `server -> client`: `test-begin`
6. Repeat for each group:
7. `server -> client`: `testgroup-start`
8. Repeat for each testcase:
9. `server -> client`: `testcase`
10. `client -> server`: `testcase-solved`
11. `server -> client`: `testcase-result` (`passed`/`failed`)
12. `server -> client`: `testgroup-end`
13. `server -> client`: `test-end`

### Keepalive and stop
- `server -> client`: optional `ping`
- `client -> server`: `pong` (response to ping)
- If no message arrives within client heartbeat timeout, client stops the session (`reasonCode: timeout`) and fails fast
- `client/server -> peer`: `stop` (`reasonCode`, `reasonDetail`)
- `server -> client`: `error` for protocol/internal failures
