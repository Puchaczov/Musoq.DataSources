# Musoq .NET Plugin Package Specification

This document describes the required directory structure and file format for packaging .NET Data Source plugins for distribution (e.g., via the Musoq Toolbox).

## Package File Name

The final package must be a Zip archive with a filename following this pattern:

```text
{PluginName}-{Platform}-{Architecture}.zip
```

**Examples:**
- `Musoq.DataSources.Git-windows-x64.zip`
- `Musoq.DataSources.Time-linux-x64.zip`
- `Musoq.DataSources.Json-alpine-x64.zip`

## Versioning and Registry Compatibility

Plugin package versions use SemVer without build metadata. Supported examples:

- `1.2.3`
- `1.2.3-alpha`
- `1.2.3-alpha.1`
- `1.2.3-beta.1`
- `1.2.3-rc.1`

`Version.txt` must contain the exact version string from the plugin project. Do not strip prerelease suffixes from `Version.txt`, GitHub release metadata, NuGet packages, or registry history.

GitHub release tags must be path-safe and must include both the exact version and plugin name:

```text
{Version}-{PluginName}
```

Examples:

- `8.4.8-Musoq.DataSources.Json`
- `8.4.9-alpha.1-Musoq.DataSources.Json`
- `8.4.9-beta.1-Musoq.DataSources.Json`
- `8.4.9-rc.1-Musoq.DataSources.Json`

The plugin registry is backwards-compatible. Schema `1.1` keeps the schema `1.0` fields used by existing clients and adds optional channel metadata for newer clients:

- `latestVersion`, `releaseTag`, and `releaseDate` remain present and point to the latest stable version when a stable version exists.
- `versionHistory` remains present and maps every exact version to its release tag and date.
- `latestStableVersion`, `latestPrereleaseVersion`, and `channels` are optional additive fields.
- `versionHistory` entries may also include `channel` and `isPrerelease`.

Example registry entry:

```json
{
  "schemaVersion": "1.1",
  "lastUpdated": "2026-06-28T12:00:00Z",
  "repository": "https://github.com/Puchaczov/Musoq.DataSources",
  "plugins": [
    {
      "name": "Musoq.DataSources.Json",
      "shortName": "json",
      "description": "JSON datasource for Musoq.",
      "tags": ["json", "files", "datasource"],
      "latestVersion": "8.4.8",
      "releaseTag": "8.4.8-Musoq.DataSources.Json",
      "releaseDate": "2026-06-20T10:15:00Z",
      "latestStableVersion": "8.4.8",
      "latestPrereleaseVersion": "8.4.9-alpha.1",
      "channels": {
        "stable": {
          "version": "8.4.8",
          "releaseTag": "8.4.8-Musoq.DataSources.Json",
          "releaseDate": "2026-06-20T10:15:00Z"
        },
        "alpha": {
          "version": "8.4.9-alpha.1",
          "releaseTag": "8.4.9-alpha.1-Musoq.DataSources.Json",
          "releaseDate": "2026-06-28T12:00:00Z"
        }
      },
      "artifacts": {
        "windows-x64": "Musoq.DataSources.Json-windows-x64.zip",
        "linux-x64": "Musoq.DataSources.Json-linux-x64.zip",
        "macos-arm64": "Musoq.DataSources.Json-macos-arm64.zip",
        "alpine-x64": "Musoq.DataSources.Json-alpine-x64.zip"
      }
    }
  ],
  "versionHistory": {
    "Musoq.DataSources.Json": {
      "8.4.8": {
        "releaseTag": "8.4.8-Musoq.DataSources.Json",
        "releaseDate": "2026-06-20T10:15:00Z",
        "channel": "stable",
        "isPrerelease": false
      },
      "8.4.9-alpha.1": {
        "releaseTag": "8.4.9-alpha.1-Musoq.DataSources.Json",
        "releaseDate": "2026-06-28T12:00:00Z",
        "channel": "alpha",
        "isPrerelease": true
      }
    }
  }
}
```

## Package Structure

The package is a **nested Zip archive**. The outer zip file contains metadata files and an inner zip file holding the actual plugin binaries.

### Root Contents (Outer Zip)

| File | Required | Description | Content Example |
|------|----------|-------------|-----------------|
| `Plugin.zip` | Yes | The inner zip archive containing the build artifacts. | *(Binary Data)* |
| `EntryPoint.txt` | Yes | The name of the main plugin assembly DLL. | `Musoq.DataSources.Git.dll` |
| `Platform.txt` | Yes | The target operating system. | `windows`, `linux`, `macos`, or `alpine` |
| `Architecture.txt` | Yes | The target CPU architecture. | `x64` or `arm64` |
| `Version.txt` | Yes | The version string from the plugin project. | `1.2.3` |
| `LibraryName.txt` | Yes | Display name for the plugin. | `Musoq.DataSources.Git` |

### Plugin Artifacts (Inner Zip: `Plugin.zip`)

The `Plugin.zip` file must contain the published output of the plugin project.

**Contents:**
- The main plugin DLL (e.g., `Musoq.DataSources.Git.dll`)
- The XML documentation file for the main plugin DLL (e.g., `Musoq.DataSources.Git.xml`)
- The dependency configuration file (`.deps.json`)
- The runtime configuration file (`.runtimeconfig.json`)
- All required third-party dependency DLLs (e.g., `LibGit2Sharp.dll`)
- A `third-party-notices` folder containing license files for all dependencies

**Exclusions:**
The following core Musoq assemblies **MUST NOT** be included in the `Plugin.zip` as they are provided by the host environment:
- `Musoq.Schema.dll`
- `Musoq.Plugins.dll`
- `Musoq.Parser.dll`
- `Musoq.Converter.dll`
- `Musoq.Evaluator.dll`

## Visual Hierarchy

```text
Musoq.DataSources.MyPlugin-windows-x64.zip
├── EntryPoint.txt          # Content: "Musoq.DataSources.MyPlugin.dll"
├── Platform.txt            # Content: "windows"
├── Architecture.txt        # Content: "x64"
├── LibraryName.txt         # Content: "Musoq.DataSources.MyPlugin"
├── Version.txt             # Content: "1.0.0"
└── Plugin.zip              # Inner Archive
    ├── Musoq.DataSources.MyPlugin.dll
    ├── Musoq.DataSources.MyPlugin.deps.json
    ├── Musoq.DataSources.MyPlugin.runtimeconfig.json
    ├── Musoq.DataSources.MyPlugin.xml   # XML documentation
    ├── ThirdParty.Dependency.dll
    ├── third-party-notices/    # License files folder
    │   ├── report.json
    │   └── ThirdParty.Dependency/
    │       └── license.txt
    └── ... (other build artifacts)
```

## Creation Process (Example)

1. **Publish the project:**
   ```bash
   dotnet publish MyPlugin.csproj -c Release -f net10.0 -r win-x64 --no-self-contained -o ./publish
   ```

2. **Prepare the Inner Zip:**
   - Remove excluded assemblies (`Musoq.Schema.dll`, `Musoq.Plugins.dll`, `Musoq.Parser.dll`, `Musoq.Converter.dll`, `Musoq.Evaluator.dll`) from `./publish`.
   - Verify the main plugin XML documentation file exists in `./publish`.
   - Gather and place all license files into a `third-party-notices` folder within `./publish`.
   - Zip the contents of `./publish` into `Plugin.zip`.

3. **Create Metadata Files:**
   - Create `EntryPoint.txt` with the DLL name.
   - Create `Platform.txt` with the platform (e.g., `windows`, `linux`, `alpine`, `macos`).
   - Create `Architecture.txt` with the architecture (e.g., `x64`, `arm64`).
   - Create `LibraryName.txt` with the display name.
   - Create `Version.txt` with the version string.

4. **Create the Final Package:**
   - Zip `Plugin.zip`, `EntryPoint.txt`, `LibraryName.txt`, `Version.txt`, `Platform.txt`, and `Architecture.txt` into `Musoq.DataSources.MyPlugin-windows-x64.zip`.

## License Gathering Tool Setup

To comply with the requirement of including `third-party-notices`, you should use the `Musoq.Cloud.LicensesGatherer` tool.

### Prerequisites
- .NET SDK 10.0.300 or newer compatible 10.0 feature band
- The `Musoq.Cloud.LicensesGatherer` tool located in `tools/dotnet/LicenseGatherer`.

### Required Configuration Files

You need to prepare the following JSON files:

1. **OwnPackage.json**: Metadata about your plugin package.
   ```json
   {
       "PackageId": "Musoq.DataSources.MyPlugin",
       "PackageVersion": "1.0.0",
       "PackageProjectUrl": "https://github.com/myuser/myrepo",
       "License": "MIT",
       "LicenseUrl": "https://raw.githubusercontent.com/myuser/myrepo/main/LICENSE"
   }
   ```

2. **LinksManual.json**: (Optional) Manual overrides for license URLs if the tool cannot resolve them automatically.
   ```json
   {
       "Some.Package.Id": {
           "PackageId": "Some.Package.Id",
           "Url": "https://license-url.com/LICENSE"
       }
   }
   ```

3. **LinksCache.json**: (Optional) A cache file for resolved links. This file is typically **excluded from source control** (added to `.gitignore`) as it is auto-generated and environment-specific.

### Running the Tool

Assuming the tool is built and located at `tools/dotnet/LicenseGatherer`, you can run it using the `dotnet` command.

```bash
# Define paths
TOOL_PATH="tools/dotnet/LicenseGatherer/Musoq.Cloud.LicensesGatherer.dll"
PROJECT_PATH="./src/Musoq.DataSources.MyPlugin/Musoq.DataSources.MyPlugin.csproj"
OWN_PACKAGE_PATH="./OwnPackage.json"
OUTPUT_LICENSES_FOLDER="./publish/third-party-notices"
LINKS_CACHE="./LinksCache.json"
MANUAL_LINKS="./LinksManual.json"
LICENSES_CACHE_DIR="./.licenses-cache"
DOWNLOADED_LICENSES_DIR="./licenses"

# Run the tool
dotnet "$TOOL_PATH" retrieve \
    --solution-or-cs-project-file-path "$PROJECT_PATH" \
    --own-package-file-path "$OWN_PACKAGE_PATH" \
    --licenses-folder "$OUTPUT_LICENSES_FOLDER" \
    --links-cache-file-path "$LINKS_CACHE" \
    --manual-links-file-path "$MANUAL_LINKS" \
    --licenses-cache-folder "$LICENSES_CACHE_DIR" \
    --downloaded-licenses-folder "$DOWNLOADED_LICENSES_DIR"
```

### Caching Strategy

The tool uses a hybrid caching strategy to minimize network requests and ensure reproducibility:

1.  **LinksManual.json**: Committed to the repository. Contains manual overrides for packages where the license URL cannot be automatically resolved or needs to be fixed.
2.  **LinksCache.json**: **Ignored** (via `.gitignore`). Stores automatically resolved license URLs to speed up subsequent runs.
3.  **Licenses Cache Folder** (e.g., `.licenses-cache`): **Ignored**. Stores the actual downloaded license text files to avoid re-downloading them.
4.  **Downloaded Licenses Folder** (e.g., `licenses/`): **Committed**. Contains static license files for packages that cannot be downloaded (e.g., local files or proprietary licenses) referenced by `file://` URLs in `LinksManual.json`.

This command will:
1. Analyze the project dependencies.
2. Resolve license URLs using `LinksManual.json` and `LinksCache.json`.
3. Download license texts (using `Licenses Cache` to avoid redundant requests).
4. Save them into the specified `--licenses-folder`.
5. Generate a summary report.

## Installation

Once you have created the package, you can install it using the Musoq CLI.

```bash
# Install from a local package (zip or extracted directory)
musoq datasource import /path/to/Musoq.DataSources.Git-windows-x64.zip
# or
musoq datasource import /path/to/extracted/package

# Install from the built-in plugin registry
musoq datasource install git
```

### Installing from a custom registry

You can add multiple registries. The configuration is persisted by the local agent.

```bash
# Add a registry
musoq registry add custom https://github.com/{owner}/{repo}/releases/download/plugin-registry/plugin-registry.json
```

## Reusing the Release Scripts in Another Repository

Datasource authors can copy this repository's producer-side release tooling into another GitHub repository:

- `scripts/common`
- `scripts/Pack-Plugin.ps1`
- `scripts/Publish-PluginReleases.ps1`
- `scripts/Update-PluginRegistry.ps1`
- `scripts/Rollback-PluginReleases.ps1`
- `.github/workflows/release-plugins.yml`

The copied workflow must pass its own GitHub repository as `owner/repo` to the release scripts. Plugin project files must use a valid `Musoq.DataSources.*` package name, a supported SemVer `<Version>`, package metadata, XML documentation generation, and the runtime-v2 `net10.0` target. If NuGet publishing is enabled, provide a NuGet API key and publish the exact project version; prerelease suffixes must not be rewritten.

See `MusoqThirdPartyDatasourceRepositorySetup.md` for the full third-party repository checklist.
