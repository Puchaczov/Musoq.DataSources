[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ResultsPath,

    [string]$ExpectedProjectsPath,

    [string]$AllowedSkipsPath
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

function Get-OptionalProperty {
    param(
        [AllowNull()]
        [psobject]$Object,

        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    if ($null -eq $Object) {
        return $null
    }

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

function ConvertTo-RelativePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root,

        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $resolved = (Resolve-Path -LiteralPath $Path -ErrorAction Stop).Path
    if ($resolved.StartsWith($Root, [StringComparison]::OrdinalIgnoreCase)) {
        return $resolved.Substring($Root.Length).TrimStart('\', '/')
    }

    return $resolved
}

function Get-ExpectedProjects {
    param(
        [AllowNull()]
        [string]$Path
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return @()
    }

    $resolved = (Resolve-Path -LiteralPath $Path -ErrorAction Stop).Path
    $value = Get-Content -LiteralPath $resolved -Raw -Encoding utf8 | ConvertFrom-Json
    if ($value -is [array]) {
        return @($value | ForEach-Object { $_.ToString() })
    }

    if ($value -is [string]) {
        return @($value.ToString())
    }

    $projects = Get-OptionalProperty -Object $value -Name 'projects'
    if ($null -ne $projects) {
        return @($projects | ForEach-Object { if ($_ -is [string]) { $_ } else { $_.project.ToString() } })
    }

    throw "Expected project manifest '$resolved' must be an array or contain a projects array."
}

function Get-AllowedSkips {
    param(
        [AllowNull()]
        [string]$Path
    )

    $allowed = @{}
    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $allowed
    }

    $resolved = (Resolve-Path -LiteralPath $Path -ErrorAction Stop).Path
    $value = Get-Content -LiteralPath $resolved -Raw -Encoding utf8 | ConvertFrom-Json
    $skips = if ($value -is [array]) { $value } else { Get-OptionalProperty -Object $value -Name 'skips' }
    $items = if ($value -is [array]) { @($value) } elseif ($value -is [string]) { @($value) } elseif ($null -ne $skips) { @($skips) } else { throw "Allowed skip manifest '$resolved' must be an array or contain a skips array." }
    foreach ($item in $items) {
        $name = if ($item -is [string]) { $item } else { Get-OptionalProperty -Object $item -Name 'testName' }
        if ([string]::IsNullOrWhiteSpace([string]$name)) {
            throw "Allowed skip manifest '$resolved' contains an item without testName."
        }

        $project = if ($item -is [string]) { $null } else { Get-OptionalProperty -Object $item -Name 'project' }
        $key = if ([string]::IsNullOrWhiteSpace([string]$project)) { "|$($name.ToString())" } else { "$($project.ToString())|$($name.ToString())" }
        $allowed[$key] = if ($item -is [string]) { 'declared' } else { (Get-OptionalProperty -Object $item -Name 'classification') }
    }

    return $allowed
}

function Get-ProjectNames {
    param(
        [Parameter(Mandatory = $true)]
        [System.Xml.XmlDocument]$Xml,

        [Parameter(Mandatory = $true)]
        [System.IO.FileInfo]$File
    )

    $names = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($definition in @($Xml.SelectNodes("//*[local-name()='UnitTest']"))) {
        $storage = $definition.GetAttribute('storage')
        if ([string]::IsNullOrWhiteSpace($storage)) {
            continue
        }

        $assembly = [IO.Path]::GetFileNameWithoutExtension($storage)
        if (-not [string]::IsNullOrWhiteSpace($assembly)) {
            [void]$names.Add($assembly)
        }
    }

    if ($names.Count -eq 0) {
        $parentName = $File.Directory.Name
        if ($parentName -notin @('', 'TestResults')) {
            [void]$names.Add($parentName)
        }
    }

    return @($names)
}

function Get-CounterValue {
    param(
        [Parameter(Mandatory = $true)]
        [System.Xml.XmlElement]$Counters,

        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $value = $Counters.GetAttribute($Name)
    if ($value -notmatch '^\d+$') {
        throw "TRX counters attribute '$Name' is missing or not a non-negative integer."
    }

    return [int]$value
}

function Get-OutcomeBucket {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Outcome
    )

    switch ($Outcome.ToLowerInvariant()) {
        'passed' { return 'passed' }
        'failed' { return 'failed' }
        'error' { return 'failed' }
        'timeout' { return 'failed' }
        'aborted' { return 'failed' }
        'notexecuted' { return 'skipped' }
        'inconclusive' { return 'skipped' }
        'notrunnable' { return 'skipped' }
        default { return 'unknown' }
    }
}

try {
    $root = (Resolve-Path -LiteralPath $ResultsPath -ErrorAction Stop).Path
    if (-not (Test-Path -LiteralPath $root -PathType Container)) {
        throw "ResultsPath is not a directory: '$root'."
    }

    $expectedProjects = @(Get-ExpectedProjects -Path $ExpectedProjectsPath)
    $allowedSkips = Get-AllowedSkips -Path $AllowedSkipsPath
    $files = @(Get-ChildItem -LiteralPath $root -Filter '*.trx' -File -Recurse | Sort-Object FullName)
    if ($files.Count -eq 0) {
        throw "No TRX result files were found below '$root'; a zero-file success is not accepted."
    }

    $runs = [System.Collections.Generic.List[object]]::new()
    $observedProjects = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $unexpectedSkips = [System.Collections.Generic.List[object]]::new()
    $zeroTestRuns = [System.Collections.Generic.List[string]]::new()
    $unknownOutcomes = [System.Collections.Generic.List[object]]::new()
    $counterMismatches = [System.Collections.Generic.List[object]]::new()
    $totals = [ordered]@{ discovered = 0; passed = 0; failed = 0; skipped = 0 }

    foreach ($file in $files) {
        try {
            $xml = New-Object System.Xml.XmlDocument
            $xml.PreserveWhitespace = $true
            $xml.Load($file.FullName)
        }
        catch {
            throw "Could not parse TRX '$($file.FullName)': $($_.Exception.Message)"
        }

        $testRun = $xml.SelectSingleNode("/*[local-name()='TestRun']")
        $counters = $xml.SelectSingleNode("//*[local-name()='ResultSummary']/*[local-name()='Counters']")
        if ($null -eq $testRun -or $null -eq $counters -or $counters -isnot [System.Xml.XmlElement]) {
            throw "TRX '$($file.FullName)' is missing TestRun or ResultSummary/Counters."
        }

        $counterTotal = Get-CounterValue -Counters $counters -Name 'total'
        $counterExecuted = Get-CounterValue -Counters $counters -Name 'executed'
        $counterPassed = Get-CounterValue -Counters $counters -Name 'passed'
        $counterFailed = Get-CounterValue -Counters $counters -Name 'failed'
        $results = @($xml.SelectNodes("//*[local-name()='UnitTestResult']"))
        $observed = [ordered]@{ passed = 0; failed = 0; skipped = 0; unknown = 0 }
        $projects = @(Get-ProjectNames -Xml $xml -File $file)
        $definitionProjects = @{}
        foreach ($definition in @($xml.SelectNodes("//*[local-name()='UnitTest']"))) {
            $storage = $definition.GetAttribute('storage')
            if (-not [string]::IsNullOrWhiteSpace($storage)) {
                $definitionProjects[$definition.GetAttribute('id')] = [IO.Path]::GetFileNameWithoutExtension($storage)
            }
        }
        foreach ($project in $projects) {
            [void]$observedProjects.Add($project)
        }

        foreach ($result in $results) {
            $testName = $result.GetAttribute('testName')
            $outcome = $result.GetAttribute('outcome')
            $bucket = Get-OutcomeBucket -Outcome $outcome
            $observed[$bucket]++
            $resultProject = if ($definitionProjects.ContainsKey($result.GetAttribute('testId'))) { $definitionProjects[$result.GetAttribute('testId')] } else { $null }
            $projectKey = if ([string]::IsNullOrWhiteSpace([string]$resultProject)) { '' } else { $resultProject.ToString() }
            $skipKey = "$projectKey|$testName"
            if ($bucket -eq 'skipped' -and -not ($allowedSkips.ContainsKey($skipKey) -or $allowedSkips.ContainsKey("|$testName"))) {
                $unexpectedSkips.Add([ordered]@{ file = (ConvertTo-RelativePath -Root $root -Path $file.FullName); project = $projectKey; testName = $testName; outcome = $outcome })
            }
            if ($bucket -eq 'unknown') {
                $unknownOutcomes.Add([ordered]@{ file = (ConvertTo-RelativePath -Root $root -Path $file.FullName); testName = $testName; outcome = $outcome })
            }
        }

        if ($counterTotal -eq 0) {
            $zeroTestRuns.Add((ConvertTo-RelativePath -Root $root -Path $file.FullName))
        }
        $counterSkipped = $counterTotal - $counterExecuted
        if ($counterTotal -ne $results.Count -or $counterPassed -ne $observed.passed -or $counterFailed -ne $observed.failed -or $counterExecuted -ne ($observed.passed + $observed.failed) -or $counterSkipped -ne $observed.skipped) {
            $counterMismatches.Add([ordered]@{
                file = (ConvertTo-RelativePath -Root $root -Path $file.FullName)
                counters = [ordered]@{ total = $counterTotal; executed = $counterExecuted; passed = $counterPassed; failed = $counterFailed; skipped = $counterSkipped }
                observed = $observed
                resultNodeCount = $results.Count
            })
        }

        $totals.discovered += $counterTotal
        $totals.passed += $observed.passed
        $totals.failed += $observed.failed
        $totals.skipped += $observed.skipped
        $runs.Add([ordered]@{
            path = (ConvertTo-RelativePath -Root $root -Path $file.FullName)
            id = $testRun.GetAttribute('id')
            projects = $projects
            counters = [ordered]@{ total = $counterTotal; executed = $counterExecuted; passed = $counterPassed; failed = $counterFailed; skipped = $counterSkipped }
            observed = $observed
        })
    }

    $missingProjects = @($expectedProjects | Where-Object { -not $observedProjects.Contains($_) })
    $status = if ($zeroTestRuns.Count -eq 0 -and $missingProjects.Count -eq 0 -and $unexpectedSkips.Count -eq 0 -and $unknownOutcomes.Count -eq 0 -and $counterMismatches.Count -eq 0 -and $totals.passed -gt 0 -and $totals.failed -eq 0) { 'passed' } else { 'invalid' }
    $result = [ordered]@{
        formatVersion = 1
        status = $status
        resultsPath = $root
        fileCount = $files.Count
        expectedProjects = $expectedProjects
        observedProjects = @($observedProjects | Sort-Object)
        missingProjects = $missingProjects
        totals = $totals
        zeroTestRuns = $zeroTestRuns
        unexpectedSkips = $unexpectedSkips
        unknownOutcomes = $unknownOutcomes
        counterMismatches = $counterMismatches
        allowedSkipCount = $allowedSkips.Count
        runs = $runs
    }
    $result | ConvertTo-Json -Depth 12
    if ($status -ne 'passed') {
        exit 1
    }
}
catch {
    Write-Error $_
    exit 1
}
