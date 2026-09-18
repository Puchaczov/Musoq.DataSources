[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "Release.Common.ps1")
. (Join-Path $PSScriptRoot "../common/Plugin-Compatibility.ps1")

function Assert-Condition {
    param(
        [Parameter(Mandatory=$true)]
        [bool]$Condition,
        [Parameter(Mandatory=$true)]
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

$repositoryRoot = Get-ReleaseRepositoryRoot
$packages = @(Get-ReleasePackages)
$searchPackages = @($packages | Where-Object { $_.PackageId -eq "Musoq.DataSources.Search" })
Assert-Condition ($searchPackages.Count -eq 1) "The release registry must resolve exactly one production Search package."
$search = $searchPackages[0]

Assert-Condition ($search.Slug -eq "search") "Search must use the canonical release slug."
Assert-Condition ($search.Version -eq "1.0.1-alpha.1") "Search must use its next patch 1.0.1-alpha.1 version."
Assert-Condition ($search.ProjectPath -eq "Musoq.DataSources.Search/Musoq.DataSources.Search.csproj") "Search must resolve to the production project path."
Assert-Condition (Test-ReleasePluginProject -ProjectPath $search.FullProjectPath) "The registered Search project must contain a production datasource schema."

$excludedRegistryProjects = @($packages | Where-Object {
    $_.ProjectPath -match '(?i)(?:^|[\\/])[^\\/]*(?:\.Tests|\.Benchmarks)(?:[\\/]|$)'
})
Assert-Condition ($excludedRegistryProjects.Count -eq 0) "The release registry must exclude test and benchmark projects."

$discoveredProductionProjects = @(Get-PluginProjects)
$discoveredNames = @($discoveredProductionProjects | ForEach-Object { $_.BaseName })
Assert-Condition ($discoveredNames -contains "Musoq.DataSources.Search") "Production project discovery must include Search."
Assert-Condition ($discoveredNames -notcontains "Musoq.DataSources.Search.Tests") "Production project discovery must exclude Search.Tests."
Assert-Condition ($discoveredNames -notcontains "Musoq.DataSources.Search.Benchmarks") "Production project discovery must exclude Search.Benchmarks."

$compatibility = Get-MusoqPluginCompatibility -ProjectPath $search.FullProjectPath
Assert-Condition ($compatibility.targetFramework -eq "net10.0") "Search compatibility must target net10.0."

$searchTag = "$($search.Version)-$($search.PackageId)"
$release = Resolve-DatasourceReleaseTag -Tag $searchTag
Assert-Condition ($release.PackageId -eq "Musoq.DataSources.Search") "Search tag validation must resolve the Search package."
Assert-Condition ($release.Version -eq $search.Version) "Search tag validation must preserve the registry version."

$batchJson = & (Join-Path $PSScriptRoot "Resolve-BatchRelease.ps1") -Selection "search" -Json
if ($LASTEXITCODE -ne 0) {
    throw "Batch release selection failed for Search."
}
$batchRelease = @($batchJson | ConvertFrom-Json)
Assert-Condition ($batchRelease.Count -eq 1) "Batch release selection must resolve exactly one Search package."
Assert-Condition ($batchRelease[0].tag -eq $searchTag) "Batch release selection must produce Search's exact release tag."

[ordered]@{
    packageCount = $packages.Count
    productionProjectCount = $discoveredProductionProjects.Count
    search = [ordered]@{
        slug = $search.Slug
        packageId = $search.PackageId
        version = $search.Version
        projectPath = $search.ProjectPath
        releaseTag = $release.Tag
        targetFramework = $compatibility.targetFramework
        hostPackages = $compatibility.hostPackages
    }
    excludedRegistryProjectCount = $excludedRegistryProjects.Count
    excludedProductionProjectNames = @(
        "Musoq.DataSources.Search.Tests",
        "Musoq.DataSources.Search.Benchmarks"
    ) | Where-Object { $discoveredNames -contains $_ }
} | ConvertTo-Json -Depth 10
