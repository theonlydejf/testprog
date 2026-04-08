# Server Configuration v1

The server is configured from a single JSON document with `server` and `suite` sections.

## Top-level shape

```json
{
  "server": {},
  "suite": {}
}
```

## `server` section

- `serverId` (string, optional, default: machine name)
- `advertiseHost` (string, optional, default: auto-detected IPv4)
- `discoveryMulticastAddress` (string, optional, default: `239.0.0.222`)
- `discoveryPort` (int, optional, default: `11000`)
- `tcpPort` (int, optional, default: `5000`)
- `maxConcurrentSessions` (int, optional, default: `32`)
- `clientResponseTimeoutSeconds` (number, optional, default: `10`)
- `studentIdWhitelist` (array<string>, optional)

`clientResponseTimeoutSeconds` is the default timeout used when a testcase or random group does not set its own `timeoutSeconds`.

Whitelist behavior:

- missing or empty whitelist: all student IDs are accepted
- non-empty whitelist: only listed IDs are accepted (case-insensitive)

## `suite` section

- `groups` (array, required, minimum 1)

Each group must define exactly one testcase source:

- `testcases` (static list)
- `random` (generated testcases)

### Static testcase group

```json
{
  "id": "sum-basic",
  "name": "Sum basics",
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

Testcase rules:

- `id` is required and must be unique within the group
- `input.mode` currently supports only `inline`
- exactly one of `expectedOutput` or `goldenStandard` is required
- `timeoutSeconds` (number, optional) overrides `server.clientResponseTimeoutSeconds` for this testcase

`comparisonMode`:

- `strict-json` (default)
- `normalized-text`

### Random testcase group

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

Random group requirements:

- `goldenStandard` is required
- `inputGenerator` is required
- `count` must be greater than 0
- `timeoutSeconds` (number, optional) overrides `server.clientResponseTimeoutSeconds` for every generated testcase in the group

Input generator modes:

- `default`: integer fields with ranges
- `source-file`: custom code with `Generate(Random random, int testcaseIndex)`

## Source file contracts

### Golden standard

- configured by `goldenStandard.sourceFile`
- compiled by server at startup
- required static method signature:

```csharp
public static object Solve(JObject input)
```

### Source-file input generator

- configured by `inputGenerator.sourceFile`
- compiled by server at startup
- required static method signature:

```csharp
public static object Generate(Random random, int testcaseIndex)
```

## Path resolution

- `LoadFromFile(...)`: relative source paths are resolved against the config file directory
- `LoadFromJson(...)`: relative source paths are resolved against process working directory

## Validation summary

Configuration loading rejects:

- invalid JSON or wrong node types
- unsupported `comparisonMode`, `input.mode`, or `inputGenerator.mode`
- duplicate `group.id` values
- duplicate `testcase.id` values within static groups
- invalid ports or non-positive timeout values
- invalid whitelist entries (empty or duplicate values)
- invalid random ranges (`min > max`)
