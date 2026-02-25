# Student Guide: Add the library to your own project

This guide supports two official approaches:

1. use a NuGet package (recommended)
2. use ZIP/DLL artifacts from GitHub Releases

## Option A: NuGet (recommended)

For student projects, `testprog.client` is usually enough.

```bash
dotnet add package testprog.client
```

If you want the full package set through one reference, use the meta package:

```bash
dotnet add package testprog
```

Example `.csproj`:

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

Note:

- replace `0.1.0` with the version required by your instructor

## Option B: ZIP/DLL from GitHub Releases

Use this option if NuGet is not available in your environment and your instructor provides release artifacts.

Steps:

1. open GitHub Releases for the repository
2. download the libraries ZIP (for example `testprog-libs-<version>.zip`)
3. extract it into your project, for example `lib/testprog/`
4. add DLL references in your `.csproj`

Example `.csproj` with manual references:

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

Notes:

- adjust `HintPath` values to your actual extracted folder structure
- `server.dll` is not required for student client projects

## Which option should you choose

- NuGet: default and easiest workflow
- ZIP/DLL: fallback when NuGet cannot be used

After the library is in your project, continue with [Student Guide: How to use the testing client](student-guide.md).
