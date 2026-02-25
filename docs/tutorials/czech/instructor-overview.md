# Vyučující: Jak systém funguje

Tato stránka popisuje celkový model práce.

## Role komponent

- `server-cli`: spouští testovací server, dashboard a logování
- `server` knihovna: model suite, validace konfigurace, běh session
- `client` knihovna: studentské API pro řešení úloh
- `messenger`: protokol a přenos zpráv

## Typický provozní scénář

1. připravíte `server-config.json`
2. spustíte `server-cli`
3. studenti spouští klienty se svou identitou
4. server vyhodnocuje testcase a průběžně loguje výsledky

## Kde jsou detailní návody

- [Vyučující - server-cli](instructor-server-cli.md)
- [Vyučující - Konfigurace (detailně)](instructor-configuration.md)
- [Vyučující - Server programově](instructor-programmatic-server.md)
- [Internal Specifications](../../internal/live-testing/index.md)
