# Musoq License Gatherer Tool

This tool gathers licenses for .NET project dependencies.

## Setup

1. Copy this entire folder to your repository (e.g., `tools/dotnet/LicenseGatherer`).
2. Ensure you have .NET 8.0 SDK installed.

## Usage

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

## Configuration Files

- **OwnPackage.json**: Metadata about your package.
- **LinksManual.json**: Manual overrides for license URLs. (See `LinksManual.example.json`)
- **LinksCache.json**: Cache for resolved URLs. (See `LinksCache.example.json`)
