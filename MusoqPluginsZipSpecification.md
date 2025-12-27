# Musoq .NET Plugin Package Specification

This document describes the required directory structure and file format for packaging .NET Data Source plugins for distribution (e.g., via the Musoq Toolbox).

## Package File Name

The final package must be a Zip archive with a filename following this pattern:

```text
{PluginName}-{Platform}-{Architecture}.zip
```

**Examples:**
- `Musoq.DataSources.Postgres-windows-x64.zip`
- `Musoq.DataSources.Sqlite-linux-arm64.zip`
- `Musoq.DataSources.Json-alpine-x64.zip`

## Package Structure

The package is a **nested Zip archive**. The outer zip file contains metadata files and an inner zip file holding the actual plugin binaries.

### Root Contents (Outer Zip)

| File | Required | Description | Content Example |
|------|----------|-------------|-----------------|
| `Plugin.zip` | Yes | The inner zip archive containing the build artifacts. | *(Binary Data)* |
| `EntryPoint.txt` | Yes | The name of the main plugin assembly DLL. | `Musoq.DataSources.Postgres.dll` |
| `Platform.txt` | Yes | The target operating system. | `windows`, `linux`, `macos`, or `alpine` |
| `Architecture.txt` | Yes | The target CPU architecture. | `x64` or `arm64` |

### Plugin Artifacts (Inner Zip: `Plugin.zip`)

The `Plugin.zip` file must contain the published output of the plugin project.

**Contents:**
- The main plugin DLL (e.g., `Musoq.DataSources.Postgres.dll`)
- The dependency configuration file (`.deps.json`)
- The runtime configuration file (`.runtimeconfig.json`)
- All required third-party dependency DLLs (e.g., `Npgsql.dll`)
- A `third-party-notices` folder containing license files for all dependencies

**Exclusions:**
The following core Musoq assemblies **MUST NOT** be included in the `Plugin.zip` as they are provided by the host environment:
- `Musoq.Schema.dll`
- `Musoq.Parser.dll`
- `Musoq.Plugins.dll`

## Visual Hierarchy

```text
Musoq.DataSources.MyPlugin-windows-x64.zip
├── EntryPoint.txt          # Content: "Musoq.DataSources.MyPlugin.dll"
├── Platform.txt            # Content: "windows"
├── Architecture.txt        # Content: "x64"
└── Plugin.zip              # Inner Archive
    ├── Musoq.DataSources.MyPlugin.dll
    ├── Musoq.DataSources.MyPlugin.deps.json
    ├── Musoq.DataSources.MyPlugin.runtimeconfig.json
    ├── ThirdParty.Dependency.dll
    ├── third-party-notices/    # License files folder
    │   ├── LICENSE.txt
    │   └── ThirdParty.Dependency.LICENSE.txt
    └── ... (other build artifacts)
```

## Creation Process (Example)

1. **Publish the project:**
   ```bash
   dotnet publish MyPlugin.csproj -c Release -r win-x64 --no-self-contained -o ./publish
   ```

2. **Prepare the Inner Zip:**
   - Remove excluded assemblies (`Musoq.Schema.dll`, `Musoq.Parser.dll`, `Musoq.Plugins.dll`) from `./publish`.
   - Gather and place all license files into a `third-party-notices` folder within `./publish`.
   - Zip the contents of `./publish` into `Plugin.zip`.

3. **Create Metadata Files:**
   - Create `EntryPoint.txt` with the DLL name.
   - Create `Platform.txt` with `windows`.
   - Create `Architecture.txt` with `x64`.

4. **Create the Final Package:**
   - Zip `Plugin.zip`, `EntryPoint.txt`, `Platform.txt`, and `Architecture.txt` into `Musoq.DataSources.MyPlugin-windows-x64.zip`.

## License Gathering Tool Setup

To comply with the requirement of including `third-party-notices`, you should use the `Musoq.Cloud.LicensesGatherer` tool.

### Prerequisites
- .NET SDK 8.0+
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
