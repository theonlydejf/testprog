# Tutorial pro vyucujici: Provoz a diagnostika

## Doporuceny operacni postup

1. Spustit server s explicitnim configem a log souborem.
2. Overit pripojeni testovacim studentskym klientem.
3. Behem cviceni sledovat dashboard a log.
4. Pri incidentu ulozit log a konfiguraci k reprodukci.

## Spousteci prikazy

```bash
dotnet run --project server-cli -- \
  --config path/to/server-config.json \
  --log-file logs/seminar-a.log
```

## Typicke problemy

### Neautorizovany student (`unauthorized`)

- Zkontrolujte `studentIdWhitelist` v konfiguraci.
- Overte, ze student pouziva spravne `StudentId`.

### Timeouty (`timeout`)

- Zvednete `clientResponseTimeoutSeconds`.
- Snizte slozitost testu nebo jejich pocet.

### Chybne formatovana odpoved (`invalid-answer`)

- Porovnejte ocekavany JSON shape testcase a studentsky vystup.
- U textovych uloh zvazte `comparisonMode: normalized-text`.

## Co logovat do LMS / reportu

- verzi konfigurace
- commit/hash test suite (pokud pouzivate VCS)
- cas behu, pass/fail statistiky
- duvod stop udalosti (`reasonCode`, `reasonDetail`)

## Doporucene zdroje

- Protokol: `docs/internal/live-testing/comm-protocol.md`
- Server CLI: `docs/internal/live-testing/server-cli-v1.md`
- API: `docs/api/index.md`
