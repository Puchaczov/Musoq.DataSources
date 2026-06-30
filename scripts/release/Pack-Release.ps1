[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$Tag,
    [string]$OutputPath = "artifacts/release"
)

. (Join-Path $PSScriptRoot "Release.Common.ps1")

$release = Resolve-DatasourceReleaseTag -Tag $Tag
$repositoryRoot = Get-ReleaseRepositoryRoot

$resolvedOutputPath = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
    [System.IO.Path]::GetFullPath($OutputPath)
}
else {
    [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputPath))
}

$nupkgOutputPath = Join-Path $resolvedOutputPath "nupkgs"
$pluginOutputPath = Join-Path $resolvedOutputPath "plugins"
New-Item -ItemType Directory -Force -Path $nupkgOutputPath | Out-Null
New-Item -ItemType Directory -Force -Path $pluginOutputPath | Out-Null

Write-Host "Packing NuGet package for $($release.PackageId) $($release.Version)" -ForegroundColor Cyan
Invoke-ReleaseCommand -FilePath "dotnet" -Arguments @(
    "pack",
    $release.FullProjectPath,
    "--configuration",
    "Release",
    "--no-build",
    "--output",
    $nupkgOutputPath,
    "--nologo",
    "--verbosity",
    "quiet"
)

$nupkgPath = Join-Path $nupkgOutputPath "$($release.PackageId).$($release.Version).nupkg"
$snupkgPath = Join-Path $nupkgOutputPath "$($release.PackageId).$($release.Version).snupkg"
if (-not (Test-Path -LiteralPath $nupkgPath)) {
    throw "Expected NuGet package was not produced: $nupkgPath"
}

if (-not (Test-Path -LiteralPath $snupkgPath)) {
    throw "Expected symbol package was not produced: $snupkgPath"
}

Write-Host "Packing plugin zips for $($release.PackageId)" -ForegroundColor Cyan
$packPluginScript = Join-Path $repositoryRoot "scripts/Pack-Plugin.ps1"
Set-StrictMode -Off
try {
    & $packPluginScript -PluginName $release.PackageId -OutputDirectory $pluginOutputPath
    if ($LASTEXITCODE -ne 0) {
        throw "Plugin zip packing failed for $($release.PackageId)."
    }
}
finally {
    Set-StrictMode -Version Latest
}

$pluginArtifacts = @()
foreach ($artifactName in (Get-ArtifactNames -ProjectName $release.PackageId).Values) {
    $artifactPath = Join-Path $pluginOutputPath $artifactName
    if (-not (Test-Path -LiteralPath $artifactPath)) {
        throw "Expected plugin zip was not produced: $artifactPath"
    }

    $pluginArtifacts += $artifactPath
}

$manifest = [PSCustomObject]@{
    tag = $release.Tag
    version = $release.Version
    channel = $release.Channel
    isPrerelease = $release.IsPrerelease
    packageId = $release.PackageId
    slug = $release.Slug
    nupkg = $nupkgPath
    snupkg = $snupkgPath
    pluginArtifacts = @($pluginArtifacts)
}

$manifestPath = Join-Path $resolvedOutputPath "release-artifacts.json"
$manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $manifestPath -Encoding UTF8

Write-Host "Release artifact manifest: $manifestPath" -ForegroundColor Gray
