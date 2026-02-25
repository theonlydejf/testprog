# Student: Přidání knihovny do vlastního projektu

V tomto návodu máte dvě oficiální možnosti:

1. použít NuGet balíček (doporučeno)
2. použít ZIP/DLL soubory z GitHub Releases

## Varianta A: NuGet (doporučeno)

Pro studentský projekt obvykle stačí balíček `testprog.client`.

```bash
dotnet add package testprog.client
```

Pokud chcete mít v projektu rovnou celou sadu balíčků, můžete použít meta balíček:

```bash
dotnet add package testprog
```

Ukázka `.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net7.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="testprog.client" Version="0.1.0" />
  </ItemGroup>
</Project>
```

Poznámka:

- místo `0.1.0` použijte verzi, kterou vám zadal vyučující

## Varianta B: ZIP/DLL z GitHub Releases

Tuto variantu použijte, pokud nemůžete použít NuGet (např. školní síť/proxy) a vyučující vám poskytl release artefakt s DLL.

Postup:

1. otevřete GitHub Releases daného repozitáře
2. stáhněte ZIP s knihovnami (např. `testprog-libs-<verze>.zip`)
3. rozbalte ho do projektu, např. do složky `lib/testprog/`
4. přidejte reference na DLL do `.csproj`

Ukázka `.csproj` pro ruční reference:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net7.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="client">
      <HintPath>lib/testprog/client/client.dll</HintPath>
      <Private>true</Private>
    </Reference>
    <Reference Include="messenger">
      <HintPath>lib/testprog/messenger/messenger.dll</HintPath>
      <Private>true</Private>
    </Reference>
    <Reference Include="Newtonsoft.Json">
      <HintPath>lib/testprog/messenger/Newtonsoft.Json.dll</HintPath>
      <Private>true</Private>
    </Reference>
  </ItemGroup>
</Project>
```

Poznámky:

- cesty `HintPath` upravte podle skutečného umístění DLL
- pro studentské klienty nepotřebujete `server.dll`

## Jak vybrat variantu

- NuGet: standardní a nejjednodušší cesta
- ZIP/DLL: fallback varianta, když nelze použít NuGet

Jakmile máte knihovnu přidanou, pokračujte na [Student - Jak použít testovací klient](student-guide.md).
