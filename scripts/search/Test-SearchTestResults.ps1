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

function Write-Trx {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Project,

        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$ResultsXml,

        [Parameter(Mandatory = $true)]
        [string]$CountersXml,

        [string]$DefinitionsXml = ''
    )

    $xml = @"
<?xml version="1.0" encoding="utf-8"?>
<TestRun id="$([Guid]::NewGuid())" xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <TestDefinitions>$DefinitionsXml</TestDefinitions>
  <Results>$ResultsXml</Results>
  <ResultSummary><Counters $CountersXml /></ResultSummary>
</TestRun>
"@
    Set-Content -LiteralPath $Path -Value $xml -Encoding utf8NoBOM
}

function Invoke-Inspector {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ResultsPath,

        [AllowNull()]
        [string]$ExpectedProjectsPath
    )

    $inspector = Join-Path $PSScriptRoot 'Inspect-SearchTestResults.ps1'
    $pwsh = (Get-Command pwsh -CommandType Application -ErrorAction Stop).Source
    $arguments = @('-NoProfile', '-NonInteractive', '-File', $inspector, '-ResultsPath', $ResultsPath)
    if (-not [string]::IsNullOrWhiteSpace($ExpectedProjectsPath)) {
        $arguments += @('-ExpectedProjectsPath', $ExpectedProjectsPath)
    }

    $output = @(& $pwsh @arguments 2>&1)
    [pscustomobject]@{
        exitCode = $LASTEXITCODE
        text = ConvertTo-Text $output
    }
}

function New-ExpectedProjects {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string[]]$Projects
    )

    $Projects | ConvertTo-Json | Set-Content -LiteralPath $Path -Encoding utf8NoBOM
}

$script:assertionCount = 0
$scenarioCount = 0
$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) ('musoq-search-trx-' + [Guid]::NewGuid().ToString('N'))
$scenarios = [System.Collections.Generic.List[object]]::new()

try {
    $validRoot = Join-Path $temporaryRoot 'valid\Fixture.Project'
    New-Item -ItemType Directory -Path $validRoot -Force | Out-Null
    $definitions = '<UnitTest name="PassOne" storage="C:\fixture\Fixture.Project.dll" id="one" /><UnitTest name="PassTwo" storage="C:\fixture\Fixture.Project.dll" id="two" />'
    $results = '<UnitTestResult testName="PassOne" outcome="Passed" /><UnitTestResult testName="PassTwo" outcome="Passed" />'
    Write-Trx -Path (Join-Path $validRoot 'valid.trx') -Project 'Fixture.Project' -DefinitionsXml $definitions -ResultsXml $results -CountersXml 'total="2" executed="2" passed="2" failed="0" notExecuted="0" inconclusive="0" notRunnable="0"'
    $validManifest = Join-Path $temporaryRoot 'valid-projects.json'
    New-ExpectedProjects -Path $validManifest -Projects @('Fixture.Project')
    $valid = Invoke-Inspector -ResultsPath (Join-Path $temporaryRoot 'valid') -ExpectedProjectsPath $validManifest
    Assert-Equal -Expected 0 -Actual $valid.exitCode -Message "A consistent TRX should pass. Output: $($valid.text)"
    $validResult = $valid.text | ConvertFrom-Json
    Assert-Equal -Expected 'passed' -Actual $validResult.status -Message 'Consistent TRX should be marked passed.'
    Assert-Equal -Expected 2 -Actual $validResult.totals.discovered -Message 'TRX discovered count should come from counters.'
    Assert-True -Condition ($validResult.observedProjects -contains 'Fixture.Project') -Message 'Project should be derived from assembly storage metadata.'
    $scenarioCount++
    $scenarios.Add([ordered]@{ name = 'consistent-trx'; result = 'passed'; assertions = 4 })

    $zeroRoot = Join-Path $temporaryRoot 'zero\Zero.Project'
    New-Item -ItemType Directory -Path $zeroRoot -Force | Out-Null
    Write-Trx -Path (Join-Path $zeroRoot 'zero.trx') -Project 'Zero.Project' -ResultsXml '' -CountersXml 'total="0" executed="0" passed="0" failed="0" notExecuted="0" inconclusive="0" notRunnable="0"'
    $zero = Invoke-Inspector -ResultsPath (Join-Path $temporaryRoot 'zero')
    Assert-True -Condition ($zero.exitCode -ne 0) -Message 'A zero-test TRX must fail validation.'
    $zeroResult = $zero.text | ConvertFrom-Json
    Assert-Equal -Expected 'invalid' -Actual $zeroResult.status -Message 'Zero-test TRX should be marked invalid.'
    Assert-True -Condition ($zeroResult.zeroTestRuns.Count -eq 1) -Message 'Zero-test TRX should be listed explicitly.'
    $scenarioCount++
    $scenarios.Add([ordered]@{ name = 'zero-test-success'; result = 'passed'; assertions = 3 })

    $missingRoot = Join-Path $temporaryRoot 'missing\Present.Project'
    New-Item -ItemType Directory -Path $missingRoot -Force | Out-Null
    Write-Trx -Path (Join-Path $missingRoot 'present.trx') -Project 'Present.Project' -DefinitionsXml '<UnitTest name="Present" storage="C:\fixture\Present.Project.dll" id="present" />' -ResultsXml '<UnitTestResult testName="Present" outcome="Passed" />' -CountersXml 'total="1" executed="1" passed="1" failed="0" notExecuted="0" inconclusive="0" notRunnable="0"'
    $missingManifest = Join-Path $temporaryRoot 'missing-projects.json'
    New-ExpectedProjects -Path $missingManifest -Projects @('Present.Project', 'Missing.Project')
    $missing = Invoke-Inspector -ResultsPath (Join-Path $temporaryRoot 'missing') -ExpectedProjectsPath $missingManifest
    Assert-True -Condition ($missing.exitCode -ne 0) -Message 'A missing expected project must fail validation.'
    $missingResult = $missing.text | ConvertFrom-Json
    Assert-True -Condition ($missingResult.missingProjects -contains 'Missing.Project') -Message 'Missing project should be named in the result.'
    $scenarioCount++
    $scenarios.Add([ordered]@{ name = 'missing-project'; result = 'passed'; assertions = 2 })

    $skipRoot = Join-Path $temporaryRoot 'unexpected-skip\Skip.Project'
    New-Item -ItemType Directory -Path $skipRoot -Force | Out-Null
    Write-Trx -Path (Join-Path $skipRoot 'skip.trx') -Project 'Skip.Project' -DefinitionsXml '<UnitTest name="Skipped" storage="C:\fixture\Skip.Project.dll" id="skip" />' -ResultsXml '<UnitTestResult testName="Skipped" outcome="NotExecuted" />' -CountersXml 'total="1" executed="0" passed="0" failed="0" notExecuted="1" inconclusive="0" notRunnable="0"'
    $skip = Invoke-Inspector -ResultsPath (Join-Path $temporaryRoot 'unexpected-skip')
    Assert-True -Condition ($skip.exitCode -ne 0) -Message 'An unapproved skip must fail validation.'
    $skipResult = $skip.text | ConvertFrom-Json
    Assert-True -Condition ($skipResult.unexpectedSkips.Count -eq 1) -Message 'Unexpected skip should be retained with its test name.'
    $scenarioCount++
    $scenarios.Add([ordered]@{ name = 'unexpected-skip'; result = 'passed'; assertions = 2 })

    [ordered]@{
        formatVersion = 1
        test = 'Search TRX result inspection'
        status = 'passed'
        scenarios = $scenarios
        scenarioCount = $scenarioCount
        assertions = $script:assertionCount
        temporaryFixturePolicy = 'All TRX fixtures were created below the system temporary directory and removed in finally; no repository result files were changed.'
    } | ConvertTo-Json -Depth 10
}
finally {
    if (Test-Path -LiteralPath $temporaryRoot) {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
    }
}
