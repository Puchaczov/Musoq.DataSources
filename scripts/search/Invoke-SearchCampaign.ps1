[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('select', 'validate')]
    [string]$Operation,

    [string]$CampaignPath = (Join-Path $PSScriptRoot '../../docs/campaigns/search/campaign.json'),

    [ValidateSet('core', 'engine-adapter', 'deep-search', 'product-followup')]
    [string]$Track,

    [string]$RepositoryRoot = (Join-Path $PSScriptRoot '../..'),

    [switch]$RequireReady
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function ConvertTo-Text {
    param(
        [AllowNull()]
        [object[]]$Value
    )

    if ($null -eq $Value) {
        return ''
    }

    return (($Value | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine).Trim()
}

try {
    $campaign = (Resolve-Path -LiteralPath $CampaignPath -ErrorAction Stop).Path
    $repo = (Resolve-Path -LiteralPath $RepositoryRoot -ErrorAction Stop).Path
    $validator = Join-Path (Split-Path -Parent $campaign) 'validate_campaign.py'
    if (-not (Test-Path -LiteralPath $validator -PathType Leaf)) {
        throw "Campaign validator is missing beside campaign.json: '$validator'."
    }

    $arguments = @($validator, $campaign, '--repo-root', $repo)
    if (-not [string]::IsNullOrWhiteSpace($Track)) {
        $arguments += @('--track', $Track)
    }
    if ($RequireReady) {
        $arguments += '--require-ready'
    }

    $output = @(& python @arguments 2>&1)
    $exitCode = $LASTEXITCODE
    $text = ConvertTo-Text $output
    if ($exitCode -ne 0) {
        if (-not [string]::IsNullOrWhiteSpace($text)) {
            Write-Output $text
        }
        exit $exitCode
    }

    $result = $text | ConvertFrom-Json
    if ($Operation -eq 'validate') {
        $result | ConvertTo-Json -Depth 12
        exit 0
    }

    [ordered]@{
        formatVersion = 1
        operation = 'select'
        campaignPath = $campaign
        repositoryRoot = $repo
        selectedTrack = $result.selectedTrack
        activeScope = $result.activeScope
        nextEligibleScope = $result.nextEligibleScope
        blockedByHandoffs = @($result.blockedByHandoffs)
        completedScopes = $result.statistics.completedScopes
        selectionPolicy = 'An active scope is resumed; otherwise the validator returns the first eligible scope in campaign order. No later scope is selected when an earlier dependency is unfinished or blocked.'
    } | ConvertTo-Json -Depth 8
}
catch {
    Write-Error $_
    exit 1
}
