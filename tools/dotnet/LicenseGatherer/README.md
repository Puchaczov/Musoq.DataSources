# Musoq License Gatherer Tool

This tool gathers licenses for .NET project dependencies during an explicit
license-snapshot refresh. Normal datasource packaging consumes committed
snapshots and does not invoke this tool.

## Setup

1. Copy this entire folder to your repository (e.g., `tools/dotnet/LicenseGatherer`).
2. Ensure you have .NET 8.0 SDK installed.
3. For an explicit snapshot refresh, provision the external `nuget-license`
   command from the repository-pinned tool manifest (normal packaging does not
   require it):

   ```powershell
   ./scripts/Restore-PluginTooling.ps1
   ```

   The manifest pins `nuget-license` 4.0.16 and the helper installs it into a
   runner-local tool path. The bundled gatherer may report the obsolete
   `dotnet-project-licenses` package name in its error message; that package
   does not provide the `nuget-license` command used by the gatherer.

## Usage during an explicit refresh

Use the repository refresh wrapper so the pinned tool version, staged output,
dependency graph, and snapshot hashes are validated together:

```powershell
.\scripts\release\Update-LicenseSnapshots.ps1 -PluginName All
.\scripts\release\Update-LicenseSnapshots.ps1 -PluginName Musoq.DataSources.Json
```

The wrapper provisions `nuget-license` 4.0.16 from the repository tool
manifest and invokes only the bundled gatherer. It stores working data under
`.builds/license-refresh/`; `.licenses-cache/` and `LinksCache.json` are
transient and must not be committed.

For gatherer development or troubleshooting, the executable can also be run
directly:

Run the tool using `dotnet`:

```bash
dotnet Musoq.Cloud.LicensesGatherer.dll retrieve \
    --solution-or-cs-project-file-path "<path-to-csproj>" \
    --own-package-file-path "<path-to-own-package.json>" \
    --licenses-folder "<output-folder>" \
    --links-cache-file-path "<path-to-links-cache.json>" \
    --manual-links-file-path "<path-to-manual-links.json>" \
    --licenses-cache-folder "<path-to-licenses-cache-dir>" \
    --downloaded-licenses-folder "<path-to-downloaded-licenses-dir>"
```

Verify the prerequisite independently with:

```powershell
nuget-license --version
```

## Artifact contract

Committed snapshots must contain complete, non-abbreviated text for every
package in the restored graph. `Assert-LicenseSnapshots.ps1` validates the
manifest and input hashes, and `Assert-ReleaseLicenseArtifacts.ps1` validates
the four embedded archive inventories. Release smoke tests and the
representative plugin package smoke tests reject packages where
`third-party-notices` is missing, is a file, or is empty.

## Configuration Files

- **OwnPackage.json**: Metadata about your package.
- **LinksManual.json**: Committed manual overrides for license URLs and a
  snapshot input. (See `LinksManual.example.json`.)
- **LinksCache.json**: Refresh-only cache for resolved URLs; it is ignored and
  must not be part of a committed snapshot. (See `LinksCache.example.json`.)
