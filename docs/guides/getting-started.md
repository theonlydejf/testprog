# Rychly start

Minimalni postup pro lokalni vyvoj a testovani.

## Pozadavky

- `.NET SDK 7.0`
- `docfx` (global tool nebo lokalni binarka)

## Build a test projektu

```bash
# z korene repozitare

dotnet build testprog.sln
dotnet test unit-tests/unit-tests.csproj
```

## Spusteni serveru (CLI)

```bash
dotnet run --project server-cli -- \
  --config docs/internal/live-testing/examples/sum-smoke/server-config.v1.json
```

Volitelne s logem:

```bash
dotnet run --project server-cli -- \
  --config docs/internal/live-testing/examples/sum-smoke/server-config.v1.json \
  --log-file logs/sum-smoke.log
```

## Spusteni studentskeho klienta

V druhem terminalu:

```bash
dotnet run --project examples/sum-student-client
```

## Build dokumentace (DocFX)

```bash
cd docs
docfx metadata docfx.json
docfx build docfx.json
```

Vystup: `docs/_site`.
