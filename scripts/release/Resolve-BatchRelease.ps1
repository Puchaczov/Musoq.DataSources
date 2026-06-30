[CmdletBinding()]
param(
    [string]$Selection = "All",
    [switch]$Json
)

. (Join-Path $PSScriptRoot "Release.Common.ps1")

function Split-BatchReleaseSelection {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return @("All")
    }

    return @($Value -split '[,\r\n\t ]+' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
}

function Resolve-BatchReleaseToken {
    param(
        [Parameter(Mandatory=$true)]
        [string]$Token,
        [Parameter(Mandatory=$true)]
        [array]$Packages
    )

    $parsedTag = ConvertFrom-MusoqPluginReleaseTag -ReleaseTag $Token
    if ($parsedTag) {
        return Resolve-DatasourceReleaseTag -Tag $Token
    }

    $normalizedToken = $Token.ToLowerInvariant()
    $matches = @($Packages | Where-Object {
        $_.PackageId -eq $Token -or
        $_.Slug -eq $normalizedToken -or
        ($_.PackageId -replace '^Musoq\.DataSources\.', '') -eq $Token
    })

    if ($matches.Count -ne 1) {
        throw "Batch release selection '$Token' did not match exactly one datasource package."
    }

    $package = $matches[0]
    $version = Get-MsBuildProperty -ProjectPath $package.FullProjectPath -PropertyName "Version"
    $tag = New-MusoqPluginReleaseTag -Version $version -PluginName $package.PackageId
    return Resolve-DatasourceReleaseTag -Tag $tag
}

$packages = @(Get-ReleasePackages)
$tokens = @(Split-BatchReleaseSelection -Value $Selection)
$releases = @()
$seenTags = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)

if ($tokens.Count -eq 1 -and $tokens[0].Equals("All", [System.StringComparison]::OrdinalIgnoreCase)) {
    foreach ($package in $packages) {
        $version = Get-MsBuildProperty -ProjectPath $package.FullProjectPath -PropertyName "Version"
        $tag = New-MusoqPluginReleaseTag -Version $version -PluginName $package.PackageId
        $release = Resolve-DatasourceReleaseTag -Tag $tag
        if ($seenTags.Add($release.Tag)) {
            $releases += $release
        }
    }
}
else {
    foreach ($token in $tokens) {
        if ($token.Equals("All", [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Batch release selection cannot combine 'All' with explicit packages."
        }

        $release = Resolve-BatchReleaseToken -Token $token -Packages $packages
        if ($seenTags.Add($release.Tag)) {
            $releases += $release
        }
    }
}

$summaries = @($releases | ForEach-Object { New-DatasourceReleaseSummary -Release $_ })

if ($Json) {
    ConvertTo-Json -InputObject $summaries -Depth 10 -Compress
    return
}

$summaries | Format-Table tag, packageId, version, channel, isPrerelease -AutoSize
