# Tutorial pro vyucujici: Jak tvorit test suite

Konfigurace je JSON se sekcemi `server` a `suite`.

## Minimalni staticka skupina

```json
{
  "server": {
    "serverId": "seminar-a",
    "advertiseHost": "127.0.0.1",
    "discoveryPort": 15001,
    "tcpPort": 15000
  },
  "suite": {
    "groups": [
      {
        "id": "sum-basic",
        "name": "Scitani",
        "testcases": [
          {
            "id": "sum-001",
            "comparisonMode": "strict-json",
            "input": {
              "mode": "inline",
              "value": { "a": 2, "b": 3 }
            },
            "expectedOutput": 5
          }
        ]
      }
    ]
  }
}
```

## `goldenStandard`

Misto pevneho `expectedOutput` muzete pouzit zdrojovy soubor:

```json
"goldenStandard": {
  "sourceFile": "sum-golden-standard.cs",
  "typeName": "GoldenStandard",
  "methodName": "Solve"
}
```

Pozadovana signatura:

```csharp
public static object Solve(JObject input)
```

Ukazka: `docs/internal/live-testing/examples/sum-smoke/sum-golden-standard.cs`.

## Random testy

Skupina muze mit `random` misto `testcases`.

Dva rezimy generatoru vstupu:

- `default`: pole `intFields` s rozsahy
- `source-file`: vlastni metoda `Generate(Random random, int testcaseIndex)`

Ukazka generatoru: `docs/internal/live-testing/examples/sum-smoke/sum-random-input-generator.cs`.

## Dulezite validace

- kazda skupina musi mit prave jeden zdroj: `testcases` nebo `random`
- kazdy testcase musi mit prave jedno: `expectedOutput` nebo `goldenStandard`
- `group.id` i `testcase.id` musi byt unikatni
- porty 1..65535, timeouty > 0

Detailni reference: `docs/internal/live-testing/server-config-v1.md`.
