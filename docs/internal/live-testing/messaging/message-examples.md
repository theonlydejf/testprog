# Message Examples

This page contains formatted JSON examples for every protocol message type.

All examples use protocol version `v = 2` and illustrative values.

## server-wanted

```json
{
  "v": 2,
  "type": "server-wanted",
  "requestId": "a10fd4557cb74f26bf5c8ef0ed4f483d",
  "sentAtUtc": "2026-02-17T12:00:50Z",
  "payload": {
    "studentId": "novakj",
    "displayName": "Jan Novak"
  }
}
```

## server-available

```json
{
  "v": 2,
  "type": "server-available",
  "requestId": "dce5c02474684f4ea1743f62d245f390",
  "sentAtUtc": "2026-02-17T12:01:00Z",
  "payload": {
    "serverId": "teacher-pc-01",
    "serverHost": "192.168.1.10",
    "serverPort": 5000
  }
}
```

## client-hello

```json
{
  "v": 2,
  "type": "client-hello",
  "requestId": "91f60e8b6f4840db87e9c42cc6f5dd11",
  "sentAtUtc": "2026-02-17T12:01:10Z",
  "payload": {
    "studentId": "novakj",
    "displayName": "Jan Novak",
    "clientVersion": "1.0.0"
  }
}
```

## server-hello

```json
{
  "v": 2,
  "type": "server-hello",
  "requestId": "d777f9f1964142448c8da5f9f571ed20",
  "sentAtUtc": "2026-02-17T12:01:15Z",
  "payload": {
    "sessionToken": "ae2f8c657da44fd1a2312a3bdc5d6ca6",
    "heartbeatSeconds": 10
  }
}
```

## test-begin

```json
{
  "v": 2,
  "type": "test-begin",
  "sessionToken": "ae2f8c657da44fd1a2312a3bdc5d6ca6",
  "requestId": "3f07bdffafdb4b8ca8bfd8c36f0686a1",
  "sentAtUtc": "2026-02-17T12:01:20Z",
  "payload": {
    "studentId": "novakj",
    "displayName": "Jan Novak"
  }
}
```

## ping

```json
{
  "v": 2,
  "type": "ping",
  "sessionToken": "ae2f8c657da44fd1a2312a3bdc5d6ca6",
  "requestId": "a2684f224b7841cd8d8dbde97f7f62c2",
  "sentAtUtc": "2026-02-17T12:01:25Z",
  "payload": {}
}
```

## pong

```json
{
  "v": 2,
  "type": "pong",
  "sessionToken": "ae2f8c657da44fd1a2312a3bdc5d6ca6",
  "requestId": "d5305b5dd77a4650aef2234f7d66db20",
  "sentAtUtc": "2026-02-17T12:01:25Z",
  "payload": {}
}
```

## testgroup-start

```json
{
  "v": 2,
  "type": "testgroup-start",
  "sessionToken": "ae2f8c657da44fd1a2312a3bdc5d6ca6",
  "requestId": "9ce5d56289ef4d20a67e58a7b996d8de",
  "sentAtUtc": "2026-02-17T12:01:30Z",
  "payload": {
    "groupId": "sum-basic",
    "displayName": "Addition basics",
    "testcaseCount": 2
  }
}
```

## testcase

```json
{
  "v": 2,
  "type": "testcase",
  "sessionToken": "ae2f8c657da44fd1a2312a3bdc5d6ca6",
  "requestId": "b8a0df4e9e5b4b61a004a6f4ec48176c",
  "sentAtUtc": "2026-02-17T12:02:00Z",
  "payload": {
    "testcaseId": "sum-001",
    "input": {
      "a": 2,
      "b": 3
    }
  }
}
```

## testcase-solved

```json
{
  "v": 2,
  "type": "testcase-solved",
  "sessionToken": "ae2f8c657da44fd1a2312a3bdc5d6ca6",
  "requestId": "6fcf9e8d34c54c8ab8d2c17b1b4c8c54",
  "sentAtUtc": "2026-02-17T12:02:01Z",
  "payload": {
    "testcaseId": "sum-001",
    "output": {
      "result": 5
    }
  }
}
```

## testcase-result

```json
{
  "v": 2,
  "type": "testcase-result",
  "sessionToken": "ae2f8c657da44fd1a2312a3bdc5d6ca6",
  "requestId": "cf6bb35f4f534f46a3d560f3f8bb9baa",
  "sentAtUtc": "2026-02-17T12:02:01Z",
  "payload": {
    "testcaseId": "sum-001",
    "status": "passed",
    "reasonCode": null,
    "reasonDetail": null
  }
}
```

## testgroup-end

```json
{
  "v": 2,
  "type": "testgroup-end",
  "sessionToken": "ae2f8c657da44fd1a2312a3bdc5d6ca6",
  "requestId": "80f786ba09da4f7889a2de5e89fcdcbc",
  "sentAtUtc": "2026-02-17T12:02:10Z",
  "payload": {
    "groupId": "sum-basic"
  }
}
```

## test-end

```json
{
  "v": 2,
  "type": "test-end",
  "sessionToken": "ae2f8c657da44fd1a2312a3bdc5d6ca6",
  "requestId": "8e32ad6c9ac04c17acef11ea12260376",
  "sentAtUtc": "2026-02-17T12:02:15Z",
  "payload": {
    "testGroupCount": 1,
    "testcaseCount": 2,
    "passedCount": 2,
    "failedCount": 0
  }
}
```

## stop-client

```json
{
  "v": 2,
  "type": "stop",
  "sessionToken": "ae2f8c657da44fd1a2312a3bdc5d6ca6",
  "requestId": "43e856d28d1646ed9aa7a19f89385d20",
  "sentAtUtc": "2026-02-17T12:05:00Z",
  "payload": {
    "reasonCode": "client-stop",
    "reasonDetail": "Student cancelled the run."
  }
}
```

## stop-server

```json
{
  "v": 2,
  "type": "stop",
  "sessionToken": "ae2f8c657da44fd1a2312a3bdc5d6ca6",
  "requestId": "f13f3311ac744929af7f2b2dbc4aa0e1",
  "sentAtUtc": "2026-02-17T12:06:00Z",
  "payload": {
    "reasonCode": "server-stop",
    "reasonDetail": "Teacher terminated this testing session."
  }
}
```

## error

```json
{
  "v": 2,
  "type": "error",
  "sessionToken": "ae2f8c657da44fd1a2312a3bdc5d6ca6",
  "requestId": "e9e3896a6ad540d6aef58cd85f6317ba",
  "sentAtUtc": "2026-02-17T12:06:05Z",
  "payload": {
    "reasonCode": "internal-server-error",
    "reasonDetail": "Unexpected runtime error while evaluating testcase."
  }
}
```
