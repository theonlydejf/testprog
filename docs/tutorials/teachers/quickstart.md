# Tutorial pro vyucujici: Quickstart

Cil: do 5 minut spustit server a overit studentske reseni.

## 1. Pripravte konfiguraci

Pouzijte hotovy smoke config:

- `docs/internal/live-testing/examples/sum-smoke/server-config.v1.json`

Obsahuje:

- staticke testcases
- `goldenStandard`
- random skupiny (`default` i `source-file` generator)

## 2. Spustte server

```bash
dotnet run --project server-cli -- \
  --config docs/internal/live-testing/examples/sum-smoke/server-config.v1.json
```

## 3. Spustte ukazkoveho klienta

```bash
dotnet run --project examples/sum-student-client -- novakj "Jan Novak" 127.0.0.1 15000
```

## 4. Co sledovat v dashboardu

- stav studenta (`Running`, `Completed`, `Stopped`, `Rejected`)
- pocitadla pass/fail
- aktualni skupina
- cesta k log souboru

## 5. Graceful stop

`Ctrl+C` v terminalu serveru.
