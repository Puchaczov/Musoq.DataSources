# Datasource release maintenance

Run the commands from the repository root. The package specifications describe the format that consumers receive.

## Unified NuGet and Plugin Release Flow

For repositories that publish both NuGet packages and plugin zips, one Git tag releases one datasource:

```powershell
git tag 9.0.0-alpha.1-Musoq.DataSources.Json
git push origin 9.0.0-alpha.1-Musoq.DataSources.Json
```

The release workflow validates that the tag version exactly matches the project `<Version>`, packs the `.nupkg` and `.snupkg`, packs all plugin runtime zips, uploads all assets to the same GitHub release, and updates `plugin-registry.json`.

Multiple datasource releases can point to the same commit:

```powershell
git tag 9.0.0-alpha.1-Musoq.DataSources.Json
git tag 2.0.0-alpha.1-Musoq.DataSources.Git
git push origin 9.0.0-alpha.1-Musoq.DataSources.Json 2.0.0-alpha.1-Musoq.DataSources.Git
```

NuGet-only helper packages that do not implement a datasource schema are not part of the unified plugin release flow.

## License Snapshots and Explicit Refresh

Datasource release packages consume committed license snapshots. Each registered
package has a directory under `licenses/release/<PackageId>/` containing the
manifest, the package's own `license.txt`, the dependency report, and the
`third-party-notices` files copied into `Plugin.zip`. The root `license.txt` is
snapshot provenance and is intentionally not added to the existing `Plugin.zip`
layout.

Normal `Pack-Plugin.ps1`, `Pack-Release.ps1`, and release workflows validate and
copy these files offline. They do not invoke `nuget-license`, the bundled
`Musoq.LicensesGatherer`, license URL resolution, `LinksCache.json`, or a
license download cache. NuGet restore remains a normal build prerequisite and
may still require package feeds; license retrieval itself is not part of that
path.

### Refreshing snapshots

Live license resolution is available only through the explicit refresh command:

```powershell
pwsh ./scripts/release/Update-LicenseSnapshots.ps1 -PluginName All
pwsh ./scripts/release/Update-LicenseSnapshots.ps1 -PluginName Musoq.DataSources.Json
```

The refresh script provisions the repository-pinned `nuget-license` 4.0.16,
uses only the bundled gatherer, validates the staged report and hashes, and
replaces a snapshot only after validation succeeds. The committed
`LinksManual.json` and any committed static `licenses/*.txt` overrides are
inputs to the snapshot manifest.

Refresh working data is transient and must not be committed:

- `.artifacts/license-refresh/` contains per-refresh working directories and the
  runner-local tooling installation.
- `.licenses-cache/` and `LinksCache.json` contain downloaded text or resolved
  URL caches when a refresh uses them.
- Other generated refresh output belongs under ignored build/artifact paths.

`Assert-LicenseSnapshots.ps1` fails closed when a snapshot is missing,
abbreviated, malformed, tampered with, generated for a different package or
version, has a stale dependency graph/input hash, or records a different tool
or bundled gatherer. `Assert-ReleaseLicenseArtifacts.ps1` additionally checks
that every four-RID archive contains exactly the committed
`third-party-notices` inventory with matching sizes and SHA-256 hashes.

Changes to a registered project, package version, transitive dependency graph,
manual link mapping, static license override, pinned tool, or bundled gatherer
therefore require an explicit snapshot refresh before packaging can pass.

## Reusing the Release Scripts in Another Repository

Datasource authors can copy this repository's producer-side release tooling into another GitHub repository:

- `scripts/common`
- `scripts/release`
- `scripts/Pack-Plugin.ps1`
- `scripts/Update-PluginRegistry.ps1`
- `.github/workflows/release-datasource.yml`
- `.github/workflows/release-datasources-batch.yml`
- `.github/workflows/rollback-release.yml`
- `.github/workflows/validate-plugin-packages.yml`

The copied workflow must pass its own GitHub repository as `owner/repo` to the release scripts. Plugin project files must use a valid `Musoq.DataSources.*` package name, a supported SemVer `<Version>`, package metadata, XML documentation generation, and the runtime-v2 `net10.0` target. Configure NuGet Trusted Publishing or `NUGET_MUSOQ_KEY` before enabling tag-push releases. Prerelease suffixes must not be rewritten.

Rollback is handled by `scripts/release/Rollback-Release.ps1` and `.github/workflows/rollback-release.yml`. It accepts an exact datasource release tag, deletes that GitHub release only when explicitly applied, and regenerates the registry from remaining releases. NuGet-only helper packages that do not implement a datasource schema are not part of the unified datasource release flow yet.

Use the single-tag workflow for canary or one-off releases. Use `release-datasources-batch.yml` for coordinated releases because it restores and builds once, publishes all selected datasource artifacts, and updates the registry once. Solution tests are supplied by the branch `Build` workflow.

See `MusoqThirdPartyDatasourceRepositorySetup.md` for the full third-party repository checklist.
