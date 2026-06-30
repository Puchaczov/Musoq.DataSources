$script:MusoqSemVerPattern = '^(?<Major>0|[1-9]\d*)\.(?<Minor>0|[1-9]\d*)\.(?<Patch>0|[1-9]\d*)(?:-(?<Prerelease>[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?$'

function ConvertTo-MusoqSemVer {
    param(
        [Parameter(Mandatory=$true)]
        [string]$Version
    )

    if ([string]::IsNullOrWhiteSpace($Version)) {
        throw "Version cannot be empty."
    }

    $trimmed = $Version.Trim()
    if ($trimmed -notmatch $script:MusoqSemVerPattern) {
        throw "Invalid SemVer version: $Version"
    }

    $major = [int]$Matches['Major']
    $minor = [int]$Matches['Minor']
    $patch = [int]$Matches['Patch']
    $prerelease = $Matches['Prerelease']
    $identifiers = @()
    if (-not [string]::IsNullOrWhiteSpace($prerelease)) {
        $identifiers = @($prerelease -split '\.')
        foreach ($identifier in $identifiers) {
            if ($identifier -match '^\d+$' -and $identifier.Length -gt 1 -and $identifier.StartsWith('0')) {
                throw "Invalid SemVer prerelease numeric identifier with leading zero: $Version"
            }
        }
    }

    [PSCustomObject]@{
        Original = $trimmed
        Major = $major
        Minor = $minor
        Patch = $patch
        Prerelease = $prerelease
        PrereleaseIdentifiers = $identifiers
        IsPrerelease = -not [string]::IsNullOrWhiteSpace($prerelease)
        Channel = if ([string]::IsNullOrWhiteSpace($prerelease)) { "stable" } else { $identifiers[0].ToLowerInvariant() }
    }
}

function Test-MusoqSemVer {
    param([string]$Version)

    try {
        [void](ConvertTo-MusoqSemVer -Version $Version)
        return $true
    }
    catch {
        return $false
    }
}

function Compare-MusoqSemVer {
    param(
        [Parameter(Mandatory=$true)]
        [string]$Left,
        [Parameter(Mandatory=$true)]
        [string]$Right
    )

    $leftVersion = ConvertTo-MusoqSemVer -Version $Left
    $rightVersion = ConvertTo-MusoqSemVer -Version $Right

    foreach ($part in @("Major", "Minor", "Patch")) {
        if ($leftVersion.$part -lt $rightVersion.$part) { return -1 }
        if ($leftVersion.$part -gt $rightVersion.$part) { return 1 }
    }

    if (-not $leftVersion.IsPrerelease -and -not $rightVersion.IsPrerelease) { return 0 }
    if (-not $leftVersion.IsPrerelease -and $rightVersion.IsPrerelease) { return 1 }
    if ($leftVersion.IsPrerelease -and -not $rightVersion.IsPrerelease) { return -1 }

    $max = [Math]::Max($leftVersion.PrereleaseIdentifiers.Count, $rightVersion.PrereleaseIdentifiers.Count)
    for ($index = 0; $index -lt $max; $index++) {
        if ($index -ge $leftVersion.PrereleaseIdentifiers.Count) { return -1 }
        if ($index -ge $rightVersion.PrereleaseIdentifiers.Count) { return 1 }

        $leftIdentifier = $leftVersion.PrereleaseIdentifiers[$index]
        $rightIdentifier = $rightVersion.PrereleaseIdentifiers[$index]
        $leftNumeric = $leftIdentifier -match '^\d+$'
        $rightNumeric = $rightIdentifier -match '^\d+$'

        if ($leftNumeric -and $rightNumeric) {
            $leftNumber = [int64]$leftIdentifier
            $rightNumber = [int64]$rightIdentifier
            if ($leftNumber -lt $rightNumber) { return -1 }
            if ($leftNumber -gt $rightNumber) { return 1 }
            continue
        }

        if ($leftNumeric -and -not $rightNumeric) { return -1 }
        if (-not $leftNumeric -and $rightNumeric) { return 1 }

        $textCompare = [String]::CompareOrdinal($leftIdentifier, $rightIdentifier)
        if ($textCompare -lt 0) { return -1 }
        if ($textCompare -gt 0) { return 1 }
    }

    return 0
}

function Get-MusoqVersionChannel {
    param(
        [Parameter(Mandatory=$true)]
        [string]$Version
    )

    return (ConvertTo-MusoqSemVer -Version $Version).Channel
}

function Test-MusoqPrereleaseVersion {
    param(
        [Parameter(Mandatory=$true)]
        [string]$Version
    )

    return (ConvertTo-MusoqSemVer -Version $Version).IsPrerelease
}

function New-MusoqPluginReleaseTag {
    param(
        [Parameter(Mandatory=$true)]
        [string]$Version,
        [Parameter(Mandatory=$true)]
        [string]$PluginName
    )

    if (-not (Test-MusoqSemVer -Version $Version)) {
        throw "Invalid version format: $Version"
    }

    $tag = "$Version-$PluginName"
    if ($tag.Length -gt 200 -or $tag -match '[<>|&;`$\s\\\/]') {
        throw "Invalid release tag format: $tag"
    }

    return $tag
}

function ConvertFrom-MusoqPluginReleaseTag {
    param(
        [Parameter(Mandatory=$true)]
        [string]$ReleaseTag
    )

    if ([string]::IsNullOrWhiteSpace($ReleaseTag)) {
        return $null
    }

    $semVerCapture = '(?<Version>(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?)'
    if ($ReleaseTag -notmatch "^$semVerCapture-(?<PluginName>Musoq\.DataSources\.[A-Za-z][A-Za-z0-9]*)$") {
        return $null
    }

    [PSCustomObject]@{
        Version = $Matches['Version']
        PluginName = $Matches['PluginName']
        Channel = Get-MusoqVersionChannel -Version $Matches['Version']
        IsPrerelease = Test-MusoqPrereleaseVersion -Version $Matches['Version']
    }
}

function Sort-MusoqVersions {
    param(
        [Parameter(Mandatory=$true)]
        [array]$Versions,
        [switch]$Descending
    )

    # Sort-Object cannot use the custom SemVer comparer directly, so use insertion sort over
    # the usually small release lists returned by GitHub.
    $result = New-Object System.Collections.Generic.List[string]
    foreach ($version in $Versions) {
        [void](ConvertTo-MusoqSemVer -Version $version)
        $inserted = $false
        for ($index = 0; $index -lt $result.Count; $index++) {
            $comparison = Compare-MusoqSemVer -Left $version -Right $result[$index]
            if (($Descending -and $comparison -gt 0) -or (-not $Descending -and $comparison -lt 0)) {
                $result.Insert($index, $version)
                $inserted = $true
                break
            }
        }

        if (-not $inserted) {
            [void]$result.Add($version)
        }
    }

    return @($result)
}

function Get-MusoqLatestVersion {
    param(
        [Parameter(Mandatory=$true)]
        [array]$Versions,
        [switch]$StableOnly,
        [switch]$PrereleaseOnly,
        [string]$Channel
    )

    $candidates = @($Versions) | Where-Object {
        $include = Test-MusoqSemVer -Version $_
        if ($include) {
            $parsed = ConvertTo-MusoqSemVer -Version $_
            if ($StableOnly -and $parsed.IsPrerelease) { $include = $false }
            if ($PrereleaseOnly -and -not $parsed.IsPrerelease) { $include = $false }
            if ($Channel -and $parsed.Channel -ne $Channel) { $include = $false }
        }
        $include
    }

    if ($candidates.Count -eq 0) {
        return $null
    }

    return @(Sort-MusoqVersions -Versions $candidates -Descending)[0]
}

function New-MusoqVersionHistoryEntry {
    param(
        [Parameter(Mandatory=$true)]
        [string]$ReleaseTag,
        [Parameter(Mandatory=$true)]
        [string]$ReleaseDate,
        [Parameter(Mandatory=$true)]
        [string]$Version
    )

    $parsed = ConvertTo-MusoqSemVer -Version $Version
    return @{
        releaseTag = $ReleaseTag
        releaseDate = $ReleaseDate
        channel = $parsed.Channel
        isPrerelease = $parsed.IsPrerelease
    }
}

function New-MusoqChannelInfo {
    param(
        [Parameter(Mandatory=$true)]
        [string]$Version,
        [Parameter(Mandatory=$true)]
        [object]$VersionInfo
    )

    return @{
        version = $Version
        releaseTag = $VersionInfo.releaseTag
        releaseDate = $VersionInfo.releaseDate
    }
}

function Get-MusoqPluginRegistryProjection {
    param(
        [Parameter(Mandatory=$true)]
        [hashtable]$Versions
    )

    $versionKeys = @($Versions.Keys)
    if ($versionKeys.Count -eq 0) {
        return $null
    }

    $latestStable = Get-MusoqLatestVersion -Versions $versionKeys -StableOnly
    $latestPrerelease = Get-MusoqLatestVersion -Versions $versionKeys -PrereleaseOnly
    $latestAny = Get-MusoqLatestVersion -Versions $versionKeys
    $latestDefault = if ($latestStable) { $latestStable } else { $latestAny }
    $latestDefaultInfo = $Versions[$latestDefault]

    $channels = @{}
    $channelNames = @($versionKeys | ForEach-Object { Get-MusoqVersionChannel -Version $_ } | Sort-Object -Unique)
    foreach ($channel in $channelNames) {
        $channelLatest = Get-MusoqLatestVersion -Versions $versionKeys -Channel $channel
        if ($channelLatest) {
            $channels[$channel] = New-MusoqChannelInfo -Version $channelLatest -VersionInfo $Versions[$channelLatest]
        }
    }

    return @{
        LatestVersion = $latestDefault
        ReleaseTag = $latestDefaultInfo.releaseTag
        ReleaseDate = $latestDefaultInfo.releaseDate
        LatestStableVersion = $latestStable
        LatestPrereleaseVersion = $latestPrerelease
        Channels = $channels
    }
}
