# Student: Jak použít testovací klient krok za krokem

Tento návod předpokládá, že už máte ve svém projektu přidanou klientskou knihovnu.
Pokud ne, otevřete nejdřív [Student - Přidání knihovny](student-add-library.md).

## Co budete potřebovat

- vlastní C# projekt (console app)
- přístup k běžícímu testovacímu serveru
- vaše identita: `StudentId` a `DisplayName`

## Krok 1: Připravte `StudentClientOptions` (UDP discovery)

Identita je povinná. Server ji používá pro autorizaci (whitelist) i logy.

- `StudentId`: stabilní technické ID, například `novakj`
- `DisplayName`: čitelné jméno, například `Jan Novák`

Kopírovatelná ukázka konfigurace klienta přes **UDP discovery**:

```csharp
using testprog.client;

StudentClientOptions options = new()
{
    StudentId = "novakj",            // TODO: vaše ID
    DisplayName = "Jan Novák",       // TODO: vaše jméno
};
```

## Krok 2: Vytvořte metodu, která řeší úlohu

Nejjednodušší je mít jednu metodu `Solve`, kterou budete postupně upravovat pro své zadání.

```csharp
using testprog.messenger;

public static object Solve(TestInput input)
{
    // TODO: upravte podle svého zadání
    int a = input.GetInt("a");
    int b = input.GetInt("b");
    return a + b;
}
```

Poznámky:

- `Solve` musí mít návratovou hodnotu, může vrátit číslo, string i objekt
- pokud server očekává JSON objekt, nastavte návratovou hodnotu na `object` a vracejte například `new { result = a + b }`

## Krok 3: Spusťte test pomocí `StudentConsoleTestRunner`

Níže je příklad funkčního `Program.cs`, který má za úkol sečist 2 čísla.

```csharp
using testprog.client;
using testprog.messenger;

namespace MyStudentApp;

internal static class Program
{
    public static int Main(string[] args)
    {
        StudentClientOptions options = new()
        {
            StudentId = "novakj",            // TODO: vaše ID
            DisplayName = "Jan Novák",       // TODO: vaše jméno
        };

        return StudentConsoleTestRunner.RunWithExitCode(options, Solve);
    }

    public static object Solve(TestInput input)
    {
        int a = input.GetInt("a");
        int b = input.GetInt("b");
        return a + b;
    }
}
```

## Krok 4: Jak číst výstup

Během běhu uvidíte:

- navázání spojení
- začátek skupiny testů
- `PASS`/`FAIL` pro každý testcase
- finální souhrn

Exit code procesu:

- `0`: běh dokončen a všechny testcase prošly
- `1`: nedokončený běh nebo alespoň jeden fail

## Krok 5: Nejčastější chyby

## `Run was stopped (unauthorized)`

- `StudentId` není povolený na serveru
- kontaktujte vyučujícího a pošlete přesně hodnotu `StudentId`

## `Unable to connect within configured timeout`

- server neběží
- nesedí `DiscoveryPort` nebo `DiscoveryMulticastAddress`
- síť blokuje multicast UDP

## `invalid-answer`

- vracíte jiný typ/tvar dat než očekává test
- porovnejte zadání se strukturou výstupu metody `Solve`

## Doporučení pro práci v průběhu semestru

- držte řešení v metodě `Solve`, ne v `Main`
- používejte malé helper metody, ale finální návrat držte konzistentní
- když něco neprojde, řešte první neúspěšný testcase a postupujte dál
