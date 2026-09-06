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

function Invoke-Gate {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ConfigPath,

        [switch]$RequireIgnored
    )

    $gate = Join-Path $PSScriptRoot 'Test-SearchGateConfiguration.ps1'
    $root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
    $pwsh = (Get-Command pwsh -CommandType Application -ErrorAction Stop).Source
    $arguments = @('-NoProfile', '-NonInteractive', '-File', $gate, '-ConfigPath', $ConfigPath, '-RepositoryRoot', $root)
    if ($RequireIgnored) {
        $arguments += '-RequireIgnored'
    }

    $output = @(& $pwsh @arguments 2>&1)
    [pscustomobject]@{
        exitCode = $LASTEXITCODE
        text = ConvertTo-Text $output
    }
}

function Write-Config {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [hashtable]$Value
    )

    $Value | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $Path -Encoding utf8NoBOM
}

$script:assertionCount = 0
$scenarioCount = 0
$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) ('musoq-search-gates-' + [Guid]::NewGuid().ToString('N'))
$scenarios = [System.Collections.Generic.List[object]]::new()

try {
    New-Item -ItemType Directory -Path $temporaryRoot -Force | Out-Null

    $example = Invoke-Gate -ConfigPath (Join-Path $PSScriptRoot '../../docs/campaigns/search/environment.example.json')
    Assert-Equal -Expected 0 -Actual $example.exitCode -Message "The shared environment example should pass. Output: $($example.text)"
    $exampleResult = $example.text | ConvertFrom-Json
    Assert-Equal -Expected 0 -Actual $exampleResult.secretLikeProperties -Message 'The shared environment example should contain no secret-like properties.'
    $scenarioCount++
    $scenarios.Add([ordered]@{ name = 'shared-example-command-and-secret-scan'; result = 'passed'; assertions = 2 })

    $local = Invoke-Gate -ConfigPath (Join-Path $PSScriptRoot '../../docs/campaigns/search/environment.local.json') -RequireIgnored
    Assert-Equal -Expected 0 -Actual $local.exitCode -Message "The machine-local configuration should pass and be ignored. Output: $($local.text)"
    $localResult = $local.text | ConvertFrom-Json
    Assert-True -Condition $localResult.ignored -Message 'Machine-local configuration must be ignored.'
    $scenarioCount++
    $scenarios.Add([ordered]@{ name = 'ignored-local-configuration'; result = 'passed'; assertions = 2 })

    $invalidVectorPath = Join-Path $temporaryRoot 'invalid-vector.json'
    Write-Config -Path $invalidVectorPath -Value @{
        formatVersion = 1
        fullSuiteArgv = @('dotnet', 'test', '--configuration', 'Release', '|', 'Out-File', 'result.log')
    }
    $invalidVector = Invoke-Gate -ConfigPath $invalidVectorPath
    Assert-True -Condition ($invalidVector.exitCode -ne 0) -Message 'A shell-interpolated or extended command vector must fail.'
    Assert-True -Condition ($invalidVector.text -match 'fullSuiteArgv') -Message "Invalid command vector should be named. Output: $($invalidVector.text)"
    $scenarioCount++
    $scenarios.Add([ordered]@{ name = 'invalid-command-vector'; result = 'passed'; assertions = 2 })

    $secretPath = Join-Path $temporaryRoot 'secret.json'
    Write-Config -Path $secretPath -Value @{
        formatVersion = 1
        fullSuiteArgv = @('dotnet', 'test', '--configuration', 'Release')
        apiToken = 'not-a-real-secret'
    }
    $secret = Invoke-Gate -ConfigPath $secretPath
    Assert-True -Condition ($secret.exitCode -ne 0) -Message 'A secret-like configuration property must fail.'
    Assert-True -Condition ($secret.text -match 'secret-like') -Message "Secret diagnostic should name the policy. Output: $($secret.text)"
    $scenarioCount++
    $scenarios.Add([ordered]@{ name = 'secret-like-configuration'; result = 'passed'; assertions = 2 })

    $notIgnoredPath = Join-Path $PSScriptRoot 'search-gate-unignored.json'
    Write-Config -Path $notIgnoredPath -Value @{
        formatVersion = 1
        fullSuiteArgv = @('dotnet', 'test', '--configuration', 'Release')
    }
    try {
        $notIgnored = Invoke-Gate -ConfigPath $notIgnoredPath -RequireIgnored
        Assert-True -Condition ($notIgnored.exitCode -ne 0) -Message 'A machine-specific config outside the ignored campaign path must fail RequireIgnored.'
        Assert-True -Condition ($notIgnored.text -match 'not ignored') -Message "RequireIgnored diagnostic should name the boundary. Output: $($notIgnored.text)"
    }
    finally {
        if (Test-Path -LiteralPath $notIgnoredPath) {
            Remove-Item -LiteralPath $notIgnoredPath -Force
        }
    }
    $scenarioCount++
    $scenarios.Add([ordered]@{ name = 'unignored-machine-path'; result = 'passed'; assertions = 2 })

    [ordered]@{
        formatVersion = 1
        test = 'Search gate configuration'
        status = 'passed'
        scenarios = $scenarios
        scenarioCount = $scenarioCount
        assertions = $script:assertionCount
        temporaryFixturePolicy = 'Temporary invalid configurations were created below the system temporary directory; the one repository fixture was removed in finally and no product configuration was changed.'
    } | ConvertTo-Json -Depth 10
}
finally {
    if (Test-Path -LiteralPath $temporaryRoot) {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
    }
}
