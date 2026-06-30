# Third-Party Datasource Repository Setup

This guide describes how to reuse the Musoq datasource release tooling in another GitHub repository. The scripts are producer-side only: they build plugin zip packages, publish NuGet packages for real datasource plugins, create GitHub releases, and publish a `plugin-registry.json` file that Musoq hosts can consume by URL.

## Required Repository Shape

Use plugin project names that match the datasource plugin name:

```text
Musoq.DataSources.Weather/
  Musoq.DataSources.Weather.csproj
```

Each real datasource project must implement a schema and include package metadata:

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

Use the channel spelling `alpha`, `beta`, `rc`, or stable with no suffix. `alfa` is not a supported channel token.

## Files To Copy

Copy these files and directories as a set:

```text
scripts/common
scripts/release
scripts/Pack-Plugin.ps1
scripts/Update-PluginRegistry.ps1
.github/workflows/release-datasource.yml
.github/workflows/release-datasources-batch.yml
```

Optional but recommended:

```text
.github/workflows/rollback-release.yml
.github/workflows/validate-plugin-packages.yml
```

Do not copy retired split-release scripts. The unified tag-driven workflow is the production path for datasource plugins. NuGet-only helper packages that do not implement a datasource schema are not handled by this flow yet; publish them with a separate future NuGet-only process.

## Release Package Registry

Update `scripts/release/packages.json` after copying the scripts. It must contain only real datasource plugin projects, not tests, helpers, shared libraries, or common packages:

```json
{
  "packages": [
    {
      "slug": "weather",
      "packageId": "Musoq.DataSources.Weather",
      "projectPath": "Musoq.DataSources.Weather/Musoq.DataSources.Weather.csproj"
    }
  ]
}
```

The validation script rejects tags for projects that are not listed here.

## GitHub Inputs

The release scripts require the repository name in `owner/repo` format. In GitHub Actions, pass:

```powershell
-Repository "${{ github.repository }}"
```

Production release permissions:

```yaml
permissions:
  contents: write
  id-token: write
```

Use a protected environment for publishing and rollback. This repository uses `nuget-production`.

Supported secrets:

- `NUGET_USER` for NuGet Trusted Publishing.
- `NUGET_MUSOQ_KEY` only when Trusted Publishing is not available.
- `GITHUB_TOKEN` is provided by GitHub Actions for release and registry assets.

Registry authentication is handled by the consuming Musoq host, not by this producer repository.

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

## Production Release

The only production release command for a datasource is a tag push:

```powershell
git tag 9.0.0-alpha.1-Musoq.DataSources.Json
git push origin 9.0.0-alpha.1-Musoq.DataSources.Json
```

One tag releases one datasource: `.nupkg`, `.snupkg`, all runtime plugin zips, GitHub release assets, and a registry update. The tag version must match the target project `<Version>` exactly.

Multiple datasource tags can point to the same commit:

```powershell
git tag 1.2.3-alpha.1-Musoq.DataSources.Weather
git tag 2.0.0-alpha.1-Musoq.DataSources.Inventory
git push origin 1.2.3-alpha.1-Musoq.DataSources.Weather 2.0.0-alpha.1-Musoq.DataSources.Inventory
```

Pushing multiple tags starts one workflow run per tag. For larger coordinated releases, use the manual `release-datasources-batch.yml` workflow instead. It accepts `All` or a comma/newline/space separated list of datasource slugs, package IDs, suffixes, or exact release tags. The batch workflow restores, builds, and tests once, then packs and publishes each selected datasource, and updates `plugin-registry.json` once at the end.

Batch workflow examples:

```text
All
json,git,os
Musoq.DataSources.Json Musoq.DataSources.Git
9.0.0-alpha.2-Musoq.DataSources.Json
```

The batch workflow defaults to dry-run mode and skips GitHub releases that already exist. It creates GitHub release tags through the release API at the workflow commit instead of pushing git tags, so it does not trigger the single-tag release workflow for every datasource.

## Rollback

Rollback is explicit and tag-scoped. The workflow first validates in dry-run mode. To apply rollback, dispatch `rollback-release.yml` with the exact release tag and `dry_run` set to `false`.

Local dry run:

```powershell
.\scripts\release\Rollback-Release.ps1 -Tag 9.0.0-alpha.1-Musoq.DataSources.Json -Repository owner/repo
```

Local apply:

```powershell
.\scripts\release\Rollback-Release.ps1 -Tag 9.0.0-alpha.1-Musoq.DataSources.Json -Repository owner/repo -Apply
```

Rollback deletes the GitHub release for that exact tag and regenerates `plugin-registry.json` from the remaining releases. It does not delete the git tag.

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
.\scripts\release\Validate-Release.ps1 -Tag 9.0.0-alpha.1-Musoq.DataSources.Json
.\scripts\release\Pack-Release.ps1 -Tag 9.0.0-alpha.1-Musoq.DataSources.Json -OutputPath artifacts/release
.\scripts\release\Test-ReleaseSmoke.ps1 -Tag 9.0.0-alpha.1-Musoq.DataSources.Json -ArtifactDirectory artifacts/release
```

Then inspect one generated zip:

- outer zip contains `EntryPoint.txt`, `LibraryName.txt`, `Version.txt`, `Platform.txt`, `Architecture.txt`, and `Plugin.zip`
- `Version.txt` matches the exact project `<Version>`
- inner `Plugin.zip` contains the main DLL and XML docs
- host-provided Musoq assemblies are not included
