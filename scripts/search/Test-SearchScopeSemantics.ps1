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

function Get-Fixture {
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$Contract,

        [Parameter(Mandatory = $true)]
        [string]$Id
    )

    $fixture = @($Contract.fixtures | Where-Object { $_.id -eq $Id })
    if ($fixture.Count -ne 1) {
        throw "Expected exactly one scope fixture '$Id', got $($fixture.Count)."
    }

    return $fixture[0]
}

function Normalize-PosixPath {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Path
    )

    $parts = [System.Collections.Generic.List[string]]::new()
    foreach ($part in ($Path -replace '\\', '/' -split '/')) {
        if ([string]::IsNullOrEmpty($part) -or $part -eq '.') {
            continue
        }

        if ($part -eq '..') {
            if ($parts.Count -eq 0) {
                return $null
            }

            $parts.RemoveAt($parts.Count - 1)
            continue
        }

        $parts.Add($part)
    }

    return ($parts -join '/')
}

function Test-LexicallyContained {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root,

        [Parameter(Mandatory = $true)]
        [string]$Candidate
    )

    $normalizedRoot = Normalize-PosixPath -Path $Root
    $normalizedCandidate = Normalize-PosixPath -Path $Candidate
    if ($null -eq $normalizedRoot -or $null -eq $normalizedCandidate) {
        return $false
    }

    return $normalizedCandidate -eq $normalizedRoot -or $normalizedCandidate.StartsWith($normalizedRoot + '/', [StringComparison]::Ordinal)
}

function Test-GlobMatch {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Glob,

        [Parameter(Mandatory = $true)]
        [ValidateSet('case-sensitive', 'case-insensitive')]
        [string]$Comparison
    )

    if ($Comparison -eq 'case-insensitive') {
        return $Path.ToLowerInvariant() -like $Glob.ToLowerInvariant()
    }

    return $Path -clike $Glob
}

$script:assertionCount = 0
$script:scenarioCount = 0
$script:scenarios = [System.Collections.Generic.List[object]]::new()
$contractPath = Join-Path $PSScriptRoot '../../docs/search/search-scope-contract-v1.json'
$contract = Get-Content -LiteralPath $contractPath -Raw -Encoding utf8 | ConvertFrom-Json

try {
    $start = $script:assertionCount
    Assert-Equal -Expected 1 -Actual $contract.version -Message 'The scope contract version must be one.'
    Assert-Equal -Expected 'proposed' -Actual $contract.status -Message 'The scope contract must remain explicitly proposed.'
    Assert-Equal -Expected 'W01-S03' -Actual $contract.scope -Message 'The scope contract must identify its owning scope.'
    Assert-Equal -Expected $true -Actual $contract.defaults.recursive -Message 'Recursive traversal must be the documented default.'
    Assert-Equal -Expected 'filesystem-native' -Actual $contract.defaults.pathComparison -Message 'Path comparison must be resolved from the filesystem and recorded.'
    Assert-Equal -Expected 'disabled' -Actual $contract.defaults.globalIgnores -Message 'Global ignores must be disabled by default.'
    Assert-Equal -Expected 'do not follow' -Actual $contract.defaults.followLinks -Message 'Links must not be followed by default.'
    Assert-Equal -Expected 'fail' -Actual $contract.defaults.inaccessibleEntries -Message 'Inaccessible entries must fail by default.'
    Assert-Equal -Expected 'fail-before-scan' -Actual $contract.defaults.missingRoot -Message 'Missing roots must fail before scanning.'
    Assert-True -Condition ($contract.scopeFingerprint.fields -contains 'sorted candidate decision manifest') -Message 'The fingerprint must include the candidate decision manifest.'
    Assert-True -Condition ($contract.scopeFingerprint.fields -contains 'loaded ignore source hashes') -Message 'The fingerprint must include loaded ignore source hashes.'
    Assert-Equal -Expected 'SHA-256 of canonical UTF-8 JSON' -Actual $contract.scopeFingerprint.algorithm -Message 'The scope fingerprint algorithm must be explicit.'
    Complete-Scenario -Name 'contract-identity-and-fingerprint' -StartAssertion $start

    $start = $script:assertionCount
    Assert-Equal -Expected 'security containment' -Actual $contract.includeExclude.evaluationOrder[0] -Message 'Containment must run before convenience filters.'
    Assert-Equal -Expected 'include allow-list' -Actual $contract.includeExclude.evaluationOrder[1] -Message 'Include evaluation must precede excludes.'
    Assert-Equal -Expected 'exclude deny-list' -Actual $contract.includeExclude.evaluationOrder[2] -Message 'Exclude evaluation must be explicit.'
    Assert-Equal -Expected 'repository/global ignore rules' -Actual $contract.includeExclude.evaluationOrder[3] -Message 'Ignore evaluation must be ordered after explicit filters.'
    Assert-True -Condition ($contract.includeExclude.exclude -match 'excludes win over includes') -Message 'The include/exclude conflict rule must be explicit.'
    Assert-True -Condition ($contract.includeExclude.include -match 'descendant could match') -Message 'An unmatched directory must remain traversable when a descendant may match.'
    Assert-Equal -Expected '.gitignore,.ignore,.rgignore' -Actual (@($contract.ignorePolicy.repositorySources | Sort-Object precedence | ForEach-Object { $_.name }) -join ',') -Message 'Repository ignore precedence must be deterministic.'
    Assert-True -Condition $contract.ignorePolicy.higherPrecedenceWins -Message 'Higher-precedence ignore sources must win.'
    Assert-True -Condition ($contract.ignorePolicy.withinFile -match 'later matching rule wins') -Message 'Within-file ignore order must be explicit.'
    Complete-Scenario -Name 'filter-and-ignore-order' -StartAssertion $start

    $start = $script:assertionCount
    $nested = Get-Fixture -Contract $contract -Id 'nested-ignore-negation'
    Assert-Equal -Expected 'src/a.cs,src/keep.generated.cs' -Actual (@($nested.expectedIncluded) -join ',') -Message 'Nested ignore negation must re-include the named file.'
    Assert-Equal -Expected 'src/drop.generated.cs,src/cache.tmp' -Actual (@($nested.expectedExcluded) -join ',') -Message 'Nested and root ignore exclusions must be retained.'
    $parent = Get-Fixture -Contract $contract -Id 'ignored-parent-blocks-descendant-negation'
    Assert-Equal -Expected 0 -Actual @($parent.expectedIncluded).Count -Message 'An ignored parent must not be re-opened by a descendant negation.'
    Assert-Equal -Expected 'build/drop.cs,build/keep.cs' -Actual (@($parent.expectedExcluded) -join ',') -Message 'Both files under the pruned parent must remain excluded.'
    Assert-True -Condition ($contract.ignorePolicy.parentDirectory -match 'not reopened') -Message 'Parent-pruning behavior must be documented.'
    Complete-Scenario -Name 'nested-ignores-and-negation' -StartAssertion $start

    $start = $script:assertionCount
    $containment = Get-Fixture -Contract $contract -Id 'lexical-versus-physical-containment'
    $lexicalInside = Test-LexicallyContained -Root $containment.root -Candidate $containment.candidates[0].path
    Assert-True -Condition $lexicalInside -Message 'A dot-segment path resolving under the root must remain lexically contained.'
    Assert-Equal -Expected 'included' -Actual $containment.candidates[0].expected -Message 'The normalized in-root candidate must be included.'
    Assert-True -Condition (Test-LexicallyContained -Root $containment.root -Candidate $containment.candidates[1].path) -Message 'A symlink spelling can be lexically in-root before physical resolution.'
    Assert-Equal -Expected 'rejected-outside-physical-root' -Actual $containment.candidates[1].expected -Message 'A followed symlink outside the physical root must be rejected.'
    Assert-True -Condition (-not (Test-LexicallyContained -Root $containment.root -Candidate $containment.candidates[2].path)) -Message 'A dot-segment path outside the root must fail lexical containment.'
    Assert-Equal -Expected 'rejected-outside-lexical-root' -Actual $containment.candidates[2].expected -Message 'Lexical escape must be rejected.'
    Assert-True -Condition ($contract.security.outsideRoot -match 'cannot be made safe') -Message 'Convenience filters must not override containment.'
    Complete-Scenario -Name 'lexical-and-physical-containment' -StartAssertion $start

    $start = $script:assertionCount
    $caseFixture = Get-Fixture -Contract $contract -Id 'case-sensitive-filesystem'
    $sensitive = @($caseFixture.paths | Where-Object { Test-GlobMatch -Path $_ -Glob $caseFixture.cases[0].glob -Comparison 'case-sensitive' })
    $insensitive = @($caseFixture.paths | Where-Object { Test-GlobMatch -Path $_ -Glob $caseFixture.cases[1].glob -Comparison 'case-insensitive' })
    Assert-Equal -Expected 'A.cs' -Actual ($sensitive -join ',') -Message 'Case-sensitive matching must not fold path case.'
    Assert-Equal -Expected 'A.cs,a.cs' -Actual ($insensitive -join ',') -Message 'Case-insensitive matching must fold path case explicitly.'
    Assert-Equal -Expected 'filesystem-native' -Actual $contract.defaults.pathComparison -Message 'The implementation must not infer path case only from the OS name.'
    $hardLinks = Get-Fixture -Contract $contract -Id 'hard-link-path-identity'
    Assert-Equal -Expected 'a.txt,alias.txt' -Actual (@($hardLinks.expectedPaths) -join ',') -Message 'Hard-linked directory entries must remain distinct path rows.'
    Assert-Equal -Expected 'distinct-paths' -Actual $contract.defaults.hardLinks -Message 'Hard-link path policy must be explicit.'
    Complete-Scenario -Name 'case-comparison-and-hard-links' -StartAssertion $start

    $start = $script:assertionCount
    $extension = Get-Fixture -Contract $contract -Id 'extension-does-not-prune-directory'
    Assert-True -Condition $extension.expectedTraverseDirectory -Message 'An extension include must not prune an unmatched directory.'
    Assert-True -Condition (Test-GlobMatch -Path $extension.descendant -Glob $extension.include[0] -Comparison 'case-sensitive') -Message 'The included descendant must match the extension glob.'
    Assert-True -Condition $extension.expectedIncludeDescendant -Message 'The descendant file must remain eligible.'
    Assert-True -Condition ($contract.traversal.directoryPruning -match 'never prune a directory') -Message 'Directory-pruning safety must be explicit.'
    $inaccessible = Get-Fixture -Contract $contract -Id 'inaccessible-descendant'
    Assert-Equal -Expected 'failed-with-path-and-phase' -Actual $inaccessible.expected -Message 'Inaccessible descendants must retain a typed failure outcome.'
    Assert-Equal -Expected 'fail' -Actual $inaccessible.defaultPolicy -Message 'The inaccessible fixture must exercise the strict default.'
    Complete-Scenario -Name 'descendant-traversal-and-access-errors' -StartAssertion $start

    [ordered]@{
        formatVersion = 1
        test = 'Search scope semantics'
        status = 'passed'
        contractPath = 'docs/search/search-scope-contract-v1.json'
        scenarios = $script:scenarios
        scenarioCount = $script:scenarioCount
        assertions = $script:assertionCount
        implementationNote = 'This is a proposed scope test with independent path/decision helpers; it does not claim that Search traversal is installed.'
    } | ConvertTo-Json -Depth 15
}
catch {
    [ordered]@{
        formatVersion = 1
        test = 'Search scope semantics'
        status = 'failed'
        scenarioCount = $script:scenarioCount
        assertions = $script:assertionCount
        error = $_.Exception.Message
    } | ConvertTo-Json -Depth 12
    exit 1
}
