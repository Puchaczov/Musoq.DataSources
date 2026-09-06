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
        [AllowNull()]
        [object]$Expected,

        [Parameter(Mandatory = $true)]
        [AllowNull()]
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

function Find-DuplicateJsonProperties {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Json
    )

    $duplicates = [System.Collections.Generic.List[string]]::new()
    $objectKeys = [System.Collections.Generic.List[System.Collections.Generic.HashSet[string]]]::new()
    $index = 0

    while ($index -lt $Json.Length) {
        $character = $Json[$index]
        if ($character -eq '"') {
            $start = $index
            $index++
            while ($index -lt $Json.Length) {
                if ($Json[$index] -eq '\\') {
                    $index += 2
                    continue
                }

                if ($Json[$index] -eq '"') {
                    break
                }

                $index++
            }

            $end = $index
            $lookahead = $index + 1
            while ($lookahead -lt $Json.Length -and [char]::IsWhiteSpace($Json[$lookahead])) {
                $lookahead++
            }

            if ($lookahead -lt $Json.Length -and $Json[$lookahead] -eq ':' -and $objectKeys.Count -gt 0) {
                $rawName = $Json.Substring($start + 1, $end - $start - 1)
                $propertyName = try {
                    ConvertFrom-Json -InputObject ('"' + $rawName + '"') -ErrorAction Stop
                }
                catch {
                    $rawName
                }
                $currentObject = $objectKeys[$objectKeys.Count - 1]
                if (-not $currentObject.Add([string]$propertyName)) {
                    $duplicates.Add([string]$propertyName)
                }
            }

            $index++
            continue
        }

        if ($character -eq '{') {
            $objectKeys.Add([System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal))
        }
        elseif ($character -eq '}' -and $objectKeys.Count -gt 0) {
            $objectKeys.RemoveAt($objectKeys.Count - 1)
        }

        $index++
    }

    return @($duplicates)
}

function Get-RequestValidationError {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$RawJson
    )

    $duplicates = @(Find-DuplicateJsonProperties -Json $RawJson)
    if ($duplicates.Count -gt 0) {
        return 'reject-duplicate-json-property'
    }

    try {
        $request = $RawJson | ConvertFrom-Json -AsHashtable -ErrorAction Stop
    }
    catch {
        return 'reject-invalid-json'
    }

    if ($request -isnot [System.Collections.IDictionary]) {
        return 'reject-invalid-root-shape'
    }

    $allowedRequestProperties = @('version', 'patterns', 'options')
    foreach ($property in @($request.Keys)) {
        if ($allowedRequestProperties -notcontains [string]$property) {
            return 'reject-unknown-property'
        }
    }

    if (-not $request.ContainsKey('version') -or $request['version'] -ne 1) {
        return 'reject-invalid-version'
    }

    if (-not $request.ContainsKey('patterns') -or $request['patterns'] -is [string]) {
        return 'reject-invalid-pattern-array'
    }

    $patterns = @($request['patterns'])
    if ($patterns.Count -lt 1 -or $patterns.Count -gt 1024) {
        return 'reject-pattern-count'
    }

    $patternIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($pattern in $patterns) {
        if ($pattern -isnot [System.Collections.IDictionary]) {
            return 'reject-invalid-pattern-object'
        }

        $allowedPatternProperties = @('id', 'pattern', 'mode')
        foreach ($property in @($pattern.Keys)) {
            if ($allowedPatternProperties -notcontains [string]$property) {
                return 'reject-unknown-property'
            }
        }

        foreach ($required in @('id', 'pattern', 'mode')) {
            if (-not $pattern.ContainsKey($required)) {
                return 'reject-missing-pattern-property'
            }
        }

        $id = [string]$pattern['id']
        $patternText = [string]$pattern['pattern']
        if ($id.Length -lt 1 -or $id.Length -gt 128 -or $id -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$') {
            return 'reject-pattern-id-bounds'
        }

        if ($patternText.Length -lt 1 -or $patternText.Length -gt 65536) {
            return 'reject-pattern-text-bounds'
        }

        if (-not $patternIds.Add($id)) {
            return 'reject-duplicate-pattern-id'
        }

        if (@('literal', 'regex') -notcontains [string]$pattern['mode']) {
            return 'reject-invalid-enum'
        }
    }

    if ($request.ContainsKey('options')) {
        $options = $request['options']
        if ($options -isnot [System.Collections.IDictionary]) {
            return 'reject-invalid-options-object'
        }

        $allowedOptionProperties = @('case', 'wholeWord', 'encoding', 'selection', 'take', 'partialPolicy', 'validation', 'scope')
        foreach ($property in @($options.Keys)) {
            if ($allowedOptionProperties -notcontains [string]$property) {
                return 'reject-unknown-property'
            }
        }

        if ($options.ContainsKey('case') -and @('sensitive', 'insensitive') -notcontains [string]$options['case']) {
            return 'reject-invalid-enum'
        }
        if ($options.ContainsKey('wholeWord') -and $options['wholeWord'] -isnot [bool]) {
            return 'reject-invalid-boolean'
        }
        if ($options.ContainsKey('encoding') -and @('auto', 'utf8', 'utf8-bom', 'utf16-le-bom', 'utf16-be-bom') -notcontains [string]$options['encoding']) {
            return 'reject-invalid-enum'
        }
        if ($options.ContainsKey('selection') -and [string]$options['selection'] -ne 'leftmost-first-non-overlapping') {
            return 'reject-invalid-enum'
        }
        if ($options.ContainsKey('take')) {
            $take = $options['take']
            if ($take -isnot [int] -and $take -isnot [long] -or [long]$take -lt 1 -or [long]$take -gt 1000000) {
                return 'reject-take-bounds'
            }
        }
        if ($options.ContainsKey('partialPolicy') -and @('reject', 'allow') -notcontains [string]$options['partialPolicy']) {
            return 'reject-invalid-enum'
        }
        if ($options.ContainsKey('validation') -and @('full-input', 'observed-prefix') -notcontains [string]$options['validation']) {
            return 'reject-invalid-enum'
        }

        if ($options.ContainsKey('scope')) {
            $scope = $options['scope']
            if ($scope -isnot [System.Collections.IDictionary]) {
                return 'reject-invalid-scope-object'
            }

            $allowedScopeProperties = @('recursive', 'include', 'exclude', 'repositoryIgnores', 'globalIgnores', 'hiddenEntries', 'followLinks', 'inaccessibleEntries')
            foreach ($property in @($scope.Keys)) {
                if ($allowedScopeProperties -notcontains [string]$property) {
                    return 'reject-unknown-property'
                }
            }

            if ($scope.ContainsKey('recursive') -and $scope['recursive'] -isnot [bool]) {
                return 'reject-invalid-boolean'
            }
            foreach ($listName in @('include', 'exclude')) {
                if ($scope.ContainsKey($listName)) {
                    $list = @($scope[$listName])
                    if ($list.Count -gt 128 -or @($list | Where-Object { $_ -isnot [string] -or $_.Length -gt 4096 }).Count -gt 0) {
                        return 'reject-scope-list-bounds'
                    }
                }
            }
            if ($scope.ContainsKey('repositoryIgnores') -and @('respect', 'disabled') -notcontains [string]$scope['repositoryIgnores']) {
                return 'reject-invalid-enum'
            }
            if ($scope.ContainsKey('globalIgnores') -and @('disabled', 'configured') -notcontains [string]$scope['globalIgnores']) {
                return 'reject-invalid-enum'
            }
            if ($scope.ContainsKey('hiddenEntries') -and @('exclude', 'include') -notcontains [string]$scope['hiddenEntries']) {
                return 'reject-invalid-enum'
            }
            if ($scope.ContainsKey('followLinks') -and @('do not follow', 'follow') -notcontains [string]$scope['followLinks']) {
                return 'reject-invalid-enum'
            }
            if ($scope.ContainsKey('inaccessibleEntries') -and @('fail', 'skip') -notcontains [string]$scope['inaccessibleEntries']) {
                return 'reject-invalid-enum'
            }
        }
    }

    return $null
}

$script:assertionCount = 0
$script:scenarioCount = 0
$script:scenarios = [System.Collections.Generic.List[object]]::new()
$contractPath = Join-Path $PSScriptRoot '../../docs/search/search-request-contract-v1.json'
$contract = Get-Content -LiteralPath $contractPath -Raw -Encoding utf8 | ConvertFrom-Json

try {
    $start = $script:assertionCount
    Assert-Equal -Expected 1 -Actual $contract.version -Message 'The request contract version must be one.'
    Assert-Equal -Expected 'proposed' -Actual $contract.status -Message 'The request contract must remain explicitly proposed.'
    Assert-Equal -Expected 'W01-S05' -Actual $contract.scope -Message 'The request contract must identify its owning scope.'
    Assert-Equal -Expected 'one scalar JSON string containing the versioned request object' -Actual $contract.transport.many.shape -Message 'Scalar JSON transport must be explicit.'
    Assert-True -Condition ($contract.transport.many.collectionBinding -match 'not assumed') -Message 'Collection binding must not be inferred from documentation.'
    Assert-Equal -Expected 'version,patterns' -Actual (@($contract.requestSchema.required) -join ',') -Message 'Version and patterns must be required.'
    Assert-Equal -Expected $false -Actual $contract.requestSchema.additionalProperties -Message 'The top-level request must be closed.'
    Assert-Equal -Expected 1024 -Actual $contract.requestSchema.properties.patterns.maxItems -Message 'Pattern count must be bounded.'
    Assert-Equal -Expected 'id,pattern,mode' -Actual (@($contract.requestSchema.properties.patterns.items.required) -join ',') -Message 'Pattern labels, text and mode must be required.'
    Assert-Equal -Expected $false -Actual $contract.requestSchema.properties.options.additionalProperties -Message 'Options must be closed.'
    Complete-Scenario -Name 'schema-and-transport' -StartAssertion $start

    $start = $script:assertionCount
    Assert-Equal -Expected 'duplicate JSON property detection,JSON syntax,closed-schema unknown-property rejection,version and enum validation,pattern ID uniqueness,bounds and cross-field rules,filesystem access' -Actual (@($contract.transport.validationOrder) -join ',') -Message 'Validation order must keep filesystem access last.'
    Assert-True -Condition ($contract.semanticRules.duplicateJsonProperty -match 'Reject duplicate') -Message 'Duplicate JSON properties must be rejected.'
    Assert-True -Condition ($contract.semanticRules.duplicatePatternId -match 'Reject duplicate') -Message 'Duplicate pattern IDs must be rejected.'
    Assert-True -Condition ($contract.semanticRules.unknownProperty -match 'Reject unknown') -Message 'Unknown properties must be rejected.'
    Assert-True -Condition ($contract.semanticRules.scalarTransport -match 'complete request') -Message 'Scalar JSON must be validated as a complete request.'
    Assert-True -Condition ($contract.transport.filesystemBoundary -match 'before opening') -Message 'Invalid requests must fail before filesystem access.'
    Assert-True -Condition ($contract.semanticRules.metadata -match 'proposed') -Message 'Metadata expectations must remain explicitly proposed.'
    Complete-Scenario -Name 'validation-order-and-rejection-rules' -StartAssertion $start

    $start = $script:assertionCount
    foreach ($case in @($contract.validationCases)) {
        if ($case.PSObject.Properties.Name -contains 'raw') {
            $raw = [string]$case.raw
        }
        else {
            $raw = $case.request | ConvertTo-Json -Compress -Depth 15
        }

        $actual = Get-RequestValidationError -RawJson $raw
        Assert-Equal -Expected $case.expected -Actual $actual -Message "Validation result mismatch for '$($case.id)'."
    }
    Complete-Scenario -Name 'rejection-corpus' -StartAssertion $start

    $start = $script:assertionCount
    foreach ($example in @($contract.acceptanceExamples)) {
        $raw = $example.request | ConvertTo-Json -Compress -Depth 15
        Assert-Equal -Expected $null -Actual (Get-RequestValidationError -RawJson $raw) -Message "Acceptance request '$($example.id)' must validate."
        if ($example.style -eq 'positional') {
            Assert-True -Condition ([string]$example.sql -match 'search\.many\(') -Message "Acceptance example '$($example.id)' must name the proposed source."
            Assert-True -Condition ([string]$example.sql -match "search\.many\('./fixture',") -Message 'The positional example must use positional root and request arguments.'
        }
        elseif ($example.style -eq 'named') {
            Assert-True -Condition ([string]$example.sql -match 'search\.many\(') -Message "Acceptance example '$($example.id)' must name the proposed source."
            Assert-True -Condition ([string]$example.sql -match "root: './fixture', request:") -Message 'The named example must expose root and request names.'
        }
        else {
            Assert-Equal -Expected 'scalar-json' -Actual $example.style -Message 'The labeled example must retain scalar JSON transport.'
        }

        $expectedRows = @($example.expectedRows)
        Assert-True -Condition ($expectedRows.Count -gt 0) -Message "Acceptance example '$($example.id)' must include expected rows."
        Assert-Equal -Expected ($expectedRows.Count) -Actual (@($expectedRows | Where-Object { $_.Path -eq 'src/a.txt' }).Count) -Message "Expected rows for '$($example.id)' must retain path identity."
        Assert-True -Condition (@($expectedRows | Where-Object { [string]::IsNullOrEmpty([string]$_.PatternId) }).Count -eq 0) -Message "Expected rows for '$($example.id)' must retain PatternId labels."
    }

    $two = @($contract.acceptanceExamples | Where-Object { $_.id -eq 'labeled-two-patterns' })[0]
    Assert-Equal -Expected 4 -Actual @($two.expectedRows).Count -Message 'Equal text under two labels must retain four independent occurrence rows.'
    Assert-Equal -Expected 'todo-primary,todo-primary,todo-secondary,todo-secondary' -Actual (@($two.expectedRows | ForEach-Object { $_.PatternId }) -join ',') -Message 'Pattern labels must remain row identity.'
    Assert-True -Condition (@($contract.acceptanceExamples | Where-Object { $_.style -eq 'positional' }).Count -gt 0) -Message 'A positional acceptance example is required.'
    Assert-True -Condition (@($contract.acceptanceExamples | Where-Object { $_.style -eq 'named' }).Count -gt 0) -Message 'A named acceptance example is required.'
    Complete-Scenario -Name 'acceptance-examples-and-cardinality' -StartAssertion $start

    [ordered]@{
        formatVersion = 1
        test = 'Search request contract'
        status = 'passed'
        contractPath = 'docs/search/search-request-contract-v1.json'
        scenarios = $script:scenarios
        scenarioCount = $script:scenarioCount
        assertions = $script:assertionCount
        implementationNote = 'This is a proposed request/schema test with an independent duplicate-key scanner and validator; it does not claim that Search request binding or SQL execution is installed.'
    } | ConvertTo-Json -Depth 15
}
catch {
    [ordered]@{
        formatVersion = 1
        test = 'Search request contract'
        status = 'failed'
        scenarioCount = $script:scenarioCount
        assertions = $script:assertionCount
        error = $_.Exception.Message
    } | ConvertTo-Json -Depth 12
    exit 1
}
