# Instructor Guide: Configuration file in detail

This page is a detailed explanation of `server-config.json`.

## Top-level structure

```json
{
  "server": {},
  "suite": {}
}
```

Both sections are required.

## `server` section

### `serverId` (string)

- logical server identifier
- defaults to machine name when missing

### `advertiseHost` (string)

- host advertised to clients in discovery replies
- if missing, server attempts to auto-detect an IPv4 address

### `discoveryMulticastAddress` (string)

- default: `239.0.0.222`
- must be an IPv4 multicast address (224.0.0.0/4)

### `discoveryPort` (int)

- UDP discovery port
- valid range: 1..65535

### `tcpPort` (int)

- TCP session port
- valid range: 1..65535

### `maxConcurrentSessions` (int)

- max parallel student sessions
- must be > 0

### `clientResponseTimeoutSeconds` (number)

- timeout for student response to a testcase
- must be > 0
- used as default when a testcase or random group does not define its own `timeoutSeconds`

### `studentIdWhitelist` (array<string>)

- empty/missing: all student IDs accepted
- non-empty: only listed IDs accepted
- duplicate and empty values are invalid

## `suite` section

`suite.groups` is an array of groups (minimum 1).

Every group must define exactly one testcase source:

- `testcases` (static)
- `random` (generated)

## Static testcase group (`testcases`)

```json
{
  "id": "sum-basic",
  "name": "Addition",
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

Rules:

- `input.mode` currently supports only `inline`
- testcase must define exactly one of:
  - `expectedOutput`
  - `goldenStandard`
- testcase IDs must be unique inside the group
- optional `timeoutSeconds` overrides the server default for this testcase

## `goldenStandard` in testcase

```json
"goldenStandard": {
  "sourceFile": "sum-golden-standard.cs",
  "typeName": "GoldenStandard",
  "methodName": "Solve"
}
```

Required method signature:

```csharp
public static object Solve(JObject input)
```

## Random group (`random`)

```json
{
  "id": "sum-random",
  "name": "Random addition",
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

Required parts for random groups:

- `goldenStandard`
- `inputGenerator`
- optional `timeoutSeconds` overrides the server default for every generated testcase in the group

### `inputGenerator.mode = default`

- define `intFields` with value ranges
- validation rejects `min > max`
- duplicate field names are invalid

### `inputGenerator.mode = source-file`

```json
"inputGenerator": {
  "mode": "source-file",
  "sourceFile": "sum-random-input-generator.cs",
  "typeName": "RandomInputGenerator",
  "methodName": "Generate"
}
```

Required method signature:

```csharp
public static object Generate(Random random, int testcaseIndex)
```

## `comparisonMode`

Supported values:

- `strict-json` (default)
- `normalized-text`

Use `normalized-text` for text outputs where whitespace differences should be tolerated.

## Relative source path resolution

- `LoadFromFile(path)`: relative paths are resolved against config file directory
- `LoadFromJson(json)`: relative paths are resolved against process working directory

## Common config mistakes

- both `testcases` and `random` set in one group
- testcase without `expectedOutput` and without `goldenStandard`
- invalid port value (`0`, `> 65535`)
- duplicate `group.id` or `testcase.id`
- whitelist containing empty value

## Maintenance recommendations

- version control your config files
- keep internal templates for assignment types
- run a smoke test before each class

Reference spec: [Server Configuration v1](../../internal/live-testing/server-config-v1.md)
