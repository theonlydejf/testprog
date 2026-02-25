# Getting Started

This guide covers the quickest path to run the full `testprog` flow locally.

## Prerequisites

- .NET SDK 7.0+
- repository cloned locally

## Build and test

```bash
dotnet build testprog.sln
dotnet test unit-tests/unit-tests.csproj
```

## Run the server

```bash
dotnet run --project server-cli -- \
  --config docs/internal/live-testing/examples/sum-smoke/server-config.v1.json
```

## Run the sample student client

In another terminal:

```bash
dotnet run --project examples/sum-student-client -- novakj "Jan Novak" 127.0.0.1 15000
```

## Expected outcome

- server dashboard shows an active student session
- client prints live testcase progress
- run ends with a final summary
