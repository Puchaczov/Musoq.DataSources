# Third-Party Datasource Repository Setup

This guide describes how to reuse the Musoq datasource release tooling in another GitHub repository. The scripts are producer-side only: they build plugin zip packages, create GitHub releases, and publish a `plugin-registry.json` file that Musoq hosts can consume by URL.

## Required Repository Shape

Use plugin project names that match the datasource plugin name:

```text
Musoq.DataSources.Weather/
  Musoq.DataSources.Weather.csproj
```

Each plugin project should include package metadata:

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <Version>1.2.3-alpha.1</Version>
  <Description>Weather datasource for Musoq.</Description>
  <PackageTags>musoq,datasource,weather,runtime-v2</PackageTags>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
</PropertyGroup>
```

Supported versions are SemVer without build metadata:

- `1.2.3`
- `1.2.3-alpha`
- `1.2.3-alpha.1`
- `1.2.3-beta.1`
- `1.2.3-rc.1`

## Files To Copy

Copy these files and directories as a set:

```text
scripts/common
scripts/Pack-Plugin.ps1
scripts/Publish-PluginReleases.ps1
scripts/Update-PluginRegistry.ps1
scripts/Rollback-PluginReleases.ps1
.github/workflows/release-plugins.yml
```

If NuGet publishing is needed, also copy `Publish.Nuget.ps1` and adapt the workflow that calls it.

## GitHub Inputs

The release scripts require the repository name in `owner/repo` format. In GitHub Actions, pass:

```powershell
-Repository "${{ github.repository }}"
```

Required permissions:

```yaml
permissions:
  contents: write
```

Optional secrets:

- `nuget_musoq_key` or a repository-specific NuGet key, only when publishing NuGet packages.
- Registry authentication is handled by the consuming Musoq host, not by this producer repository.

## Release Matrix

The default package matrix is:

| RID | Registry Platform | Registry Architecture |
|-----|-------------------|-----------------------|
| `win-x64` | `windows` | `x64` |
| `linux-x64` | `linux` | `x64` |
| `osx-arm64` | `macos` | `arm64` |
| `linux-musl-x64` | `alpine` | `x64` |

Artifacts are named:

```text
Musoq.DataSources.Weather-windows-x64.zip
Musoq.DataSources.Weather-linux-x64.zip
Musoq.DataSources.Weather-macos-arm64.zip
Musoq.DataSources.Weather-alpine-x64.zip
```

Release tags are path-safe and include the exact version:

```text
1.2.3-Musoq.DataSources.Weather
1.2.3-alpha.1-Musoq.DataSources.Weather
```

## Registry URL

After the workflow publishes `plugin-registry`, consumers can add:

```text
https://github.com/{owner}/{repo}/releases/download/plugin-registry/plugin-registry.json
```

The registry keeps stable clients compatible by preserving `latestVersion`, `releaseTag`, `releaseDate`, `artifacts`, and `versionHistory`. Channel-aware clients can also use `latestStableVersion`, `latestPrereleaseVersion`, and `channels`.

## Local Validation

Before publishing:

```powershell
dotnet restore --nologo
dotnet build -c Release --no-restore
dotnet test -c Release --no-build
.\scripts\Pack-Plugin.ps1 -PluginName All -OutputDirectory artifacts
```

Then inspect one generated zip:

- outer zip contains `EntryPoint.txt`, `LibraryName.txt`, `Version.txt`, `Platform.txt`, `Architecture.txt`, and `Plugin.zip`
- `Version.txt` matches the exact project `<Version>`
- inner `Plugin.zip` contains the main DLL and XML docs
- host-provided Musoq assemblies are not included
