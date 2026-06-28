param(
    [string]$PluginName = "All",
    [Parameter(Mandatory=$true)]
    [string]$Repository,
    [switch]$DryRun,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$PluginName = $PluginName.Trim()
$Repository = $Repository.Trim()

. "$PSScriptRoot/common/Plugin-Config.ps1"

$script:MaxReleasesToFetch = 1000
$script:BatchWindowHours = 1

function ConvertFrom-Iso8601Date {
    param([object]$DateInput)

    if ($DateInput -is [DateTime]) {
        return $DateInput.ToUniversalTime()
    }

    if ([string]::IsNullOrWhiteSpace($DateInput)) {
        return [DateTime]::UtcNow
    }

    try {
        return [DateTime]::ParseExact(
            $DateInput,
            "yyyy-MM-ddTHH:mm:ssZ",
            [System.Globalization.CultureInfo]::InvariantCulture,
            [System.Globalization.DateTimeStyles]::AssumeUniversal -bor [System.Globalization.DateTimeStyles]::AdjustToUniversal)
    }
    catch {
        return [DateTime]::Parse($DateInput, [System.Globalization.CultureInfo]::InvariantCulture).ToUniversalTime()
    }
}

function Get-LatestReleaseForPlugin {
    param([array]$Releases)

    if ($Releases.Count -eq 0) {
        return $null
    }

    $latestVersion = Get-MusoqLatestVersion -Versions @($Releases | ForEach-Object { $_.Version })
    return @($Releases | Where-Object { $_.Version -eq $latestVersion })[0]
}

if (-not (Test-ValidRepository -Repository $Repository)) {
    Write-Error "Invalid repository format: $Repository. Expected 'owner/repo' format."
    exit 1
}

if ($PluginName -ne "All" -and -not (Test-ValidPluginName -Name $PluginName)) {
    Write-Error "Invalid plugin name format: $PluginName"
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   Plugin Rollback Tool" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if ($DryRun) {
    Write-Host "[DRY RUN MODE] No changes will be made." -ForegroundColor Yellow
    Write-Host ""
}

Write-Host "Fetching releases from $Repository..." -ForegroundColor Cyan
$releasesJson = gh release list --repo $Repository --limit $script:MaxReleasesToFetch --json tagName,createdAt 2>$null
if ($LASTEXITCODE -ne 0 -or -not $releasesJson) {
    Write-Error "Failed to fetch releases from repository"
    exit 1
}

$registryTag = $script:RegistryReleaseTag
$releases = $releasesJson | ConvertFrom-Json
$pluginVersionsMap = @{}

foreach ($release in $releases) {
    $tag = $release.tagName
    if ($tag -eq $registryTag) {
        continue
    }

    $parsedTag = ConvertFrom-MusoqPluginReleaseTag -ReleaseTag $tag
    if (-not $parsedTag) {
        continue
    }

    if (-not $pluginVersionsMap.ContainsKey($parsedTag.PluginName)) {
        $pluginVersionsMap[$parsedTag.PluginName] = @()
    }

    $pluginVersionsMap[$parsedTag.PluginName] += [PSCustomObject]@{
        PluginName = $parsedTag.PluginName
        Version = $parsedTag.Version
        ReleaseTag = $tag
        CreatedAt = $release.createdAt
    }
}

foreach ($name in @($pluginVersionsMap.Keys)) {
    $sortedVersions = Sort-MusoqVersions -Versions @($pluginVersionsMap[$name] | ForEach-Object { $_.Version }) -Descending
    $sortedReleases = @()
    foreach ($version in $sortedVersions) {
        $sortedReleases += @($pluginVersionsMap[$name] | Where-Object { $_.Version -eq $version })[0]
    }
    $pluginVersionsMap[$name] = $sortedReleases
}

$pluginsToRollback = @()

if ($PluginName -eq "All") {
    $allLatestReleases = @()
    foreach ($name in @($pluginVersionsMap.Keys)) {
        $latest = Get-LatestReleaseForPlugin -Releases $pluginVersionsMap[$name]
        if ($latest) {
            $allLatestReleases += $latest
        }
    }

    if ($allLatestReleases.Count -eq 0) {
        Write-Host "No plugin releases found." -ForegroundColor Yellow
        exit 0
    }

    $sortedByDate = $allLatestReleases | Sort-Object { ConvertFrom-Iso8601Date $_.CreatedAt } -Descending
    $latestReleaseDate = ConvertFrom-Iso8601Date $sortedByDate[0].CreatedAt
    $batchWindow = $latestReleaseDate.AddHours(-$script:BatchWindowHours)

    foreach ($release in $sortedByDate) {
        if ((ConvertFrom-Iso8601Date $release.CreatedAt) -ge $batchWindow) {
            $pluginsToRollback += $release
        }
    }
} else {
    if (-not $pluginVersionsMap.ContainsKey($PluginName)) {
        Write-Host "No releases found for plugin: $PluginName" -ForegroundColor Yellow
        exit 0
    }

    $latestVersion = Get-LatestReleaseForPlugin -Releases $pluginVersionsMap[$PluginName]
    if (-not $latestVersion) {
        Write-Host "No valid releases found for plugin: $PluginName" -ForegroundColor Yellow
        exit 0
    }

    $pluginsToRollback += $latestVersion
}

if ($pluginsToRollback.Count -eq 0) {
    Write-Host "No plugins to rollback." -ForegroundColor Yellow
    exit 0
}

Write-Host ""
Write-Host "The following releases will be deleted:" -ForegroundColor Yellow
Write-Host ""
foreach ($release in $pluginsToRollback) {
    $previousVersion = "none"
    $remaining = @($pluginVersionsMap[$release.PluginName] | Where-Object { $_.Version -ne $release.Version })
    if ($remaining.Count -gt 0) {
        $previous = Get-LatestReleaseForPlugin -Releases $remaining
        if ($previous) {
            $previousVersion = $previous.Version
        }
    }

    Write-Host "  - $($release.PluginName) v$($release.Version) (tag: $($release.ReleaseTag))" -ForegroundColor White
    Write-Host "    Previous version: $previousVersion" -ForegroundColor Gray
}
Write-Host ""

if (-not $DryRun -and -not $Force) {
    $confirmation = Read-Host "Are you sure you want to delete these releases? (yes/no)"
    if ($confirmation -ne "yes") {
        Write-Host "Rollback cancelled." -ForegroundColor Yellow
        exit 0
    }
}

$deletedCount = 0
$failedCount = 0

foreach ($release in @($pluginsToRollback)) {
    Write-Host "Deleting release: $($release.ReleaseTag)..." -ForegroundColor Cyan

    if ($DryRun) {
        Write-Host "  [DRY RUN] Would delete release $($release.ReleaseTag)" -ForegroundColor Yellow
        $deletedCount++
        continue
    }

    gh release delete $release.ReleaseTag --repo $Repository --yes 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  Deleted release $($release.ReleaseTag)" -ForegroundColor Green
        $deletedCount++
    } else {
        Write-Host "  Failed to delete release $($release.ReleaseTag)" -ForegroundColor Red
        $failedCount++
    }
}

if (-not $DryRun -and $deletedCount -gt 0) {
    Write-Host ""
    Write-Host "Rebuilding plugin registry from remaining releases..." -ForegroundColor Cyan
    & "$PSScriptRoot/Update-PluginRegistry.ps1" -Repository $Repository -RegenerateFromReleases
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  Failed to rebuild registry" -ForegroundColor Red
        $failedCount++
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   Rollback Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Deleted: $deletedCount release(s)" -ForegroundColor $(if ($deletedCount -gt 0) { "Green" } else { "Yellow" })
Write-Host "  Failed:  $failedCount" -ForegroundColor $(if ($failedCount -gt 0) { "Red" } else { "Green" })
Write-Host ""

if ($DryRun) {
    Write-Host "[DRY RUN] No actual changes were made." -ForegroundColor Yellow
    Write-Host "Run without -DryRun to perform the rollback." -ForegroundColor Yellow
}

if ($failedCount -gt 0) {
    exit 1
}
