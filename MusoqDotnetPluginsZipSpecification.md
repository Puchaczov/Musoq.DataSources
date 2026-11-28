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

The plugin registry is backwards-compatible. Schema `1.2` keeps every schema `1.0` and `1.1` top-level field used by existing clients and adds authoritative compatibility and integrity metadata to eligible version-history entries:

- `latestVersion`, `releaseTag`, and `releaseDate` remain present and point to the latest stable version when a stable version exists.
- `versionHistory` remains present and maps every exact version to its release tag and date.
- `latestStableVersion`, `latestPrereleaseVersion`, and `channels` are optional additive fields.
- `versionHistory` entries may also include `channel` and `isPrerelease`.
- New releases with a valid `plugin-release-metadata.json` add `runtimeCompatibility` and four per-platform artifact records to their exact version-history entry.
- Releases without immutable release metadata remain visible as legacy history, but are not runtime-v2 candidates.

Example registry entry:

```json
{
  "schemaVersion": "1.2",
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
        "isPrerelease": true,
        "runtimeCompatibility": {
          "formatVersion": 1,
          "runtimeFamily": "musoq-runtime-v2",
          "targetFramework": "net10.0",
          "hostPackages": {
            "Musoq.Schema": {
              "minimumVersionInclusive": "17.0.11-alpha.2",
              "maximumVersionExclusive": "18.0.0"
            },
            "Musoq.Plugins": {
              "minimumVersionInclusive": "17.0.11-alpha.2",
              "maximumVersionExclusive": "18.0.0"
            }
          }
        },
        "artifacts": {
          "windows-x64": {
            "fileName": "Musoq.DataSources.Json-windows-x64.zip",
            "sizeBytes": 12345678,
            "md5": "0123456789abcdef0123456789abcdef",
            "sha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
          },
          "linux-x64": {
            "fileName": "Musoq.DataSources.Json-linux-x64.zip",
            "sizeBytes": 12345679,
            "md5": "1123456789abcdef0123456789abcdef",
            "sha256": "1123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
          },
          "macos-arm64": {
            "fileName": "Musoq.DataSources.Json-macos-arm64.zip",
            "sizeBytes": 12345680,
            "md5": "2123456789abcdef0123456789abcdef",
            "sha256": "2123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
          },
          "alpine-x64": {
            "fileName": "Musoq.DataSources.Json-alpine-x64.zip",
            "sizeBytes": 12345681,
            "md5": "3123456789abcdef0123456789abcdef",
            "sha256": "3123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
          }
        }
      }
    }
  }
}
```

Release selection rules:

- `latestVersion`, root `releaseTag`, and root `releaseDate` point to the latest stable release when a stable release exists.
- If only prereleases exist for a plugin, `latestVersion` points to the highest prerelease so the plugin remains discoverable.
- `latestStableVersion` may be `null` or omitted when no stable release exists.
- `latestPrereleaseVersion` points to the highest SemVer prerelease across all prerelease channels.
- `channels.stable`, `channels.alpha`, `channels.beta`, and `channels.rc` point to the latest version in each channel when present.
- Existing clients can continue using only `latestVersion`, `releaseTag`, and top-level `artifacts`; unknown schema-1.2 fields are additive.

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
- The generated runtime compatibility manifest (`MusoqPluginCompatibility.json`)
- All required third-party dependency DLLs (e.g., `LibGit2Sharp.dll`)
- A `third-party-notices` directory containing at least one non-empty license file for the dependencies

The `third-party-notices` directory is an artifact contract, not optional
metadata. Packaging must fail if it is missing or empty. Release smoke
validation and the representative plugin-package smoke suite enforce the same
contract after extracting `Plugin.zip`.

**Exclusions:**
The following core Musoq assemblies **MUST NOT** be included in the `Plugin.zip` as they are provided by the host environment:
- `Musoq.Schema.dll`
- `Musoq.Plugins.dll`
- `Musoq.Parser.dll`
- `Musoq.Converter.dll`
- `Musoq.Evaluator.dll`
- `Musoq.Targets.*.dll`

The entry-point assembly's PE AssemblyReference table is also part of this
boundary. Packaging and release smoke validation **MUST** reject direct
references to `Musoq.Parser`, `Musoq.Converter`, `Musoq.Evaluator`,
`Musoq.CommandLine`, or `Musoq.Targets.*`, even when those DLLs are absent from
the archive. The only supported direct host ABI references are `Musoq.Schema`
and `Musoq.Plugins`; removing host DLLs from the publish directory does not
remove or satisfy an invalid assembly reference.

### Runtime Compatibility Manifest

`MusoqPluginCompatibility.json` is generated for every RID from evaluated MSBuild properties and package references. It must not be maintained by hand. Packaging fails unless the project targets `net10.0`, references matching supported versions of `Musoq.Schema` and `Musoq.Plugins`, and excludes their runtime assets.

```json
{
  "formatVersion": 1,
  "runtimeFamily": "musoq-runtime-v2",
  "targetFramework": "net10.0",
  "hostPackages": {
    "Musoq.Schema": {
      "minimumVersionInclusive": "17.0.11-alpha.2",
      "maximumVersionExclusive": "18.0.0"
    },
    "Musoq.Plugins": {
      "minimumVersionInclusive": "17.0.11-alpha.2",
      "maximumVersionExclusive": "18.0.0"
    }
  }
}
```

### Release Artifact Integrity

After all four outer datasource ZIPs are finalized, the release pipeline generates `plugin-release-metadata.json`. For every platform it records the exact outer ZIP filename, byte length, lowercase MD5, and lowercase SHA-256. It also copies the validated embedded runtime compatibility contract into the release metadata.

The metadata file is uploaded as a release asset beside the datasource ZIPs. Hash records are immutable for a `(plugin, version, platform)` tuple. Publishing an existing asset downloads and verifies its bytes; matching assets are retained, while any size, MD5, SHA-256, or metadata difference fails the release. Release assets are never clobbered. SHA-256 is the security-relevant digest; MD5 is retained only as an additional consistency check.

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
    ├── MusoqPluginCompatibility.json    # Generated host ABI contract
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
   - Remove host-owned assemblies (`Musoq.Schema.dll`, `Musoq.Plugins.dll`, `Musoq.Parser.dll`, `Musoq.Converter.dll`, `Musoq.Evaluator.dll`, and `Musoq.Targets.*.dll`) recursively from `./publish`, then fail if any remain before compression.
   - Inspect the entry-point PE assembly references and fail on direct references to Parser, Converter, Evaluator, CommandLine, or Targets host assemblies, regardless of whether those files are present.
   - Generate `MusoqPluginCompatibility.json` from evaluated MSBuild data.
   - Verify the main plugin XML documentation file exists in `./publish`.
   - Gather and place all license files into a `third-party-notices` folder within `./publish`; fail if the directory is missing or contains no non-empty files.
   - Zip the contents of `./publish` into `Plugin.zip`.

3. **Create Metadata Files:**
   - Create `EntryPoint.txt` with the DLL name.
   - Create `Platform.txt` with the platform (e.g., `windows`, `linux`, `alpine`, `macos`).
   - Create `Architecture.txt` with the architecture (e.g., `x64`, `arm64`).
   - Create `LibraryName.txt` with the display name.
   - Create `Version.txt` with the version string.

4. **Create the Final Package:**
   - Zip `Plugin.zip`, `EntryPoint.txt`, `LibraryName.txt`, `Version.txt`, `Platform.txt`, and `Architecture.txt` into `Musoq.DataSources.MyPlugin-windows-x64.zip`.

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

## Native typed ABI

Use the typed source and library contracts in the plugin authoring guides. Build against
the Schema and Plugins versions supported by the target host. Test the package with
that host to verify that it can load the entrypoint and run a representative query.
Keep host-owned assemblies out of the ZIP. Set the package compatibility range from
the evaluated SDK references and the host versions the plugin supports.
