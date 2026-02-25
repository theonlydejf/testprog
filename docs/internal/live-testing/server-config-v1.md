## Server Config v1

Configuration is a single JSON document with `server` and `suite` sections.

### Top-level shape
```json
{
  "server": { },
  "suite": { }
}
```

### `server` section
- `serverId` (string, optional, default machine name)
- `advertiseHost` (string, optional, default auto-detected IPv4)
- `discoveryMulticastAddress` (string, optional, default `239.0.0.222`)
- `discoveryPort` (int, optional, default `11000`)
- `tcpPort` (int, optional, default `5000`)
- `maxConcurrentSessions` (int, optional, default `32`)
- `clientResponseTimeoutSeconds` (number, optional, default `10`)
- `studentIdWhitelist` (array of strings, optional)
  - empty or missing: everyone is allowed
  - non-empty: only listed `studentId` values can run tests

### `suite` section
- `groups` (array, required, at least 1)

Group shape:
- `id` (string, required)
- `name` (string, required)
- testcase source (exactly one required):
  - `testcases` (array, static cases)
  - `random` (object, generated cases)

Testcase shape:
- `id` (string, required)
- `comparisonMode` (string, optional)
  - `strict-json` (default)
  - `normalized-text`
- `input` (object, required)
  - `mode` (string, required)
    - `inline` (only supported mode in v1)
  - `value` (object, required for `inline`)
- expected output source (exactly one required):
  - `expectedOutput` (any JSON token)
  - `goldenStandard` (object)
    - `sourceFile` (string, required)
    - `typeName` (string, optional, default `GoldenStandard`)
    - `methodName` (string, optional, default `Solve`)

Random group (`random`) shape:
- `count` (int, optional, default `10`)
- `testCaseIdPrefix` (string, optional, default `random-`)
- `seed` (int, optional)
- `comparisonMode` (string, optional, default `strict-json`)
- `goldenStandard` (object, required)
  - `sourceFile` (string, required)
  - `typeName` (string, optional, default `GoldenStandard`)
  - `methodName` (string, optional, default `Solve`)
- `inputGenerator` (object, required)
  - `mode` (string, optional, default `default`)
    - `default`
    - `source-file`
  - when `mode = default`:
    - `intFields` (array, required, at least 1)
      - field shape:
        - `name` (string, required)
        - `min` (int, optional, default `0`)
        - `max` (int, optional, default `100`)
  - when `mode = source-file`:
    - `sourceFile` (string, required)
    - `typeName` (string, optional, default `RandomInputGenerator`)
    - `methodName` (string, optional, default `Generate`)

Path rules for source files:
- in `LoadFromFile(...)`, relative paths are resolved against config file directory
- in `LoadFromJson(...)`, relative paths are resolved against process working directory

`goldenStandard` contract:
- server compiles `sourceFile` at runtime
- expected static method signature: `public static object Solve(JObject input)`
- method return can be `JToken` or any JSON-serializable object

`source-file` random input generator contract:
- server compiles `inputGenerator.sourceFile` at runtime
- expected static method signature: `public static object Generate(Random random, int testcaseIndex)`
- method return must be a JSON object (`JObject` or JSON-serializable object)
- minimal sample: `docs/internal/live-testing/random-input-generator.example.cs`

### Validation rules
- duplicate group ids are rejected
- duplicate testcase ids inside one static group are rejected
- invalid ports/timeouts are rejected
- invalid/duplicate whitelist values are rejected
- unsupported `input.mode` or `comparisonMode` are rejected
- testcase must define exactly one of `expectedOutput` / `goldenStandard`
- each group must define exactly one source: `testcases` or `random`
- `random.goldenStandard` is required
- `random.inputGenerator` is required and must match selected `mode`
- default random generator rejects duplicate field names or invalid ranges (`min > max`)

### Future extension note
`random` is intentionally group-level, so v2 can add richer generators without changing static testcase format.
