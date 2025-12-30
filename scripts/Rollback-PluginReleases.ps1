param(
    [string]$PluginName = "All",
    [Parameter(Mandatory=$true)]
    [string]$Repository,
    [switch]$DryRun,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

# Trim whitespace from string parameters to handle accidental spaces
$PluginName = $PluginName.Trim()
$Repository = $Repository.Trim()

. "$PSScriptRoot/common/Plugin-Config.ps1"

$script:MaxReleasesToFetch = 1000
$script:BatchWindowHours = 1

<#
.SYNOPSIS
    Rollback plugin releases by deleting the latest version and updating the registry.

.DESCRIPTION
    This script performs the following:
    1. Identifies the latest released version(s) for the specified plugin(s)
    2. Deletes the GitHub release(s)
    3. Updates the plugin registry to remove the deleted version(s) and set the previous version as latest
    
    Use -DryRun to see what would be deleted without making changes.
    Use -Force to skip confirmation prompts.

.PARAMETER PluginName
    The name of a specific plugin to rollback (e.g., "Musoq.DataSources.Git"), 
    or "All" to rollback the latest version of all plugins.

.PARAMETER Repository
    The GitHub repository in "owner/repo" format.

.PARAMETER DryRun
    If specified, shows what would be deleted without making actual changes.

.PARAMETER Force
    If specified, skips confirmation prompts.
#>

<#
.SYNOPSIS
    Converts an ISO 8601 date string or DateTime object to a DateTime in UTC.

.PARAMETER DateInput
    The date as either a string in ISO 8601 format (e.g., "2025-12-26T15:30:00Z") or a DateTime object.
    If a DateTime object is provided and not in UTC, it will be converted to UTC.

.OUTPUTS
    A DateTime object in UTC.
#>
function ConvertFrom-Iso8601Date {
    param([object]$DateInput)
    
    # If already a DateTime, ensure it's in UTC and return
    if ($DateInput -is [DateTime]) {
        if ($DateInput.Kind -eq [System.DateTimeKind]::Utc) {
            return $DateInput
        }
        return $DateInput.ToUniversalTime()
    }
    
    # If null or empty string, return current UTC time
    if ([string]::IsNullOrWhiteSpace($DateInput)) {
        return [DateTime]::UtcNow
    }
    
    # Parse string date
    try {
        return [DateTime]::ParseExact(
            $DateInput, 
            "yyyy-MM-ddTHH:mm:ssZ", 
            [System.Globalization.CultureInfo]::InvariantCulture,
            [System.Globalization.DateTimeStyles]::AssumeUniversal -bor [System.Globalization.DateTimeStyles]::AdjustToUniversal
        )
    } catch {
        return [DateTime]::Parse($DateInput, [System.Globalization.CultureInfo]::InvariantCulture)
    }
}

if (-not (Test-ValidRepository -Repository $Repository)) {
    Write-Error "Invalid repository format: $Repository. Expected 'owner/repo' format."
    exit 1
}

if ($PluginName -ne "All") {
    if (-not (Test-ValidPluginName -Name $PluginName)) {
        Write-Error "Invalid plugin name format: $PluginName"
        exit 1
    }
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
$ReleasesJson = gh release list --repo $Repository --limit $script:MaxReleasesToFetch --json tagName,createdAt 2>$null
if ($LASTEXITCODE -ne 0 -or -not $ReleasesJson) {
    Write-Error "Failed to fetch releases from repository"
    exit 1
}

$Releases = $ReleasesJson | ConvertFrom-Json

$RegistryTag = $script:RegistryReleaseTag
$RegistryFile = $script:RegistryFileName

$PluginVersionsMap = @{}

foreach ($Release in $Releases) {
    $Tag = $Release.tagName
    
    if ($Tag -eq $RegistryTag) {
        continue
    }
    
    if ($Tag -notmatch '^(\d+\.\d+\.\d+(-[\w\d]+)?)-(.+)$') {
        continue
    }
    
    # Capture match results immediately to prevent them from being overwritten
    # by subsequent regex operations in validation functions (Test-ValidPluginName
    # and Test-ValidVersion both use -match/-notmatch which modify $Matches)
    $Version = $Matches[1]
    $ParsedPluginName = $Matches[3]
    
    if (-not (Test-ValidPluginName -Name $ParsedPluginName)) {
        continue
    }
    
    if (-not (Test-ValidVersion -Version $Version)) {
        continue
    }
    
    if (-not $PluginVersionsMap.ContainsKey($ParsedPluginName)) {
        $PluginVersionsMap[$ParsedPluginName] = @()
    }
    
    $PluginVersionsMap[$ParsedPluginName] += @{
        Version = $Version
        ReleaseTag = $Tag
        CreatedAt = $Release.createdAt
    }
}

foreach ($Name in @($PluginVersionsMap.Keys)) {
    $PluginVersionsMap[$Name] = $PluginVersionsMap[$Name] | Sort-Object { 
        try {
            [version]($_.Version -replace '-.*$', '')
        } catch {
            [version]"0.0.0"
        }
    } -Descending
}

$PluginsToRollback = @()

if ($PluginName -eq "All") {
    $AllVersions = @()
    foreach ($Name in @($PluginVersionsMap.Keys)) {
        foreach ($v in $PluginVersionsMap[$Name]) {
            $AllVersions += @{
                PluginName = $Name
                Version = $v.Version
                ReleaseTag = $v.ReleaseTag
                CreatedAt = $v.CreatedAt
            }
        }
    }
    
    if ($AllVersions.Count -eq 0) {
        Write-Host "No plugin releases found." -ForegroundColor Yellow
        exit 0
    }
    
    $SortedVersions = $AllVersions | Sort-Object { ConvertFrom-Iso8601Date $_.CreatedAt } -Descending
    $LatestReleaseDate = ConvertFrom-Iso8601Date $SortedVersions[0].CreatedAt
    
    $BatchWindow = $LatestReleaseDate.AddHours(-$script:BatchWindowHours)
    
    foreach ($v in $SortedVersions) {
        $ReleaseDate = ConvertFrom-Iso8601Date $v.CreatedAt
        if ($ReleaseDate -ge $BatchWindow) {
            $LatestForPlugin = $PluginVersionsMap[$v.PluginName][0]
            if ($LatestForPlugin.ReleaseTag -eq $v.ReleaseTag) {
                $PluginsToRollback += $v
            }
        }
    }
} else {
    if (-not $PluginVersionsMap.ContainsKey($PluginName)) {
        Write-Host "No releases found for plugin: $PluginName" -ForegroundColor Yellow
        Write-Host "Available plugins in map: $($PluginVersionsMap.Keys -join ', ')" -ForegroundColor Gray
        exit 0
    }
    
    $PluginVersions = $PluginVersionsMap[$PluginName]
    Write-Host "Found plugin in map with $($PluginVersions.Count) version(s)" -ForegroundColor Gray
    
    # Debug: Show all versions in the array
    for ($i = 0; $i -lt [Math]::Min($PluginVersions.Count, 5); $i++) {
        Write-Host "  [$i] Version='$($PluginVersions[$i].Version)', Tag='$($PluginVersions[$i].ReleaseTag)'" -ForegroundColor Gray
    }
    
    # Check for null, empty array, or null first element
    # PowerShell returns 0 for .Count on null and $null for out-of-bounds array access
    if ($null -eq $PluginVersions -or $PluginVersions.Count -eq 0 -or $null -eq $PluginVersions[0]) {
        Write-Host "No valid releases found for plugin: $PluginName" -ForegroundColor Yellow
        if ($PluginVersions.Count -gt 0) {
            Write-Host "First element details: Version='$($PluginVersions[0].Version)', Tag='$($PluginVersions[0].ReleaseTag)'" -ForegroundColor Gray
        }
        exit 0
    }
    
    # Additional check: verify the first element has valid Version and ReleaseTag
    if ([string]::IsNullOrWhiteSpace($PluginVersions[0].Version) -or [string]::IsNullOrWhiteSpace($PluginVersions[0].ReleaseTag)) {
        Write-Host "No valid releases found for plugin: $PluginName (first release has empty version or tag)" -ForegroundColor Yellow
        exit 0
    }
    
    $LatestVersion = $PluginVersions[0]
    $PluginsToRollback += @{
        PluginName = $PluginName
        Version = $LatestVersion.Version
        ReleaseTag = $LatestVersion.ReleaseTag
        CreatedAt = $LatestVersion.CreatedAt
    }
}

if ($PluginsToRollback.Count -eq 0) {
    Write-Host "No plugins to rollback." -ForegroundColor Yellow
    exit 0
}

Write-Host ""
Write-Host "The following releases will be deleted:" -ForegroundColor Yellow
Write-Host ""
foreach ($p in $PluginsToRollback) {
    $PreviousVersion = "none"
    if ($PluginVersionsMap[$p.PluginName].Count -gt 1) {
        $PreviousVersion = $PluginVersionsMap[$p.PluginName][1].Version
    }
    Write-Host "  - $($p.PluginName) v$($p.Version) (tag: $($p.ReleaseTag))" -ForegroundColor White
    Write-Host "    Previous version: $PreviousVersion" -ForegroundColor Gray
}
Write-Host ""

if (-not $DryRun -and -not $Force) {
    $Confirmation = Read-Host "Are you sure you want to delete these releases? (yes/no)"
    if ($Confirmation -ne "yes") {
        Write-Host "Rollback cancelled." -ForegroundColor Yellow
        exit 0
    }
}

$TempDir = Join-Path ([System.IO.Path]::GetTempPath()) "plugin-rollback-$(Get-Random)"
New-Item -ItemType Directory -Path $TempDir -Force | Out-Null

try {
    $LocalRegistryPath = Join-Path $TempDir $RegistryFile
    
    $RegistryReleaseExists = $false
    gh release view $RegistryTag --repo $Repository 1>$null 2>$null
    if ($LASTEXITCODE -eq 0) { $RegistryReleaseExists = $true }
    
    $Registry = $null
    
    if ($RegistryReleaseExists) {
        Write-Host "Downloading plugin registry..." -ForegroundColor Cyan
        
        Push-Location $TempDir
        gh release download $RegistryTag --pattern $RegistryFile --repo $Repository 2>$null
        Pop-Location
        
        if (Test-Path $LocalRegistryPath) {
            try {
                $RegistryContent = Get-Content $LocalRegistryPath -Raw
                $Registry = $RegistryContent | ConvertFrom-Json -AsHashtable
                Write-Host "  Loaded registry with $($Registry.plugins.Count) plugin(s)" -ForegroundColor Gray
            } catch {
                Write-Warning "Failed to parse registry: $($_.Exception.Message)"
                $Registry = $null
            }
        }
    }
    
    if (-not $Registry) {
        Write-Host "No existing registry found. Will skip registry update." -ForegroundColor Yellow
        $Registry = @{
            schemaVersion = "1.0"
            lastUpdated = ""
            repository = "https://github.com/$Repository"
            plugins = @()
            versionHistory = @{}
        }
    }
    
    $DeletedCount = 0
    $FailedCount = 0
    
    foreach ($p in @($PluginsToRollback)) {
        Write-Host "Deleting release: $($p.ReleaseTag)..." -ForegroundColor Cyan
        
        if ($DryRun) {
            Write-Host "  [DRY RUN] Would delete release $($p.ReleaseTag)" -ForegroundColor Yellow
            $DeletedCount++
        } else {
            gh release delete $p.ReleaseTag --repo $Repository --yes 2>$null
            if ($LASTEXITCODE -eq 0) {
                Write-Host "  Deleted release $($p.ReleaseTag)" -ForegroundColor Green
                $DeletedCount++
            } else {
                Write-Host "  Failed to delete release $($p.ReleaseTag)" -ForegroundColor Red
                $FailedCount++
            }
        }
        
        $PluginName = $p.PluginName
        $DeletedVersion = $p.Version
        
        # Skip version history update if version is null or empty
        if ([string]::IsNullOrWhiteSpace($DeletedVersion)) {
            Write-Warning "Skipping version history update for ${PluginName}: version is null or empty"
            continue
        }
        
        if ($Registry.versionHistory -and $Registry.versionHistory.ContainsKey($PluginName)) {
            if ($Registry.versionHistory[$PluginName].ContainsKey($DeletedVersion)) {
                if ($DryRun) {
                    Write-Host "  [DRY RUN] Would remove v$DeletedVersion from version history" -ForegroundColor Yellow
                } else {
                    $Registry.versionHistory[$PluginName].Remove($DeletedVersion)
                    Write-Host "  Removed v$DeletedVersion from version history" -ForegroundColor Gray
                }
            }
        }
        
        $PluginEntry = $Registry.plugins | Where-Object { $_.name -eq $PluginName }
        if ($PluginEntry) {
            if ($PluginEntry.latestVersion -eq $DeletedVersion) {
                $PreviousVersions = @()
                if ($PluginVersionsMap.ContainsKey($PluginName)) {
                    $PreviousVersions = $PluginVersionsMap[$PluginName] | Where-Object { $_.Version -ne $DeletedVersion }
                }
                
                if ($PreviousVersions.Count -gt 0) {
                    $NewLatest = $PreviousVersions[0]
                    if ($DryRun) {
                        Write-Host "  [DRY RUN] Would update $PluginName to v$($NewLatest.Version)" -ForegroundColor Yellow
                    } else {
                        $PluginEntry.latestVersion = $NewLatest.Version
                        $PluginEntry.releaseTag = $NewLatest.ReleaseTag
                        
                        if ($Registry.versionHistory -and 
                            $Registry.versionHistory.ContainsKey($PluginName) -and
                            $Registry.versionHistory[$PluginName].ContainsKey($NewLatest.Version)) {
                            $PluginEntry.releaseDate = $Registry.versionHistory[$PluginName][$NewLatest.Version].releaseDate
                        } else {
                            $PluginEntry.releaseDate = $NewLatest.CreatedAt
                        }
                        
                        Write-Host "  Updated $PluginName to v$($NewLatest.Version)" -ForegroundColor Gray
                    }
                } else {
                    if ($DryRun) {
                        Write-Host "  [DRY RUN] Would remove $PluginName from registry (no previous versions)" -ForegroundColor Yellow
                    } else {
                        $Registry.plugins = $Registry.plugins | Where-Object { $_.name -ne $PluginName }
                        Write-Host "  Removed $PluginName from registry (no previous versions)" -ForegroundColor Gray
                    }
                    
                    if ($Registry.versionHistory -and $Registry.versionHistory.ContainsKey($PluginName)) {
                        if (-not $DryRun) {
                            $Registry.versionHistory.Remove($PluginName)
                        }
                    }
                }
            }
        }
    }
    
    if (-not $DryRun -and $RegistryReleaseExists -and $DeletedCount -gt 0) {
        $Registry.lastUpdated = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", [System.Globalization.CultureInfo]::InvariantCulture)
        
        $RegistryJson = $Registry | ConvertTo-Json -Depth 10
        $RegistryJson | Set-Content -Path $LocalRegistryPath -Encoding UTF8
        
        Write-Host ""
        Write-Host "Uploading updated registry..." -ForegroundColor Cyan
        
        gh release upload $RegistryTag $LocalRegistryPath --clobber --repo $Repository
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  Registry updated successfully" -ForegroundColor Green
        } else {
            Write-Host "  Failed to update registry" -ForegroundColor Red
            $FailedCount++
        }
    }
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "   Rollback Summary" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  Deleted: $DeletedCount release(s)" -ForegroundColor $(if ($DeletedCount -gt 0) { "Green" } else { "Yellow" })
    Write-Host "  Failed:  $FailedCount" -ForegroundColor $(if ($FailedCount -gt 0) { "Red" } else { "Green" })
    Write-Host ""
    
    if ($DryRun) {
        Write-Host "[DRY RUN] No actual changes were made." -ForegroundColor Yellow
        Write-Host "Run without -DryRun to perform the rollback." -ForegroundColor Yellow
    }
    
    if ($FailedCount -gt 0) {
        exit 1
    }
}
finally {
    if (Test-Path $TempDir) {
        Remove-Item $TempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
