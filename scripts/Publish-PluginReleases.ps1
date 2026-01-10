param(
    [string]$PluginName = "All",
    [string]$ArtifactsDirectory = "$PSScriptRoot/../artifacts",
    [Parameter(Mandatory=$true)]
    [string]$Repository,
    [string]$OutputMetadataPath = ""
)

$ErrorActionPreference = "Stop"

. "$PSScriptRoot/common/Plugin-Config.ps1"

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

if ($OutputMetadataPath) {
    $OutputDir = Split-Path -Parent $OutputMetadataPath
    if ($OutputDir -and -not (Test-Path $OutputDir)) {
        Write-Error "Output directory does not exist: $OutputDir"
        exit 1
    }
    
    if ($OutputMetadataPath -match '\.\.[/\\]') {
        Write-Error "Invalid output path: path traversal not allowed"
        exit 1
    }
}

if (-not (Test-Path $ArtifactsDirectory)) {
    Write-Error "Artifacts directory not found: $ArtifactsDirectory"
    exit 1
}
$ArtifactsDirectory = Resolve-Path $ArtifactsDirectory

$SolutionRoot = Get-SolutionRoot
$ArtifactsFullPath = (Resolve-Path $ArtifactsDirectory).Path
if (-not $ArtifactsFullPath.StartsWith($SolutionRoot.Path, [System.StringComparison]::OrdinalIgnoreCase)) {
    $TempPath = [System.IO.Path]::GetTempPath()
    if (-not $ArtifactsFullPath.StartsWith($TempPath, [System.StringComparison]::OrdinalIgnoreCase)) {
        Write-Warning "Artifacts directory is outside solution root: $ArtifactsFullPath"
    }
}

$Projects = Get-PluginProjects -PluginName $PluginName

if ($Projects.Count -eq 0) {
    Write-Error "No matching plugin projects found."
    exit 1
}

$PublishedPlugins = @()
$PublishedCount = 0
$SkippedCount = 0

foreach ($Project in $Projects) {
    try {
        $Metadata = Get-ProjectMetadata -Project $Project
    }
    catch {
        Write-Warning "Skipping $($Project.BaseName): Failed to extract valid metadata - $_"
        $SkippedCount++
        continue
    }
    
    $ProjectName = $Metadata.Name
    $Version = $Metadata.Version
    $ReleaseTag = $Metadata.ReleaseTag
    
    Write-Host "Processing $ProjectName with version $Version" -ForegroundColor Cyan
    
    if (-not (Test-ValidPluginName -Name $ProjectName)) {
        Write-Warning "  Skipping: Invalid plugin name format"
        $SkippedCount++
        continue
    }
    
    if (-not (Test-ValidVersion -Version $Version)) {
        Write-Warning "  Skipping: Invalid version format"
        $SkippedCount++
        continue
    }
    
    $ReleaseExists = $false
    gh release view $ReleaseTag --repo $Repository 1>$null 2>$null
    if ($LASTEXITCODE -eq 0) { $ReleaseExists = $true }
    
    if ($ReleaseExists) {
        Write-Host "  Release $ReleaseTag already exists. Skipping..." -ForegroundColor Yellow
        $SkippedCount++
        continue
    }
    
    $ArtifactMatches = @(Get-ChildItem -Path $ArtifactsDirectory -Filter "*$ProjectName*.zip" -ErrorAction SilentlyContinue)
    if ($ArtifactMatches.Count -eq 0) {
        Write-Host "  No artifacts found. Skipping release creation..." -ForegroundColor Yellow
        $SkippedCount++
        continue
    }
    
    $ValidArtifacts = @()
    foreach ($artifact in $ArtifactMatches) {
        if ($artifact.Name -notmatch "^Musoq\.DataSources\.[A-Za-z0-9]+-(windows|linux|macos|alpine)-(x64|arm64)\.zip$") {
            Write-Warning "  Skipping invalid artifact: $($artifact.Name)"
            continue
        }
        
        $bytes = [System.IO.File]::ReadAllBytes($artifact.FullName) | Select-Object -First 4
        if ($bytes.Count -ge 4 -and $bytes[0] -eq 0x50 -and $bytes[1] -eq 0x4B) {
            # Validate that Plugin.zip contains the entry point DLL
            $ValidationTempDir = Join-Path ([System.IO.Path]::GetTempPath()) "artifact-validation-$([guid]::NewGuid().ToString('N'))"
            try {
                New-Item -ItemType Directory -Path $ValidationTempDir -Force | Out-Null
                Expand-Archive -Path $artifact.FullName -DestinationPath $ValidationTempDir -Force
                
                $EntryPointFile = Join-Path $ValidationTempDir "EntryPoint.txt"
                $PluginZipFile = Join-Path $ValidationTempDir "Plugin.zip"
                
                if (-not (Test-Path $EntryPointFile)) {
                    Write-Warning "  Skipping artifact missing EntryPoint.txt: $($artifact.Name)"
                    continue
                }
                
                if (-not (Test-Path $PluginZipFile)) {
                    Write-Warning "  Skipping artifact missing Plugin.zip: $($artifact.Name)"
                    continue
                }
                
                $EntryPointDll = (Get-Content $EntryPointFile -Raw).Trim()
                if ([string]::IsNullOrWhiteSpace($EntryPointDll)) {
                    Write-Warning "  Skipping artifact with empty EntryPoint.txt: $($artifact.Name)"
                    continue
                }
                
                # Extract Plugin.zip and verify entry point DLL exists
                $PluginTempDir = Join-Path $ValidationTempDir "plugin-contents"
                New-Item -ItemType Directory -Path $PluginTempDir -Force | Out-Null
                Expand-Archive -Path $PluginZipFile -DestinationPath $PluginTempDir -Force
                
                $EntryPointPath = Join-Path $PluginTempDir $EntryPointDll
                if (-not (Test-Path $EntryPointPath)) {
                    Write-Warning "  Skipping artifact with missing entry point DLL '$EntryPointDll': $($artifact.Name)"
                    continue
                }
                
                $ValidArtifacts += $artifact
            }
            catch {
                Write-Warning "  Skipping artifact due to validation error: $($artifact.Name) - $_"
                continue
            }
            finally {
                if (Test-Path $ValidationTempDir) {
                    Remove-Item $ValidationTempDir -Recurse -Force -ErrorAction SilentlyContinue
                }
            }
        } else {
            Write-Warning "  Skipping invalid zip file: $($artifact.Name)"
        }
    }
    
    if ($ValidArtifacts.Count -eq 0) {
        Write-Warning "  No valid artifacts found. Skipping release creation..."
        $SkippedCount++
        continue
    }
    
    $ArtifactPaths = $ValidArtifacts | ForEach-Object { $_.FullName }
    gh release create $ReleaseTag --title "Release $Version ($ProjectName)" --generate-notes @ArtifactPaths --repo $Repository
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to create release $ReleaseTag for $ProjectName"
        exit 1
    }
    
    Write-Host "  Created release $ReleaseTag" -ForegroundColor Green
    $PublishedCount++
    
    $ArtifactInfo = Get-ArtifactNames -ProjectName $ProjectName
    $PublishedPlugins += @{
        Name = $ProjectName
        ShortName = $Metadata.ShortName
        Description = $Metadata.Description
        Tags = $Metadata.Tags
        Version = $Version
        ReleaseTag = $ReleaseTag
        ReleaseDate = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", [System.Globalization.CultureInfo]::InvariantCulture)
        Artifacts = $ArtifactInfo
    }
}

Write-Host ""
Write-Host "Summary: Published=$PublishedCount, Skipped=$SkippedCount" -ForegroundColor Magenta

if ($OutputMetadataPath -and $PublishedPlugins.Count -gt 0) {
    $PublishedPlugins | ConvertTo-Json -Depth 10 | Set-Content -Path $OutputMetadataPath -Encoding UTF8
    Write-Host "Published plugin metadata written to: $OutputMetadataPath" -ForegroundColor Gray
}

return $PublishedPlugins
