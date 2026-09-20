[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-True {
    param(
        [Parameter(Mandatory = $true)]
        [bool]$Condition,

        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    $script:assertionCount++
    if (-not $Condition) {
        throw $Message
    }
}

function Assert-Equal {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Expected,

        [Parameter(Mandatory = $true)]
        [object]$Actual,

        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    $script:assertionCount++
    if ($Expected -ne $Actual) {
        throw "$Message Expected '$Expected', got '$Actual'."
    }
}

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

function Invoke-Campaign {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Operation,

        [Parameter(Mandatory = $true)]
        [string]$CampaignPath,

        [switch]$RequireReady
    )

    $scriptPath = Join-Path $PSScriptRoot 'Invoke-SearchCampaign.ps1'
    $arguments = @('-NoProfile', '-NonInteractive', '-File', $scriptPath, '-Operation', $Operation, '-CampaignPath', $CampaignPath, '-Track', 'core', '-RepositoryRoot', (Join-Path $PSScriptRoot '../..'))
    if ($RequireReady) {
        $arguments += '-RequireReady'
    }

    $pwsh = (Get-Command pwsh -CommandType Application -ErrorAction Stop).Source
    $output = @(& $pwsh @arguments 2>&1)
    [pscustomobject]@{
        exitCode = $LASTEXITCODE
        text = ConvertTo-Text $output
    }
}

function Copy-CampaignFixture {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceDirectory,

        [Parameter(Mandatory = $true)]
        [string]$DestinationDirectory
    )

    Copy-Item -LiteralPath $SourceDirectory -Destination $DestinationDirectory -Recurse -Force
    return Join-Path $DestinationDirectory 'campaign.json'
}

function Update-CampaignFixture {
    param(
        [Parameter(Mandatory = $true)]
        [string]$CampaignPath,

        [Parameter(Mandatory = $true)]
        [scriptblock]$Update
    )

    $campaign = Get-Content -LiteralPath $CampaignPath -Raw -Encoding utf8 | ConvertFrom-Json
    & $Update $campaign
    $campaign | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $CampaignPath -Encoding utf8NoBOM
}

function Get-Scope {
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$Campaign,

        [Parameter(Mandatory = $true)]
        [string]$Id
    )

    return @($Campaign.scopes | Where-Object id -eq $Id)[0]
}

$script:assertionCount = 0
$scenarioCount = 0
$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) ('musoq-search-campaign-' + [Guid]::NewGuid().ToString('N'))
$sourceDirectory = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../../docs/campaigns/search')).Path
$scenarios = [System.Collections.Generic.List[object]]::new()

try {
    New-Item -ItemType Directory -Path $temporaryRoot -Force | Out-Null

    $baselineRoot = Join-Path $temporaryRoot 'baseline'
    $baselineCampaign = Copy-CampaignFixture -SourceDirectory $sourceDirectory -DestinationDirectory $baselineRoot
    $selection = Invoke-Campaign -Operation 'select' -CampaignPath $baselineCampaign
    Assert-Equal -Expected 0 -Actual $selection.exitCode -Message "Baseline selection should pass. Output: $($selection.text)"
    $selectionResult = $selection.text | ConvertFrom-Json
    Assert-Equal -Expected 'core' -Actual $selectionResult.selectedTrack -Message 'Selection should retain the explicit core track.'
    Assert-Equal -Expected 'W00-S04' -Actual $selectionResult.nextEligibleScope -Message 'Selection should return exactly the first eligible scope.'
    Assert-Equal -Expected 3 -Actual $selectionResult.completedScopes -Message 'Selection should use recorded completed evidence.'
    $scenarioCount++
    $scenarios.Add([ordered]@{ name = 'first-eligible-selection'; result = 'passed'; assertions = 3 })

    $activeRoot = Join-Path $temporaryRoot 'active'
    $activeCampaign = Copy-CampaignFixture -SourceDirectory $sourceDirectory -DestinationDirectory $activeRoot
    Update-CampaignFixture -CampaignPath $activeCampaign -Update {
        param($campaign)
        $scope = Get-Scope -Campaign $campaign -Id 'W00-S04'
        $scope.status = 'in_progress'
        $scope.attempts = @([ordered]@{ attempt = 1; status = 'in_progress' })
    }
    $active = Invoke-Campaign -Operation 'select' -CampaignPath $activeCampaign
    Assert-Equal -Expected 0 -Actual $active.exitCode -Message "An interrupted active scope should remain resumable. Output: $($active.text)"
    $activeResult = $active.text | ConvertFrom-Json
    Assert-Equal -Expected 'W00-S04' -Actual $activeResult.activeScope -Message 'An active scope must win over later eligible scopes.'
    Assert-Equal -Expected 'W00-S04' -Actual $activeResult.nextEligibleScope -Message 'Selection must not advance past active work.'
    $scenarioCount++
    $scenarios.Add([ordered]@{ name = 'interrupted-active-scope'; result = 'passed'; assertions = 2 })

    $cycleRoot = Join-Path $temporaryRoot 'cycle'
    $cycleCampaign = Copy-CampaignFixture -SourceDirectory $sourceDirectory -DestinationDirectory $cycleRoot
    Update-CampaignFixture -CampaignPath $cycleCampaign -Update {
        param($campaign)
        $scope = Get-Scope -Campaign $campaign -Id 'W00-S04'
        $scope.dependencies = @('W00-S04')
    }
    $cycle = Invoke-Campaign -Operation 'validate' -CampaignPath $cycleCampaign
    Assert-True -Condition ($cycle.exitCode -ne 0) -Message 'A dependency cycle must fail validation.'
    Assert-True -Condition ($cycle.text -match 'Cycle in scope graph') -Message "Cycle diagnostic should name the graph. Output: $($cycle.text)"
    $scenarioCount++
    $scenarios.Add([ordered]@{ name = 'dag-cycle'; result = 'passed'; assertions = 2 })

    $unfinishedRoot = Join-Path $temporaryRoot 'unfinished'
    $unfinishedCampaign = Copy-CampaignFixture -SourceDirectory $sourceDirectory -DestinationDirectory $unfinishedRoot
    Update-CampaignFixture -CampaignPath $unfinishedCampaign -Update {
        param($campaign)
        $scope = Get-Scope -Campaign $campaign -Id 'W00-S02'
        $scope.status = 'planned'
        $scope.completion = $null
        $scope.attempts = @()
    }
    $unfinished = Invoke-Campaign -Operation 'validate' -CampaignPath $unfinishedCampaign
    Assert-True -Condition ($unfinished.exitCode -ne 0) -Message 'A completed scope with an unfinished dependency must fail validation.'
    Assert-True -Condition ($unfinished.text -match 'W00-S03: unfinished local dependency') -Message "Unfinished dependency diagnostic should name the dependent scope. Output: $($unfinished.text)"
    $scenarioCount++
    $scenarios.Add([ordered]@{ name = 'unfinished-dependency'; result = 'passed'; assertions = 2 })

    $duplicateRoot = Join-Path $temporaryRoot 'duplicate-trailer'
    $duplicateCampaign = Copy-CampaignFixture -SourceDirectory $sourceDirectory -DestinationDirectory $duplicateRoot
    Update-CampaignFixture -CampaignPath $duplicateCampaign -Update {
        param($campaign)
        $scope = Get-Scope -Campaign $campaign -Id 'W00-S04'
        $scope.commitTrailer = 'Search-Scope: W00-S03'
    }
    $duplicate = Invoke-Campaign -Operation 'validate' -CampaignPath $duplicateCampaign
    Assert-True -Condition ($duplicate.exitCode -ne 0) -Message 'Duplicate completion trailer metadata must fail validation.'
    Assert-True -Condition ($duplicate.text -match 'W00-S04: wrong scope trailer') -Message "Duplicate trailer diagnostic should name the scope. Output: $($duplicate.text)"
    $scenarioCount++
    $scenarios.Add([ordered]@{ name = 'duplicate-completion-trailer'; result = 'passed'; assertions = 2 })

    $evidenceRoot = Join-Path $temporaryRoot 'absent-evidence'
    $evidenceCampaign = Copy-CampaignFixture -SourceDirectory $sourceDirectory -DestinationDirectory $evidenceRoot
    Update-CampaignFixture -CampaignPath $evidenceCampaign -Update {
        param($campaign)
        $scope = Get-Scope -Campaign $campaign -Id 'W00-S04'
        $scope.status = 'completed'
        $scope.completion = [ordered]@{ reportPath = 'docs/campaigns/search/evidence/W00-S04/not-present.json' }
    }
    $evidence = Invoke-Campaign -Operation 'validate' -CampaignPath $evidenceCampaign
    Assert-True -Condition ($evidence.exitCode -ne 0) -Message 'Completed status without evidence must fail validation.'
    Assert-True -Condition ($evidence.text -match 'W00-S04: missing report') -Message "Absent evidence diagnostic should name the report. Output: $($evidence.text)"
    $scenarioCount++
    $scenarios.Add([ordered]@{ name = 'absent-evidence'; result = 'passed'; assertions = 2 })

    [ordered]@{
        formatVersion = 1
        test = 'Search campaign selection and status validation'
        status = 'passed'
        scenarios = $scenarios
        scenarioCount = $scenarioCount
        assertions = $script:assertionCount
        temporaryFixturePolicy = 'Campaign copies were created below the system temporary directory and removed in finally; no repository campaign state or Git history was mutated by the simulations.'
    } | ConvertTo-Json -Depth 10
}
finally {
    if (Test-Path -LiteralPath $temporaryRoot) {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
    }
}
