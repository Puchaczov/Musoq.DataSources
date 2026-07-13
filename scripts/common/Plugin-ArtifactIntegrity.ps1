$script:MusoqPluginReleaseMetadataFileName = "plugin-release-metadata.json"
$script:MusoqPluginReleaseMetadataFormatVersion = 1
$script:MusoqRequiredArtifactPlatforms = @(
    "windows-x64",
    "linux-x64",
    "macos-arm64",
    "alpine-x64"
)

. (Join-Path $PSScriptRoot "Plugin-Config.ps1")
. (Join-Path $PSScriptRoot "Plugin-Compatibility.ps1")

function Get-MusoqArtifactIntegrity {
    param(
        [Parameter(Mandatory=$true)]
        [string]$Path
    )

    $file = Get-Item -LiteralPath $Path -ErrorAction Stop
    if ($file.PSIsContainer) {
        throw "Artifact path must be a file: $Path"
    }

    return [ordered]@{
        sizeBytes = [int64]$file.Length
        md5 = (Get-FileHash -LiteralPath $file.FullName -Algorithm MD5).Hash.ToLowerInvariant()
        sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

function Assert-MusoqArtifactRecord {
    param(
        [Parameter(Mandatory=$true)]
        [object]$Record,
        [string]$Context = "Artifact"
    )

    $sizeBytes = Get-MusoqCompatibilityProperty -Value $Record -Name "sizeBytes"
    $md5 = [string](Get-MusoqCompatibilityProperty -Value $Record -Name "md5")
    $sha256 = [string](Get-MusoqCompatibilityProperty -Value $Record -Name "sha256")

    if ($null -eq $sizeBytes -or [int64]$sizeBytes -le 0) {
        throw "$Context sizeBytes must be positive."
    }
    if ($md5 -cnotmatch '^[0-9a-f]{32}$') {
        throw "$Context MD5 must be 32 lowercase hexadecimal characters."
    }
    if ($sha256 -cnotmatch '^[0-9a-f]{64}$') {
        throw "$Context SHA-256 must be 64 lowercase hexadecimal characters."
    }
}

function Assert-MusoqArtifactMatchesRecord {
    param(
        [Parameter(Mandatory=$true)]
        [string]$Path,
        [Parameter(Mandatory=$true)]
        [object]$Expected,
        [string]$Context = "Artifact"
    )

    Assert-MusoqArtifactRecord -Record $Expected -Context $Context
    $actual = Get-MusoqArtifactIntegrity -Path $Path
    foreach ($propertyName in @("sizeBytes", "md5", "sha256")) {
        $expectedValue = Get-MusoqCompatibilityProperty -Value $Expected -Name $propertyName
        if ($actual[$propertyName] -cne $expectedValue) {
            throw "$Context $propertyName mismatch. Expected '$expectedValue', got '$($actual[$propertyName])'."
        }
    }

    return $actual
}

function Get-MusoqPluginPackageCompatibility {
    param(
        [Parameter(Mandatory=$true)]
        [string]$PackagePath
    )

    $resolvedPackagePath = (Resolve-Path -LiteralPath $PackagePath).Path
    $tempDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "musoq-package-compatibility-$([guid]::NewGuid().ToString('N'))"
    $outerDirectory = Join-Path $tempDirectory "outer"
    $pluginDirectory = Join-Path $tempDirectory "plugin"
    try {
        New-Item -ItemType Directory -Force -Path $outerDirectory | Out-Null
        New-Item -ItemType Directory -Force -Path $pluginDirectory | Out-Null
        Expand-Archive -LiteralPath $resolvedPackagePath -DestinationPath $outerDirectory -Force
        $pluginZipPath = Join-Path $outerDirectory "Plugin.zip"
        if (-not (Test-Path -LiteralPath $pluginZipPath -PathType Leaf)) {
            throw "Plugin package is missing Plugin.zip: $resolvedPackagePath"
        }

        Expand-Archive -LiteralPath $pluginZipPath -DestinationPath $pluginDirectory -Force
        return Read-MusoqPluginCompatibilityManifest -Path (Join-Path $pluginDirectory $script:MusoqPluginCompatibilityFileName)
    }
    finally {
        if (Test-Path -LiteralPath $tempDirectory) {
            Remove-Item -LiteralPath $tempDirectory -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

function ConvertTo-MusoqCanonicalCompatibility {
    param(
        [Parameter(Mandatory=$true)]
        [object]$Compatibility
    )

    Assert-MusoqPluginCompatibility -Compatibility $Compatibility
    $hostPackages = Get-MusoqCompatibilityProperty -Value $Compatibility -Name "hostPackages"
    $schema = Get-MusoqCompatibilityProperty -Value $hostPackages -Name "Musoq.Schema"
    $plugins = Get-MusoqCompatibilityProperty -Value $hostPackages -Name "Musoq.Plugins"
    return New-MusoqPluginCompatibility `
        -TargetFramework ([string](Get-MusoqCompatibilityProperty -Value $Compatibility -Name "targetFramework")) `
        -SchemaVersion ([string](Get-MusoqCompatibilityProperty -Value $schema -Name "minimumVersionInclusive")) `
        -PluginsVersion ([string](Get-MusoqCompatibilityProperty -Value $plugins -Name "minimumVersionInclusive"))
}

function Test-MusoqCompatibilityEqual {
    param(
        [Parameter(Mandatory=$true)]
        [object]$Left,
        [Parameter(Mandatory=$true)]
        [object]$Right
    )

    $leftJson = ConvertTo-MusoqCanonicalCompatibility -Compatibility $Left | ConvertTo-Json -Depth 10 -Compress
    $rightJson = ConvertTo-MusoqCanonicalCompatibility -Compatibility $Right | ConvertTo-Json -Depth 10 -Compress
    return $leftJson -ceq $rightJson
}

function New-MusoqPluginReleaseMetadata {
    param(
        [Parameter(Mandatory=$true)]
        [string]$PluginName,
        [Parameter(Mandatory=$true)]
        [string]$Version,
        [Parameter(Mandatory=$true)]
        [string]$ReleaseTag,
        [Parameter(Mandatory=$true)]
        [System.Collections.IDictionary]$ArtifactPaths
    )

    if (-not (Test-ValidPluginName -Name $PluginName)) {
        throw "Invalid plugin name for release metadata: $PluginName"
    }
    $parsedVersion = ConvertTo-MusoqSemVer -Version $Version
    $expectedTag = New-MusoqPluginReleaseTag -Version $Version -PluginName $PluginName
    if ($ReleaseTag -ne $expectedTag) {
        throw "Release metadata tag '$ReleaseTag' does not match '$expectedTag'."
    }

    $artifacts = [ordered]@{}
    $runtimeCompatibility = $null
    foreach ($platform in $script:MusoqRequiredArtifactPlatforms) {
        if (-not $ArtifactPaths.Contains($platform)) {
            throw "Release metadata is missing artifact path for '$platform'."
        }

        $artifactPath = [string]$ArtifactPaths[$platform]
        $compatibility = Get-MusoqPluginPackageCompatibility -PackagePath $artifactPath
        if ($null -eq $runtimeCompatibility) {
            $runtimeCompatibility = ConvertTo-MusoqCanonicalCompatibility -Compatibility $compatibility
        }
        elseif (-not (Test-MusoqCompatibilityEqual -Left $runtimeCompatibility -Right $compatibility)) {
            throw "Artifact '$artifactPath' has compatibility metadata that differs from the other RIDs."
        }

        $integrity = Get-MusoqArtifactIntegrity -Path $artifactPath
        $artifacts[$platform] = [ordered]@{
            fileName = [System.IO.Path]::GetFileName($artifactPath)
            sizeBytes = $integrity.sizeBytes
            md5 = $integrity.md5
            sha256 = $integrity.sha256
        }
    }

    return [ordered]@{
        formatVersion = $script:MusoqPluginReleaseMetadataFormatVersion
        plugin = $PluginName
        version = $parsedVersion.Original
        releaseTag = $ReleaseTag
        channel = $parsedVersion.Channel
        isPrerelease = $parsedVersion.IsPrerelease
        runtimeCompatibility = $runtimeCompatibility
        artifacts = $artifacts
    }
}

function Assert-MusoqPluginReleaseMetadata {
    param(
        [Parameter(Mandatory=$true)]
        [object]$Metadata
    )

    $formatVersion = Get-MusoqCompatibilityProperty -Value $Metadata -Name "formatVersion"
    $plugin = [string](Get-MusoqCompatibilityProperty -Value $Metadata -Name "plugin")
    $version = [string](Get-MusoqCompatibilityProperty -Value $Metadata -Name "version")
    $releaseTag = [string](Get-MusoqCompatibilityProperty -Value $Metadata -Name "releaseTag")
    $channel = [string](Get-MusoqCompatibilityProperty -Value $Metadata -Name "channel")
    $isPrerelease = Get-MusoqCompatibilityProperty -Value $Metadata -Name "isPrerelease"
    $compatibility = Get-MusoqCompatibilityProperty -Value $Metadata -Name "runtimeCompatibility"
    $artifacts = Get-MusoqCompatibilityProperty -Value $Metadata -Name "artifacts"

    if ($formatVersion -ne $script:MusoqPluginReleaseMetadataFormatVersion) {
        throw "Unsupported plugin release metadata format version '$formatVersion'."
    }
    if ($null -eq $isPrerelease) {
        throw "Plugin release metadata isPrerelease is required."
    }
    if (-not (Test-ValidPluginName -Name $plugin)) {
        throw "Plugin release metadata has invalid plugin identity '$plugin'."
    }

    $parsedVersion = ConvertTo-MusoqSemVer -Version $version
    if ($releaseTag -ne (New-MusoqPluginReleaseTag -Version $version -PluginName $plugin)) {
        throw "Plugin release metadata has an invalid release tag '$releaseTag'."
    }
    if ($channel -cne $parsedVersion.Channel -or [bool]$isPrerelease -ne $parsedVersion.IsPrerelease) {
        throw "Plugin release metadata channel does not match version '$version'."
    }
    Assert-MusoqPluginCompatibility -Compatibility $compatibility
    if ($null -eq $artifacts) {
        throw "Plugin release metadata artifacts are required."
    }

    foreach ($platform in $script:MusoqRequiredArtifactPlatforms) {
        $record = Get-MusoqCompatibilityProperty -Value $artifacts -Name $platform
        if ($null -eq $record) {
            throw "Plugin release metadata is missing required platform '$platform'."
        }

        $fileName = [string](Get-MusoqCompatibilityProperty -Value $record -Name "fileName")
        $expectedFileName = "$plugin-$platform.zip"
        if ([string]::IsNullOrWhiteSpace($fileName) -or $fileName -match '[\\/]' -or $fileName -ne $expectedFileName) {
            throw "Plugin release metadata has invalid filename '$fileName' for '$platform'. Expected '$expectedFileName'."
        }

        Assert-MusoqArtifactRecord -Record $record -Context "$plugin $version $platform"
    }
}

function Read-MusoqPluginReleaseMetadata {
    param(
        [Parameter(Mandatory=$true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Plugin release metadata was not found: $Path"
    }

    try {
        $metadata = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -Depth 100
    }
    catch {
        throw "Plugin release metadata is malformed: $Path. $_"
    }

    Assert-MusoqPluginReleaseMetadata -Metadata $metadata
    return $metadata
}

function ConvertTo-MusoqCanonicalReleaseMetadataJson {
    param(
        [Parameter(Mandatory=$true)]
        [object]$Metadata
    )

    Assert-MusoqPluginReleaseMetadata -Metadata $Metadata
    $compatibility = ConvertTo-MusoqCanonicalCompatibility -Compatibility (Get-MusoqCompatibilityProperty -Value $Metadata -Name "runtimeCompatibility")
    $sourceArtifacts = Get-MusoqCompatibilityProperty -Value $Metadata -Name "artifacts"
    $artifacts = [ordered]@{}
    foreach ($platform in $script:MusoqRequiredArtifactPlatforms) {
        $source = Get-MusoqCompatibilityProperty -Value $sourceArtifacts -Name $platform
        $artifacts[$platform] = [ordered]@{
            fileName = [string](Get-MusoqCompatibilityProperty -Value $source -Name "fileName")
            sizeBytes = [int64](Get-MusoqCompatibilityProperty -Value $source -Name "sizeBytes")
            md5 = [string](Get-MusoqCompatibilityProperty -Value $source -Name "md5")
            sha256 = [string](Get-MusoqCompatibilityProperty -Value $source -Name "sha256")
        }
    }

    $canonical = [ordered]@{
        formatVersion = $script:MusoqPluginReleaseMetadataFormatVersion
        plugin = [string](Get-MusoqCompatibilityProperty -Value $Metadata -Name "plugin")
        version = [string](Get-MusoqCompatibilityProperty -Value $Metadata -Name "version")
        releaseTag = [string](Get-MusoqCompatibilityProperty -Value $Metadata -Name "releaseTag")
        channel = [string](Get-MusoqCompatibilityProperty -Value $Metadata -Name "channel")
        isPrerelease = [bool](Get-MusoqCompatibilityProperty -Value $Metadata -Name "isPrerelease")
        runtimeCompatibility = $compatibility
        artifacts = $artifacts
    }
    return ($canonical | ConvertTo-Json -Depth 20)
}

function Write-MusoqImmutablePluginReleaseMetadata {
    param(
        [Parameter(Mandatory=$true)]
        [object]$Metadata,
        [Parameter(Mandatory=$true)]
        [string]$Path
    )

    $json = ConvertTo-MusoqCanonicalReleaseMetadataJson -Metadata $Metadata
    if (Test-Path -LiteralPath $Path) {
        $existing = Read-MusoqPluginReleaseMetadata -Path $Path
        $existingJson = ConvertTo-MusoqCanonicalReleaseMetadataJson -Metadata $existing
        if ($existingJson -cne $json) {
            throw "Plugin release metadata is immutable and already contains different artifact hashes: $Path"
        }

        return
    }

    [System.IO.File]::WriteAllText($Path, $json, [System.Text.UTF8Encoding]::new($false))
}
