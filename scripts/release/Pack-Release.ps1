[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$Tag,
    [string]$OutputPath = "artifacts/release"
)

. (Join-Path $PSScriptRoot "Release.Common.ps1")
. (Join-Path $PSScriptRoot "../common/Plugin-ArtifactIntegrity.ps1")

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

$pluginReleaseMetadataPath = Join-Path $resolvedOutputPath $script:MusoqPluginReleaseMetadataFileName
if (Test-Path -LiteralPath $pluginReleaseMetadataPath) {
    $existingMetadata = Read-MusoqPluginReleaseMetadata -Path $pluginReleaseMetadataPath
    if ($existingMetadata.plugin -ne $release.PackageId -or
        $existingMetadata.version -ne $release.Version -or
        $existingMetadata.releaseTag -ne $release.Tag) {
        throw "Release output already contains immutable metadata for a different plugin release: $pluginReleaseMetadataPath"
    }

    & (Join-Path $PSScriptRoot "Test-ReleaseSmoke.ps1") -Tag $Tag -ArtifactDirectory $resolvedOutputPath
    if ($LASTEXITCODE -ne 0) {
        throw "Existing immutable release output failed verification for $Tag."
    }

    Write-Host "Accepted existing immutable release output for $Tag." -ForegroundColor Gray
    return
}

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
$pluginArtifactPaths = [ordered]@{}
foreach ($artifactEntry in (Get-ArtifactNames -ProjectName $release.PackageId).GetEnumerator()) {
    $artifactName = $artifactEntry.Value
    $artifactPath = Join-Path $pluginOutputPath $artifactName
    if (-not (Test-Path -LiteralPath $artifactPath)) {
        throw "Expected plugin zip was not produced: $artifactPath"
    }

    $pluginArtifacts += $artifactPath
    $pluginArtifactPaths[$artifactEntry.Key] = $artifactPath
}

& (Join-Path $PSScriptRoot "Assert-ReleaseLicenseArtifacts.ps1") `
    -PluginName $release.PackageId `
    -RepositoryRoot $repositoryRoot `
    -AssetsDirectory $pluginOutputPath `
    -ExpectedArchiveCount 4 | Out-Null

$pluginReleaseMetadata = New-MusoqPluginReleaseMetadata `
    -PluginName $release.PackageId `
    -Version $release.Version `
    -ReleaseTag $release.Tag `
    -ArtifactPaths $pluginArtifactPaths
Write-MusoqImmutablePluginReleaseMetadata -Metadata $pluginReleaseMetadata -Path $pluginReleaseMetadataPath

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
    pluginArtifactIntegrity = $pluginReleaseMetadata.artifacts
    runtimeCompatibility = $pluginReleaseMetadata.runtimeCompatibility
    pluginReleaseMetadata = $pluginReleaseMetadataPath
}

$manifestPath = Join-Path $resolvedOutputPath "release-artifacts.json"
$manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $manifestPath -Encoding UTF8

Write-Host "Release artifact manifest: $manifestPath" -ForegroundColor Gray
