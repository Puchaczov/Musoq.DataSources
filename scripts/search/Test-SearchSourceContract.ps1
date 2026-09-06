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

function Get-CallShape {
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$Contract,

        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $shape = @($Contract.callShapes | Where-Object { $_.name -eq $Name })
    if ($shape.Count -ne 1) {
        throw "Expected exactly one call shape for '$Name', got $($shape.Count)."
    }

    return $shape[0]
}

function Get-SourceSchema {
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$Contract,

        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $property = @($Contract.sourceSchemas.PSObject.Properties | Where-Object { $_.Name -eq $Name })
    if ($property.Count -ne 1) {
        throw "Expected exactly one source schema for '$Name', got $($property.Count)."
    }

    return $property[0].Value
}

function Get-Field {
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$Schema,

        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $collectionFields = if ($null -ne $Schema.PSObject.Properties['explicitCollections']) {
        @($Schema.explicitCollections)
    }
    else {
        @()
    }
    $field = @(@($Schema.selectStar) + $collectionFields |
        Where-Object { $_.name -eq $Name })
    if ($field.Count -ne 1) {
        throw "Expected exactly one '$Name' field, got $($field.Count)."
    }

    return $field[0]
}

function Get-LiteralOffsets {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Text,

        [Parameter(Mandatory = $true)]
        [string]$Pattern
    )

    $offsets = [System.Collections.Generic.List[int]]::new()
    $cursor = 0
    while ($cursor -le ($Text.Length - $Pattern.Length)) {
        $offset = $Text.IndexOf($Pattern, $cursor, [StringComparison]::Ordinal)
        if ($offset -lt 0) {
            break
        }

        $offsets.Add($offset)
        $cursor = $offset + $Pattern.Length
    }

    return @($offsets)
}

$script:assertionCount = 0
$scenarioCount = 0
$scenarios = [System.Collections.Generic.List[object]]::new()
$contractPath = Join-Path $PSScriptRoot '../../docs/search/search-source-contract-v1.json'
$contract = Get-Content -LiteralPath $contractPath -Raw -Encoding utf8 | ConvertFrom-Json

try {
    Assert-Equal -Expected 1 -Actual $contract.version -Message 'The source contract version must be one.'
    Assert-Equal -Expected 'proposed' -Actual $contract.status -Message 'The source contract must remain explicitly proposed before implementation.'
    Assert-Equal -Expected 'W01-S01' -Actual $contract.scope -Message 'The contract must identify its owning scope.'
    $scenarioCount++
    $scenarios.Add([ordered]@{ name = 'contract-identity'; result = 'passed'; assertions = 3 })

    $expectedNames = @('matches', 'lines', 'files', 'counts', 'paths', 'many', 'bytes')
    $actualNames = @($contract.callShapes | ForEach-Object { $_.name })
    Assert-Equal -Expected ($expectedNames -join ',') -Actual ($actualNames -join ',') -Message 'The source list and order must be stable.'
    foreach ($name in $expectedNames) {
        $shape = Get-CallShape -Contract $contract -Name $name
        Assert-True -Condition $shape.projectionInvariant -Message "$name must preserve row identity under projection."
        Assert-True -Condition ($shape.arguments.Count -ge 1) -Message "$name must expose at least its root argument."
    }
    Assert-True -Condition (-not (Get-CallShape -Contract $contract -Name 'paths').contentRead) -Message 'paths must not read file content.'
    Assert-True -Condition ((Get-CallShape -Contract $contract -Name 'matches').contentRead) -Message 'matches must be a content source.'
    $scenarioCount++
    $scenarios.Add([ordered]@{ name = 'source-shapes-and-projection'; result = 'passed'; assertions = 17 })

    $matchesSchema = Get-SourceSchema -Contract $contract -Name 'matches'
    $linesSchema = Get-SourceSchema -Contract $contract -Name 'lines'
    $countsSchema = Get-SourceSchema -Contract $contract -Name 'counts'
    $bytesSchema = Get-SourceSchema -Contract $contract -Name 'bytes'
    Assert-Equal -Expected 'string' -Actual (Get-Field -Schema $matchesSchema -Name 'Path').type -Message 'matches.Path must be a string.'
    Assert-True -Condition (-not (Get-Field -Schema $matchesSchema -Name 'Path').nullable) -Message 'matches.Path must be non-null.'
    Assert-Equal -Expected 'long' -Actual (Get-Field -Schema $matchesSchema -Name 'MatchIndex').type -Message 'MatchIndex must be a 64-bit ordinal.'
    Assert-True -Condition ((Get-Field -Schema $matchesSchema -Name 'MatchText').nullable) -Message 'Compact matches must permit null MatchText.'
    Assert-True -Condition ((Get-Field -Schema $matchesSchema -Name 'LineText').nullable) -Message 'Compact matches must permit null LineText.'
    Assert-True -Condition (-not (Get-Field -Schema $matchesSchema -Name 'Captures').nullable) -Message 'Captures must be an empty collection, not null.'
    Assert-Equal -Expected 'long' -Actual (Get-Field -Schema $linesSchema -Name 'OccurrenceCount').type -Message 'lines.OccurrenceCount must be a 64-bit counter.'
    Assert-True -Condition (-not (Get-Field -Schema $countsSchema -Name 'Complete').nullable) -Message 'counts.Complete must be non-null.'
    Assert-Equal -Expected 'long' -Actual (Get-Field -Schema $countsSchema -Name 'BytesScanned').type -Message 'counts.BytesScanned must be a 64-bit counter.'
    Assert-True -Condition (-not (Get-Field -Schema $bytesSchema -Name 'ByteOffset').nullable) -Message 'bytes.ByteOffset must always be available.'
    Assert-True -Condition (-not (Get-Field -Schema $bytesSchema -Name 'ByteLength').nullable) -Message 'bytes.ByteLength must always be available.'
    Assert-True -Condition ((Get-Field -Schema $bytesSchema -Name 'LineNumber').nullable) -Message 'Raw byte rows must not infer text line numbers.'
    Assert-Equal -Expected 'empty' -Actual $contract.defaults.captures -Message 'The default capture collection must be empty.'
    Assert-Equal -Expected 'empty' -Actual $contract.defaults.context -Message 'The default context collection must be empty.'
    Assert-Equal -Expected 'literal' -Actual $contract.defaults.textMode -Message 'Literal matching must be the simple default.'
    $scenarioCount++
    $scenarios.Add([ordered]@{ name = 'schema-nullability-and-defaults'; result = 'passed'; assertions = 15 })

    $fixture = @(
        [pscustomobject]@{ path = 'src/a.txt'; text = "TODO TODO`n" },
        [pscustomobject]@{ path = 'src/empty.txt'; text = '' },
        [pscustomobject]@{ path = 'src/none.txt'; text = "DONE`n" }
    )
    $occurrenceRows = [System.Collections.Generic.List[object]]::new()
    foreach ($file in $fixture) {
        $offsets = @(Get-LiteralOffsets -Text $file.text -Pattern 'TODO')
        for ($index = 0; $index -lt $offsets.Count; $index++) {
            $occurrenceRows.Add([pscustomobject]@{
                    path = $file.path
                    matchIndex = $index
                    byteOffset = $offsets[$index]
                    lineNumber = 1
            })
        }
    }
    $twoHitsExample = @($contract.goldenExamples | Where-Object { $_.id -eq 'two-hits-one-line' })[0]
    $projectedPaths = @($occurrenceRows | ForEach-Object { $_.path })
    Assert-Equal -Expected $twoHitsExample.expected.matchesRows -Actual $occurrenceRows.Count -Message 'Two hits on one line must produce two occurrence rows.'
    Assert-Equal -Expected (@($twoHitsExample.expected.projectedPathFromMatches) -join ',') -Actual ($projectedPaths -join ',') -Message 'Projecting Path must retain occurrence multiplicity.'
    Assert-Equal -Expected $twoHitsExample.expected.matchingOccurrences -Actual (@($occurrenceRows | Where-Object { $_.lineNumber -eq 1 }).Count) -Message 'Both occurrences must remain on the physical line.'
    Assert-Equal -Expected $twoHitsExample.expected.matchingFiles -Actual (@($occurrenceRows | Select-Object -ExpandProperty path -Unique).Count) -Message 'The occurrence fixture must have one matching file.'
    $scenarioCount++
    $scenarios.Add([ordered]@{ name = 'two-hits-one-line'; result = 'passed'; assertions = 4 })

    $countRows = @(
        [pscustomobject]@{ path = 'src/a.txt'; occurrenceCount = 2; matchingLineCount = 1 },
        [pscustomobject]@{ path = 'src/empty.txt'; occurrenceCount = 0; matchingLineCount = 0 },
        [pscustomobject]@{ path = 'src/none.txt'; occurrenceCount = 0; matchingLineCount = 0 }
    )
    $zeroHitExample = @($contract.goldenExamples | Where-Object { $_.id -eq 'zero-hit-files' })[0]
    Assert-Equal -Expected $twoHitsExample.expected.countsRows -Actual $countRows.Count -Message 'Counts must retain every eligible file.'
    Assert-Equal -Expected (@($zeroHitExample.expected.zeroHitCountPaths).Count) -Actual (@($countRows | Where-Object { $_.occurrenceCount -eq 0 }).Count) -Message 'Counts must include both zero-hit files.'
    Assert-Equal -Expected (@($zeroHitExample.expected.zeroHitMatchPaths).Count) -Actual (@($occurrenceRows | Where-Object { $_.path -in @('src/empty.txt', 'src/none.txt') }).Count) -Message 'Zero-hit files must not emit occurrence rows.'
    Assert-Equal -Expected $twoHitsExample.expected.pathsRows -Actual $fixture.Count -Message 'Path discovery must include all eligible files without content matching.'
    $scenarioCount++
    $scenarios.Add([ordered]@{ name = 'zero-hit-files-and-counts'; result = 'passed'; assertions = 4 })

    $manyRows = [System.Collections.Generic.List[object]]::new()
    foreach ($patternId in @('todo-primary', 'todo-secondary')) {
        foreach ($row in $occurrenceRows) {
            $manyRows.Add([pscustomobject]@{
                    patternId = $patternId
                    path = $row.path
                    matchIndex = $row.matchIndex
            })
        }
    }
    $manyExample = @($contract.goldenExamples | Where-Object { $_.id -eq 'two-identical-patterns' })[0]
    $primaryRows = @($manyRows | Where-Object { $_.patternId -eq 'todo-primary' })
    $secondaryRows = @($manyRows | Where-Object { $_.patternId -eq 'todo-secondary' })
    Assert-Equal -Expected $manyExample.expected.manyRows -Actual $manyRows.Count -Message 'Equal pattern text under two labels must produce four rows.'
    Assert-Equal -Expected $manyExample.expected.rowsByPatternId.'todo-primary' -Actual $primaryRows.Count -Message 'The primary pattern label must retain both occurrences.'
    Assert-Equal -Expected $manyExample.expected.rowsByPatternId.'todo-secondary' -Actual $secondaryRows.Count -Message 'The secondary pattern label must retain both occurrences.'
    Assert-Equal -Expected $manyExample.expected.distinctPatternIds -Actual (@($manyRows | Select-Object -ExpandProperty patternId -Unique).Count) -Message 'Many must retain distinct pattern IDs.'
    $scenarioCount++
    $scenarios.Add([ordered]@{ name = 'two-identical-patterns'; result = 'passed'; assertions = 4 })

    Assert-True -Condition $contract.audit.freshExecution -Message 'Audit must execute a fresh scan on every invocation.'
    Assert-True -Condition (-not $contract.audit.globalLastRunState) -Message 'Audit must not depend on global last-run state.'
    $auditOne = [Guid]::NewGuid().ToString('N')
    $auditTwo = [Guid]::NewGuid().ToString('N')
    Assert-True -Condition ($auditOne -ne $auditTwo) -Message 'Fresh audit invocations require distinct scan identities.'
    $freshAuditExample = @($contract.goldenExamples | Where-Object { $_.id -eq 'fresh-audit' })[0]
    Assert-True -Condition $freshAuditExample.expected.twoInvocations -Message 'The fresh-audit golden example must require two invocations.'
    Assert-True -Condition $freshAuditExample.expected.previousInvocationIsNotRead -Message 'The fresh-audit golden example must forbid last-run reuse.'
    $scenarioCount++
    $scenarios.Add([ordered]@{ name = 'fresh-audit'; result = 'passed'; assertions = 5 })

    [ordered]@{
        formatVersion = 1
        test = 'Search source contract'
        status = 'passed'
        contractPath = 'docs/search/search-source-contract-v1.json'
        scenarios = $scenarios
        scenarioCount = $scenarioCount
        assertions = $script:assertionCount
        implementationNote = 'This is a design-contract test with an independent literal oracle; it does not claim that Search methods are installed.'
    } | ConvertTo-Json -Depth 12
}
catch {
    [ordered]@{
        formatVersion = 1
        test = 'Search source contract'
        status = 'failed'
        scenarioCount = $scenarioCount
        assertions = $script:assertionCount
        error = $_.Exception.Message
    } | ConvertTo-Json -Depth 12
    exit 1
}
