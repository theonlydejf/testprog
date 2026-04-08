# Vyučující: Konfigurační soubor do detailu

Toto je detailní popis formátu `server-config.json`.

## Top-level struktura

```json
{
  "server": {},
  "suite": {}
}
```

Obě sekce jsou povinné.

## Sekce `server`

### `serverId` (string)

- logická identita serveru
- pokud není uvedeno, použije se název stroje

### `advertiseHost` (string)

- host, který server vrací klientům v discovery odpovědi
- pokud chybí, server zkusí automaticky detekovat IPv4

### `discoveryMulticastAddress` (string)

- výchozí: `239.0.0.222`
- musí být IPv4 multicast adresa (224.0.0.0/4)

### `discoveryPort` (int)

- UDP port pro discovery
- rozsah 1..65535

### `tcpPort` (int)

- TCP port pro testovací session
- rozsah 1..65535

### `maxConcurrentSessions` (int)

- limit současných klientských session
- musí být > 0

### `clientResponseTimeoutSeconds` (number)

- timeout na odpověď klienta na testcase
- musí být > 0
- používá se jako default, pokud testcase nebo random skupina nemá vlastní `timeoutSeconds`

### `studentIdWhitelist` (array<string>)

- prázdné/nezadané: povoleni všichni
- neprázdné: povoleni jen uvedení studenti
- duplicity a prázdné hodnoty jsou neplatné

## Sekce `suite`

`suite.groups` je pole skupin. Musí obsahovat alespoň jednu položku.

Každá skupina musí mít přesně jeden zdroj testů:

- `testcases` (statické)
- `random` (generované)

## Statické testcase: `testcases`

```json
{
  "id": "sum-basic",
  "name": "Scitani",
  "testcases": [
    {
      "id": "sum-001",
      "timeoutSeconds": 2.5,
      "comparisonMode": "strict-json",
      "input": {
        "mode": "inline",
        "value": { "a": 2, "b": 3 }
      },
      "expectedOutput": 5
    }
  ]
}
```

Pravidla:

- `input.mode` je aktuálně pouze `inline`
- testcase musí mít právě jedno z:
  - `expectedOutput`
  - `goldenStandard`
- `id` testcase musí být unikátní v rámci skupiny
- volitelné `timeoutSeconds` přepíše serverový default pro tento testcase

## `goldenStandard` v testcase

```json
"goldenStandard": {
  "sourceFile": "sum-golden-standard.cs",
  "typeName": "GoldenStandard",
  "methodName": "Solve"
}
```

Požadovaný podpis metody:

```csharp
public static object Solve(JObject input)
```

## Random skupina: `random`

```json
{
  "id": "sum-random",
  "name": "Random sum",
  "random": {
    "count": 10,
    "testCaseIdPrefix": "random-",
    "seed": 42,
    "timeoutSeconds": 15,
    "comparisonMode": "strict-json",
    "goldenStandard": {
      "sourceFile": "sum-golden-standard.cs"
    },
    "inputGenerator": {
      "mode": "default",
      "intFields": [
        { "name": "a", "min": -100, "max": 100 },
        { "name": "b", "min": -100, "max": 100 }
      ]
    }
  }
}
```

Povinné části random skupiny:

- `goldenStandard`
- `inputGenerator`
- volitelné `timeoutSeconds` přepíše serverový default pro každý vygenerovaný testcase ve skupině

### `inputGenerator.mode = default`

- definujete `intFields` a rozsahy
- validace odmítne `min > max`
- duplicita názvů polí je neplatná

### `inputGenerator.mode = source-file`

```json
"inputGenerator": {
  "mode": "source-file",
  "sourceFile": "sum-random-input-generator.cs",
  "typeName": "RandomInputGenerator",
  "methodName": "Generate"
}
```

Požadovaný podpis metody:

```csharp
public static object Generate(Random random, int testcaseIndex)
```

## `comparisonMode`

Podporované hodnoty:

- `strict-json` (default)
- `normalized-text`

`normalized-text` se hodí pro textové výstupy, kde nechcete penalizovat whitespace.

## Rozlišování relativních cest

- při `LoadFromFile(path)` jsou relativní cesty vztažené k adresáři config souboru
- při `LoadFromJson(json)` jsou relativní cesty vztažené k aktuálnímu pracovnímu adresáři procesu

## Nejčastější chyby v configu

- obě `testcases` i `random` ve stejné skupině
- testcase bez `expectedOutput` i bez `goldenStandard`
- neplatný port (`0`, `> 65535`)
- duplicita `group.id` nebo `testcase.id`
- whitelist s prázdnou položkou

## Doporučení pro údržbu

- držte config ve verzovacím systému
- používejte komentované interní šablony pro každý typ úlohy
- před hodinou prověřte config na smoke testu

Detailní specifikace: [Server Configuration v1](../../internal/live-testing/server-config-v1.md)
