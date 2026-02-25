# Tutorial pro studenty: Jak psat solver

`solve` delegat dostava `TestInput` a vraci objekt serializovatelny do JSON.

## Nejcastejsi vzory

### Skalarni vystup

```csharp
return input.GetInt("a") + input.GetInt("b");
```

### Objektovy vystup

```csharp
int a = input.GetInt("a");
int b = input.GetInt("b");
return new { result = a + b };
```

### Parsovani do vlastniho typu

```csharp
public sealed class SumInput
{
    public int A { get; set; }
    public int B { get; set; }
}

SumInput model = input.Parse<SumInput>();
return model.A + model.B;
```

## Doporuceni

- Drzte solver jako cistou funkci bez side effectu.
- Osetrete neplatna data jen pokud to zadani vyzaduje.
- U stabilnich uloh preferujte deterministicky vystup.

## Co casto zpusobuje FAIL

- spatny tvar JSON (vracite jiny objekt nez server ocekava)
- prace s nespravnym klicem (`input.GetInt("x")` kdyz testcase pouziva `a`)
- textovy vystup s jinym formatem pri `strict-json`
