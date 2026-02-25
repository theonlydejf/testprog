# Messaging Overview

All protocol message types used by discovery and test sessions.

| Message Type | Transport | Direction | Purpose | Example |
| --- | --- | --- | --- | --- |
| `server-wanted` | UDP | client -> multicast | Ask for available servers | [server-wanted](message-examples.md#server-wanted) |
| `server-available` | UDP | server -> client | Advertise server endpoint | [server-available](message-examples.md#server-available) |
| `client-hello` | TCP | client -> server | Send student identity | [client-hello](message-examples.md#client-hello) |
| `server-hello` | TCP | server -> client | Confirm session and token | [server-hello](message-examples.md#server-hello) |
| `test-begin` | TCP | server -> client | Start test run | [test-begin](message-examples.md#test-begin) |
| `ping` | TCP | server -> client | Keepalive signal | [ping](message-examples.md#ping) |
| `pong` | TCP | client -> server | Keepalive response | [pong](message-examples.md#pong) |
| `testgroup-start` | TCP | server -> client | Start a group | [testgroup-start](message-examples.md#testgroup-start) |
| `testcase` | TCP | server -> client | Send testcase input | [testcase](message-examples.md#testcase) |
| `testcase-solved` | TCP | client -> server | Submit computed output | [testcase-solved](message-examples.md#testcase-solved) |
| `testcase-result` | TCP | server -> client | Verdict for one testcase | [testcase-result](message-examples.md#testcase-result) |
| `testgroup-end` | TCP | server -> client | End a group | [testgroup-end](message-examples.md#testgroup-end) |
| `test-end` | TCP | server -> client | End test run | [test-end](message-examples.md#test-end) |
| `stop` | TCP | both | Graceful/forced session stop | [client](message-examples.md#stop-client), [server](message-examples.md#stop-server) |
| `error` | TCP | server -> client | Protocol/runtime error | [error](message-examples.md#error) |

## Notes

- all TCP messages are wrapped in the common protocol envelope
- after `server-hello`, every TCP message must carry `sessionToken`
- semantic details are defined in [Communication Protocol v2](../comm-protocol.md)
