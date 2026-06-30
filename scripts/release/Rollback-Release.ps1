[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$Tag,
    [Parameter(Mandatory=$true)]
    [string]$Repository,
    [switch]$Apply,
    [switch]$SkipRegistryUpdate
)

. (Join-Path $PSScriptRoot "Release.Common.ps1")

if (-not (Test-ValidRepository -Repository $Repository)) {
    throw "Invalid repository format: $Repository. Expected 'owner/repo'."
}

if ([string]::IsNullOrWhiteSpace($Tag) -or $Tag -ne $Tag.Trim()) {
    throw "Invalid release tag format."
}

if ($Tag -match '[\s;&|`$<>(){}\[\]''"\\/]') {
    throw "Invalid release tag format."
}

$parsedTag = ConvertFrom-MusoqPluginReleaseTag -ReleaseTag $Tag
if (-not $parsedTag) {
    throw "Invalid release tag format. Expected '{semver}-Musoq.DataSources.{Name}'."
}

$package = Get-ReleasePackageByPackageId -PackageId $parsedTag.PluginName
$dryRun = -not $Apply.IsPresent

Write-Host "Rollback tag validated: $Tag"
Write-Host "Package: $($package.PackageId)"
Write-Host "Version: $($parsedTag.Version)"
Write-Host "Channel: $($parsedTag.Channel)"

$releaseJson = gh release view $Tag --repo $Repository --json tagName,createdAt,isPrerelease 2>$null
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($releaseJson)) {
    throw "GitHub release '$Tag' was not found in '$Repository'."
}

$release = $releaseJson | ConvertFrom-Json
Write-Host "Release found: $($release.tagName) created at $($release.createdAt)"

if ($dryRun) {
    Write-Host "[DRY RUN] Would delete GitHub release '$Tag' and regenerate plugin registry." -ForegroundColor Yellow
    Write-Host "Run with -Apply to perform the rollback." -ForegroundColor Yellow
    return
}

Write-Host "Deleting GitHub release '$Tag'..." -ForegroundColor Cyan
gh release delete $Tag --repo $Repository --yes
if ($LASTEXITCODE -ne 0) {
    throw "Failed to delete GitHub release '$Tag'."
}

if ($SkipRegistryUpdate) {
    Write-Host "Registry regeneration skipped." -ForegroundColor Yellow
    return
}

Write-Host "Regenerating plugin registry from remaining releases..." -ForegroundColor Cyan
& (Join-Path (Split-Path -Parent $PSScriptRoot) "Update-PluginRegistry.ps1") -Repository $Repository -RegenerateFromReleases
if ($LASTEXITCODE -ne 0) {
    throw "Failed to regenerate plugin registry."
}

Write-Host "Rollback complete for '$Tag'." -ForegroundColor Green
