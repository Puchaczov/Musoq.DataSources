<#
.SYNOPSIS
    Updates the plugin registry with newly published plugins.

.DESCRIPTION
    Downloads the existing plugin registry from the dedicated GitHub release,
    merges in newly published plugin versions, and uploads the updated registry.
    Creates the registry release if it doesn't exist.

.PARAMETER Repository
    The GitHub repository in "owner/repo" format.

.PARAMETER PublishedMetadataPath
    Path to JSON file containing metadata about newly published plugins.

.PARAMETER PublishedPlugins
    Alternative to PublishedMetadataPath - array of plugin metadata objects.

.EXAMPLE
    ./Update-PluginRegistry.ps1 -Repository "Puchaczov/Musoq.DataSources" -PublishedMetadataPath "./published.json"
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$Repository,
    [string]$PublishedMetadataPath = "",
    [array]$PublishedPlugins = @()
)

$ErrorActionPreference = "Stop"

# Load common configuration
. "$PSScriptRoot/common/Plugin-Config.ps1"

$RegistryTag = $script:RegistryReleaseTag
$RegistryFile = $script:RegistryFileName
$TempDir = Join-Path ([System.IO.Path]::GetTempPath()) "plugin-registry-$(Get-Random)"

# Load published plugins from file if path provided
if ($PublishedMetadataPath -and (Test-Path $PublishedMetadataPath)) {
    $PublishedPlugins = Get-Content $PublishedMetadataPath -Raw | ConvertFrom-Json
}

if ($PublishedPlugins.Count -eq 0) {
    Write-Host "No plugins to add to registry. Skipping update." -ForegroundColor Yellow
    exit 0
}

Write-Host "Updating plugin registry with $($PublishedPlugins.Count) plugin(s)..." -ForegroundColor Cyan

try {
    # Create temp directory
    New-Item -ItemType Directory -Path $TempDir -Force | Out-Null
    $LocalRegistryPath = Join-Path $TempDir $RegistryFile
    
    # Check if registry release exists
    $RegistryReleaseExists = $false
    gh release view $RegistryTag --repo $Repository 1>$null 2>$null
    if ($LASTEXITCODE -eq 0) { $RegistryReleaseExists = $true }
    
    # Initialize or download existing registry
    $Registry = $null
    
    if ($RegistryReleaseExists) {
        Write-Host "  Downloading existing registry..." -ForegroundColor Gray
        
        # Download the registry file from the release
        Push-Location $TempDir
        gh release download $RegistryTag --pattern $RegistryFile --repo $Repository 2>$null
        Pop-Location
        
        if (Test-Path $LocalRegistryPath) {
            $Registry = Get-Content $LocalRegistryPath -Raw | ConvertFrom-Json -AsHashtable
            Write-Host "  Loaded existing registry with $($Registry.plugins.Count) plugin(s)" -ForegroundColor Gray
        }
    }
    
    # Create new registry if not exists or failed to download
    if (-not $Registry) {
        Write-Host "  Creating new registry..." -ForegroundColor Gray
        $Registry = @{
            schemaVersion = "1.0"
            lastUpdated = ""
            repository = "https://github.com/$Repository"
            plugins = @()
            versionHistory = @{}
        }
    }
    
    # Ensure plugins is an array (handle legacy format migration)
    if ($Registry.plugins -is [hashtable]) {
        Write-Host "  Migrating from legacy format..." -ForegroundColor Gray
        $Registry.plugins = @()
        $Registry.versionHistory = @{}
    }
    
    # Ensure versionHistory exists
    if (-not $Registry.versionHistory) {
        $Registry.versionHistory = @{}
    }
    
    # Convert plugins array to hashtable for easier lookup during update
    $PluginsMap = @{}
    foreach ($p in $Registry.plugins) {
        $PluginsMap[$p.name] = $p
    }
    
    # Update registry with published plugins
    foreach ($Plugin in $PublishedPlugins) {
        $PluginName = $Plugin.Name
        $Version = $Plugin.Version
        
        Write-Host "  Adding $PluginName v$Version to registry..." -ForegroundColor Gray
        
        # Ensure versionHistory entry exists for this plugin
        if (-not $Registry.versionHistory.ContainsKey($PluginName)) {
            $Registry.versionHistory[$PluginName] = @{}
        }
        
        # Add to version history
        $Registry.versionHistory[$PluginName][$Version] = @{
            releaseTag = $Plugin.ReleaseTag
            releaseDate = $Plugin.ReleaseDate
        }
        
        # Check if plugin already exists in registry
        if ($PluginsMap.ContainsKey($PluginName)) {
            $ExistingPlugin = $PluginsMap[$PluginName]
            
            # Update if this version is newer
            $CurrentLatest = $ExistingPlugin.latestVersion
            if (-not $CurrentLatest -or ([version]$Version -ge [version]$CurrentLatest)) {
                $ExistingPlugin.latestVersion = $Version
                $ExistingPlugin.releaseTag = $Plugin.ReleaseTag
                $ExistingPlugin.releaseDate = $Plugin.ReleaseDate
                $ExistingPlugin.artifacts = $Plugin.Artifacts
                # Update metadata in case it changed
                $ExistingPlugin.description = $Plugin.Description
                $ExistingPlugin.tags = $Plugin.Tags
            }
        } else {
            # Add new plugin entry (optimized flat structure for fast search/list)
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
    
    # Convert map back to sorted array
    $Registry.plugins = $PluginsMap.Values | Sort-Object { $_.name }
    
    # Update timestamp
    $Registry.lastUpdated = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    
    # Write updated registry to file
    $Registry | ConvertTo-Json -Depth 10 | Set-Content -Path $LocalRegistryPath -Encoding UTF8
    Write-Host "  Registry updated with $($Registry.plugins.Count) total plugin(s)" -ForegroundColor Gray
    
    # Create or update the registry release
    if ($RegistryReleaseExists) {
        # Upload with clobber to replace existing
        gh release upload $RegistryTag $LocalRegistryPath --clobber --repo $Repository
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to upload registry to existing release"
            exit 1
        }
        Write-Host "  Updated registry in release '$RegistryTag'" -ForegroundColor Green
    } else {
        # Create new release with the registry file
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
    # Cleanup temp directory
    if (Test-Path $TempDir) {
        Remove-Item $TempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
