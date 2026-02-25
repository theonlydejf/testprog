# testprog.messenger

Core messaging library used by `testprog.client` and `testprog.server`.

## What it provides

- Message contracts for test session communication
- Serialization helpers
- Transport primitives
- UDP-based discovery support

## Install

```bash
dotnet add package testprog.messenger
```

## Typical usage

Most users do not consume this package directly. Prefer:

- `testprog.client` for student-side apps
- `testprog.server` for server-side hosting
- `testprog` when you want all components
