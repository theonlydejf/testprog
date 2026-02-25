# Instructor Overview: How the system works

This page explains the operating model at a high level.

## Component roles

- `server-cli`: starts the test host, dashboard, and logs
- `server` library: suite model, configuration validation, runtime host
- `client` library: student-facing runtime API
- `messenger`: protocol and message transport layer

## Typical teaching workflow

1. prepare `server-config.json`
2. start `server-cli`
3. students run clients with identity
4. server evaluates testcases and logs outcomes

## Where to go next

- [Instructor - server-cli](instructor-server-cli.md)
- [Instructor - Configuration (detailed)](instructor-configuration.md)
- [Instructor - Programmatic Server](instructor-programmatic-server.md)
- [Internal Specifications](../../internal/live-testing/index.md)
