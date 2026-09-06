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

function Complete-Scenario {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [int]$StartAssertion
    )

    $script:scenarioCount++
    $script:scenarios.Add([ordered]@{
            name = $Name
            result = 'passed'
            assertions = $script:assertionCount - $StartAssertion
        })
}

function Get-OptionalValue {
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$Object,

        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [AllowNull()]
        [object]$Default
    )

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) {
        return $Default
    }

    return $property.Value
}

function Get-Case {
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$Contract,

        [Parameter(Mandatory = $true)]
        [string]$Id
    )

    $cases = @($Contract.requiredCases | Where-Object { $_.id -eq $Id })
    if ($cases.Count -ne 1) {
        throw "Expected exactly one completion fixture '$Id', got $($cases.Count)."
    }

    return $cases[0]
}

function Resolve-TerminalOutcome {
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$Case,

        [Parameter(Mandatory = $true)]
        [string]$DefaultFailurePolicy,

        [Parameter(Mandatory = $true)]
        [string]$DefaultPartialPolicy,

        [Parameter(Mandatory = $true)]
        [string]$DefaultValidation
    )

    $partialPolicy = [string](Get-OptionalValue -Object $Case -Name 'partialPolicy' -Default $DefaultPartialPolicy)
    $validation = [string](Get-OptionalValue -Object $Case -Name 'validation' -Default $DefaultValidation)
    $observedRows = [int](Get-OptionalValue -Object $Case -Name 'observedRows' -Default 0)
    $summary = [ordered]@{
        outcome = $null
        terminalReason = $null
        complete = $false
        scopeExhausted = $false
        querySatisfied = $false
        countsExact = $false
        prefixAccepted = $false
        terminalSummaryCount = 1
        scopeResolved = $true
        observedPrefix = $observedRows -gt 0
        observedRows = $observedRows
        failureCode = $null
        failurePath = $null
    }

    switch ([string]$Case.kind) {
        'scope-complete' {
            $summary.complete = $true
            $summary.scopeExhausted = $true
            $summary.countsExact = $true
            $summary.prefixAccepted = $true
            $eligibleFiles = [int](Get-OptionalValue -Object $Case -Name 'eligibleFiles' -Default 0)
            $occurrences = [int](Get-OptionalValue -Object $Case -Name 'occurrences' -Default 0)
            if ($eligibleFiles -eq 0) {
                $summary.outcome = 'ScopeExhausted'
                $summary.terminalReason = 'no-eligible-files'
            }
            elseif ($occurrences -eq 0) {
                $summary.outcome = 'ScopeExhausted'
                $summary.terminalReason = 'no-match'
            }
            else {
                $summary.outcome = 'ScopeExhausted'
                $summary.terminalReason = 'completed-scan'
            }
        }
        'take' {
            if (-not [bool](Get-OptionalValue -Object $Case -Name 'takeReached' -Default $false)) {
                throw "TAKE fixture '$($Case.id)' did not reach its declared limit."
            }

            $summary.outcome = 'QuerySatisfied'
            $summary.terminalReason = 'take-reached'
            $summary.complete = $true
            $summary.querySatisfied = $true
            $summary.prefixAccepted = $true
        }
        'binary-marker' {
            if ($validation -eq 'full-input') {
                $summary.outcome = 'ScopeExhausted'
                $summary.terminalReason = 'binary-skipped-by-policy'
                $summary.complete = $true
                $summary.scopeExhausted = $true
                $summary.countsExact = $true
                $summary.prefixAccepted = $true
            }
            elseif ($partialPolicy -eq 'allow' -and $observedRows -gt 0) {
                $summary.outcome = 'Partial'
                $summary.terminalReason = 'binary-late-marker'
                $summary.prefixAccepted = $true
                $summary.failureCode = 'binary-late-marker'
            }
            else {
                $summary.outcome = 'Failed'
                $summary.terminalReason = 'binary-late-marker'
                $summary.prefixAccepted = $false
                $summary.failureCode = 'binary-late-marker'
            }
        }
        'output-cap' {
            if ([bool](Get-OptionalValue -Object $Case -Name 'outputCapReached' -Default $false) -and $partialPolicy -eq 'allow' -and $observedRows -gt 0) {
                $summary.outcome = 'Partial'
                $summary.terminalReason = 'budget-exhausted'
                $summary.prefixAccepted = $true
            }
            else {
                $summary.outcome = 'Failed'
                $summary.terminalReason = 'budget-exhausted'
                $summary.prefixAccepted = $false
            }

            $summary.failureCode = 'output-cap'
        }
        'failure' {
            $failureCode = [string](Get-OptionalValue -Object $Case -Name 'failureCode' -Default 'failure')
            $summary.terminalReason = $failureCode
            $summary.failureCode = $failureCode
            $summary.failurePath = Get-OptionalValue -Object $Case -Name 'failurePath' -Default $null
            if ($failureCode -eq 'root-missing') {
                $summary.scopeResolved = $false
            }

            if ($partialPolicy -eq 'allow' -and $observedRows -gt 0 -and $DefaultFailurePolicy -eq 'strict') {
                $summary.outcome = 'Partial'
                $summary.prefixAccepted = $true
            }
            else {
                $summary.outcome = 'Failed'
                $summary.prefixAccepted = $false
            }
        }
        default {
            throw "Unknown completion fixture kind '$($Case.kind)'."
        }
    }

    return [pscustomobject]$summary
}

$script:assertionCount = 0
$script:scenarioCount = 0
$script:scenarios = [System.Collections.Generic.List[object]]::new()
$contractPath = Join-Path $PSScriptRoot '../../docs/search/search-completion-contract-v1.json'
$contract = Get-Content -LiteralPath $contractPath -Raw -Encoding utf8 | ConvertFrom-Json

try {
    $start = $script:assertionCount
    Assert-Equal -Expected 1 -Actual $contract.version -Message 'The completion contract version must be one.'
    Assert-Equal -Expected 'proposed' -Actual $contract.status -Message 'The completion contract must remain explicitly proposed.'
    Assert-Equal -Expected 'W01-S04' -Actual $contract.scope -Message 'The completion contract must identify its owning scope.'
    Assert-Equal -Expected 'QuerySatisfied,ScopeExhausted,Failed,Partial' -Actual (@($contract.outcomes.values) -join ',') -Message 'All four terminal outcomes must be present in stable order.'
    Assert-Equal -Expected 'outcome' -Actual $contract.outcomes.terminalSummary.authoritativeField -Message 'The terminal outcome must be authoritative.'
    Assert-True -Condition ($contract.outcomes.terminalSummary.requiredFields.Count -ge 12) -Message 'The terminal summary must expose the required counters and failure fields.'
    Assert-True -Condition ($contract.outcomes.terminalSummary.requiredFields -contains 'scopeFingerprint') -Message 'The terminal summary must identify the resolved scope.'
    Assert-True -Condition ($contract.outcomes.terminalSummary.requiredFields -contains 'failureCode') -Message 'The terminal summary must reserve a typed failure code.'
    Assert-True -Condition (@($contract.outcomes.invariants -match 'exactly once').Count -gt 0) -Message 'Terminal-summary cardinality must be explicit.'
    Assert-True -Condition (@($contract.outcomes.invariants -match 'ScopeExhausted is the only outcome').Count -gt 0) -Message 'Exhaustive negative-answer ownership must be explicit.'
    Complete-Scenario -Name 'outcome-taxonomy-and-terminal-summary' -StartAssertion $start

    $start = $script:assertionCount
    Assert-Equal -Expected 'strict' -Actual $contract.defaults.failurePolicy -Message 'Strict failure must be the default.'
    Assert-Equal -Expected 'reject' -Actual $contract.defaults.partialPolicy -Message 'Partial results must require explicit opt-in.'
    Assert-Equal -Expected 'full-input' -Actual $contract.defaults.requestValidation -Message 'Full-input validation must be the default.'
    Assert-Equal -Expected 'skip-and-report' -Actual $contract.defaults.binaryPolicy -Message 'Binary policy must be explicit and visible.'
    Assert-Equal -Expected 'fail' -Actual $contract.defaults.outputCapPolicy -Message 'Output-cap exhaustion must fail by default.'
    Assert-Equal -Expected 'query-satisfaction' -Actual $contract.defaults.takePolicy -Message 'TAKE must be modeled as query satisfaction.'
    Assert-True -Condition ($contract.validation.fullInput.lateFailure -match 'before rows') -Message 'Full-input late-failure behavior must be frozen.'
    Assert-True -Condition ($contract.validation.observedPrefix.lateFailure -match 'never retracted invisibly') -Message 'Observed-prefix late-failure behavior must be frozen.'
    Assert-True -Condition ($contract.validation.observedPrefix.scopeClaim -match 'never claim ScopeExhausted') -Message 'Observed-prefix validation must not claim exhaustion.'
    Assert-True -Condition (@($contract.budgets.principles -match 'TAKE limits requested output').Count -gt 0) -Message 'TAKE must not be confused with a scan budget.'
    Assert-True -Condition (@($contract.budgets.principles -match 'never as ScopeExhausted').Count -gt 0) -Message 'Budget exhaustion must not become exhaustive success.'
    Complete-Scenario -Name 'strict-validation-and-budget-policy' -StartAssertion $start

    $start = $script:assertionCount
    Assert-Equal -Expected 'no-files-eligible,no-match,policy-excluded-binary' -Actual (@($contract.zeroMatchOutcomes.id) -join ',') -Message 'Zero-match outcomes must be enumerated separately.'
    Assert-True -Condition ($contract.counters.unknownRoot -match 'do not masquerade as zero') -Message 'Unknown roots must not look like empty scopes.'
    Assert-True -Condition ($contract.counters.exactness.ScopeExhausted -match 'exact') -Message 'Exhaustive counters must be identified as exact.'
    Assert-True -Condition ($contract.counters.exactness.Failed -match 'observed-prefix') -Message 'Failed counters must be identified as prefix observations.'
    Assert-True -Condition ($contract.binaryPolicy.lateMarker -match 'cannot retract') -Message 'Late binary markers must not retract evidence invisibly.'
    Complete-Scenario -Name 'zero-match-and-counter-exactness' -StartAssertion $start

    $start = $script:assertionCount
    foreach ($case in @($contract.requiredCases)) {
        $resolved = Resolve-TerminalOutcome -Case $case -DefaultFailurePolicy $contract.defaults.failurePolicy -DefaultPartialPolicy $contract.defaults.partialPolicy -DefaultValidation $contract.defaults.requestValidation
        $expected = $case.expected
        Assert-Equal -Expected $expected.outcome -Actual $resolved.outcome -Message "Outcome mismatch for '$($case.id)'."
        Assert-Equal -Expected $expected.terminalReason -Actual $resolved.terminalReason -Message "Terminal reason mismatch for '$($case.id)'."
        Assert-Equal -Expected $expected.complete -Actual $resolved.complete -Message "Complete flag mismatch for '$($case.id)'."
        Assert-Equal -Expected $expected.scopeExhausted -Actual $resolved.scopeExhausted -Message "Scope-exhausted flag mismatch for '$($case.id)'."
        Assert-Equal -Expected $expected.querySatisfied -Actual $resolved.querySatisfied -Message "Query-satisfied flag mismatch for '$($case.id)'."
        Assert-Equal -Expected $expected.countsExact -Actual $resolved.countsExact -Message "Counter exactness mismatch for '$($case.id)'."
        Assert-Equal -Expected $expected.prefixAccepted -Actual $resolved.prefixAccepted -Message "Prefix acceptance mismatch for '$($case.id)'."
        Assert-Equal -Expected 1 -Actual $resolved.terminalSummaryCount -Message "Terminal summary must be delivered once for '$($case.id)'."
        if ($expected.PSObject.Properties.Name -contains 'failureCode') {
            Assert-Equal -Expected $expected.failureCode -Actual $resolved.failureCode -Message "Failure code mismatch for '$($case.id)'."
        }
    }
    Complete-Scenario -Name 'required-terminal-outcome-corpus' -StartAssertion $start

    [ordered]@{
        formatVersion = 1
        test = 'Search completion semantics'
        status = 'passed'
        contractPath = 'docs/search/search-completion-contract-v1.json'
        scenarios = $script:scenarios
        scenarioCount = $script:scenarioCount
        assertions = $script:assertionCount
        implementationNote = 'This is a proposed completion and budget test with an independent outcome resolver; it does not claim that Search terminal summaries are installed.'
    } | ConvertTo-Json -Depth 15
}
catch {
    [ordered]@{
        formatVersion = 1
        test = 'Search completion semantics'
        status = 'failed'
        scenarioCount = $script:scenarioCount
        assertions = $script:assertionCount
        error = $_.Exception.Message
    } | ConvertTo-Json -Depth 12
    exit 1
}
