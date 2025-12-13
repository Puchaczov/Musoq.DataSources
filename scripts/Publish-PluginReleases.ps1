<#
.SYNOPSIS
    Publishes plugin releases to GitHub.

.DESCRIPTION
    Creates GitHub releases for plugins that don't already have releases for their current version.
    Outputs metadata about published plugins for registry updates.

.PARAMETER PluginName
    The name of a specific plugin to release, or "All" for all plugins.

.PARAMETER ArtifactsDirectory
    The directory containing the plugin artifacts (zip files).

.PARAMETER Repository
    The GitHub repository in "owner/repo" format.

.PARAMETER OutputMetadataPath
    Optional path to output JSON metadata about published releases.

.EXAMPLE
    ./Publish-PluginReleases.ps1 -PluginName "All" -Repository "Puchaczov/Musoq.DataSources"
#>

param(
    [string]$PluginName = "All",
    [string]$ArtifactsDirectory = "$PSScriptRoot/../artifacts",
    [Parameter(Mandatory=$true)]
    [string]$Repository,
    [string]$OutputMetadataPath = ""
)

$ErrorActionPreference = "Stop"

# Load common configuration
. "$PSScriptRoot/common/Plugin-Config.ps1"

# Resolve artifacts directory
if (-not (Test-Path $ArtifactsDirectory)) {
    Write-Error "Artifacts directory not found: $ArtifactsDirectory"
    exit 1
}
$ArtifactsDirectory = Resolve-Path $ArtifactsDirectory

# Get projects to process
$Projects = Get-PluginProjects -PluginName $PluginName

if ($Projects.Count -eq 0) {
    Write-Error "No matching plugin projects found."
    exit 1
}

$PublishedPlugins = @()
$PublishedCount = 0
$SkippedCount = 0

foreach ($Project in $Projects) {
    $Metadata = Get-ProjectMetadata -Project $Project
    $ProjectName = $Metadata.Name
    $Version = $Metadata.Version
    $ReleaseTag = $Metadata.ReleaseTag
    
    Write-Host "Processing $ProjectName with version $Version" -ForegroundColor Cyan
    
    # Check if release already exists
    $ReleaseExists = $false
    gh release view $ReleaseTag --repo $Repository 1>$null 2>$null
    if ($LASTEXITCODE -eq 0) { $ReleaseExists = $true }
    
    if ($ReleaseExists) {
        Write-Host "  Release $ReleaseTag already exists. Skipping..." -ForegroundColor Yellow
        $SkippedCount++
        continue
    }
    
    # Check if matching artifacts exist
    $ArtifactMatches = @(Get-ChildItem -Path $ArtifactsDirectory -Filter "*$ProjectName*.zip" -ErrorAction SilentlyContinue)
    if ($ArtifactMatches.Count -eq 0) {
        Write-Host "  No artifacts found. Skipping release creation..." -ForegroundColor Yellow
        $SkippedCount++
        continue
    }
    
    # Create the release
    $ArtifactPattern = Join-Path $ArtifactsDirectory "*$ProjectName*.zip"
    gh release create $ReleaseTag --title "Release $Version ($ProjectName)" --generate-notes $ArtifactPattern --repo $Repository
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to create release $ReleaseTag for $ProjectName"
        exit 1
    }
    
    Write-Host "  Created release $ReleaseTag" -ForegroundColor Green
    $PublishedCount++
    
    # Collect metadata for registry update
    $ArtifactInfo = Get-ArtifactNames -ProjectName $ProjectName
    $PublishedPlugins += @{
        Name = $ProjectName
        ShortName = $Metadata.ShortName
        Description = $Metadata.Description
        Tags = $Metadata.Tags
        Version = $Version
        ReleaseTag = $ReleaseTag
        ReleaseDate = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
        Artifacts = $ArtifactInfo
    }
}

Write-Host ""
Write-Host "Summary: Published=$PublishedCount, Skipped=$SkippedCount" -ForegroundColor Magenta

# Output metadata for registry update if requested
if ($OutputMetadataPath -and $PublishedPlugins.Count -gt 0) {
    $PublishedPlugins | ConvertTo-Json -Depth 10 | Set-Content -Path $OutputMetadataPath -Encoding UTF8
    Write-Host "Published plugin metadata written to: $OutputMetadataPath" -ForegroundColor Gray
}

# Return published plugins for pipeline use
return $PublishedPlugins
