# testprog Documentation

Dokumentace pro projekt `testprog` postavena nad **DocFX**.

## Co tu najdete

- [Rychly start](guides/getting-started.md)
- [Architektura projektu](guides/architecture.md)
- [Tutorial Cesky (s diakritikou)](tutorials/tutorial-cs.md)
- [Tutorial English](tutorials/tutorial-en.md)
- [API reference](api/index.md)

## Pro koho je dokumentace

- **Studenti**: jak napsat solver, pripojit se k serveru a cist vysledky.
- **Vyucujici**: jak pripravit test suite, spustit server a vyhodnocovat beh.

## Jak sestavit dokumentaci

```bash
# z korene repozitare
cd docs

docfx metadata docfx.json
docfx build docfx.json
```

Lokalni vystup bude v `docs/_site`.
