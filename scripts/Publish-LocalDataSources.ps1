#Requires -Version 7.2
[CmdletBinding()]
param(
    [string] $Selection = 'All',
    [string] $OutputDirectory = 'C:/Shared/Musoq.DataSources'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$resolveBatchScript = Join-Path $PSScriptRoot 'release/Resolve-BatchRelease.ps1'
$packPluginScript = Join-Path $PSScriptRoot 'Pack-Plugin.ps1'
$assertLicenseArtifactsScript = Join-Path $PSScriptRoot 'release/Assert-ReleaseLicenseArtifacts.ps1'
$packageRegistryPath = Join-Path $PSScriptRoot 'release/packages.json'

function Get-ResolvedOutputDirectory {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $fullPath = [IO.Path]::GetFullPath($Path)
    New-Item -ItemType Directory -Path $fullPath -Force | Out-Null
    return (Resolve-Path -LiteralPath $fullPath).Path
}

function Resolve-SelectedDatasourceReleases {
    param(
        [Parameter(Mandatory)]
        [string] $Value
    )

    $json = & $resolveBatchScript -Selection $Value -Json
    if ($LASTEXITCODE -ne 0) {
        throw "Datasource selection resolution failed with exit code $LASTEXITCODE."
    }

    $releases = @($json | ConvertFrom-Json -Depth 20)
    if ($releases.Count -eq 0) {
        throw "Datasource selection '$Value' did not resolve any registered datasource packages."
    }

    $registry = Get-Content -LiteralPath $packageRegistryPath -Raw | ConvertFrom-Json -Depth 20
    $order = @{}
    $index = 0
    foreach ($package in @($registry.packages)) {
        $order[[string]$package.packageId] = $index
        $index++
    }

    foreach ($release in $releases) {
        $packageId = [string]$release.packageId
        if (-not $order.ContainsKey($packageId)) {
            throw "Resolved datasource '$packageId' is not present in packages.json."
        }
    }

    return @($releases | Sort-Object -Property @{ Expression = { $order[[string]$_.packageId] } })
}

function Get-ExpectedDatasourceArchives {
    param(
        [Parameter(Mandatory)]
        [string] $PackageId,
        [Parameter(Mandatory)]
        [string] $Directory
    )

    $archiveNames = @(
        "$PackageId-windows-x64.zip",
        "$PackageId-linux-x64.zip",
        "$PackageId-macos-arm64.zip",
        "$PackageId-alpine-x64.zip"
    )

    $archivePaths = @()
    foreach ($archiveName in $archiveNames) {
        $archivePath = Join-Path $Directory $archiveName
        if (-not (Test-Path -LiteralPath $archivePath -PathType Leaf)) {
            throw "Expected local datasource archive was not produced: $archivePath"
        }

        $archivePaths += (Resolve-Path -LiteralPath $archivePath).Path
    }

    return @($archivePaths)
}

$selectedReleases = @(Resolve-SelectedDatasourceReleases -Value $Selection)
$resolvedOutputDirectory = Get-ResolvedOutputDirectory -Path $OutputDirectory
$results = @()

Write-Host "Packaging $($selectedReleases.Count) datasource package(s) into '$resolvedOutputDirectory'." -ForegroundColor Cyan

foreach ($release in $selectedReleases) {
    $packageId = [string]$release.packageId
    $slug = [string]$release.slug
    $version = [string]$release.version
    $packageOutputDirectory = Join-Path $resolvedOutputDirectory $slug

    New-Item -ItemType Directory -Path $packageOutputDirectory -Force | Out-Null

    Write-Host "Packaging $packageId $version -> $packageOutputDirectory" -ForegroundColor Cyan
    & $packPluginScript -PluginName $packageId -OutputDirectory $packageOutputDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "Local datasource packaging failed for $packageId $version with exit code $LASTEXITCODE."
    }

    $archivePaths = @(Get-ExpectedDatasourceArchives -PackageId $packageId -Directory $packageOutputDirectory)

    & $assertLicenseArtifactsScript `
        -PluginName $packageId `
        -RepositoryRoot $repositoryRoot `
        -AssetsDirectory $packageOutputDirectory `
        -ExpectedArchiveCount 4
    if ($LASTEXITCODE -ne 0) {
        throw "Local datasource license archive validation failed for $packageId $version with exit code $LASTEXITCODE."
    }

    $results += [PSCustomObject][ordered]@{
        packageId = $packageId
        version = $version
        slug = $slug
        outputDirectory = (Resolve-Path -LiteralPath $packageOutputDirectory).Path
        archives = $archivePaths
    }
}

Write-Host "Local datasource packaging completed." -ForegroundColor Green
foreach ($result in $results) {
    Write-Host "  $($result.packageId) $($result.version)" -ForegroundColor Gray
    foreach ($archive in @($result.archives)) {
        Write-Host "    $archive" -ForegroundColor Gray
    }
}
