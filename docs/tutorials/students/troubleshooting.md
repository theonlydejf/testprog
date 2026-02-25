# Tutorial pro studenty: Troubleshooting

## `Unable to connect within configured timeout`

Server neni dostupny nebo je spatne `host/port`.

Kontrola:

- bezi `server-cli`
- pouzivate stejny `tcpPort` jako v server configu
- pri auto-discovery sedi `discoveryPort` a multicast adresa

## `Expected 'server-hello' but received ...`

Pripojili jste se na jiny endpoint nebo nekompatibilni sluzbu.

## `Run was stopped (timeout)`

Server nedostal odpoved na testcase v limite.

Reseni:

- zrychlit solver
- omezit I/O a externi volani
- kontaktovat vyucujiciho kvuli nastaveni `clientResponseTimeoutSeconds`

## `Run was stopped (unauthorized)`

Vase `studentId` neni na whitelistu serveru.

Poslete vyucujicimu presne `studentId`, ktere pouzivate.
