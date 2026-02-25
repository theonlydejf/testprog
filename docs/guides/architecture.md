# Architecture

The solution is split into focused projects.

## Project layout

- `messenger`: transport, discovery, protocol envelopes, and core client runtime.
- `client`: student-facing API (`StudentClientOptions`, `StudentConsoleTestRunner`).
- `server`: suite model, config loader, and test host runtime.
- `server-cli`: executable host with dashboard and log output.
- `unit-tests`: protocol, behavior, and validation tests.

## Runtime flow

1. Client discovers server (`Auto`) or connects directly (`DirectTcp`).
2. TCP handshake is established (`client-hello` -> `server-hello`).
3. Server streams groups and testcases.
4. Client responds with `testcase-solved` payloads.
5. Server returns verdicts and final run summary.

## Related specs

- [Communication Protocol](../internal/live-testing/comm-protocol.md)
- [Server Configuration v1](../internal/live-testing/server-config-v1.md)
- [Messaging Reference](../internal/live-testing/messaging/message-list.md)
