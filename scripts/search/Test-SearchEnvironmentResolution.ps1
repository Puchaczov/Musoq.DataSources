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

function Invoke-Git {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $output = @(& git -C $RepositoryRoot @Arguments 2>&1)
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        throw "Git command failed with exit code ${exitCode}: git -C '$RepositoryRoot' $($Arguments -join ' ')`n$(ConvertTo-Text $output)"
    }

    return ConvertTo-Text $output
}

function New-TestRepository {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot,

        [Parameter(Mandatory = $true)]
        [string]$Remote,

        [Parameter(Mandatory = $true)]
        [System.Collections.IDictionary]$PackagePins,

        [switch]$DatasourceShape
    )

    New-Item -ItemType Directory -Path $RepositoryRoot -Force | Out-Null
    Invoke-Git -RepositoryRoot $RepositoryRoot -Arguments @('init', '--quiet') | Out-Null
    Invoke-Git -RepositoryRoot $RepositoryRoot -Arguments @('config', 'user.name', 'Search Environment Resolution Test') | Out-Null
    Invoke-Git -RepositoryRoot $RepositoryRoot -Arguments @('config', 'user.email', 'search-environment@example.invalid') | Out-Null
    Invoke-Git -RepositoryRoot $RepositoryRoot -Arguments @('checkout', '--quiet', '-b', 'main') | Out-Null
    Invoke-Git -RepositoryRoot $RepositoryRoot -Arguments @('remote', 'add', 'origin', $Remote) | Out-Null

    $propertyLines = [System.Collections.Generic.List[string]]::new()
    foreach ($pin in $PackagePins.GetEnumerator()) {
        $propertyLines.Add("        <$($pin.Key)>$($pin.Value)</$($pin.Key)>")
    }
    $props = "<Project>`n    <PropertyGroup>`n$($propertyLines -join "`n")`n    </PropertyGroup>`n</Project>`n"
    Set-Content -LiteralPath (Join-Path $RepositoryRoot 'Directory.Build.props') -Value $props -Encoding utf8NoBOM
    if ($DatasourceShape) {
        Set-Content -LiteralPath (Join-Path $RepositoryRoot 'Musoq.DataSources.sln') -Value 'Microsoft Visual Studio Solution File, Format Version 12.00' -Encoding utf8NoBOM
    }
    else {
        Set-Content -LiteralPath (Join-Path $RepositoryRoot 'engine.marker') -Value 'engine fixture' -Encoding utf8NoBOM
    }

    Invoke-Git -RepositoryRoot $RepositoryRoot -Arguments @('add', '.') | Out-Null
    Invoke-Git -RepositoryRoot $RepositoryRoot -Arguments @('commit', '--quiet', '-m', 'fixture: environment') | Out-Null
    return Invoke-Git -RepositoryRoot $RepositoryRoot -Arguments @('rev-parse', 'HEAD')
}

function Write-Config {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [hashtable]$Values
    )

    $Values | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $Path -Encoding utf8NoBOM
}

function Invoke-Resolver {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ConfigPath
    )

    $resolver = Join-Path $PSScriptRoot 'Resolve-SearchEnvironment.ps1'
    $pwsh = (Get-Command pwsh -CommandType Application -ErrorAction Stop).Source
    $output = @(& $pwsh -NoProfile -NonInteractive -File $resolver -ConfigPath $ConfigPath 2>&1)
    [pscustomobject]@{
        exitCode = $LASTEXITCODE
        text = ConvertTo-Text $output
    }
}

function New-ConfigValues {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot,

        [Parameter(Mandatory = $true)]
        [System.Collections.IDictionary]$ExpectedPins,

        [AllowNull()]
        [string]$EnginePath,

        [AllowNull()]
        [string]$CliPath,

        [AllowNull()]
        [hashtable]$EngineCompatibility,

        [string]$EngineMode = 'packaged'
    )

    return @{
        formatVersion = 1
        repositoryId = 'datasources'
        repositoryRoot = $RepositoryRoot
        externalRepositoryRoots = @{
            engine = $EnginePath
            cli = $null
        }
        cliExecutable = $CliPath
        selectedTrack = 'core'
        fullSuiteArgv = @('dotnet', 'test', '--configuration', 'Release')
        expectedPackagePins = $ExpectedPins
        engineMode = $EngineMode
        engineCompatibility = $EngineCompatibility
    }
}

$script:assertionCount = 0
$scenarioCount = 0
$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) ('musoq-search-environment-' + [Guid]::NewGuid().ToString('N'))
$scenarios = [System.Collections.Generic.List[object]]::new()
$packagePins = [ordered]@{
    MusoqConverterVersion = '17.0.9-alpha.2'
    MusoqEvaluatorVersion = '17.0.9-alpha.2'
    MusoqParserVersion = '17.0.9-alpha.1'
    MusoqPluginsVersion = '17.0.9-alpha.1'
    MusoqSchemaVersion = '17.0.9-alpha.1'
}

try {
    $missingConfigPath = Join-Path $temporaryRoot 'missing-path.json'
    New-Item -ItemType Directory -Path $temporaryRoot -Force | Out-Null
    Write-Config -Path $missingConfigPath -Values (New-ConfigValues -RepositoryRoot (Join-Path $temporaryRoot 'does-not-exist') -ExpectedPins $packagePins)
    $missing = Invoke-Resolver -ConfigPath $missingConfigPath
    Assert-True -Condition ($missing.exitCode -ne 0) -Message 'A missing repository path must fail.'
    Assert-True -Condition ($missing.text -match "repositoryRoot.*does not exist") -Message "Missing path diagnostic should name repositoryRoot and the missing path. Output: $($missing.text)"
    $scenarioCount++
    $scenarios.Add([ordered]@{ name = 'missing-repository-path'; result = 'passed'; assertions = 2 })

    $wrongRoot = Join-Path $temporaryRoot 'wrong-repository'
    New-TestRepository -RepositoryRoot $wrongRoot -Remote 'git@github.com:Other/Repository.git' -PackagePins $packagePins -DatasourceShape | Out-Null
    $wrongConfigPath = Join-Path $temporaryRoot 'wrong-repository.json'
    Write-Config -Path $wrongConfigPath -Values (New-ConfigValues -RepositoryRoot $wrongRoot -ExpectedPins $packagePins)
    $wrong = Invoke-Resolver -ConfigPath $wrongConfigPath
    Assert-True -Condition ($wrong.exitCode -ne 0) -Message 'A repository with the wrong origin must fail.'
    Assert-True -Condition ($wrong.text -match 'Repository identity mismatch') -Message "Wrong repository diagnostic should identify the identity mismatch. Output: $($wrong.text)"
    $scenarioCount++
    $scenarios.Add([ordered]@{ name = 'wrong-repository'; result = 'passed'; assertions = 2 })

    $wrongTrainRoot = Join-Path $temporaryRoot 'wrong-package-train'
    $wrongPins = [ordered]@{} + $packagePins
    $wrongPins.MusoqParserVersion = '17.0.8'
    New-TestRepository -RepositoryRoot $wrongTrainRoot -Remote 'git@github.com:Puchaczov/Musoq.DataSources.git' -PackagePins $wrongPins -DatasourceShape | Out-Null
    $wrongTrainConfigPath = Join-Path $temporaryRoot 'wrong-package-train.json'
    Write-Config -Path $wrongTrainConfigPath -Values (New-ConfigValues -RepositoryRoot $wrongTrainRoot -ExpectedPins $packagePins)
    $wrongTrain = Invoke-Resolver -ConfigPath $wrongTrainConfigPath
    Assert-True -Condition ($wrongTrain.exitCode -ne 0) -Message 'A mismatched package train must fail.'
    Assert-True -Condition ($wrongTrain.text -match 'Package train mismatch') -Message "Package train diagnostic should identify the mismatch. Output: $($wrongTrain.text)"
    $scenarioCount++
    $scenarios.Add([ordered]@{ name = 'wrong-package-train'; result = 'passed'; assertions = 2 })

    $unicodeRoot = Join-Path $temporaryRoot 'checkout with spaces - Żółć'
    New-TestRepository -RepositoryRoot $unicodeRoot -Remote 'git@github.com:Puchaczov/Musoq.DataSources.git' -PackagePins $packagePins -DatasourceShape | Out-Null
    $unicodeConfigPath = Join-Path $temporaryRoot 'unicode-path.json'
    Write-Config -Path $unicodeConfigPath -Values (New-ConfigValues -RepositoryRoot $unicodeRoot -ExpectedPins $packagePins)
    $unicode = Invoke-Resolver -ConfigPath $unicodeConfigPath
    Assert-Equal -Expected 0 -Actual $unicode.exitCode -Message "A datasource path containing spaces and Unicode should resolve. Output: $($unicode.text)"
    $unicodeResult = $unicode.text | ConvertFrom-Json
    Assert-Equal -Expected 'resolved-with-blocked-prerequisites' -Actual $unicodeResult.status -Message 'Unconfigured optional CLI and CLI checkout should be reported as blocked.'
    Assert-Equal -Expected 'packaged' -Actual $unicodeResult.engine.mode -Message 'Packaged engine mode should be the default.'
    Assert-True -Condition ($unicodeResult.blockedPrerequisites -contains 'cliExecutable') -Message 'An absent CLI must be listed as blocked.'
    Assert-True -Condition ($unicodeResult.repository.path -like '*checkout with spaces - Żółć') -Message 'The Unicode checkout path should be preserved in the result.'
    $scenarioCount++
    $scenarios.Add([ordered]@{ name = 'spaces-and-unicode-path'; result = 'passed'; assertions = 5 })

    $sourceRoot = Join-Path $temporaryRoot 'source-engine'
    $sourceCommit = New-TestRepository -RepositoryRoot $sourceRoot -Remote 'git@github.com:Puchaczov/Musoq.git' -PackagePins $packagePins
    $sourceConfigPath = Join-Path $temporaryRoot 'source-engine.json'
    $sourceValues = New-ConfigValues -RepositoryRoot $unicodeRoot -ExpectedPins $packagePins -EnginePath $sourceRoot -EngineCompatibility @{
        repositoryId = 'Puchaczov/Musoq'
        commit = $sourceCommit
    } -EngineMode 'source'
    Write-Config -Path $sourceConfigPath -Values $sourceValues
    $source = Invoke-Resolver -ConfigPath $sourceConfigPath
    Assert-Equal -Expected 0 -Actual $source.exitCode -Message "An explicitly locked source engine should resolve read-only. Output: $($source.text)"
    $sourceResult = $source.text | ConvertFrom-Json
    Assert-Equal -Expected 'source' -Actual $sourceResult.engine.mode -Message 'Explicit source mode should be retained.'
    Assert-Equal -Expected $sourceCommit -Actual $sourceResult.engine.head -Message 'The source compatibility commit should be checked against HEAD.'
    Assert-True -Condition ($sourceResult.validation.externalRepositoriesMutated.Count -eq 0) -Message 'Source engine validation must not mutate external repositories.'
    $scenarioCount++
    $scenarios.Add([ordered]@{ name = 'explicit-source-engine-compatibility'; result = 'passed'; assertions = 4 })

    [ordered]@{
        formatVersion = 1
        test = 'Search environment resolution'
        status = 'passed'
        scenarios = $scenarios
        scenarioCount = $scenarioCount
        assertions = $script:assertionCount
        temporaryFixturePolicy = 'All fixtures were created below the system temporary directory and removed in finally; the target checkout and any external checkout were never mutated.'
    } | ConvertTo-Json -Depth 10
}
finally {
    if (Test-Path -LiteralPath $temporaryRoot) {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
    }
}
