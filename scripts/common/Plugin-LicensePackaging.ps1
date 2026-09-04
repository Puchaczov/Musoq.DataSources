. (Join-Path $PSScriptRoot "Plugin-ArtifactIntegrity.ps1")

function Add-MusoqPluginLicenseMapEntry {
    param(
        [Parameter(Mandatory=$true)]
        [System.Collections.IDictionary]$LicenseMap,
        [Parameter(Mandatory=$true)]
        [string]$ProjectPath,
        [Parameter(Mandatory=$true)]
        [string]$LicenseDirectory
    )

    Assert-MusoqPluginLicenseNotices `
        -PluginDirectory (Split-Path -Parent $LicenseDirectory) `
        -Context "Generated license notices for '$ProjectPath'" | Out-Null
    $LicenseMap[$ProjectPath] = $LicenseDirectory
}
