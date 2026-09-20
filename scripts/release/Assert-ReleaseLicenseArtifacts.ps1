#Requires -Version 7.2
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [Alias('PackageId', 'Product')]
    [string] $PluginName,
    [Parameter(Mandatory)] [string] $AssetsDirectory,
    [string] $RepositoryRoot = (Join-Path $PSScriptRoot '../..'),
    [ValidateRange(1, 100)] [int] $ExpectedArchiveCount = 4
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$root = [IO.Path]::GetFullPath($RepositoryRoot)
$assets = [IO.Path]::GetFullPath($AssetsDirectory)
if (-not [IO.Directory]::Exists($assets)) {
    throw "Release assets directory '$assets' is missing."
}

Import-Module (Join-Path $root 'scripts/release/LicenseSnapshot.Common.psm1') -Force
$definitions = @(Get-DatasourcePackageDefinition -RepositoryRoot $root -Selector $PluginName)
if ($definitions.Count -eq 0) {
    throw "Archive license validation found no datasource package for '$PluginName'."
}
if ($definitions.Count -gt 1) {
    foreach ($selectedDefinition in $definitions) {
        & $PSCommandPath `
            -PluginName $selectedDefinition.packageId `
            -AssetsDirectory $assets `
            -RepositoryRoot $root `
            -ExpectedArchiveCount $ExpectedArchiveCount
    }
    return
}
$definition = $definitions[0]

& (Join-Path $root 'scripts/release/Assert-LicenseSnapshots.ps1') `
    -PluginName $definition.packageId `
    -RepositoryRoot $root `
    -ValidatePackageGraph | Out-Null

$snapshotDirectory = Join-Path $root "licenses/release/$($definition.packageId)"
$manifestPath = Join-Path $snapshotDirectory 'manifest.json'
$manifest = [IO.File]::ReadAllText($manifestPath) | ConvertFrom-Json -Depth 100
$expectedFiles = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
foreach ($file in @($manifest.files)) {
    $path = [string]$file.path
    if ($path -eq 'license.txt') {
        continue
    }
    if (-not $path.StartsWith('third-party-notices/', [StringComparison]::Ordinal)) {
        throw "License snapshot '$($definition.packageId)' contains unexpected packaged path '$path'."
    }
    $expectedFiles.Add($path, $file)
}
if ($expectedFiles.Count -eq 0) {
    throw "License snapshot '$($definition.packageId)' has no third-party notice files to package."
}

$archivePrefix = "$($definition.packageId)-"
$archives = @(Get-ChildItem -LiteralPath $assets -File -Filter "$archivePrefix*.zip" | Sort-Object Name)
if ($archives.Count -ne $ExpectedArchiveCount) {
    throw "Expected $ExpectedArchiveCount $($definition.packageId) release archives, found $($archives.Count) in '$assets'."
}

function ConvertTo-NormalizedArchivePath {
    param([Parameter(Mandatory)] [string] $Path)

    $normalized = $Path.Replace('\', '/')
    while ($normalized.StartsWith('./', [StringComparison]::Ordinal)) {
        $normalized = $normalized.Substring(2)
    }
    return $normalized
}

function Get-PluginZipArchive {
    param(
        [Parameter(Mandatory)] [IO.Compression.ZipArchive] $OuterArchive,
        [Parameter(Mandatory)] [string] $ArchiveName
    )

    $pluginEntries = @($OuterArchive.Entries | Where-Object {
        (ConvertTo-NormalizedArchivePath -Path $_.FullName) -eq 'Plugin.zip' -and -not [string]::IsNullOrEmpty($_.Name)
    })
    if ($pluginEntries.Count -ne 1) {
        throw "Archive '$ArchiveName' must contain exactly one root Plugin.zip entry."
    }

    $pluginBytes = [IO.MemoryStream]::new()
    $pluginEntryStream = $pluginEntries[0].Open()
    try {
        $pluginEntryStream.CopyTo($pluginBytes)
    }
    finally {
        $pluginEntryStream.Dispose()
    }
    $pluginBytes.Position = 0
    return [PSCustomObject]@{
        Bytes = $pluginBytes
        Archive = [IO.Compression.ZipArchive]::new($pluginBytes, [IO.Compression.ZipArchiveMode]::Read, $false)
    }
}

foreach ($archivePath in $archives) {
    $outerArchive = [IO.Compression.ZipFile]::OpenRead($archivePath.FullName)
    $pluginArchiveHandle = $null
    try {
        $pluginArchiveHandle = Get-PluginZipArchive -OuterArchive $outerArchive -ArchiveName $archivePath.Name
        $pluginArchive = $pluginArchiveHandle.Archive
        $actualFiles = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
        foreach ($entry in $pluginArchive.Entries) {
            if ([string]::IsNullOrEmpty($entry.Name)) {
                continue
            }

            $path = ConvertTo-NormalizedArchivePath -Path $entry.FullName
            if ($path.StartsWith('/', [StringComparison]::Ordinal) -or
                $path -match '(^|/)\.\.(/|$)' -or
                [IO.Path]::IsPathRooted($path)) {
                throw "Archive '$($archivePath.Name)' contains unsafe path '$path'."
            }
            if ($path -eq 'license.txt') {
                throw "Archive '$($archivePath.Name)' contains root license.txt; the existing Plugin.zip layout packages only third-party-notices."
            }
            if (-not $path.StartsWith('third-party-notices/', [StringComparison]::Ordinal)) {
                continue
            }
            if ($actualFiles.ContainsKey($path)) {
                throw "Archive '$($archivePath.Name)' contains duplicate license path '$path'."
            }

            $stream = $entry.Open()
            try {
                $digest = [Security.Cryptography.SHA256]::HashData($stream)
            }
            finally {
                $stream.Dispose()
            }
            $actualFiles.Add($path, [PSCustomObject]@{
                size = [long]$entry.Length
                sha256 = [Convert]::ToHexString($digest).ToLowerInvariant()
            })
        }

        if ($actualFiles.Count -ne $expectedFiles.Count) {
            throw "Archive '$($archivePath.Name)' license inventory does not match the committed snapshot. Expected $($expectedFiles.Count) files, found $($actualFiles.Count)."
        }
        foreach ($path in $expectedFiles.Keys) {
            $actual = $null
            if (-not $actualFiles.TryGetValue($path, [ref]$actual)) {
                throw "Archive '$($archivePath.Name)' is missing committed license path '$path'."
            }
            $expected = $expectedFiles[$path]
            if ($actual.size -ne [long]$expected.size -or $actual.sha256 -cne [string]$expected.sha256) {
                throw "Archive '$($archivePath.Name)' path '$path' does not match the committed snapshot."
            }
        }
    }
    finally {
        if ($null -ne $pluginArchiveHandle) {
            $pluginArchiveHandle.Archive.Dispose()
            $pluginArchiveHandle.Bytes.Dispose()
        }
        $outerArchive.Dispose()
    }
}

$manifestHash = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host "[ARCHIVE-SNAPSHOT] package=$($definition.packageId) archives=$($archives.Count) manifest_sha256=$manifestHash" -ForegroundColor Green
if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_STEP_SUMMARY)) {
    Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value @(
        "- Verified **$($archives.Count)** $($definition.packageId) archives against committed license snapshot ``$manifestHash``.",
        ''
    )
}
