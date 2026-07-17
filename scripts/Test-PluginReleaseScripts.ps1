param()

$ErrorActionPreference = "Stop"

. "$PSScriptRoot/common/Plugin-Config.ps1"
. "$PSScriptRoot/common/Plugin-Compatibility.ps1"
. "$PSScriptRoot/common/Plugin-ArtifactIntegrity.ps1"
. "$PSScriptRoot/common/CommandLineModule-Packaging.ps1"

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Assert-Equal {
    param(
        [object]$Expected,
        [object]$Actual,
        [string]$Message
    )

    if ($Expected -ne $Actual) {
        throw "$Message Expected '$Expected', got '$Actual'."
    }
}

function Assert-Throws {
    param(
        [scriptblock]$Action,
        [string]$Message
    )

    try {
        & $Action
    }
    catch {
        return
    }

    throw $Message
}

function Test-SemVerValidation {
    Assert-True (Test-ValidVersion "1.2.3") "Stable SemVer should be valid."
    Assert-True (Test-ValidVersion "1.2.3-alpha") "Alpha SemVer should be valid."
    Assert-True (Test-ValidVersion "1.2.3-alpha.1") "Alpha dotted SemVer should be valid."
    Assert-True (Test-ValidVersion "1.2.3-beta.1") "Beta dotted SemVer should be valid."
    Assert-True (Test-ValidVersion "1.2.3-rc.1") "RC dotted SemVer should be valid."
    Assert-True (-not (Test-ValidVersion "1.2")) "Incomplete version should be invalid."
    Assert-True (-not (Test-ValidVersion "1.2.3+build.1")) "Build metadata is intentionally unsupported."
    Assert-True (-not (Test-ValidVersion "1.2.3-alpha.01")) "Leading-zero prerelease numeric identifiers should be invalid."
}

function Test-SemVerOrdering {
    Assert-True ((Compare-MusoqSemVer "1.2.3-alpha.1" "1.2.3-alpha.2") -lt 0) "alpha.1 should sort before alpha.2."
    Assert-True ((Compare-MusoqSemVer "1.2.3-beta.1" "1.2.3-alpha.9") -gt 0) "beta should sort after alpha."
    Assert-True ((Compare-MusoqSemVer "1.2.3-rc.1" "1.2.3") -lt 0) "rc should sort before stable."
    Assert-True ((Compare-MusoqSemVer "1.2.4-alpha.1" "1.2.3") -gt 0) "higher core version prerelease should sort after lower stable."
}

function Test-ReleaseTagParsing {
    $tag = New-MusoqPluginReleaseTag -Version "8.4.9-alpha.1" -PluginName "Musoq.DataSources.Json"
    Assert-Equal "8.4.9-alpha.1-Musoq.DataSources.Json" $tag "Release tag should preserve prerelease suffix."

    $parsed = ConvertFrom-MusoqPluginReleaseTag -ReleaseTag $tag
    Assert-Equal "8.4.9-alpha.1" $parsed.Version "Parsed tag should expose exact version."
    Assert-Equal "Musoq.DataSources.Json" $parsed.PluginName "Parsed tag should expose plugin name."
    Assert-Equal "alpha" $parsed.Channel "Parsed tag should expose prerelease channel."
    Assert-True $parsed.IsPrerelease "Parsed tag should mark prerelease."
}

function Test-RegistryProjectionWithStable {
    $versions = @{
        "8.4.8" = New-MusoqVersionHistoryEntry -ReleaseTag "8.4.8-Musoq.DataSources.Json" -ReleaseDate "2026-06-20T10:15:00Z" -Version "8.4.8"
        "8.4.9-alpha.1" = New-MusoqVersionHistoryEntry -ReleaseTag "8.4.9-alpha.1-Musoq.DataSources.Json" -ReleaseDate "2026-06-28T12:00:00Z" -Version "8.4.9-alpha.1"
        "8.4.9-beta.1" = New-MusoqVersionHistoryEntry -ReleaseTag "8.4.9-beta.1-Musoq.DataSources.Json" -ReleaseDate "2026-06-29T12:00:00Z" -Version "8.4.9-beta.1"
    }

    $projection = Get-MusoqPluginRegistryProjection -Versions $versions
    Assert-Equal "8.4.8" $projection.LatestVersion "latestVersion should remain stable when stable exists."
    Assert-Equal "8.4.8" $projection.LatestStableVersion "latestStableVersion should point to stable."
    Assert-Equal "8.4.9-beta.1" $projection.LatestPrereleaseVersion "latestPrereleaseVersion should be highest prerelease."
    Assert-Equal "8.4.9-alpha.1" $projection.Channels.alpha.version "alpha channel should point to latest alpha."
    Assert-Equal "8.4.9-beta.1" $projection.Channels.beta.version "beta channel should point to latest beta."
}

function Test-RegistryProjectionPrereleaseOnly {
    $versions = @{
        "8.4.9-alpha.1" = New-MusoqVersionHistoryEntry -ReleaseTag "8.4.9-alpha.1-Musoq.DataSources.Json" -ReleaseDate "2026-06-28T12:00:00Z" -Version "8.4.9-alpha.1"
        "8.4.9-beta.1" = New-MusoqVersionHistoryEntry -ReleaseTag "8.4.9-beta.1-Musoq.DataSources.Json" -ReleaseDate "2026-06-29T12:00:00Z" -Version "8.4.9-beta.1"
    }

    $projection = Get-MusoqPluginRegistryProjection -Versions $versions
    Assert-Equal "8.4.9-beta.1" $projection.LatestVersion "Prerelease-only plugins should use highest prerelease for discoverability."
    Assert-True ($null -eq $projection.LatestStableVersion) "Prerelease-only plugins should not have latestStableVersion."
    Assert-Equal "8.4.9-beta.1" $projection.LatestPrereleaseVersion "Prerelease-only latestPrereleaseVersion should be highest prerelease."
}

function Test-RollbackProjectionRecompute {
    $versions = @{
        "8.4.8" = New-MusoqVersionHistoryEntry -ReleaseTag "8.4.8-Musoq.DataSources.Json" -ReleaseDate "2026-06-20T10:15:00Z" -Version "8.4.8"
        "8.4.9-alpha.1" = New-MusoqVersionHistoryEntry -ReleaseTag "8.4.9-alpha.1-Musoq.DataSources.Json" -ReleaseDate "2026-06-28T12:00:00Z" -Version "8.4.9-alpha.1"
    }

    $versions.Remove("8.4.9-alpha.1")
    $projection = Get-MusoqPluginRegistryProjection -Versions $versions
    Assert-Equal "8.4.8" $projection.LatestVersion "Rollback recompute should keep remaining stable latest."
    Assert-True ($null -eq $projection.LatestPrereleaseVersion) "Rollback recompute should clear removed prerelease latest."
    Assert-True (-not $projection.Channels.ContainsKey("alpha")) "Rollback recompute should remove empty alpha channel."
}

function Test-PackageVersionTextPreservesPrerelease {
    $tempDir = Join-Path ([System.IO.Path]::GetTempPath()) "musoq-release-test-$([guid]::NewGuid().ToString('N'))"
    try {
        New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
        $versionPath = Join-Path $tempDir "Version.txt"
        Set-Content -Path $versionPath -Value "1.2.3-alpha.1" -NoNewline
        Assert-Equal "1.2.3-alpha.1" ((Get-Content $versionPath -Raw).Trim()) "Version.txt should preserve prerelease suffix exactly."
    }
    finally {
        if (Test-Path $tempDir) {
            Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

function Test-SyntheticRegistryJsonShape {
    $versions = @{
        "8.4.8" = New-MusoqVersionHistoryEntry -ReleaseTag "8.4.8-Musoq.DataSources.Json" -ReleaseDate "2026-06-20T10:15:00Z" -Version "8.4.8"
        "8.4.9-alpha.1" = New-MusoqVersionHistoryEntry -ReleaseTag "8.4.9-alpha.1-Musoq.DataSources.Json" -ReleaseDate "2026-06-28T12:00:00Z" -Version "8.4.9-alpha.1"
    }
    $projection = Get-MusoqPluginRegistryProjection -Versions $versions

    $registry = @{
        schemaVersion = "1.1"
        lastUpdated = "2026-06-28T12:00:00Z"
        repository = "https://github.com/Example/Musoq.DataSources.External"
        plugins = @(
            @{
                name = "Musoq.DataSources.Json"
                shortName = "json"
                description = "JSON datasource for Musoq."
                tags = @("json", "files", "datasource")
                latestVersion = $projection.LatestVersion
                releaseTag = $projection.ReleaseTag
                releaseDate = $projection.ReleaseDate
                latestStableVersion = $projection.LatestStableVersion
                latestPrereleaseVersion = $projection.LatestPrereleaseVersion
                channels = $projection.Channels
                artifacts = Get-ArtifactNames -ProjectName "Musoq.DataSources.Json"
            }
        )
        versionHistory = @{
            "Musoq.DataSources.Json" = $versions
        }
    }

    $json = $registry | ConvertTo-Json -Depth 20
    $parsed = $json | ConvertFrom-Json
    $rootReleaseDate = if ($parsed.plugins[0].releaseDate -is [DateTime]) {
        $parsed.plugins[0].releaseDate.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", [System.Globalization.CultureInfo]::InvariantCulture)
    } else {
        [string]$parsed.plugins[0].releaseDate
    }

    Assert-Equal "1.1" $parsed.schemaVersion "Synthetic registry should use schema 1.1."
    Assert-Equal "8.4.8" $parsed.plugins[0].latestVersion "latestVersion should stay stable in serialized JSON."
    Assert-Equal "8.4.8-Musoq.DataSources.Json" $parsed.plugins[0].releaseTag "Root releaseTag should stay stable in serialized JSON."
    Assert-Equal "2026-06-20T10:15:00Z" $rootReleaseDate "Root releaseDate should stay stable in serialized JSON."
    Assert-Equal "8.4.8" $parsed.plugins[0].latestStableVersion "latestStableVersion should serialize."
    Assert-Equal "8.4.9-alpha.1" $parsed.plugins[0].latestPrereleaseVersion "latestPrereleaseVersion should serialize."
    Assert-Equal "8.4.8" $parsed.plugins[0].channels.stable.version "stable channel should serialize."
    Assert-Equal "8.4.9-alpha.1" $parsed.plugins[0].channels.alpha.version "alpha channel should serialize."
    Assert-Equal $false $parsed.versionHistory.'Musoq.DataSources.Json'.'8.4.8'.isPrerelease "Stable history entry should serialize prerelease flag."
    Assert-Equal "alpha" $parsed.versionHistory.'Musoq.DataSources.Json'.'8.4.9-alpha.1'.channel "Prerelease history channel should serialize."
}

function Get-CurrentJsonReleaseTag {
    [xml]$project = Get-Content "$PSScriptRoot/../Musoq.DataSources.Json/Musoq.DataSources.Json.csproj"
    $version = [string](@($project.Project.PropertyGroup | Where-Object { $_.Version })[0].Version)
    return "$version-Musoq.DataSources.Json"
}

function Test-DatasourceReleaseValidation {
    $tag = Get-CurrentJsonReleaseTag
    $summary = & "$PSScriptRoot/release/Validate-Release.ps1" -Tag $tag -Json | ConvertFrom-Json
    Assert-Equal $tag $summary.tag "Datasource release validation should preserve tag."
    Assert-Equal "Musoq.DataSources.Json" $summary.packageId "Datasource release validation should resolve package id."
    Assert-Equal "alpha" $summary.channel "Datasource release validation should resolve channel."

    Assert-Throws {
        & "$PSScriptRoot/release/Validate-Release.ps1" -Tag "3.0.0-alpha.1-Musoq.DataSources.AsyncRowsSource" | Out-Null
    } "Datasource release validation should reject helper packages."

    Assert-Throws {
        & "$PSScriptRoot/release/Validate-Release.ps1" -Tag "0.0.1-Musoq.DataSources.Json" | Out-Null
    } "Datasource release validation should reject version mismatches."
}

function Test-BatchDatasourceReleaseResolution {
    $tag = Get-CurrentJsonReleaseTag
    $jsonRelease = & "$PSScriptRoot/release/Resolve-BatchRelease.ps1" -Selection "json" -Json | ConvertFrom-Json
    Assert-Equal 1 @($jsonRelease).Count "Batch release selection should resolve one datasource."
    Assert-Equal $tag $jsonRelease[0].tag "Batch release selection should use current project version."

    $allReleases = & "$PSScriptRoot/release/Resolve-BatchRelease.ps1" -Selection "All" -Json | ConvertFrom-Json
    Assert-True (@($allReleases).Count -ge 1) "Batch release selection should resolve all datasource packages."

    Assert-Throws {
        & "$PSScriptRoot/release/Resolve-BatchRelease.ps1" -Selection "Musoq.DataSources.AsyncRowsSource" -Json | Out-Null
    } "Batch release selection should reject helper packages."
}

function Test-PluginCompatibilityManifestGeneration {
    $compatibility = New-MusoqPluginCompatibility `
        -TargetFramework "net10.0" `
        -SchemaVersion "17.0.3-alpha.2" `
        -PluginsVersion "17.0.3-alpha.2"

    Assert-Equal 1 $compatibility.formatVersion "Compatibility format should be versioned."
    Assert-Equal "musoq-runtime-v2" $compatibility.runtimeFamily "Runtime family should identify runtime-v2."
    Assert-Equal "net10.0" $compatibility.targetFramework "Target framework should come from evaluated project data."
    Assert-Equal "17.0.3-alpha.2" $compatibility.hostPackages.'Musoq.Schema'.minimumVersionInclusive "Schema minimum should be the evaluated package version."
    Assert-Equal "18.0.0" $compatibility.hostPackages.'Musoq.Schema'.maximumVersionExclusive "Schema maximum should be the next major."
    Assert-Equal "17.0.3-alpha.2" $compatibility.hostPackages.'Musoq.Plugins'.minimumVersionInclusive "Plugins minimum should be the evaluated package version."
    Assert-Equal "18.0.0" $compatibility.hostPackages.'Musoq.Plugins'.maximumVersionExclusive "Plugins maximum should be the next major."

    Assert-Throws {
        New-MusoqPluginCompatibility -TargetFramework "net8.0" -SchemaVersion "17.0.3-alpha.2" -PluginsVersion "17.0.3-alpha.2" | Out-Null
    } "Unsupported target frameworks must fail packaging."
    Assert-Throws {
        New-MusoqPluginCompatibility -TargetFramework "net10.0" -SchemaVersion "invalid" -PluginsVersion "17.0.3-alpha.2" | Out-Null
    } "Malformed ABI versions must fail packaging."
    Assert-Throws {
        New-MusoqPluginCompatibility -TargetFramework "net10.0" -SchemaVersion "17.0.3-alpha.2" -PluginsVersion "17.0.1" | Out-Null
    } "Inconsistent ABI package versions must fail packaging."

    $systemProject = Join-Path $PSScriptRoot "../Musoq.DataSources.System/Musoq.DataSources.System.csproj"
    $evaluated = Get-MusoqPluginCompatibility -ProjectPath $systemProject
    Assert-Equal "17.0.3-alpha.2" $evaluated.hostPackages.'Musoq.Schema'.minimumVersionInclusive "Evaluated Schema version should be used."
    Assert-Equal "17.0.3-alpha.2" $evaluated.hostPackages.'Musoq.Plugins'.minimumVersionInclusive "Evaluated Plugins version should be used."
}

function Test-PluginArtifactIntegrityMetadata {
    $tempDir = Join-Path ([System.IO.Path]::GetTempPath()) "musoq-integrity-test-$([guid]::NewGuid().ToString('N'))"
    try {
        New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
        $compatibility = New-MusoqPluginCompatibility `
            -TargetFramework "net10.0" `
            -SchemaVersion "17.0.3-alpha.2" `
            -PluginsVersion "17.0.3-alpha.2"
        $compatibilityJson = ConvertTo-MusoqPluginCompatibilityJson -Compatibility $compatibility
        $artifactPaths = [ordered]@{}

        foreach ($platform in $script:MusoqRequiredArtifactPlatforms) {
            $packageDirectory = Join-Path $tempDir "package-$platform"
            $pluginDirectory = Join-Path $tempDir "plugin-$platform"
            New-Item -ItemType Directory -Path $packageDirectory, $pluginDirectory -Force | Out-Null
            [System.IO.File]::WriteAllText(
                (Join-Path $pluginDirectory $script:MusoqPluginCompatibilityFileName),
                $compatibilityJson,
                [System.Text.UTF8Encoding]::new($false))
            Set-Content -LiteralPath (Join-Path $pluginDirectory "payload-$platform.txt") -Value $platform -NoNewline
            Compress-Archive -Path (Join-Path $pluginDirectory "*") -DestinationPath (Join-Path $packageDirectory "Plugin.zip") -Force
            Set-Content -LiteralPath (Join-Path $packageDirectory "Platform.txt") -Value $platform -NoNewline

            $artifactPath = Join-Path $tempDir "Musoq.DataSources.System-$platform.zip"
            Compress-Archive -Path (Join-Path $packageDirectory "*") -DestinationPath $artifactPath -Force
            $artifactPaths[$platform] = $artifactPath
        }

        $metadata = New-MusoqPluginReleaseMetadata `
            -PluginName "Musoq.DataSources.System" `
            -Version "8.0.3-alpha.1" `
            -ReleaseTag "8.0.3-alpha.1-Musoq.DataSources.System" `
            -ArtifactPaths $artifactPaths
        Assert-MusoqPluginReleaseMetadata -Metadata $metadata
        Assert-Equal 4 $metadata.artifacts.Count "Release metadata should contain all required platforms."

        foreach ($platform in $script:MusoqRequiredArtifactPlatforms) {
            $record = $metadata.artifacts[$platform]
            Assert-True ($record.sizeBytes -gt 0) "$platform should record artifact size."
            Assert-True ($record.md5 -cmatch '^[0-9a-f]{32}$') "$platform should record lowercase MD5."
            Assert-True ($record.sha256 -cmatch '^[0-9a-f]{64}$') "$platform should record lowercase SHA-256."
            Assert-MusoqArtifactMatchesRecord -Path $artifactPaths[$platform] -Expected $record -Context $platform | Out-Null
        }

        $metadataPath = Join-Path $tempDir $script:MusoqPluginReleaseMetadataFileName
        Write-MusoqImmutablePluginReleaseMetadata -Metadata $metadata -Path $metadataPath
        $roundTripped = Read-MusoqPluginReleaseMetadata -Path $metadataPath
        Assert-Equal "Musoq.DataSources.System" $roundTripped.plugin "Release metadata should preserve plugin identity."

        $changedMetadata = (ConvertTo-MusoqCanonicalReleaseMetadataJson -Metadata $metadata) | ConvertFrom-Json -Depth 100
        $changedMetadata.artifacts.'windows-x64'.md5 = "00000000000000000000000000000000"
        Assert-Throws {
            Write-MusoqImmutablePluginReleaseMetadata -Metadata $changedMetadata -Path $metadataPath
        } "An existing plugin/version/platform hash record must be immutable."

        Add-Content -LiteralPath $artifactPaths['windows-x64'] -Value "corruption" -NoNewline
        Assert-Throws {
            Assert-MusoqArtifactMatchesRecord `
                -Path $artifactPaths['windows-x64'] `
                -Expected $metadata.artifacts['windows-x64'] `
                -Context "corrupted artifact" | Out-Null
        } "Artifact verification must reject changed bytes."

        $publishScript = Get-Content -LiteralPath (Join-Path $PSScriptRoot "release/Publish-Release.ps1") -Raw
        Assert-True ($publishScript -notmatch '--clobber') "Datasource publishing must never clobber an existing release asset."
    }
    finally {
        if (Test-Path -LiteralPath $tempDir) {
            Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

function Test-Registry12RuntimeMetadataContract {
    $compatibility = New-MusoqPluginCompatibility `
        -TargetFramework "net10.0" `
        -SchemaVersion "17.0.3-alpha.2" `
        -PluginsVersion "17.0.3-alpha.2"
    $artifactIntegrity = [ordered]@{}
    foreach ($platform in $script:MusoqRequiredArtifactPlatforms) {
        $artifactIntegrity[$platform] = [ordered]@{
            fileName = "Musoq.DataSources.System-$platform.zip"
            sizeBytes = 123
            md5 = "0123456789abcdef0123456789abcdef"
            sha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
        }
    }

    $legacyStable = New-MusoqVersionHistoryEntry `
        -ReleaseTag "8.0.0-Musoq.DataSources.System" `
        -ReleaseDate "2026-06-20T12:00:00Z" `
        -Version "8.0.0"
    $compatibleAlpha = New-MusoqVersionHistoryEntry `
        -ReleaseTag "8.0.3-alpha.1-Musoq.DataSources.System" `
        -ReleaseDate "2026-07-20T12:00:00Z" `
        -Version "8.0.3-alpha.1" `
        -RuntimeCompatibility $compatibility `
        -Artifacts $artifactIntegrity

    Assert-True (-not $legacyStable.ContainsKey('runtimeCompatibility')) "Legacy entries must remain visible without inferred compatibility."
    Assert-True (-not $legacyStable.ContainsKey('artifacts')) "Legacy entries must remain visible without inferred hashes."
    Assert-Equal "musoq-runtime-v2" $compatibleAlpha.runtimeCompatibility.runtimeFamily "Compatible history should carry runtime metadata."
    Assert-Equal 4 $compatibleAlpha.artifacts.Count "Compatible history should carry all platform hashes."

    $versions = @{
        "8.0.0" = $legacyStable
        "8.0.3-alpha.1" = $compatibleAlpha
    }
    $projection = Get-MusoqPluginRegistryProjection -Versions $versions
    $registry = [ordered]@{
        schemaVersion = "1.2"
        lastUpdated = "2026-07-20T12:00:00Z"
        repository = "https://github.com/Puchaczov/Musoq.DataSources"
        plugins = @([ordered]@{
            name = "Musoq.DataSources.System"
            shortName = "system"
            latestVersion = $projection.LatestVersion
            releaseTag = $projection.ReleaseTag
            releaseDate = $projection.ReleaseDate
            latestStableVersion = $projection.LatestStableVersion
            latestPrereleaseVersion = $projection.LatestPrereleaseVersion
            channels = $projection.Channels
            artifacts = Get-ArtifactNames -ProjectName "Musoq.DataSources.System"
        })
        versionHistory = @{
            "Musoq.DataSources.System" = $versions
        }
    }

    $parsed = ($registry | ConvertTo-Json -Depth 30) | ConvertFrom-Json -Depth 100
    Assert-Equal "1.2" $parsed.schemaVersion "Runtime metadata registry should use schema 1.2."
    Assert-Equal "8.0.0" $parsed.plugins[0].latestVersion "Legacy top-level resolution should remain on stable."
    Assert-Equal "8.0.0-Musoq.DataSources.System" $parsed.plugins[0].releaseTag "Legacy top-level release tag should remain stable."
    Assert-Equal "musoq-runtime-v2" $parsed.versionHistory.'Musoq.DataSources.System'.'8.0.3-alpha.1'.runtimeCompatibility.runtimeFamily "Compatible version metadata should serialize."
    Assert-Equal 123 $parsed.versionHistory.'Musoq.DataSources.System'.'8.0.3-alpha.1'.artifacts.'windows-x64'.sizeBytes "Artifact integrity should serialize."

    $updateScript = Get-Content -LiteralPath (Join-Path $PSScriptRoot "Update-PluginRegistry.ps1") -Raw
    Assert-True ($updateScript -match 'Read-MusoqPluginReleaseMetadata') "Registry regeneration must read immutable release metadata."
    Assert-True ($updateScript -notmatch 'Get-MusoqPluginCompatibility\s+-ProjectPath') "Registry regeneration must not infer historical compatibility from current projects."
}

function Test-RuntimeV2Alpha1ReleaseTrain {
    $registryPath = Join-Path $PSScriptRoot "release/packages.json"
    $registry = Get-Content -LiteralPath $registryPath -Raw | ConvertFrom-Json
    $packages = @($registry.packages)
    Assert-Equal 15 $packages.Count "Runtime-v2 release train should contain all 15 datasource packages."

    foreach ($package in $packages) {
        $version = [string]$package.version
        Assert-True ($version -match '-alpha\.1$') "$($package.packageId) should be pinned to alpha.1 in packages.json."
        $projectPath = Join-Path $PSScriptRoot "../$($package.projectPath)"
        [xml]$project = Get-Content -LiteralPath $projectPath
        $projectVersion = [string](@($project.Project.PropertyGroup | Where-Object { $_.Version })[0].Version)
        Assert-Equal $version $projectVersion "$($package.packageId) project and release registry versions should match."
    }

    $roslyn = @($packages | Where-Object { $_.packageId -eq 'Musoq.DataSources.Roslyn' })
    Assert-Equal 1 $roslyn.Count "Runtime-v2 release train should contain exactly one Roslyn package."
    Assert-Equal "3.0.4-alpha.1" ([string]$roslyn[0].version) "Roslyn should use the command-module release version."
}

function Test-RoslynReleaseWorkflowGates {
    $workflowPath = Join-Path $PSScriptRoot "../.github/workflows/release-datasource.yml"
    $workflow = Get-Content -LiteralPath $workflowPath -Raw

    Assert-True ($workflow -match 'release_command_line_test_project_path') "Datasource releases should discover companion command-line module tests."
    Assert-True ($workflow -match 'dotnet test \$env:RELEASE_COMMAND_LINE_TEST_PROJECT_PATH') "Datasource releases should execute companion command-line module tests."
    Assert-True ($workflow -match '\./scripts/Test-PluginReleaseScripts\.ps1') "Datasource releases should execute release-script tests."
    Assert-True ($workflow -match 'Pack four-RID release artifacts') "Datasource releases should identify four-RID packaging as a required gate."
    Assert-True ($workflow -match 'Smoke test four-RID release artifacts') "Datasource releases should identify four-RID smoke verification as a required gate."
}

function Test-CommandLineModuleManifestContract {
    $tempDir = Join-Path ([System.IO.Path]::GetTempPath()) "musoq-command-module-test-$([guid]::NewGuid().ToString('N'))"
    try {
        New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $tempDir "Example.CommandLineArguments.dll") -Value "assembly" -NoNewline
        Set-Content -LiteralPath (Join-Path $tempDir "Example.CommandLineArguments.pdb") -Value "symbols" -NoNewline
        $definition = [PSCustomObject]@{
            formatVersion = 1
            moduleId = "example.command-line"
            moduleVersion = "1.2.3"
            entryAssembly = "Example.CommandLineArguments.dll"
            framework = [PSCustomObject]@{
                packageId = "Musoq.CommandLine"
                versionRange = "[0.0.1,0.1.0)"
            }
            requiredInvocationItems = @([PSCustomObject]@{
                name = "example.transport.v1"
                contract = "transport-v1"
            })
        }

        $written = Write-MusoqCommandLineModuleManifest -Definition $definition -ModuleDirectory $tempDir
        Assert-Equal 2 @($written.files).Count "Manifest should hash the complete module closure."
        $read = Read-MusoqCommandLineModuleManifest -ModuleDirectory $tempDir
        Assert-Equal "example.command-line" $read.moduleId "Manifest should preserve module identity."
        Assert-Equal "[0.0.1,0.1.0)" $read.framework.versionRange "Manifest should preserve the framework range."

        Add-Content -LiteralPath (Join-Path $tempDir "Example.CommandLineArguments.dll") -Value "corruption" -NoNewline
        Assert-Throws {
            Read-MusoqCommandLineModuleManifest -ModuleDirectory $tempDir | Out-Null
        } "Manifest validation should reject changed module bytes."

        $roslynProject = Join-Path $PSScriptRoot "../Musoq.DataSources.Roslyn.CommandLineArguments/Musoq.DataSources.Roslyn.CommandLineArguments.csproj"
        $roslyn = Read-MusoqCommandLineModuleDefinition -ProjectPath $roslynProject
        Assert-Equal "musoq.datasource.roslyn" $roslyn.moduleId "Roslyn should declare its stable module ID."
        Assert-Equal "musoq.datasource.http-request.v1" $roslyn.requiredInvocationItems[0].name "Roslyn should declare its callback requirement."
    }
    finally {
        if (Test-Path -LiteralPath $tempDir) {
            Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

Test-SemVerValidation
Test-SemVerOrdering
Test-ReleaseTagParsing
Test-RegistryProjectionWithStable
Test-RegistryProjectionPrereleaseOnly
Test-RollbackProjectionRecompute
Test-PackageVersionTextPreservesPrerelease
Test-SyntheticRegistryJsonShape
Test-DatasourceReleaseValidation
Test-BatchDatasourceReleaseResolution
Test-PluginCompatibilityManifestGeneration
Test-PluginArtifactIntegrityMetadata
Test-Registry12RuntimeMetadataContract
Test-RuntimeV2Alpha1ReleaseTrain
Test-RoslynReleaseWorkflowGates
Test-CommandLineModuleManifestContract

Write-Host "Plugin release script tests passed." -ForegroundColor Green
