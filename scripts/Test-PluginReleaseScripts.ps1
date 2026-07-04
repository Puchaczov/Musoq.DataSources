param()

$ErrorActionPreference = "Stop"

. "$PSScriptRoot/common/Plugin-Config.ps1"

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

Write-Host "Plugin release script tests passed." -ForegroundColor Green
