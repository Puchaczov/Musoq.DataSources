[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$Tag,
    [switch]$Json
)

. (Join-Path $PSScriptRoot "Release.Common.ps1")

$release = Resolve-DatasourceReleaseTag -Tag $Tag
$summary = New-DatasourceReleaseSummary -Release $release

if ($Json) {
    $summary | ConvertTo-Json -Compress
    return
}

Write-Host "Release tag validated: $($summary.tag)"
Write-Host "Release version: $($summary.version)"
Write-Host "Release channel: $($summary.channel)"
Write-Host "Package: $($summary.packageId)"
