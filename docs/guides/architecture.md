# Architektura projektu

Projekt je rozdelen do peti casti:

- `messenger`: transport a protokol klient-server (`TestProgClient`, zpravy, serializace).
- `client`: studentske API (`StudentClientOptions`, `StudentConsoleTestRunner`).
- `server`: host testu (`TestServerHost`), konfigurace a evaluace vystupu.
- `server-cli`: spustitelna aplikace pro vyucujici.
- `unit-tests`: testy protokolu, konfigurace a chovani serveru.

## Komunikacni tok

1. Klient najde server (`discovery`) nebo se pripoji primym TCP.
2. Probehne handshake `client-hello` -> `server-hello`.
3. Server posila testcase, klient vraci vysledky.
4. Server vraci `testcase-result` a na konci souhrn nebo `stop`.

## Kde hledat detaily

- Wire protokol: [`internal/live-testing/comm-protocol.md`](../internal/live-testing/comm-protocol.md)
- Seznam zprav: [`internal/live-testing/messaging/message-list.md`](../internal/live-testing/messaging/message-list.md)
- API docs: [API reference](../api/index.md)
