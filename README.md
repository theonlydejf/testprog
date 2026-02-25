# testprog

`testprog` is a .NET toolkit for running and evaluating student solutions through a client/server workflow.

It contains:

- `testprog.messenger`: shared messaging protocol, transport, and discovery
- `testprog.client`: student-facing APIs for connecting to the test server
- `testprog.server`: instructor/server-side runtime and evaluation APIs
- `testprog`: convenience meta-package that depends on all packages above

## Documentation

- Main documentation source: `docs/`
- Tutorials: `docs/tutorials/`
- API reference source for DocFX: `docs/api/`

## Packages

NuGet packages are published on tagged releases (`vX.X.X`) through GitHub Actions.

- `testprog.messenger`
- `testprog.client`
- `testprog.server`
- `testprog`
