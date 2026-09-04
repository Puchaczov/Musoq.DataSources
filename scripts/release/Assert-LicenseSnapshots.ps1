#Requires -Version 7.2
[CmdletBinding()]
param(
    [Alias('PackageId', 'Product')]
    [string] $PluginName = 'All',
    [string] $RepositoryRoot = (Join-Path $PSScriptRoot '../..'),
    [string] $SnapshotRoot,
    [switch] $ValidatePackageGraph,
    [switch] $WriteGitHubSummary
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'LicenseSnapshot.Common.psm1') -Force

function Compare-DatasourceSnapshotRecords {
    param(
        [Parameter(Mandatory)] [object[]] $Expected,
        [Parameter(Mandatory)] [object[]] $Actual,
        [Parameter(Mandatory)] [string] $Description
    )

    if ($Expected.Count -ne $Actual.Count) {
        throw "$Description count differs. Expected $($Expected.Count), got $($Actual.Count)."
    }
    for ($index = 0; $index -lt $Expected.Count; $index++) {
        if ($Expected[$index].path -cne $Actual[$index].path -or $Expected[$index].sha256 -cne $Actual[$index].sha256) {
            throw "$Description entry $index differs. Expected '$($Expected[$index].path)', got '$($Actual[$index].path)'."
        }
    }
}

function Get-DatasourceSnapshotAdditionalInputs {
    param(
        [Parameter(Mandatory)] [string] $Root,
        [Parameter(Mandatory)] [object] $Definition
    )

    $paths = @(
        'scripts/release/packages.json',
        '.config/dotnet-tools.json',
        'LinksManual.json',
        'LICENSE'
    )
    $staticLicenseRoot = Join-Path $Root 'licenses'
    if ([IO.Directory]::Exists($staticLicenseRoot)) {
        $paths += @(Get-ChildItem -LiteralPath $staticLicenseRoot -File -Filter '*.txt' | ForEach-Object {
            Get-DatasourceRelativePath -Root $Root -Path $_.FullName
        })
    }
    return @($paths)
}

$root = [IO.Path]::GetFullPath($RepositoryRoot)
if ([string]::IsNullOrWhiteSpace($SnapshotRoot)) { $SnapshotRoot = Join-Path $root 'licenses/release' }
$snapshots = [IO.Path]::GetFullPath($SnapshotRoot)
Assert-DatasourcePathUnderRoot -Root $root -Path $snapshots -Description 'Snapshot root'

$toolManifestPath = Join-Path $root '.config/dotnet-tools.json'
if (-not [IO.File]::Exists($toolManifestPath)) { throw "Pinned tool manifest was not found: $toolManifestPath" }
try { $toolManifest = [IO.File]::ReadAllText($toolManifestPath) | ConvertFrom-Json -Depth 20 }
catch { throw "Pinned tool manifest is invalid JSON: $($_.Exception.Message)" }
$expectedToolVersion = [string]$toolManifest.tools.'nuget-license'.version
if ([string]::IsNullOrWhiteSpace($expectedToolVersion)) { throw 'Pinned nuget-license version is missing.' }

$gathererRelativePath = 'tools/dotnet/LicenseGatherer/Musoq.Cloud.LicensesGatherer.exe'
$gathererPath = Join-Path $root $gathererRelativePath
if (-not [IO.File]::Exists($gathererPath)) { throw "Bundled license gatherer was not found: $gathererPath" }
$expectedGathererHash = (Get-FileHash -LiteralPath $gathererPath -Algorithm SHA256).Hash.ToLowerInvariant()

$definitions = if ($PluginName -eq 'All') {
    @(Get-DatasourcePackageDefinitions -RepositoryRoot $root)
}
else {
    @((Get-DatasourcePackageDefinition -RepositoryRoot $root -Selector $PluginName))
}
if ($definitions.Count -eq 0) { throw 'No datasource packages were selected for license snapshot validation.' }
if (-not [IO.Directory]::Exists($snapshots)) { throw "License snapshot root does not exist: $snapshots" }

foreach ($definition in $definitions) {
    $refreshCommand = "pwsh scripts/release/Update-LicenseSnapshots.ps1 -PluginName '$($definition.packageId)'"
    $snapshotDirectory = Join-Path $snapshots $definition.packageId
    Assert-DatasourcePathUnderRoot -Root $snapshots -Path $snapshotDirectory -Description 'License snapshot'
    $manifestPath = Join-Path $snapshotDirectory 'manifest.json'
    if (-not [IO.File]::Exists($manifestPath)) {
        throw "License snapshot manifest '$manifestPath' is missing. Run: $refreshCommand"
    }

    try { $manifest = [IO.File]::ReadAllText($manifestPath) | ConvertFrom-Json -Depth 100 }
    catch { throw "License snapshot manifest '$manifestPath' is invalid JSON: $($_.Exception.Message) Run: $refreshCommand" }

    if ($manifest.schemaVersion -ne 2) { throw "License snapshot '$($definition.packageId)' has unsupported schema version '$($manifest.schemaVersion)'. Run: $refreshCommand" }
    if ($manifest.packageId -cne $definition.packageId) { throw "License snapshot '$($definition.packageId)' has the wrong package identity. Run: $refreshCommand" }
    if ($manifest.slug -cne $definition.slug) { throw "License snapshot '$($definition.packageId)' has the wrong package slug. Run: $refreshCommand" }
    if ($manifest.version -cne $definition.version) { throw "License snapshot '$($definition.packageId)' has version '$($manifest.version)' instead of '$($definition.version)'. Run: $refreshCommand" }
    if ($manifest.projectPath -cne $definition.projectPath) { throw "License snapshot '$($definition.packageId)' has the wrong project path. Run: $refreshCommand" }
    if ($manifest.entryProject -cne $definition.projectPath) { throw "License snapshot '$($definition.packageId)' has the wrong entry project. Run: $refreshCommand" }
    if ($manifest.ownPackageId -cne $definition.packageId) { throw "License snapshot '$($definition.packageId)' has the wrong own-package id. Run: $refreshCommand" }
    if ($manifest.licenseTool.package -cne 'nuget-license' -or $manifest.licenseTool.version -cne $expectedToolVersion) {
        throw "License snapshot '$($definition.packageId)' was generated with the wrong nuget-license version. Run: $refreshCommand"
    }
    if ($manifest.licenseGatherer.path -cne $gathererRelativePath -or $manifest.licenseGatherer.sha256 -cne $expectedGathererHash) {
        throw "License snapshot '$($definition.packageId)' was generated with a different bundled gatherer. Run: $refreshCommand"
    }

    $expectedInputs = @(Get-DatasourceDependencyInputManifest `
        -RepositoryRoot $root `
        -EntryProject $definition.projectPath `
        -AdditionalPath (Get-DatasourceSnapshotAdditionalInputs -Root $root -Definition $definition))
    $recordedInputs = @($manifest.dependencyInputs)
    Compare-DatasourceSnapshotRecords -Expected $expectedInputs -Actual $recordedInputs -Description "License snapshot '$($definition.packageId)' dependency inputs"

    $actualFiles = @(Get-DatasourceSnapshotFileManifest -SnapshotDirectory $snapshotDirectory)
    $recordedFiles = @($manifest.files)
    Compare-DatasourceSnapshotRecords -Expected $actualFiles -Actual $recordedFiles -Description "License snapshot '$($definition.packageId)' files"
    $seenPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($file in $actualFiles) {
        $path = [string]$file.path
        if (-not $seenPaths.Add($path)) { throw "License snapshot '$($definition.packageId)' contains duplicate file path '$path'." }
        if ($path -ne 'license.txt' -and -not $path.StartsWith('third-party-notices/', [StringComparison]::Ordinal)) {
            throw "License snapshot '$($definition.packageId)' contains unexpected file '$path'. Run: $refreshCommand"
        }
        if ($path -match '(^|/)(LinksCache\.json|\.licenses-cache|licenses-cache|cache)(/|$)') {
            throw "License snapshot '$($definition.packageId)' contains transient cache path '$path'. Run: $refreshCommand"
        }
    }

    $ownLicense = Join-Path $snapshotDirectory 'license.txt'
    if (-not [IO.File]::Exists($ownLicense) -or (Get-Item -LiteralPath $ownLicense).Length -lt 500) {
        throw "License snapshot '$($definition.packageId)' is missing a complete own-package license. Run: $refreshCommand"
    }

    $graph = @($manifest.packageGraph)
    if ($graph.Count -eq 0) { throw "License snapshot '$($definition.packageId)' has an empty package graph. Run: $refreshCommand" }
    $graphKeys = @($graph | ForEach-Object {
        if ([string]::IsNullOrWhiteSpace([string]$_.id) -or [string]::IsNullOrWhiteSpace([string]$_.version)) { throw "License snapshot '$($definition.packageId)' has a malformed package graph entry. Run: $refreshCommand" }
        "$($_.id)|$($_.version)".ToLowerInvariant()
    })
    if (@($graphKeys | Sort-Object -Unique).Count -ne $graphKeys.Count) { throw "License snapshot '$($definition.packageId)' contains duplicate package graph entries. Run: $refreshCommand" }

    foreach ($package in $graph) {
        $noticeDirectory = Join-Path $snapshotDirectory "third-party-notices/$($package.id)"
        if (-not [IO.Directory]::Exists($noticeDirectory)) { throw "License snapshot '$($definition.packageId)' has no notice directory for $($package.id) $($package.version). Run: $refreshCommand" }
        $texts = @(Get-ChildItem -LiteralPath $noticeDirectory -File -ErrorAction SilentlyContinue |
            Where-Object Name -in @('license.txt', 'third-party-notices.txt'))
        if ($texts.Count -eq 0) { throw "License snapshot '$($definition.packageId)' has no license or notice text for $($package.id) $($package.version). Run: $refreshCommand" }
        if (@($texts | Where-Object Length -ge 500).Count -eq 0) {
            throw "License snapshot '$($definition.packageId)' has only abbreviated license or notice text for $($package.id) $($package.version). Run: $refreshCommand"
        }
    }

    $reportPath = Join-Path $snapshotDirectory 'third-party-notices/report.json'
    if (-not [IO.File]::Exists($reportPath)) { throw "License snapshot '$($definition.packageId)' is missing third-party-notices/report.json. Run: $refreshCommand" }
    try { $report = @([IO.File]::ReadAllText($reportPath) | ConvertFrom-Json -Depth 100) }
    catch { throw "License snapshot '$($definition.packageId)' report is invalid JSON: $($_.Exception.Message) Run: $refreshCommand" }
    $ownReportEntries = @($report | Where-Object { $_.PackageId -eq $definition.packageId })
    $dependencyReportKeys = @($report | Where-Object { $_.PackageId -ne $definition.packageId } | ForEach-Object {
        if ([string]::IsNullOrWhiteSpace([string]$_.PackageId) -or [string]::IsNullOrWhiteSpace([string]$_.PackageVersion)) { throw "License snapshot '$($definition.packageId)' has a malformed report entry. Run: $refreshCommand" }
        "$($_.PackageId)|$($_.PackageVersion)".ToLowerInvariant()
    })
    if ($ownReportEntries.Count -ne 1 -or @($dependencyReportKeys | Sort-Object -Unique).Count -ne $dependencyReportKeys.Count -or
        (Compare-Object -ReferenceObject @($graphKeys | Sort-Object) -DifferenceObject @($dependencyReportKeys | Sort-Object))) {
        throw "License snapshot '$($definition.packageId)' report does not match its package graph. Run: $refreshCommand"
    }

    if ($ValidatePackageGraph) {
        $actualGraph = @(Get-DatasourcePackageGraph -RepositoryRoot $root -EntryProject $definition.projectPath)
        $actualGraphKeys = @($actualGraph | ForEach-Object { "$($_.id)|$($_.version)".ToLowerInvariant() })
        if (Compare-Object -ReferenceObject @($graphKeys | Sort-Object) -DifferenceObject @($actualGraphKeys | Sort-Object)) {
            throw "License snapshot '$($definition.packageId)' does not cover the restored package graph. Run: $refreshCommand"
        }
    }

    $manifestHash = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
    [Console]::Out.WriteLine("[SNAPSHOT] package=$($definition.packageId) packages=$($graph.Count) files=$($actualFiles.Count) manifest_sha256=$manifestHash source=checked-in")
    if ($WriteGitHubSummary -and -not [string]::IsNullOrWhiteSpace($env:GITHUB_STEP_SUMMARY)) {
        Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value @(
            '### Committed datasource license snapshot',
            '',
            '| Package | Dependencies | Files | Manifest SHA-256 | Source |',
            '| --- | ---: | ---: | --- | --- |',
            "| $($definition.packageId) | $($graph.Count) | $($actualFiles.Count) | ``$manifestHash`` | checked-in repository snapshot |",
            ''
        )
    }
}
