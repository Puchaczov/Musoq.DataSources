param(
    [Parameter(Mandatory=$true)]
    [string]$Repository,
    [string]$PublishedMetadataPath = "",
    [array]$PublishedPlugins = @(),
    [switch]$RegenerateFromReleases
)

$ErrorActionPreference = "Stop"

. "$PSScriptRoot/common/Plugin-Config.ps1"
. "$PSScriptRoot/common/Plugin-ArtifactIntegrity.ps1"

function ConvertTo-Iso8601String {
    param([object]$Value)

    if ($null -eq $Value) {
        return $null
    }

    if ($Value -is [DateTime]) {
        return $Value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", [System.Globalization.CultureInfo]::InvariantCulture)
    }

    $dateTime = [DateTime]::Parse($Value, [System.Globalization.CultureInfo]::InvariantCulture)
    return $dateTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", [System.Globalization.CultureInfo]::InvariantCulture)
}

function Get-Field {
    param(
        [Parameter(Mandatory=$true)]
        [object]$Object,
        [Parameter(Mandatory=$true)]
        [string]$Name
    )

    if ($Object -is [System.Collections.IDictionary]) {
        return $Object[$Name]
    }

    return $Object.$Name
}

function Set-RegistryVersionHistory {
    param(
        [Parameter(Mandatory=$true)]
        [hashtable]$Registry,
        [Parameter(Mandatory=$true)]
        [string]$PluginName,
        [Parameter(Mandatory=$true)]
        [string]$Version,
        [Parameter(Mandatory=$true)]
        [string]$ReleaseTag,
        [Parameter(Mandatory=$true)]
        [string]$ReleaseDate,
        [object]$RuntimeCompatibility = $null,
        [object]$ArtifactIntegrity = $null
    )

    if (-not $Registry.versionHistory.ContainsKey($PluginName)) {
        $Registry.versionHistory[$PluginName] = @{}
    }

    $entry = New-MusoqVersionHistoryEntry `
        -ReleaseTag $ReleaseTag `
        -ReleaseDate $ReleaseDate `
        -Version $Version `
        -RuntimeCompatibility $RuntimeCompatibility `
        -Artifacts $ArtifactIntegrity

    $Registry.versionHistory[$PluginName][$Version] = $entry
}

function Test-ValidPluginData {
    param([object]$Plugin)

    $errors = @()
    $name = Get-Field -Object $Plugin -Name "Name"
    $version = Get-Field -Object $Plugin -Name "Version"
    $shortName = Get-Field -Object $Plugin -Name "ShortName"
    $releaseTag = Get-Field -Object $Plugin -Name "ReleaseTag"
    $releaseDate = Get-Field -Object $Plugin -Name "ReleaseDate"
    $description = Get-Field -Object $Plugin -Name "Description"
    $tags = Get-Field -Object $Plugin -Name "Tags"
    $artifacts = Get-Field -Object $Plugin -Name "Artifacts"
    $runtimeCompatibility = Get-Field -Object $Plugin -Name "RuntimeCompatibility"
    $artifactIntegrity = Get-Field -Object $Plugin -Name "ArtifactIntegrity"

    if (-not $name) {
        $errors += "Missing Name"
    } elseif (-not (Test-ValidPluginName -Name $name)) {
        $errors += "Invalid Name format: $name"
    }

    if (-not $version) {
        $errors += "Missing Version"
    } elseif (-not (Test-ValidVersion -Version $version)) {
        $errors += "Invalid Version format: $version"
    }

    if (-not $shortName) {
        $errors += "Missing ShortName"
    } elseif (-not (Test-ValidShortName -ShortName $shortName)) {
        $errors += "Invalid ShortName format: $shortName"
    }

    if (-not $releaseTag) {
        $errors += "Missing ReleaseTag"
    } else {
        $parsedTag = ConvertFrom-MusoqPluginReleaseTag -ReleaseTag $releaseTag
        if (-not $parsedTag -or $parsedTag.Version -ne $version -or $parsedTag.PluginName -ne $name) {
            $errors += "Invalid ReleaseTag format"
        }
    }

    if (-not $releaseDate) {
        $errors += "Missing ReleaseDate"
    } else {
        try {
            $releaseDateStr = ConvertTo-Iso8601String -Value $releaseDate
            if ($releaseDateStr -notmatch '^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$') {
                $errors += "Invalid ReleaseDate format (expected ISO 8601)"
            }
        }
        catch {
            $errors += "Invalid ReleaseDate format (expected ISO 8601)"
        }
    }

    if ($description -and -not (Test-ValidDescription -Description $description)) {
        $errors += "Invalid Description"
    }

    if ($tags -and -not (Test-ValidTags -Tags $tags)) {
        $errors += "Invalid Tags"
    }

    if ($artifacts) {
        foreach ($key in $artifacts.Keys) {
            if ($key -notmatch '^(windows|linux|macos|alpine)-(x64|arm64)$') {
                $errors += "Invalid artifact platform key: $key"
            }

            $artifactName = $artifacts[$key]
            if ($artifactName -notmatch '^Musoq\.DataSources\.[A-Za-z0-9]+-(windows|linux|macos|alpine)-(x64|arm64)\.zip$') {
                $errors += "Invalid artifact name: $artifactName"
            }
        }
    }

    if (($null -eq $runtimeCompatibility) -xor ($null -eq $artifactIntegrity)) {
        $errors += "RuntimeCompatibility and ArtifactIntegrity must be supplied together"
    }
    elseif ($null -ne $runtimeCompatibility) {
        try {
            Assert-MusoqPluginCompatibility -Compatibility $runtimeCompatibility
            foreach ($platform in $script:MusoqRequiredArtifactPlatforms) {
                $record = Get-Field -Object $artifactIntegrity -Name $platform
                if ($null -eq $record) {
                    throw "Missing required artifact integrity platform '$platform'."
                }

                $fileName = [string](Get-Field -Object $record -Name "fileName")
                $expectedFileName = "$name-$platform.zip"
                if ($fileName -ne $expectedFileName) {
                    throw "Invalid artifact filename '$fileName' for '$platform'. Expected '$expectedFileName'."
                }
                Assert-MusoqArtifactRecord -Record $record -Context "$name $version $platform"
            }
        }
        catch {
            $errors += "Invalid runtime compatibility or artifact integrity metadata: $_"
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

    $parsedTag = ConvertFrom-MusoqPluginReleaseTag -ReleaseTag $ReleaseTag
    if (-not $parsedTag) {
        return $null
    }

    $pluginName = $parsedTag.PluginName
    $version = $parsedTag.Version

    $projects = Get-PluginProjects -PluginName $pluginName
    if ($projects.Count -eq 0) {
        return $null
    }

    try {
        $metadata = Get-ProjectMetadata -Project $projects[0]
    }
    catch {
        return $null
    }

    $releaseInfoJson = gh release view $ReleaseTag --repo $Repository --json createdAt,assets 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($releaseInfoJson)) {
        return $null
    }
    $releaseInfo = $releaseInfoJson | ConvertFrom-Json
    $releaseDate = if ($releaseInfo.createdAt) {
        ConvertTo-Iso8601String -Value $releaseInfo.createdAt
    } else {
        (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", [System.Globalization.CultureInfo]::InvariantCulture)
    }

    $pluginData = @{
        Name = $pluginName
        ShortName = $metadata.ShortName
        Description = $metadata.Description
        Tags = $metadata.Tags
        Version = $version
        ReleaseTag = $ReleaseTag
        ReleaseDate = $releaseDate
        Artifacts = Get-ArtifactNames -ProjectName $pluginName
    }

    $releaseMetadataAssets = @($releaseInfo.assets | Where-Object { $_.name -eq $script:MusoqPluginReleaseMetadataFileName })
    if ($releaseMetadataAssets.Count -eq 0) {
        return $pluginData
    }
    if ($releaseMetadataAssets.Count -ne 1) {
        Write-Warning "Release '$ReleaseTag' has duplicate plugin release metadata assets and will remain legacy-only."
        return $pluginData
    }

    $metadataDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "plugin-release-metadata-$([guid]::NewGuid().ToString('N'))"
    try {
        New-Item -ItemType Directory -Path $metadataDirectory -Force | Out-Null
        gh release download $ReleaseTag `
            --repo $Repository `
            --pattern $script:MusoqPluginReleaseMetadataFileName `
            --dir $metadataDirectory 2>$null
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Could not download plugin release metadata for '$ReleaseTag'; entry will remain legacy-only."
            return $pluginData
        }

        $metadataPath = Join-Path $metadataDirectory $script:MusoqPluginReleaseMetadataFileName
        try {
            $releaseMetadata = Read-MusoqPluginReleaseMetadata -Path $metadataPath
        }
        catch {
            Write-Warning "Invalid plugin release metadata for '$ReleaseTag'; entry will remain legacy-only. $_"
            return $pluginData
        }

        if ($releaseMetadata.plugin -ne $pluginName -or
            $releaseMetadata.version -ne $version -or
            $releaseMetadata.releaseTag -ne $ReleaseTag) {
            Write-Warning "Plugin release metadata identity does not match '$ReleaseTag'; entry will remain legacy-only."
            return $pluginData
        }

        $pluginData.RuntimeCompatibility = $releaseMetadata.runtimeCompatibility
        $pluginData.ArtifactIntegrity = $releaseMetadata.artifacts
        return $pluginData
    }
    finally {
        if (Test-Path -LiteralPath $metadataDirectory) {
            Remove-Item -LiteralPath $metadataDirectory -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

function New-EmptyRegistry {
    param([string]$Repository)

    return @{
        schemaVersion = "1.2"
        lastUpdated = ""
        repository = "https://github.com/$Repository"
        plugins = @()
        versionHistory = @{}
    }
}

function Test-RegistryStructure {
    param([hashtable]$Registry)

    if (-not $Registry.ContainsKey('schemaVersion')) { return $false }
    if (-not $Registry.ContainsKey('plugins')) { return $false }
    if ($Registry.schemaVersion -and -not ($Registry.schemaVersion -match '^\d+\.\d+$')) { return $false }
    if ($Registry.repository -and -not ($Registry.repository -match '^https://github\.com/[a-zA-Z0-9\-_]+/[a-zA-Z0-9\.\-_]+$')) { return $false }

    return $true
}

function Repair-VersionHistory {
    param([hashtable]$Registry)

    if (-not $Registry.versionHistory -or $Registry.versionHistory -isnot [hashtable]) {
        $Registry.versionHistory = @{}
        return
    }

    $cleanVersionHistory = @{}
    foreach ($pluginName in @($Registry.versionHistory.Keys)) {
        if (-not (Test-ValidPluginName -Name $pluginName)) {
            Write-Warning "Removing invalid version history for plugin '$pluginName'"
            continue
        }

        $versionEntries = $Registry.versionHistory[$pluginName]
        if ($versionEntries -isnot [hashtable]) {
            continue
        }

        $cleanVersions = @{}
        foreach ($version in @($versionEntries.Keys)) {
            if (-not (Test-ValidVersion -Version $version)) {
                Write-Warning "Removing invalid version '$version' from history for '$pluginName'"
                continue
            }

            $entry = $versionEntries[$version]
            $releaseTag = Get-Field -Object $entry -Name "releaseTag"
            $releaseDate = Get-Field -Object $entry -Name "releaseDate"
            $parsedTag = ConvertFrom-MusoqPluginReleaseTag -ReleaseTag $releaseTag
            if (-not $parsedTag -or $parsedTag.Version -ne $version -or $parsedTag.PluginName -ne $pluginName) {
                Write-Warning "Removing version '$version' from history for '$pluginName' because releaseTag is invalid"
                continue
            }

            try {
                $releaseDate = ConvertTo-Iso8601String -Value $releaseDate
            }
            catch {
                Write-Warning "Removing version '$version' from history for '$pluginName' because releaseDate is invalid"
                continue
            }

            $cleanEntry = New-MusoqVersionHistoryEntry `
                -ReleaseTag $releaseTag `
                -ReleaseDate $releaseDate `
                -Version $version `
                -RuntimeCompatibility (Get-Field -Object $entry -Name "runtimeCompatibility") `
                -Artifacts (Get-Field -Object $entry -Name "artifacts")

            $cleanVersions[$version] = $cleanEntry
        }

        if ($cleanVersions.Count -gt 0) {
            $cleanVersionHistory[$pluginName] = $cleanVersions
        }
    }

    $Registry.versionHistory = $cleanVersionHistory
}

function New-RegistryPluginEntry {
    param(
        [Parameter(Mandatory=$true)]
        [string]$PluginName,
        [Parameter(Mandatory=$true)]
        [hashtable]$VersionEntries
    )

    $projects = Get-PluginProjects -PluginName $PluginName
    if ($projects.Count -eq 0) {
        Write-Warning "Skipping '$PluginName' because no matching project exists in this repository"
        return $null
    }

    $metadata = Get-ProjectMetadata -Project $projects[0]
    $projection = Get-MusoqPluginRegistryProjection -Versions $VersionEntries
    if (-not $projection) {
        return $null
    }

    $latestVersion = $projection.LatestVersion
    $latestInfo = $VersionEntries[$latestVersion]

    return @{
        name = $PluginName
        shortName = $metadata.ShortName
        description = $metadata.Description
        tags = $metadata.Tags
        latestVersion = $latestVersion
        releaseTag = $latestInfo.releaseTag
        releaseDate = $latestInfo.releaseDate
        latestStableVersion = $projection.LatestStableVersion
        latestPrereleaseVersion = $projection.LatestPrereleaseVersion
        channels = $projection.Channels
        artifacts = Get-ArtifactNames -ProjectName $PluginName
    }
}

function Rebuild-RegistryPlugins {
    param([hashtable]$Registry)

    $plugins = @()
    foreach ($pluginName in @($Registry.versionHistory.Keys | Sort-Object)) {
        $entry = New-RegistryPluginEntry -PluginName $pluginName -VersionEntries $Registry.versionHistory[$pluginName]
        if ($entry) {
            $plugins += $entry
        }
    }

    $Registry.plugins = $plugins
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
            $rawContent = Get-Content $PublishedMetadataPath -Raw
            if ($rawContent.Length -gt 10MB) {
                Write-Error "Metadata file too large (max 10MB)"
                exit 1
            }

            $PublishedPlugins = @($rawContent | ConvertFrom-Json -AsHashtable)
        }
        catch {
            Write-Error "Failed to parse metadata file: $_"
            exit 1
        }
    }
}

$validatedPlugins = @()
foreach ($plugin in $PublishedPlugins) {
    $validation = Test-ValidPluginData -Plugin $plugin
    if ($validation.IsValid) {
        $validatedPlugins += $plugin
    } else {
        Write-Warning "Skipping invalid plugin data for '$(Get-Field -Object $plugin -Name "Name")': $($validation.Errors -join ', ')"
    }
}
$PublishedPlugins = $validatedPlugins

$hasNewPlugins = $PublishedPlugins.Count -gt 0
if ($hasNewPlugins) {
    Write-Host "Updating plugin registry with $($PublishedPlugins.Count) new plugin(s)..." -ForegroundColor Cyan
} else {
    Write-Host "No new plugins to add. Checking if registry needs to be created or updated..." -ForegroundColor Cyan
}

$registryTag = $script:RegistryReleaseTag
$registryFile = $script:RegistryFileName
$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) "plugin-registry-$(Get-Random)"

try {
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
    $localRegistryPath = Join-Path $tempDir $registryFile

    $registryReleaseExists = $false
    gh release view $registryTag --repo $Repository 1>$null 2>$null
    if ($LASTEXITCODE -eq 0) { $registryReleaseExists = $true }

    $registry = $null
    if ($registryReleaseExists) {
        Write-Host "  Downloading existing registry..." -ForegroundColor Gray
        Push-Location $tempDir
        gh release download $registryTag --pattern $registryFile --repo $Repository 2>$null
        Pop-Location

        if (Test-Path $localRegistryPath) {
            $downloadedFileSize = (Get-Item $localRegistryPath).Length
            if ($downloadedFileSize -gt 50MB) {
                Write-Warning "Downloaded registry file is suspiciously large. Creating fresh registry."
            } elseif ($downloadedFileSize -eq 0) {
                Write-Warning "Downloaded registry file is empty. Creating fresh registry."
            } else {
                try {
                    $registry = (Get-Content $localRegistryPath -Raw) | ConvertFrom-Json -AsHashtable
                    Write-Host "  Loaded existing registry with $($registry.plugins.Count) plugin(s)" -ForegroundColor Gray
                }
                catch {
                    Write-Warning "Failed to parse existing registry JSON: $($_.Exception.Message). Creating fresh registry."
                }
            }
        }
    }

    if (-not $registry -or -not (Test-RegistryStructure -Registry $registry)) {
        Write-Host "  Creating new registry..." -ForegroundColor Gray
        $registry = New-EmptyRegistry -Repository $Repository
    }

    $registry.schemaVersion = "1.2"
    $registry.repository = "https://github.com/$Repository"
    Repair-VersionHistory -Registry $registry

    if (-not $hasNewPlugins -and -not $RegenerateFromReleases) {
        Write-Host "No new plugins provided. Will scan existing releases to ensure registry is up-to-date..." -ForegroundColor Cyan
        $RegenerateFromReleases = $true
    }

    if ($RegenerateFromReleases -or -not $registryReleaseExists -or $registry.versionHistory.Count -eq 0) {
        Write-Host "  Scanning existing releases to populate registry..." -ForegroundColor Cyan
        if ($RegenerateFromReleases) {
            $registry.versionHistory = @{}
        }

        $releasesJson = gh release list --repo $Repository --limit 1000 --json tagName 2>$null
        if ($LASTEXITCODE -eq 0 -and $releasesJson) {
            $releases = $releasesJson | ConvertFrom-Json
            foreach ($release in $releases) {
                $tag = $release.tagName
                if ($tag -eq $registryTag) {
                    continue
                }

                $pluginFromRelease = Get-PluginDataFromRelease -ReleaseTag $tag -Repository $Repository
                if (-not $pluginFromRelease) {
                    continue
                }

                $validation = Test-ValidPluginData -Plugin $pluginFromRelease
                if (-not $validation.IsValid) {
                    Write-Warning "Skipping invalid release '$tag': $($validation.Errors -join ', ')"
                    continue
                }

                Set-RegistryVersionHistory `
                    -Registry $registry `
                    -PluginName $pluginFromRelease.Name `
                    -Version $pluginFromRelease.Version `
                    -ReleaseTag $pluginFromRelease.ReleaseTag `
                    -ReleaseDate $pluginFromRelease.ReleaseDate `
                    -RuntimeCompatibility $pluginFromRelease.RuntimeCompatibility `
                    -ArtifactIntegrity $pluginFromRelease.ArtifactIntegrity

                Write-Host "    Found release: $tag" -ForegroundColor Gray
            }
        }
    }

    foreach ($plugin in $PublishedPlugins) {
        $releaseTag = Get-Field -Object $plugin -Name "ReleaseTag"
        $pluginFromRelease = Get-PluginDataFromRelease -ReleaseTag $releaseTag -Repository $Repository
        if (-not $pluginFromRelease) {
            Write-Warning "Skipping '$releaseTag' because authoritative GitHub release metadata could not be read."
            continue
        }

        $validation = Test-ValidPluginData -Plugin $pluginFromRelease
        if (-not $validation.IsValid) {
            Write-Warning "Skipping invalid authoritative release '$releaseTag': $($validation.Errors -join ', ')"
            continue
        }

        $pluginName = $pluginFromRelease.Name
        $version = $pluginFromRelease.Version
        $releaseDate = $pluginFromRelease.ReleaseDate

        Write-Host "  Adding $pluginName v$version to registry..." -ForegroundColor Gray

        Set-RegistryVersionHistory `
            -Registry $registry `
            -PluginName $pluginName `
            -Version $version `
            -ReleaseTag $pluginFromRelease.ReleaseTag `
            -ReleaseDate $releaseDate `
            -RuntimeCompatibility $pluginFromRelease.RuntimeCompatibility `
            -ArtifactIntegrity $pluginFromRelease.ArtifactIntegrity
    }

    Rebuild-RegistryPlugins -Registry $registry
    $registry.lastUpdated = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", [System.Globalization.CultureInfo]::InvariantCulture)

    if ($registry.plugins.Count -gt 1000) {
        Write-Error "Registry contains too many plugins ($($registry.plugins.Count)). Maximum allowed: 1000"
        exit 1
    }

    $registryJson = $registry | ConvertTo-Json -Depth 20
    $registryBytes = [System.Text.Encoding]::UTF8.GetByteCount($registryJson)
    if ($registryBytes -gt 50MB) {
        Write-Error "Registry file too large: $([math]::Round($registryBytes / 1MB, 2)) MB. Maximum allowed: 50 MB"
        exit 1
    }

    try {
        [void]($registryJson | ConvertFrom-Json)
    }
    catch {
        Write-Error "Generated registry JSON is invalid: $($_.Exception.Message)"
        exit 1
    }

    $registryJson | Set-Content -Path $localRegistryPath -Encoding UTF8
    Write-Host "  Registry updated with $($registry.plugins.Count) total plugin(s) ($([math]::Round($registryBytes / 1KB, 2)) KB)" -ForegroundColor Gray

    if ($registryReleaseExists) {
        gh release upload $registryTag $localRegistryPath --clobber --repo $Repository
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to upload registry to existing release"
            exit 1
        }
        Write-Host "  Updated registry in release '$registryTag'" -ForegroundColor Green
    } else {
        gh release create $registryTag --title "Plugin Registry" --notes "Auto-updated plugin registry for Musoq plugin discovery." $localRegistryPath --repo $Repository
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to create registry release"
            exit 1
        }
        Write-Host "  Created registry release '$registryTag'" -ForegroundColor Green
    }

    Write-Host "Plugin registry update complete!" -ForegroundColor Cyan
}
finally {
    if (Test-Path $tempDir) {
        Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
