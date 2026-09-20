#Requires -Version 7.2
[CmdletBinding()]
param(
    [Alias('PackageId', 'Product')]
    [string] $PluginName = 'All',
    [string] $RepositoryRoot = (Join-Path $PSScriptRoot '../..'),
    [switch] $NoRestore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'LicenseSnapshot.Common.psm1') -Force

function Invoke-DatasourceCheckedCommand {
    param(
        [Parameter(Mandatory)] [string] $FilePath,
        [Parameter(Mandatory)] [string[]] $Arguments,
        [Parameter(Mandatory)] [string] $Description
    )

    $output = @(& $FilePath @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) {
        $details = ($output | Out-String).Trim()
        if ([string]::IsNullOrWhiteSpace($details)) { $details = '<no output>' }
        throw "$Description failed with exit code $LASTEXITCODE. $details"
    }
    return $output
}

function ConvertTo-DatasourceNormalizedTextFile {
    param([Parameter(Mandatory)] [string] $Path)

    $content = [IO.File]::ReadAllText($Path).Replace("`r`n", "`n").Replace("`r", "`n")
    $content = $content.TrimEnd([char[]]"`n") + "`n"
    [IO.File]::WriteAllText($Path, $content, [Text.UTF8Encoding]::new($false))
}

function Get-DatasourceProjectProperty {
    param(
        [Parameter(Mandatory)] [string] $ProjectPath,
        [Parameter(Mandatory)] [string] $PropertyName,
        [string] $DefaultValue = ''
    )

    [xml]$project = [IO.File]::ReadAllText($ProjectPath)
    $property = $project.SelectSingleNode("//*[local-name()='$PropertyName']")
    if ($null -eq $property -or [string]::IsNullOrWhiteSpace([string]$property.InnerText)) { return $DefaultValue }
    return ([string]$property.InnerText).Trim()
}

function New-DatasourceOwnPackageFile {
    param(
        [Parameter(Mandatory)] [object] $Definition,
        [Parameter(Mandatory)] [string] $Path,
        [Parameter(Mandatory)] [string] $RepositoryRoot
    )

    $projectUrl = Get-DatasourceProjectProperty -ProjectPath $Definition.fullProjectPath -PropertyName 'PackageProjectUrl' -DefaultValue 'https://github.com/Puchaczov/Musoq.DataSources'
    $ownPackage = [PSCustomObject][ordered]@{
        PackageId = $Definition.packageId
        PackageVersion = $Definition.version
        PackageProjectUrl = $projectUrl
        License = 'MIT'
        LicenseUrl = ''
    }
    $json = ($ownPackage | ConvertTo-Json -Depth 20) + "`n"
    [IO.File]::WriteAllText($Path, $json, [Text.UTF8Encoding]::new($false))
}

function Assert-DatasourceSnapshotStage {
    param(
        [Parameter(Mandatory)] [object] $Definition,
        [Parameter(Mandatory)] [string] $RepositoryRoot,
        [Parameter(Mandatory)] [string] $Stage,
        [Parameter(Mandatory)] [string] $ValidationRoot
    )

    if (Test-Path -LiteralPath $ValidationRoot) { Remove-Item -LiteralPath $ValidationRoot -Recurse -Force }
    New-Item -ItemType Directory -Path $ValidationRoot -Force | Out-Null
    Copy-Item -LiteralPath $Stage -Destination (Join-Path $ValidationRoot $Definition.packageId) -Recurse -Force
    & (Join-Path $PSScriptRoot 'Assert-LicenseSnapshots.ps1') `
        -PluginName $Definition.packageId `
        -RepositoryRoot $RepositoryRoot `
        -SnapshotRoot $ValidationRoot `
        -ValidatePackageGraph
    if ($LASTEXITCODE -ne 0) { throw "Staged license snapshot validation failed for '$($Definition.packageId)'." }
}

$root = [IO.Path]::GetFullPath($RepositoryRoot)
$refreshRoot = [IO.Path]::GetFullPath((Join-Path $root '.builds/license-refresh'))
$snapshotRoot = [IO.Path]::GetFullPath((Join-Path $root 'licenses/release'))
Assert-DatasourcePathUnderRoot -Root $root -Path $refreshRoot -Description 'License refresh working root'
Assert-DatasourcePathUnderRoot -Root $root -Path $snapshotRoot -Description 'License snapshot root'

$toolManifestPath = Join-Path $root '.config/dotnet-tools.json'
if (-not [IO.File]::Exists($toolManifestPath)) { throw "Pinned tool manifest was not found: $toolManifestPath" }
$toolManifest = [IO.File]::ReadAllText($toolManifestPath) | ConvertFrom-Json -Depth 20
$toolVersion = [string]$toolManifest.tools.'nuget-license'.version
if ([string]::IsNullOrWhiteSpace($toolVersion)) { throw 'Pinned nuget-license version is missing from the tool manifest.' }

New-Item -ItemType Directory -Path $refreshRoot, $snapshotRoot -Force | Out-Null
& (Join-Path $PSScriptRoot '../Restore-PluginTooling.ps1') `
    -ManifestPath $toolManifestPath `
    -ToolPath (Join-Path $refreshRoot "tooling/$toolVersion")
if ($LASTEXITCODE -ne 0) { throw 'Pinned nuget-license provisioning failed.' }

$licenseCommand = Get-Command nuget-license -CommandType Application -ErrorAction Stop | Select-Object -First 1
$licenseVersionOutput = & $licenseCommand.Source --version 2>&1 | Out-String
if ($LASTEXITCODE -ne 0 -or $licenseVersionOutput -notmatch "(^|[^0-9])$([regex]::Escape($toolVersion))([^0-9]|$)") {
    throw "The refresh process did not provision nuget-license $toolVersion. Found: $($licenseVersionOutput.Trim())"
}

$gathererRelativePath = 'tools/dotnet/LicenseGatherer/Musoq.Cloud.LicensesGatherer.exe'
$gathererPath = Join-Path $root $gathererRelativePath
if (-not [IO.File]::Exists($gathererPath)) { throw "Bundled license gatherer was not found: $gathererPath" }
$gathererHash = (Get-FileHash -LiteralPath $gathererPath -Algorithm SHA256).Hash.ToLowerInvariant()
$selectedDefinitions = if ($PluginName -eq 'All') {
    @(Get-DatasourcePackageDefinitions -RepositoryRoot $root)
}
else {
    @((Get-DatasourcePackageDefinition -RepositoryRoot $root -Selector $PluginName))
}
if ($selectedDefinitions.Count -eq 0) { throw 'No datasource packages were selected for license snapshot refresh.' }

$manualLinksPath = Join-Path $root 'LinksManual.json'
if (-not [IO.File]::Exists($manualLinksPath)) { throw "Shared manual license links file was not found: $manualLinksPath" }
$repositoryStaticLicenses = Join-Path $root 'licenses'

foreach ($definition in $selectedDefinitions) {
    Write-Host "Refreshing license snapshot: $($definition.packageId)" -ForegroundColor Cyan
    if (-not $NoRestore) {
        Invoke-DatasourceCheckedCommand -FilePath 'dotnet' -Arguments @('restore', $definition.fullProjectPath, '--nologo') -Description "Restore for $($definition.packageId)" | Out-Null
    }

    $productRoot = Join-Path $refreshRoot $definition.packageId
    if (Test-Path -LiteralPath $productRoot) { Remove-Item -LiteralPath $productRoot -Recurse -Force }
    $stage = Join-Path $productRoot 'snapshot'
    $notices = Join-Path $stage 'third-party-notices'
    $licenseCache = Join-Path $productRoot 'licenses-cache'
    $downloadedLicenses = Join-Path $productRoot 'downloaded-licenses'
    $linksCache = Join-Path $productRoot 'LinksCache.json'
    $ownPackagePath = Join-Path $productRoot 'OwnPackage.json'
    New-Item -ItemType Directory -Path $notices, $licenseCache, $downloadedLicenses -Force | Out-Null

    if ([IO.Directory]::Exists($repositoryStaticLicenses)) {
        foreach ($staticLicense in Get-ChildItem -LiteralPath $repositoryStaticLicenses -File -Filter '*.txt') {
            Copy-Item -LiteralPath $staticLicense.FullName -Destination (Join-Path $downloadedLicenses $staticLicense.Name) -Force
        }
    }
    Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination (Join-Path $downloadedLicenses "$($definition.packageId).txt") -Force
    New-DatasourceOwnPackageFile -Definition $definition -Path $ownPackagePath -RepositoryRoot $root

    $gatherArguments = @(
        'retrieve',
        '--solution-or-cs-project-file-path', $definition.fullProjectPath,
        '--own-package-file-path', $ownPackagePath,
        '--licenses-folder', $notices,
        '--links-cache-file-path', $linksCache,
        '--manual-links-file-path', $manualLinksPath,
        '--licenses-cache-folder', $licenseCache,
        '--downloaded-licenses-folder', $downloadedLicenses
    )
    Invoke-DatasourceCheckedCommand -FilePath $gathererPath -Arguments $gatherArguments -Description "License gathering for $($definition.packageId)" | ForEach-Object {
        Write-Host "  $_" -ForegroundColor DarkGray
    }

    $rootLicenseSource = Join-Path $root 'LICENSE'
    $rootLicenseTarget = Join-Path $stage 'license.txt'
    Copy-Item -LiteralPath $rootLicenseSource -Destination $rootLicenseTarget -Force
    foreach ($file in Get-ChildItem -LiteralPath $stage -Recurse -File) {
        ConvertTo-DatasourceNormalizedTextFile -Path $file.FullName
    }

    $reportPath = Join-Path $notices 'report.json'
    if (-not [IO.File]::Exists($reportPath)) { throw "License gathering for '$($definition.packageId)' produced no report." }
    $packageGraph = @(Get-DatasourcePackageGraph -RepositoryRoot $root -EntryProject $definition.projectPath)
    if ($packageGraph.Count -eq 0) { throw "Restored package graph for '$($definition.packageId)' is empty." }

    $report = @([IO.File]::ReadAllText($reportPath) | ConvertFrom-Json -Depth 100)
    $reportKeys = @($report | Where-Object { $_.PackageId -ne $definition.packageId } | ForEach-Object { "$($_.PackageId)|$($_.PackageVersion)".ToLowerInvariant() })
    $graphKeys = @($packageGraph | ForEach-Object { "$($_.id)|$($_.version)".ToLowerInvariant() })
    if (@($report | Where-Object { $_.PackageId -eq $definition.packageId }).Count -ne 1 -or
        @($reportKeys | Sort-Object -Unique).Count -ne $reportKeys.Count -or
        (Compare-Object -ReferenceObject @($graphKeys | Sort-Object) -DifferenceObject @($reportKeys | Sort-Object))) {
        throw "License gatherer report for '$($definition.packageId)' does not match its restored package graph."
    }

    $manifest = [PSCustomObject][ordered]@{
        schemaVersion = 2
        packageId = $definition.packageId
        slug = $definition.slug
        version = $definition.version
        projectPath = $definition.projectPath
        entryProject = $definition.projectPath
        ownPackageId = $definition.packageId
        licenseTool = [PSCustomObject][ordered]@{
            package = 'nuget-license'
            version = $toolVersion
        }
        licenseGatherer = [PSCustomObject][ordered]@{
            path = $gathererRelativePath
            sha256 = $gathererHash
        }
        dependencyInputs = @(Get-DatasourceDependencyInputManifest `
            -RepositoryRoot $root `
            -EntryProject $definition.projectPath `
            -AdditionalPath (Get-DatasourceSnapshotAdditionalInputs -RepositoryRoot $root))
        packageGraph = $packageGraph
        files = @(Get-DatasourceSnapshotFileManifest -SnapshotDirectory $stage)
    }
    $manifestJson = (($manifest | ConvertTo-Json -Depth 100).Replace("`r`n", "`n").Replace("`r", "`n")) + "`n"
    [IO.File]::WriteAllText((Join-Path $stage 'manifest.json'), $manifestJson, [Text.UTF8Encoding]::new($false))

    $validationRoot = Join-Path $productRoot 'validation-root'
    Assert-DatasourceSnapshotStage -Definition $definition -RepositoryRoot $root -Stage $stage -ValidationRoot $validationRoot

    $target = [IO.Path]::GetFullPath((Join-Path $snapshotRoot $definition.packageId))
    Assert-DatasourcePathUnderRoot -Root $snapshotRoot -Path $target -Description 'License snapshot target'
    if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Recurse -Force }
    Copy-Item -LiteralPath $stage -Destination $target -Recurse -Force
    Write-Host "[UPDATED] License snapshot '$($definition.packageId)'" -ForegroundColor Green
}

& (Join-Path $PSScriptRoot 'Assert-LicenseSnapshots.ps1') `
    -PluginName $PluginName `
    -RepositoryRoot $root `
    -SnapshotRoot $snapshotRoot `
    -ValidatePackageGraph
if ($LASTEXITCODE -ne 0) { throw 'Final license snapshot validation failed.' }
