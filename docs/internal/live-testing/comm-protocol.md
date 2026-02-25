# Communication Protocol v2

This document describes the wire protocol used by live testing clients and servers.

## Transports

- UDP multicast: discovery (`server-wanted`, `server-available`)
- TCP: handshake, testcase exchange, progress, and stop/error control

## TCP framing

Each TCP frame is one UTF-8 JSON envelope.

Frame layout:

1. 4-byte signed big-endian message length
2. JSON payload bytes

Client-side frame limit is 1 MiB.

## Envelope format

All TCP messages use the same envelope:

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

Envelope fields:

- `v`: protocol version (`2`)
- `type`: message type name
- `sessionToken`: required after server handshake
- `requestId`: correlation identifier
- `sentAtUtc`: sender timestamp
- `payload`: message-specific object

## Session lifecycle

1. client sends UDP `server-wanted`
2. server replies UDP `server-available`
3. client opens TCP and sends `client-hello`
4. server responds `server-hello` with `sessionToken`
5. server sends `test-begin`
6. per group: `testgroup-start` ... `testgroup-end`
7. per testcase: `testcase` -> `testcase-solved` -> `testcase-result`
8. server sends `test-end`

## Keepalive and termination

- server may send `ping`
- client responds with `pong`
- if heartbeat timeout is exceeded, client fails fast
- either side may send `stop` with `reasonCode` and optional `reasonDetail`
- server may send `error` on protocol/runtime failure

## Message catalog

See [Messaging Overview](messaging/message-list.md) for message-by-message semantics and sample payload links.
