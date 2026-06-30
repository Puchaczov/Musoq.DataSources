[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$Tag,
    [Parameter(Mandatory=$true)]
    [string]$Repository,
    [string]$ArtifactDirectory = "artifacts/release",
    [string]$NuGetApiKey = "",
    [string]$NuGetSource = "https://api.nuget.org/v3/index.json",
    [string]$OutputMetadataPath = ""
)

. (Join-Path $PSScriptRoot "Release.Common.ps1")

if (-not (Test-ValidRepository -Repository $Repository)) {
    throw "Invalid repository format: $Repository. Expected 'owner/repo'."
}

$release = Resolve-DatasourceReleaseTag -Tag $Tag
$repositoryRoot = Get-ReleaseRepositoryRoot
$resolvedArtifactDirectory = if ([System.IO.Path]::IsPathRooted($ArtifactDirectory)) {
    [System.IO.Path]::GetFullPath($ArtifactDirectory)
}
else {
    [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $ArtifactDirectory))
}

& (Join-Path $PSScriptRoot "Test-ReleaseSmoke.ps1") -Tag $Tag -ArtifactDirectory $resolvedArtifactDirectory
if ($LASTEXITCODE -ne 0) {
    throw "Release smoke test failed for $Tag."
}

if ([string]::IsNullOrWhiteSpace($NuGetApiKey)) {
    $NuGetApiKey = $env:TRUSTED_NUGET_API_KEY
}

if ([string]::IsNullOrWhiteSpace($NuGetApiKey)) {
    $NuGetApiKey = $env:NUGET_MUSOQ_KEY
}

if ([string]::IsNullOrWhiteSpace($NuGetApiKey)) {
    $NuGetApiKey = $env:NUGET_API_KEY
}

if ([string]::IsNullOrWhiteSpace($NuGetApiKey)) {
    throw "NuGet API key is required. Configure Trusted Publishing or NUGET_MUSOQ_KEY."
}

$manifestPath = Join-Path $resolvedArtifactDirectory "release-artifacts.json"
if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw "Release artifact manifest was not found: $manifestPath"
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.tag -ne $release.Tag) {
    throw "Release artifact manifest tag '$($manifest.tag)' does not match '$($release.Tag)'."
}

$nugetPackageFiles = @($manifest.nupkg, $manifest.snupkg)
foreach ($packageFile in $nugetPackageFiles) {
    if (-not (Test-Path -LiteralPath $packageFile)) {
        throw "NuGet package listed in manifest was not found: $packageFile"
    }

    Write-Host "Publishing $([System.IO.Path]::GetFileName($packageFile)) to NuGet..." -ForegroundColor Cyan
    dotnet nuget push $packageFile `
        --source $NuGetSource `
        --api-key $NuGetApiKey `
        --skip-duplicate

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet nuget push failed for $packageFile."
    }
}

$assetPaths = @()
$assetPaths += $nugetPackageFiles
$assetPaths += @($manifest.pluginArtifacts)
foreach ($assetPath in $assetPaths) {
    if (-not (Test-Path -LiteralPath $assetPath)) {
        throw "Release asset listed in manifest was not found: $assetPath"
    }
}

$releaseExists = $false
gh release view $release.Tag --repo $Repository 1>$null 2>$null
if ($LASTEXITCODE -eq 0) {
    $releaseExists = $true
}

if ($releaseExists) {
    Write-Host "Uploading assets to existing GitHub release $($release.Tag)..." -ForegroundColor Cyan
    gh release upload $release.Tag @assetPaths --clobber --repo $Repository
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to upload assets to GitHub release $($release.Tag)."
    }
}
else {
    Write-Host "Creating GitHub release $($release.Tag)..." -ForegroundColor Cyan
    $createArgs = @(
        "release",
        "create",
        $release.Tag,
        "--title",
        "Release $($release.Version) ($($release.PackageId))",
        "--generate-notes",
        "--repo",
        $Repository
    )

    if ($release.IsPrerelease) {
        $createArgs += "--prerelease"
    }

    $createArgs += $assetPaths
    gh @createArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to create GitHub release $($release.Tag)."
    }
}

$releaseInfo = gh release view $release.Tag --repo $Repository --json createdAt 2>$null | ConvertFrom-Json
$releaseDate = if ($releaseInfo.createdAt) {
    ([DateTime]::Parse([string]$releaseInfo.createdAt, [System.Globalization.CultureInfo]::InvariantCulture)).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", [System.Globalization.CultureInfo]::InvariantCulture)
}
else {
    (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", [System.Globalization.CultureInfo]::InvariantCulture)
}

$publishedPlugin = [PSCustomObject]@{
    Name = $release.PackageId
    ShortName = $release.ShortName
    Description = $release.Description
    Tags = @($release.Tags)
    Version = $release.Version
    ReleaseTag = $release.Tag
    ReleaseDate = $releaseDate
    Channel = $release.Channel
    IsPrerelease = $release.IsPrerelease
    Artifacts = Get-ArtifactNames -ProjectName $release.PackageId
}

if ($OutputMetadataPath) {
    $outputDirectory = Split-Path -Parent $OutputMetadataPath
    if ($outputDirectory -and -not (Test-Path -LiteralPath $outputDirectory)) {
        throw "Output directory does not exist: $outputDirectory"
    }

    if ($OutputMetadataPath -match '\.\.[/\\]') {
        throw "Invalid output path: path traversal not allowed."
    }

    @($publishedPlugin) | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $OutputMetadataPath -Encoding UTF8
    Write-Host "Published plugin metadata written to: $OutputMetadataPath" -ForegroundColor Gray
}

return @($publishedPlugin)
