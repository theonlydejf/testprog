# Vyučující: `server-cli` v praxi

Tento návod předpokládá, že máte k dispozici binárku `server-cli`.

## 1. Spuštění přes binárku (doporučeno)

```bash
./server-cli --config ./server-config.json
```

Volitelně s logem:

```bash
./server-cli --config ./server-config.json --log-file ./logs/course-a.log
```

## 2. Alternativa při vývoji: `dotnet run`

```bash
dotnet run --project server-cli -- --config ./server-config.json
```

## 3. Co uvidíte po startu

- server identitu a porty
- živý dashboard studentů
- stav session (Running/Completed/Rejected/...)
- počty pass/fail

## 4. Co dělat během výuky

- nechte dashboard otevřený po celou dobu
- při incidentu si poznamenejte `studentId`, `reasonCode` a čas
- log soubor použijte pro zpětnou analýzu

## 5. Zastavení serveru

- `Ctrl+C` provede graceful shutdown
- aktivní session se korektně ukončí

## 6. Rychlá kontrola konfigurace před hodinou

1. spusťte server s produkční konfigurací
2. připojte jeden testovací klient
3. ověřte průchod alespoň jedné skupiny testů
4. zkontrolujte, že se generuje log
