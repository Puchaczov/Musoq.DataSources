[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$Tag,
    [Parameter(Mandatory=$true)]
    [string]$Repository,
    [string]$ArtifactDirectory = "artifacts/release",
    [string]$NuGetApiKey = "",
    [string]$NuGetSource = "https://api.nuget.org/v3/index.json",
    [string]$OutputMetadataPath = "",
    [string]$TargetCommitish = ""
)

. (Join-Path $PSScriptRoot "Release.Common.ps1")
. (Join-Path $PSScriptRoot "../common/Plugin-ArtifactIntegrity.ps1")

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

if (-not [string]::IsNullOrWhiteSpace($TargetCommitish) -and $TargetCommitish -notmatch '^[0-9a-fA-F]{7,40}$') {
    throw "TargetCommitish must be a git commit SHA when supplied."
}

$manifestPath = Join-Path $resolvedArtifactDirectory "release-artifacts.json"
if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw "Release artifact manifest was not found: $manifestPath"
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.tag -ne $release.Tag) {
    throw "Release artifact manifest tag '$($manifest.tag)' does not match '$($release.Tag)'."
}

$pluginReleaseMetadataPath = [string]$manifest.pluginReleaseMetadata
if ([string]::IsNullOrWhiteSpace($pluginReleaseMetadataPath)) {
    throw "Release artifact manifest is missing pluginReleaseMetadata."
}
$pluginReleaseMetadata = Read-MusoqPluginReleaseMetadata -Path $pluginReleaseMetadataPath
if ($pluginReleaseMetadata.plugin -ne $release.PackageId -or
    $pluginReleaseMetadata.version -ne $release.Version -or
    $pluginReleaseMetadata.releaseTag -ne $release.Tag) {
    throw "Plugin release metadata identity does not match '$($release.Tag)'."
}

$nugetPackageFiles = @($manifest.nupkg, $manifest.snupkg)
$assetPaths = @()
$assetPaths += $nugetPackageFiles
$assetPaths += @($manifest.pluginArtifacts)
$assetPaths += $pluginReleaseMetadataPath
foreach ($assetPath in $assetPaths) {
    if (-not (Test-Path -LiteralPath $assetPath)) {
        throw "Release asset listed in manifest was not found: $assetPath"
    }
}

$pluginRecordsByFileName = @{}
foreach ($platform in $script:MusoqRequiredArtifactPlatforms) {
    $record = $pluginReleaseMetadata.artifacts.$platform
    $pluginRecordsByFileName[[string]$record.fileName] = $record
}

$releaseExists = $false
gh release view $release.Tag --repo $Repository 1>$null 2>$null
if ($LASTEXITCODE -eq 0) {
    $releaseExists = $true
}

$uploadPaths = @($assetPaths)
$remoteValidationDirectory = $null
if ($releaseExists) {
    $releaseAssetsJson = gh release view $release.Tag --repo $Repository --json assets 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Could not inspect assets on existing GitHub release '$($release.Tag)': $($releaseAssetsJson -join "`n")"
    }

    $releaseAssets = @((($releaseAssetsJson -join "`n") | ConvertFrom-Json).assets)
    $existingAssetNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($asset in $releaseAssets) {
        if (-not $existingAssetNames.Add([string]$asset.name)) {
            throw "GitHub release '$($release.Tag)' contains duplicate asset name '$($asset.name)'."
        }
    }

    $metadataFileName = [System.IO.Path]::GetFileName($pluginReleaseMetadataPath)
    $hasExistingPluginArtifact = @($pluginRecordsByFileName.Keys | Where-Object { $existingAssetNames.Contains($_) }).Count -gt 0
    if ($hasExistingPluginArtifact -and -not $existingAssetNames.Contains($metadataFileName)) {
        throw "Existing plugin assets cannot be accepted without '$metadataFileName'; refusing to overwrite legacy release '$($release.Tag)'."
    }

    $remoteValidationDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "musoq-release-assets-$([guid]::NewGuid().ToString('N'))"
    New-Item -ItemType Directory -Force -Path $remoteValidationDirectory | Out-Null
    try {
        $uploadPaths = @()
        foreach ($assetPath in $assetPaths) {
            $assetName = [System.IO.Path]::GetFileName([string]$assetPath)
            if (-not $existingAssetNames.Contains($assetName)) {
                $uploadPaths += $assetPath
                continue
            }

            $assetDirectory = Join-Path $remoteValidationDirectory ([guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Force -Path $assetDirectory | Out-Null
            gh release download $release.Tag --repo $Repository --pattern $assetName --dir $assetDirectory
            if ($LASTEXITCODE -ne 0) {
                throw "Could not download existing release asset '$assetName' for immutable verification."
            }

            $remoteAssetPath = Join-Path $assetDirectory $assetName
            if (-not (Test-Path -LiteralPath $remoteAssetPath -PathType Leaf)) {
                throw "Downloaded release asset was not found: $remoteAssetPath"
            }

            if ($assetName -eq $metadataFileName) {
                $remoteMetadata = Read-MusoqPluginReleaseMetadata -Path $remoteAssetPath
                $localJson = ConvertTo-MusoqCanonicalReleaseMetadataJson -Metadata $pluginReleaseMetadata
                $remoteJson = ConvertTo-MusoqCanonicalReleaseMetadataJson -Metadata $remoteMetadata
                if ($localJson -cne $remoteJson) {
                    throw "Existing '$metadataFileName' differs from local immutable release metadata."
                }
            }

            if ($pluginRecordsByFileName.ContainsKey($assetName)) {
                Assert-MusoqArtifactMatchesRecord `
                    -Path $remoteAssetPath `
                    -Expected $pluginRecordsByFileName[$assetName] `
                    -Context "Existing release asset '$assetName'" | Out-Null
            }
            else {
                $localIntegrity = Get-MusoqArtifactIntegrity -Path $assetPath
                Assert-MusoqArtifactMatchesRecord `
                    -Path $remoteAssetPath `
                    -Expected $localIntegrity `
                    -Context "Existing release asset '$assetName'" | Out-Null
            }

            Write-Host "Accepted immutable existing release asset: $assetName" -ForegroundColor Gray
        }
    }
    finally {
        if (Test-Path -LiteralPath $remoteValidationDirectory) {
            Remove-Item -LiteralPath $remoteValidationDirectory -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

foreach ($packageFile in $nugetPackageFiles) {
    Write-Host "Publishing $([System.IO.Path]::GetFileName($packageFile)) to NuGet..." -ForegroundColor Cyan
    dotnet nuget push $packageFile `
        --source $NuGetSource `
        --api-key $NuGetApiKey `
        --skip-duplicate

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet nuget push failed for $packageFile."
    }
}

if ($releaseExists) {
    if ($uploadPaths.Count -gt 0) {
        Write-Host "Uploading missing assets to existing GitHub release $($release.Tag)..." -ForegroundColor Cyan
        gh release upload $release.Tag @uploadPaths --repo $Repository
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to upload missing assets to GitHub release $($release.Tag)."
        }
    }
    else {
        Write-Host "All GitHub release assets already exist with matching bytes." -ForegroundColor Gray
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

    if (-not [string]::IsNullOrWhiteSpace($TargetCommitish)) {
        $createArgs += @("--target", $TargetCommitish)
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
