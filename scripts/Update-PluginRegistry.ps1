param(
    [Parameter(Mandatory=$true)]
    [string]$Repository,
    [string]$PublishedMetadataPath = "",
    [array]$PublishedPlugins = @(),
    [switch]$RegenerateFromReleases
)

$ErrorActionPreference = "Stop"

. "$PSScriptRoot/common/Plugin-Config.ps1"

<#
.SYNOPSIS
    Converts a value to ISO 8601 date string format.

.DESCRIPTION
    Normalizes date values to ISO 8601 format (yyyy-MM-ddTHH:mm:ssZ).
    PowerShell's ConvertFrom-Json automatically converts ISO 8601 date strings to DateTime objects.
    This function converts DateTime objects back to the expected string format using InvariantCulture
    to ensure consistent validation regardless of the system's culture settings.

.PARAMETER Value
    The value to convert. Can be a DateTime object, string, or null.

.OUTPUTS
    String in ISO 8601 format (yyyy-MM-ddTHH:mm:ssZ) if input is a DateTime object,
    the original value if it's already a string, or null if input is null.
#>
function ConvertTo-Iso8601String {
    param([object]$Value)
    
    if ($null -eq $Value) {
        return $null
    }
    
    if ($Value -is [DateTime]) {
        return $Value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", [System.Globalization.CultureInfo]::InvariantCulture)
    }
    
    return $Value
}

<#
.SYNOPSIS
    Strips the pre-release suffix from a version string for comparison.

.DESCRIPTION
    Removes any pre-release suffix (everything after the first hyphen) from a version string
    to enable proper version comparison using PowerShell's [version] type.

.PARAMETER Version
    The version string that may contain a pre-release suffix (e.g., "5.3.0-beta").

.OUTPUTS
    String with pre-release suffix removed (e.g., "5.3.0").
#>
function Get-BaseVersion {
    param([string]$Version)
    
    if ([string]::IsNullOrWhiteSpace($Version)) {
        return $null
    }
    
    return $Version -replace '-.*$', ''
}

<#
.SYNOPSIS
    Updates the version history for a plugin.

.DESCRIPTION
    Adds or updates the version history entry for a specific plugin version.

.PARAMETER Registry
    The registry hashtable to update.

.PARAMETER PluginName
    The name of the plugin.

.PARAMETER Version
    The version string.

.PARAMETER ReleaseTag
    The release tag for this version.

.PARAMETER ReleaseDate
    The release date for this version.
#>
function Update-PluginVersionHistory {
    param(
        [hashtable]$Registry,
        [string]$PluginName,
        [string]$Version,
        [string]$ReleaseTag,
        [string]$ReleaseDate
    )
    
    if (-not $Registry.versionHistory.ContainsKey($PluginName)) {
        $Registry.versionHistory[$PluginName] = @{}
    }
    
    $Registry.versionHistory[$PluginName][$Version] = @{
        releaseTag = $ReleaseTag
        releaseDate = $ReleaseDate
    }
}

if (-not (Test-ValidRepository -Repository $Repository)) {
    Write-Error "Invalid repository format: $Repository. Expected 'owner/repo' format."
    exit 1
}

if ($PublishedMetadataPath) {
    if ($PublishedMetadataPath -match '\.\.[/\\]') {
        Write-Error "Invalid metadata path: path traversal not allowed"
        exit 1
    }
    
    if (Test-Path $PublishedMetadataPath) {
        try {
            $RawContent = Get-Content $PublishedMetadataPath -Raw
            
            if ($RawContent.Length -gt 10MB) {
                Write-Error "Metadata file too large (max 10MB)"
                exit 1
            }
            
            $PublishedPlugins = $RawContent | ConvertFrom-Json
            
            # Normalize ReleaseDate fields that may have been auto-converted to DateTime objects
            foreach ($plugin in $PublishedPlugins) {
                if ($plugin.ReleaseDate) {
                    $plugin.ReleaseDate = ConvertTo-Iso8601String -Value $plugin.ReleaseDate
                }
            }
        }
        catch {
            Write-Error "Failed to parse metadata file: $_"
            exit 1
        }
    }
}

function Test-ValidPluginData {
    param([object]$Plugin)
    
    $errors = @()
    
    if (-not $Plugin.Name) {
        $errors += "Missing Name"
    } elseif (-not (Test-ValidPluginName -Name $Plugin.Name)) {
        $errors += "Invalid Name format: $($Plugin.Name)"
    }
    
    if (-not $Plugin.Version) {
        $errors += "Missing Version"
    } elseif (-not (Test-ValidVersion -Version $Plugin.Version)) {
        $errors += "Invalid Version format: $($Plugin.Version)"
    }
    
    if (-not $Plugin.ShortName) {
        $errors += "Missing ShortName"
    } elseif (-not (Test-ValidShortName -ShortName $Plugin.ShortName)) {
        $errors += "Invalid ShortName format: $($Plugin.ShortName)"
    }
    
    if (-not $Plugin.ReleaseTag) {
        $errors += "Missing ReleaseTag"
    } elseif ($Plugin.ReleaseTag.Length -gt 200 -or $Plugin.ReleaseTag -match '[<>|&;`$\s]') {
        $errors += "Invalid ReleaseTag format"
    }
    
    if (-not $Plugin.ReleaseDate) {
        $errors += "Missing ReleaseDate"
    } else {
        $releaseDateStr = ConvertTo-Iso8601String -Value $Plugin.ReleaseDate
        if ($releaseDateStr -notmatch '^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$') {
            $errors += "Invalid ReleaseDate format (expected ISO 8601)"
        }
    }
    
    if ($Plugin.Description -and -not (Test-ValidDescription -Description $Plugin.Description)) {
        $errors += "Invalid Description"
    }
    
    if ($Plugin.Tags -and -not (Test-ValidTags -Tags $Plugin.Tags)) {
        $errors += "Invalid Tags"
    }
    
    if ($Plugin.Artifacts) {
        foreach ($key in $Plugin.Artifacts.Keys) {
            if ($key -notmatch '^(windows|linux|macos|alpine)-(x64|arm64)$') {
                $errors += "Invalid artifact platform key: $key"
            }
            $artifactName = $Plugin.Artifacts[$key]
            if ($artifactName -notmatch '^Musoq\.DataSources\.[A-Za-z0-9]+-(windows|linux|macos|alpine)-(x64|arm64)\.zip$') {
                $errors += "Invalid artifact name: $artifactName"
            }
        }
    }
    
    return @{
        IsValid = ($errors.Count -eq 0)
        Errors = $errors
    }
}

function Get-PluginDataFromRelease {
    param(
        [string]$ReleaseTag,
        [string]$Repository
    )
    
    if ($ReleaseTag -notmatch '^(\d+\.\d+\.\d+(-[\w\d]+)?)-(.+)$') {
        return $null
    }
    
    $Version = $Matches[1]
    $PluginName = $Matches[3]
    
    if (-not (Test-ValidPluginName -Name $PluginName)) {
        return $null
    }
    
    if (-not (Test-ValidVersion -Version $Version)) {
        return $null
    }
    
    $Projects = Get-PluginProjects -PluginName $PluginName
    if ($Projects.Count -eq 0) {
        return $null
    }
    
    try {
        $Metadata = Get-ProjectMetadata -Project $Projects[0]
    } catch {
        return $null
    }
    
    $ReleaseInfo = gh release view $ReleaseTag --repo $Repository --json createdAt 2>$null | ConvertFrom-Json
    $ReleaseDate = if ($ReleaseInfo.createdAt) { 
        [DateTime]::Parse($ReleaseInfo.createdAt).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", [System.Globalization.CultureInfo]::InvariantCulture) 
    } else { 
        (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", [System.Globalization.CultureInfo]::InvariantCulture) 
    }
    
    return @{
        Name = $PluginName
        ShortName = $Metadata.ShortName
        Description = $Metadata.Description
        Tags = $Metadata.Tags
        Version = $Version
        ReleaseTag = $ReleaseTag
        ReleaseDate = $ReleaseDate
        Artifacts = Get-ArtifactNames -ProjectName $PluginName
    }
}

$ValidatedPlugins = @()
foreach ($Plugin in $PublishedPlugins) {
    $validation = Test-ValidPluginData -Plugin $Plugin
    if ($validation.IsValid) {
        $ValidatedPlugins += $Plugin
    } else {
        Write-Warning "Skipping invalid plugin data for '$($Plugin.Name)': $($validation.Errors -join ', ')"
    }
}
$PublishedPlugins = $ValidatedPlugins

$HasNewPlugins = $PublishedPlugins.Count -gt 0

if ($HasNewPlugins) {
    Write-Host "Updating plugin registry with $($PublishedPlugins.Count) new plugin(s)..." -ForegroundColor Cyan
} else {
    Write-Host "No new plugins to add. Checking if registry needs to be created or updated..." -ForegroundColor Cyan
}

$RegistryTag = $script:RegistryReleaseTag
$RegistryFile = $script:RegistryFileName
$TempDir = Join-Path ([System.IO.Path]::GetTempPath()) "plugin-registry-$(Get-Random)"

try {
    New-Item -ItemType Directory -Path $TempDir -Force | Out-Null
    $LocalRegistryPath = Join-Path $TempDir $RegistryFile
    
    $RegistryReleaseExists = $false
    gh release view $RegistryTag --repo $Repository 1>$null 2>$null
    if ($LASTEXITCODE -eq 0) { $RegistryReleaseExists = $true }
    
    $Registry = $null
    
    if ($RegistryReleaseExists) {
        Write-Host "  Downloading existing registry..." -ForegroundColor Gray
        
        Push-Location $TempDir
        gh release download $RegistryTag --pattern $RegistryFile --repo $Repository 2>$null
        Pop-Location
        
        if (Test-Path $LocalRegistryPath) {
            $DownloadedFileSize = (Get-Item $LocalRegistryPath).Length
            $MaxRegistryDownloadSize = 50MB
            
            if ($DownloadedFileSize -gt $MaxRegistryDownloadSize) {
                Write-Warning "Downloaded registry file is suspiciously large ($([math]::Round($DownloadedFileSize / 1MB, 2)) MB). Creating fresh registry."
            } elseif ($DownloadedFileSize -eq 0) {
                Write-Warning "Downloaded registry file is empty. Creating fresh registry."
            } else {
                try {
                    $RegistryContent = Get-Content $LocalRegistryPath -Raw
                    $Registry = $RegistryContent | ConvertFrom-Json -AsHashtable
                    Write-Host "  Loaded existing registry with $($Registry.plugins.Count) plugin(s)" -ForegroundColor Gray
                } catch {
                    Write-Warning "Failed to parse existing registry JSON: $($_.Exception.Message). Creating fresh registry."
                    $Registry = $null
                }
            }
        }
    }
    
    $NeedToCreateRegistry = (-not $Registry) -or (-not $RegistryReleaseExists)
    
    # Always ensure registry is up-to-date by scanning releases when no new plugins are provided
    # This ensures the registry stays synchronized with actual releases even when publish-plugins
    # doesn't create new releases (e.g., they already exist)
    if (-not $HasNewPlugins -and -not $RegenerateFromReleases) {
        Write-Host "No new plugins provided. Will scan existing releases to ensure registry is up-to-date..." -ForegroundColor Cyan
        $RegenerateFromReleases = $true
    }
    
    if (-not $Registry) {
        Write-Host "  Creating new registry..." -ForegroundColor Gray
        $Registry = @{
            schemaVersion = "1.0"
            lastUpdated = ""
            repository = "https://github.com/$Repository"
            plugins = @()
            versionHistory = @{}
        }
    } else {
        Write-Host "  Validating registry structure..." -ForegroundColor Gray
        
        $RegistryValid = $true
        
        if (-not $Registry.ContainsKey('schemaVersion')) {
            Write-Warning "Registry missing 'schemaVersion' field, will reset"
            $RegistryValid = $false
        }
        if (-not $Registry.ContainsKey('plugins')) {
            Write-Warning "Registry missing 'plugins' field, will reset"
            $RegistryValid = $false
        }
        
        if ($RegistryValid -and $Registry.schemaVersion -and -not ($Registry.schemaVersion -match '^\d+\.\d+$')) {
            Write-Warning "Registry has invalid schemaVersion format: '$($Registry.schemaVersion)', will reset"
            $RegistryValid = $false
        }
        
        if ($RegistryValid -and $Registry.repository) {
            if (-not ($Registry.repository -match '^https://github\.com/[a-zA-Z0-9\-_]+/[a-zA-Z0-9\.\-_]+$')) {
                Write-Warning "Registry has invalid repository URL format, will reset"
                $RegistryValid = $false
            }
        }
        
        if (-not $RegistryValid) {
            Write-Host "  Registry structure invalid, creating fresh registry..." -ForegroundColor Yellow
            $Registry = @{
                schemaVersion = "1.0"
                lastUpdated = ""
                repository = "https://github.com/$Repository"
                plugins = @()
                versionHistory = @{}
            }
        }
    }
    
    if ($Registry.plugins -is [hashtable]) {
        Write-Host "  Migrating from legacy format..." -ForegroundColor Gray
        $Registry.plugins = @()
        $Registry.versionHistory = @{}
    }
    
    if (-not $Registry.versionHistory) {
        $Registry.versionHistory = @{}
    }
    
    if ($Registry.plugins -and $Registry.plugins.Count -gt 0) {
        Write-Host "  Validating $($Registry.plugins.Count) existing plugin(s) in registry..." -ForegroundColor Gray
        $ValidExistingPlugins = @()
        $InvalidCount = 0
        
        foreach ($ExistingPlugin in $Registry.plugins) {
            $PluginData = @{
                Name = $ExistingPlugin.name
                ShortName = $ExistingPlugin.shortName
                Description = $ExistingPlugin.description
                Tags = $ExistingPlugin.tags
                Version = $ExistingPlugin.latestVersion
                ReleaseTag = $ExistingPlugin.releaseTag
                ReleaseDate = ConvertTo-Iso8601String -Value $ExistingPlugin.releaseDate
                Artifacts = $ExistingPlugin.artifacts
            }
            
            $validation = Test-ValidPluginData -Plugin $PluginData
            if ($validation.IsValid) {
                $ValidExistingPlugins += $ExistingPlugin
            } else {
                $InvalidCount++
                Write-Warning "Removing invalid existing plugin '$($ExistingPlugin.name)' from registry: $($validation.Errors -join ', ')"
            }
        }
        
        if ($InvalidCount -gt 0) {
            Write-Host "  Removed $InvalidCount invalid plugin(s) from registry" -ForegroundColor Yellow
        }
        
        $Registry.plugins = $ValidExistingPlugins
    }
    
    if ($Registry.versionHistory -and $Registry.versionHistory -is [hashtable]) {
        $CleanVersionHistory = @{}
        foreach ($PluginName in $Registry.versionHistory.Keys) {
            if (-not (Test-ValidPluginName -Name $PluginName)) {
                Write-Warning "Removing invalid version history for plugin '$PluginName'"
                continue
            }
            
            $VersionEntries = $Registry.versionHistory[$PluginName]
            if ($VersionEntries -is [hashtable]) {
                $CleanVersions = @{}
                foreach ($Version in $VersionEntries.Keys) {
                    if (-not (Test-ValidVersion -Version $Version)) {
                        Write-Warning "Removing invalid version '$Version' from history for '$PluginName'"
                        continue
                    }
                    $CleanVersions[$Version] = $VersionEntries[$Version]
                }
                if ($CleanVersions.Count -gt 0) {
                    $CleanVersionHistory[$PluginName] = $CleanVersions
                }
            }
        }
        $Registry.versionHistory = $CleanVersionHistory
    }
    
    if ($RegenerateFromReleases -or $NeedToCreateRegistry -or ($Registry.plugins.Count -eq 0)) {
        Write-Host "  Scanning existing releases to populate registry..." -ForegroundColor Cyan
        
        # Use a high limit to get all releases - GitHub API supports up to 1000 per page
        $ReleasesJson = gh release list --repo $Repository --limit 1000 --json tagName 2>$null
        if ($LASTEXITCODE -eq 0 -and $ReleasesJson) {
            $Releases = $ReleasesJson | ConvertFrom-Json
            
            # Build a map of all plugin versions from releases
            # Key: plugin name, Value: hashtable of version -> release data
            $ReleaseVersionsMap = @{}
            
            foreach ($Release in $Releases) {
                $Tag = $Release.tagName
                
                if ($Tag -eq $RegistryTag) {
                    continue
                }
                
                $PluginFromRelease = Get-PluginDataFromRelease -ReleaseTag $Tag -Repository $Repository
                if ($PluginFromRelease) {
                    $validation = Test-ValidPluginData -Plugin $PluginFromRelease
                    if ($validation.IsValid) {
                        $PluginName = $PluginFromRelease.Name
                        $Version = $PluginFromRelease.Version
                        
                        if (-not $ReleaseVersionsMap.ContainsKey($PluginName)) {
                            $ReleaseVersionsMap[$PluginName] = @{}
                        }
                        
                        $ReleaseVersionsMap[$PluginName][$Version] = $PluginFromRelease
                        Write-Host "    Found release: $Tag" -ForegroundColor Gray
                        
                        # Also update version history for all versions found
                        Update-PluginVersionHistory -Registry $Registry -PluginName $PluginName -Version $Version -ReleaseTag $PluginFromRelease.ReleaseTag -ReleaseDate $PluginFromRelease.ReleaseDate
                    }
                }
            }
            
            # For each plugin, find the latest version and add to PublishedPlugins
            foreach ($PluginName in $ReleaseVersionsMap.Keys) {
                $VersionsMap = $ReleaseVersionsMap[$PluginName]
                $LatestVersion = $null
                $LatestPlugin = $null
                
                foreach ($Version in $VersionsMap.Keys) {
                    $VersionToCompare = Get-BaseVersion -Version $Version
                    try {
                        if ($null -eq $LatestVersion) {
                            $LatestVersion = $VersionToCompare
                            $LatestPlugin = $VersionsMap[$Version]
                        } elseif ([version]$VersionToCompare -gt [version]$LatestVersion) {
                            $LatestVersion = $VersionToCompare
                            $LatestPlugin = $VersionsMap[$Version]
                        }
                    } catch {
                        # Skip invalid versions
                        continue
                    }
                }
                
                if ($LatestPlugin) {
                    $PublishedPlugins += $LatestPlugin
                }
            }
        }
    }
    
    $PluginsMap = @{}
    foreach ($p in $Registry.plugins) {
        $PluginsMap[$p.name] = $p
    }
    
    foreach ($Plugin in $PublishedPlugins) {
        $PluginName = $Plugin.Name
        $Version = $Plugin.Version
        
        Write-Host "  Adding $PluginName v$Version to registry..." -ForegroundColor Gray
        
        Update-PluginVersionHistory -Registry $Registry -PluginName $PluginName -Version $Version -ReleaseTag $Plugin.ReleaseTag -ReleaseDate $Plugin.ReleaseDate
        
        if ($PluginsMap.ContainsKey($PluginName)) {
            $ExistingPlugin = $PluginsMap[$PluginName]
            
            $CurrentLatest = $ExistingPlugin.latestVersion
            # Strip pre-release suffix for version comparison using helper function
            $VersionToCompare = Get-BaseVersion -Version $Version
            $CurrentLatestToCompare = Get-BaseVersion -Version $CurrentLatest
            if (-not $CurrentLatestToCompare -or ([version]$VersionToCompare -ge [version]$CurrentLatestToCompare)) {
                $ExistingPlugin.latestVersion = $Version
                $ExistingPlugin.releaseTag = $Plugin.ReleaseTag
                $ExistingPlugin.releaseDate = $Plugin.ReleaseDate
                $ExistingPlugin.artifacts = $Plugin.Artifacts
                $ExistingPlugin.description = $Plugin.Description
                $ExistingPlugin.tags = $Plugin.Tags
            }
        } else {
            $NewPlugin = @{
                name = $PluginName
                shortName = $Plugin.ShortName
                description = $Plugin.Description
                tags = $Plugin.Tags
                latestVersion = $Version
                releaseTag = $Plugin.ReleaseTag
                releaseDate = $Plugin.ReleaseDate
                artifacts = $Plugin.Artifacts
            }
            $PluginsMap[$PluginName] = $NewPlugin
        }
    }
    
    $Registry.plugins = $PluginsMap.Values | Sort-Object { $_.name }
    
    $Registry.lastUpdated = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", [System.Globalization.CultureInfo]::InvariantCulture)
    
    Write-Host "  Performing final registry validation..." -ForegroundColor Gray
    
    $MaxPluginCount = 1000
    if ($Registry.plugins.Count -gt $MaxPluginCount) {
        Write-Error "Registry contains too many plugins ($($Registry.plugins.Count)). Maximum allowed: $MaxPluginCount"
        exit 1
    }
    
    $FinalValidPlugins = @()
    foreach ($FinalPlugin in $Registry.plugins) {
        $PluginData = @{
            Name = $FinalPlugin.name
            ShortName = $FinalPlugin.shortName
            Description = $FinalPlugin.description
            Tags = $FinalPlugin.tags
            Version = $FinalPlugin.latestVersion
            ReleaseTag = $FinalPlugin.releaseTag
            ReleaseDate = ConvertTo-Iso8601String -Value $FinalPlugin.releaseDate
            Artifacts = $FinalPlugin.artifacts
        }
        
        $validation = Test-ValidPluginData -Plugin $PluginData
        if ($validation.IsValid) {
            $FinalValidPlugins += $FinalPlugin
        } else {
            Write-Warning "Final validation failed for '$($FinalPlugin.name)': $($validation.Errors -join ', ')"
        }
    }
    $Registry.plugins = $FinalValidPlugins
    
    if ($Registry.plugins.Count -eq 0) {
        Write-Host "No valid plugins in registry. Creating empty registry structure." -ForegroundColor Yellow
    }
    
    $RegistryJson = $Registry | ConvertTo-Json -Depth 10
    $MaxRegistrySizeBytes = 50MB
    $RegistryBytes = [System.Text.Encoding]::UTF8.GetByteCount($RegistryJson)
    
    if ($RegistryBytes -gt $MaxRegistrySizeBytes) {
        Write-Error "Registry file too large: $([math]::Round($RegistryBytes / 1MB, 2)) MB. Maximum allowed: $([math]::Round($MaxRegistrySizeBytes / 1MB, 2)) MB"
        exit 1
    }
    
    try {
        $null = $RegistryJson | ConvertFrom-Json
    } catch {
        Write-Error "Generated registry JSON is invalid: $($_.Exception.Message)"
        exit 1
    }
    
    $RegistryJson | Set-Content -Path $LocalRegistryPath -Encoding UTF8
    Write-Host "  Registry updated with $($Registry.plugins.Count) total plugin(s) ($([math]::Round($RegistryBytes / 1KB, 2)) KB)" -ForegroundColor Gray
    
    if ($RegistryReleaseExists) {
        gh release upload $RegistryTag $LocalRegistryPath --clobber --repo $Repository
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to upload registry to existing release"
            exit 1
        }
        Write-Host "  Updated registry in release '$RegistryTag'" -ForegroundColor Green
    } else {
        gh release create $RegistryTag --title "Plugin Registry" --notes "Auto-updated plugin registry for Musoq plugin discovery." $LocalRegistryPath --repo $Repository
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to create registry release"
            exit 1
        }
        Write-Host "  Created registry release '$RegistryTag'" -ForegroundColor Green
    }
    
    Write-Host "Plugin registry update complete!" -ForegroundColor Cyan
}
finally {
    if (Test-Path $TempDir) {
        Remove-Item $TempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
