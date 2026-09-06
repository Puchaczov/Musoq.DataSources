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

function Get-TruthTable {
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$Contract,

        [Parameter(Mandatory = $true)]
        [string]$Id
    )

    $table = @($Contract.truthTables | Where-Object { $_.id -eq $Id })
    if ($table.Count -ne 1) {
        throw "Expected exactly one truth table '$Id', got $($table.Count)."
    }

    return $table[0]
}

function Get-Case {
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$Table,

        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $case = @($Table.cases | Where-Object { $_.name -eq $Name })
    if ($case.Count -ne 1) {
        throw "Expected exactly one case '$Name', got $($case.Count)."
    }

    return $case[0]
}

function Get-LiteralSpans {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Record,

        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Pattern
    )

    if ($Pattern.Length -eq 0) {
        return @()
    }

    $spans = [System.Collections.Generic.List[object]]::new()
    $cursor = 0
    while ($cursor -le ($Record.Length - $Pattern.Length)) {
        $start = $Record.IndexOf($Pattern, $cursor, [StringComparison]::Ordinal)
        if ($start -lt 0) {
            break
        }

        $spans.Add([pscustomobject]@{
                start = $start
                end = $start + $Pattern.Length
            })
        $cursor = $start + $Pattern.Length
    }

    return @($spans)
}

function Format-Spans {
    param(
        [AllowNull()]
        [object]$Spans
    )

    if ($null -eq $Spans) {
        return ''
    }

    $items = @($Spans)
    if ($items.Count -eq 2 -and $items[0] -is [ValueType] -and $items[1] -is [ValueType]) {
        return "$($items[0])-$($items[1])"
    }

    $formatted = foreach ($span in $items) {
        $startProperty = @($span.PSObject.Properties | Where-Object Name -eq 'start')
        if ($startProperty.Count -eq 1) {
            "$($span.start)-$($span.end)"
            continue
        }

        $values = @($span)
        if ($values.Count -lt 2) {
            throw 'Every truth-table span must contain a start and end.'
        }
        "$($values[0])-$($values[1])"
    }

    return ($formatted -join ',')
}

function Assert-Spans {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Expected,

        [Parameter(Mandatory = $true)]
        [object]$Actual,

        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    Assert-Equal -Expected (Format-Spans -Spans $Expected) -Actual (Format-Spans -Spans $Actual) -Message $Message
}

$script:assertionCount = 0
$script:scenarioCount = 0
$script:scenarios = [System.Collections.Generic.List[object]]::new()
$contractPath = Join-Path $PSScriptRoot '../../docs/search/search-match-semantics-v1.json'
$contract = Get-Content -LiteralPath $contractPath -Raw -Encoding utf8 | ConvertFrom-Json

try {
    $start = $script:assertionCount
    Assert-Equal -Expected 1 -Actual $contract.version -Message 'The match-semantics contract version must be one.'
    Assert-Equal -Expected 'proposed' -Actual $contract.status -Message 'The match-semantics contract must remain explicitly proposed.'
    Assert-Equal -Expected 'W01-S02' -Actual $contract.scope -Message 'The match-semantics contract must identify its owning scope.'
    Assert-Equal -Expected 'literal' -Actual $contract.defaults.textMode -Message 'Literal must be the default mode.'
    Assert-Equal -Expected 'sensitive' -Actual $contract.defaults.case -Message 'Case-sensitive matching must be the default.'
    Assert-Equal -Expected 'leftmost-first-non-overlapping' -Actual $contract.defaults.selection -Message 'The default selection policy must be explicit.'
    Assert-Equal -Expected 'none' -Actual $contract.defaults.fallback -Message 'Silent backend fallback must be disabled.'
    Complete-Scenario -Name 'contract-identity-and-defaults' -StartAssertion $start

    $start = $script:assertionCount
    $expectedTableIds = @('prefix-suffix-overlap', 'duplicate-ids', 'empty-literal', 'anchors', 'zero-width', 'unicode-boundaries')
    $actualTableIds = @($contract.truthTables | ForEach-Object { $_.id })
    Assert-Equal -Expected ($expectedTableIds -join ',') -Actual ($actualTableIds -join ',') -Message 'The truth-table corpus must cover every required semantic family.'
    foreach ($tableId in $expectedTableIds) {
        $table = Get-TruthTable -Contract $contract -Id $tableId
        Assert-True -Condition (@($table.cases).Count -gt 0) -Message "Truth table '$tableId' must contain cases."
    }
    Assert-Equal -Expected 'portable non-backtracking subset' -Actual $contract.regexSemantics.engineProfile -Message 'The regex engine profile must be portable and non-backtracking.'
    Assert-True -Condition ($contract.regexSemantics.rejectedFeatures -contains 'backreferences') -Message 'Backreferences must be rejected by the portable profile.'
    Assert-True -Condition ($contract.regexSemantics.rejectedFeatures -contains 'lookahead and lookbehind') -Message 'Lookaround must be rejected by the portable profile.'
    Assert-True -Condition ($contract.regexSemantics.unsupportedBehavior -match 'never silently retry') -Message 'Unsupported regex syntax must not silently fall back.'
    Assert-Equal -Expected 'reject-before-read' -Actual $contract.defaults.unsupportedConstructBehavior -Message 'Unsupported constructs must fail before content reads.'
    Complete-Scenario -Name 'truth-table-coverage-and-portable-dialect' -StartAssertion $start

    $start = $script:assertionCount
    $overlap = Get-TruthTable -Contract $contract -Id 'prefix-suffix-overlap'
    $single = Get-Case -Table $overlap -Name 'single-pattern-overlap'
    $actualSingle = @(Get-LiteralSpans -Record $single.record -Pattern $single.pattern)
    Assert-Spans -Expected $single.expectedUtf16Spans -Actual $actualSingle -Message 'Single-pattern matching must be non-overlapping.'
    $cross = Get-Case -Table $overlap -Name 'cross-pattern-prefix'
    Assert-Spans -Expected $cross.expectedByPatternId.short -Actual @(Get-LiteralSpans -Record $cross.record -Pattern 'a') -Message 'The short cross-pattern prefix must be retained.'
    Assert-Spans -Expected $cross.expectedByPatternId.long -Actual @(Get-LiteralSpans -Record $cross.record -Pattern 'ab') -Message 'The long cross-pattern prefix must be retained.'
    $alternatives = Get-Case -Table $overlap -Name 'regex-alternative-order'
    Assert-Spans -Expected $alternatives.records[0].expectedUtf16Spans -Actual @([int[]]@(0, 1)) -Message 'A first alternative must win a same-start regex tie.'
    Assert-Spans -Expected $alternatives.records[1].expectedUtf16Spans -Actual @([int[]]@(0, 2)) -Message 'Reordered alternatives must change a same-start tie.'
    Assert-Equal -Expected 'increasing source start, then increasing end for a same-start tie' -Actual $contract.ordering.withinPattern -Message 'Within-pattern ordering must be explicit.'
    Assert-Equal -Expected 'input pattern order is the deterministic tie-breaker after path and byte start' -Actual $contract.ordering.manySameStart -Message 'Many same-start ordering must be explicit.'
    Complete-Scenario -Name 'literal-overlap-and-leftmost-selection' -StartAssertion $start

    $start = $script:assertionCount
    $duplicates = Get-TruthTable -Contract $contract -Id 'duplicate-ids'
    $duplicate = Get-Case -Table $duplicates -Name 'duplicate-id-rejected'
    Assert-Equal -Expected 'reject-before-read' -Actual $duplicate.expected -Message 'Duplicate pattern IDs must be rejected before reading.'
    $equal = Get-Case -Table $duplicates -Name 'equal-text-distinct-ids-accepted'
    Assert-Equal -Expected 'first,second' -Actual (@($equal.expectedPatternIds) -join ',') -Message 'Distinct IDs must remain independently labeled.'
    $equalRows = 0
    foreach ($pattern in @($equal.patterns)) {
        $equalRows += @(Get-LiteralSpans -Record 'TODO' -Pattern $pattern.pattern).Count
    }
    Assert-Equal -Expected $equal.expectedRowsForRecordTODO -Actual $equalRows -Message 'Equal text under distinct IDs must not be deduplicated.'
    $empty = Get-TruthTable -Contract $contract -Id 'empty-literal' | Select-Object -ExpandProperty cases | Where-Object name -eq 'empty-literal-rejected'
    Assert-Equal -Expected 'reject-before-read' -Actual $empty.expected -Message 'Empty literals must be rejected before reading.'
    Assert-Equal -Expected 0 -Actual @(Get-LiteralSpans -Record 'anything' -Pattern '').Count -Message 'The independent oracle must not emit empty literal rows.'
    Complete-Scenario -Name 'duplicate-identities-and-empty-literals' -StartAssertion $start

    $start = $script:assertionCount
    $anchors = Get-TruthTable -Contract $contract -Id 'anchors'
    foreach ($name in @('whole-record', 'not-whole-record', 'end-anchor')) {
        $case = Get-Case -Table $anchors -Name $name
        Assert-True -Condition ($null -ne $case.expectedUtf16Spans) -Message "Anchor case '$name' must declare expected spans."
    }
    Assert-Spans -Expected (Get-Case -Table $anchors -Name 'whole-record').expectedUtf16Spans -Actual @([int[]]@(0, 4)) -Message 'Whole-record anchors must match the entire record.'
    Assert-Spans -Expected (Get-Case -Table $anchors -Name 'not-whole-record').expectedUtf16Spans -Actual @() -Message 'Whole-record anchors must reject a suffix.'
    Assert-Spans -Expected (Get-Case -Table $anchors -Name 'end-anchor').expectedUtf16Spans -Actual @([int[]]@(4, 8)) -Message 'End anchors must match the record end.'
    $zeroWidth = Get-TruthTable -Contract $contract -Id 'zero-width'
    Assert-Spans -Expected (Get-Case -Table $zeroWidth -Name 'start-anchor').expectedUtf16Spans -Actual @([int[]]@(0, 0)) -Message 'Start zero-width matches must be emitted.'
    Assert-Spans -Expected (Get-Case -Table $zeroWidth -Name 'end-anchor').expectedUtf16Spans -Actual @([int[]]@(3, 3)) -Message 'End zero-width matches must be emitted once.'
    Assert-Spans -Expected (Get-Case -Table $zeroWidth -Name 'both-anchors').expectedUtf16Spans -Actual @([int[]]@(0, 0), [int[]]@(3, 3)) -Message 'Zero-width progress must reach the end without looping.'
    Assert-Equal -Expected 0 -Actual $contract.zeroWidthSemantics.byteLength -Message 'Zero-width matches must have zero byte length.'
    Assert-True -Condition ($contract.zeroWidthSemantics.progress -match 'one Unicode scalar') -Message 'Zero-width progress must be Unicode-scalar based.'
    Complete-Scenario -Name 'anchors-and-zero-width-progress' -StartAssertion $start

    $start = $script:assertionCount
    $unicode = Get-TruthTable -Contract $contract -Id 'unicode-boundaries'
    Assert-Spans -Expected (Get-Case -Table $unicode -Name 'composed-word').expectedUtf16Spans -Actual @([int[]]@(0, 4)) -Message 'Composed Unicode whole-word matching must retain its exact form.'
    Assert-Spans -Expected (Get-Case -Table $unicode -Name 'decomposed-word').expectedUtf16Spans -Actual @([int[]]@(5, 10)) -Message 'Decomposed Unicode whole-word matching must include its combining mark.'
    Assert-Spans -Expected (Get-Case -Table $unicode -Name 'combining-mark-does-not-create-boundary').expectedUtf16Spans -Actual @() -Message 'A following combining mark must not create a false whole-word boundary.'
    Assert-Equal -Expected 'none' -Actual $contract.wholeWordSemantics.normalization -Message 'Whole-word matching must not normalize text.'
    Assert-True -Condition ($contract.wholeWordSemantics.wordScalarCategories -contains 'non-spacing-mark') -Message 'Combining marks must be classified as word-like.'
    Assert-Equal -Expected 'auto,utf8,utf8-bom,utf16-le-bom,utf16-be-bom' -Actual (@($contract.encodingSemantics.modes) -join ',') -Message 'Encoding modes must be explicit.'
    Assert-True -Condition ($contract.encodingSemantics.invalidBytes -match 'typed failure') -Message 'Invalid input bytes must not use replacement fallback.'
    Complete-Scenario -Name 'unicode-boundaries-and-encoding' -StartAssertion $start

    [ordered]@{
        formatVersion = 1
        test = 'Search match semantics'
        status = 'passed'
        contractPath = 'docs/search/search-match-semantics-v1.json'
        scenarios = $script:scenarios
        scenarioCount = $script:scenarioCount
        assertions = $script:assertionCount
        implementationNote = 'This is a proposed semantics test with an independent literal oracle; it does not claim that Search methods are installed.'
    } | ConvertTo-Json -Depth 15
}
catch {
    [ordered]@{
        formatVersion = 1
        test = 'Search match semantics'
        status = 'failed'
        scenarioCount = $script:scenarioCount
        assertions = $script:assertionCount
        error = $_.Exception.Message
    } | ConvertTo-Json -Depth 12
    exit 1
}
