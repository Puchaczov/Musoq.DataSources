[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ConfigPath,

    [string]$ExpectedRepository = 'Puchaczov/Musoq.DataSources',

    [string]$ExpectedTrack = 'core'
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

function Get-ConfigProperty {
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$Object,

        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

function Get-RequiredConfigProperty {
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$Object,

        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $value = Get-ConfigProperty -Object $Object -Name $Name
    if ($null -eq $value -or ($value -is [string] -and [string]::IsNullOrWhiteSpace($value))) {
        throw "Configuration property '$Name' is required and unresolved. Null is not a default."
    }

    return $value
}

function Resolve-ConfiguredPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$PropertyName,

        [ValidateSet('Container', 'Leaf')]
        [string]$PathType = 'Container'
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        throw "Configuration property '$PropertyName' is unresolved. Supply an explicit path."
    }

    if (-not [IO.Path]::IsPathRooted($Path)) {
        throw "Configuration property '$PropertyName' must be an absolute path; got '$Path'."
    }

    if (-not (Test-Path -LiteralPath $Path -PathType $PathType)) {
        throw "Configured $PropertyName does not exist as a $($PathType.ToLowerInvariant()): '$Path'."
    }

    return (Resolve-Path -LiteralPath $Path -ErrorAction Stop).Path
}

function Invoke-Executable {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $output = @(& $Path @Arguments 2>&1)
    $exitCode = $LASTEXITCODE
    [pscustomobject]@{
        exitCode = $exitCode
        output   = ConvertTo-Text $output
    }
}

function Invoke-Git {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $result = Invoke-Executable -Path 'git' -Arguments (@('-C', $Root) + $Arguments)
    if ($result.exitCode -ne 0) {
        throw "Git command failed with exit code $($result.exitCode): git -C '$Root' $($Arguments -join ' ')`n$($result.output)"
    }

    return $result.output
}

function ConvertTo-RepositoryIdentity {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Remote
    )

    $identity = $Remote.Trim()
    $identity = [regex]::Replace($identity, '^[a-z][a-z0-9+.-]*://[^/]+/', '', 'IgnoreCase')
    $identity = [regex]::Replace($identity, '^[^/@:]+@[^:]+:', '')
    $identity = $identity.TrimEnd('/')
    if ($identity.EndsWith('.git', [StringComparison]::OrdinalIgnoreCase)) {
        $identity = $identity.Substring(0, $identity.Length - 4)
    }

    return $identity
}

function Get-PackagePins {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot
    )

    $propsPath = Join-Path $RepositoryRoot 'Directory.Build.props'
    if (-not (Test-Path -LiteralPath $propsPath -PathType Leaf)) {
        throw "Datasource authority file is missing: '$propsPath'."
    }

    try {
        [xml]$props = Get-Content -LiteralPath $propsPath -Raw -Encoding utf8
    }
    catch {
        throw "Could not parse datasource package authority '$propsPath': $($_.Exception.Message)"
    }

    $names = @(
        'MusoqConverterVersion',
        'MusoqEvaluatorVersion',
        'MusoqParserVersion',
        'MusoqPluginsVersion',
        'MusoqSchemaVersion'
    )
    $pins = [ordered]@{}
    foreach ($name in $names) {
        $node = $props.SelectSingleNode("//*[local-name()='$name']")
        if ($null -eq $node -or [string]::IsNullOrWhiteSpace($node.InnerText)) {
            throw "Datasource package authority '$propsPath' does not define '$name'."
        }

        $pins[$name] = $node.InnerText.Trim()
    }

    return $pins
}

function Assert-PackagePins {
    param(
        [Parameter(Mandatory = $true)]
        [System.Collections.IDictionary]$Actual,

        [AllowNull()]
        [psobject]$Expected
    )

    if ($null -eq $Expected) {
        return [ordered]@{
            status = 'blocked'
            reason = 'expectedPackagePins is unresolved; actual pins were recorded without inferring compatibility.'
        }
    }

    $mismatches = [System.Collections.Generic.List[object]]::new()
    foreach ($name in $Actual.Keys) {
        $expectedValue = Get-ConfigProperty -Object $Expected -Name $name
        if ($null -eq $expectedValue) {
            $mismatches.Add([ordered]@{ property = $name; expected = $null; actual = $Actual[$name] })
            continue
        }

        if ($expectedValue.ToString() -ne $Actual[$name].ToString()) {
            $mismatches.Add([ordered]@{ property = $name; expected = $expectedValue.ToString(); actual = $Actual[$name] })
        }
    }

    if ($mismatches.Count -gt 0) {
        $details = $mismatches | ConvertTo-Json -Compress -Depth 5
        throw "Package train mismatch in Directory.Build.props: $details"
    }

    return [ordered]@{
        status = 'matched'
        expected = $Expected
        actual = $Actual
    }
}

function Get-GitRepository {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root,

        [Parameter(Mandatory = $true)]
        [string]$PropertyName,

        [Parameter(Mandatory = $true)]
        [string]$ExpectedIdentity
    )

    $topLevel = (Invoke-Git -Root $Root -Arguments @('rev-parse', '--show-toplevel')).Trim()
    $resolvedTopLevel = (Resolve-Path -LiteralPath $topLevel -ErrorAction Stop).Path
    if (-not [StringComparer]::OrdinalIgnoreCase.Equals($resolvedTopLevel, $Root)) {
        throw "$PropertyName must be a Git worktree top-level; Git reported '$resolvedTopLevel' for '$Root'."
    }

    $remote = (Invoke-Git -Root $Root -Arguments @('remote', 'get-url', 'origin')).Trim()
    if ([string]::IsNullOrWhiteSpace($remote)) {
        throw "$PropertyName has no origin remote; repository identity cannot be verified."
    }

    $identity = ConvertTo-RepositoryIdentity -Remote $remote
    if (-not [StringComparer]::OrdinalIgnoreCase.Equals($identity, $ExpectedIdentity)) {
        throw "Repository identity mismatch for $PropertyName. Expected '$ExpectedIdentity', got '$identity'."
    }

    $head = (Invoke-Git -Root $Root -Arguments @('rev-parse', 'HEAD')).Trim()
    [ordered]@{
        path = $Root
        remote = [regex]::Replace($remote, '(?i)(https?://)[^/@]+@', '$1<redacted>@')
        identity = $identity
        head = $head
    }
}

function Get-ToolPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $command = Get-Command $Name -CommandType Application -ErrorAction Stop | Select-Object -First 1
    return (Resolve-Path -LiteralPath $command.Source -ErrorAction Stop).Path
}

function Get-ToolObservation {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $path = Get-ToolPath -Name $Name
    $result = Invoke-Executable -Path $path -Arguments $Arguments
    if ($result.exitCode -ne 0) {
        throw "Could not observe $Name at '$path'; exit code $($result.exitCode): $($result.output)"
    }

    [ordered]@{
        name = $Name
        path = $path
        arguments = $Arguments
        output = $result.output
        exitCode = $result.exitCode
    }
}

function Get-OptionalExecutableObservation {
    param(
        [AllowNull()]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$PropertyName,

        [AllowNull()]
        [object]$VersionArguments
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return [ordered]@{
            status = 'blocked'
            path = $null
            version = $null
            reason = "$PropertyName is unresolved; PATH discovery was not used as a substitute."
        }
    }

    $resolved = Resolve-ConfiguredPath -Path $Path -PropertyName $PropertyName -PathType Leaf
    $arguments = @()
    if ($null -ne $VersionArguments) {
        $arguments = @($VersionArguments | ForEach-Object { $_.ToString() })
    }

    if ($arguments.Count -eq 0) {
        return [ordered]@{
            status = 'resolved-unprobed'
            path = $resolved
            version = $null
            reason = "No version argument was configured for $PropertyName; the executable was only path-validated."
        }
    }

    $result = Invoke-Executable -Path $resolved -Arguments $arguments
    if ($result.exitCode -ne 0) {
        throw "$PropertyName failed its configured version probe with exit code $($result.exitCode): $($result.output)"
    }

    [ordered]@{
        status = 'resolved'
        path = $resolved
        versionArguments = $arguments
        version = $result.output
        exitCode = $result.exitCode
    }
}

function Get-EngineObservation {
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$Config,

        [Parameter(Mandatory = $true)]
        [string]$ExpectedRepository
    )

    $configuredMode = Get-ConfigProperty -Object $Config -Name 'engineMode'
    $mode = if ([string]::IsNullOrWhiteSpace([string]$configuredMode)) { 'packaged' } else { $configuredMode.ToString().ToLowerInvariant() }
    if ($mode -notin @('packaged', 'source')) {
        throw "Unsupported engineMode '$mode'; expected 'packaged' or 'source'."
    }

    $roots = Get-ConfigProperty -Object $Config -Name 'externalRepositoryRoots'
    if ($null -eq $roots) {
        $roots = [pscustomobject]@{}
    }

    $enginePath = Get-ConfigProperty -Object $roots -Name 'engine'
    $compatibility = Get-ConfigProperty -Object $Config -Name 'engineCompatibility'
    if ($mode -eq 'packaged') {
        $observation = [ordered]@{
            mode = 'packaged'
            status = 'selected'
            path = $null
            compatibility = $null
        }
        if (-not [string]::IsNullOrWhiteSpace([string]$enginePath)) {
            $resolved = Resolve-ConfiguredPath -Path $enginePath -PropertyName 'externalRepositoryRoots.engine' -PathType Container
            $observation.path = $resolved
            $observation.status = 'observed'
            $observation.note = 'An engine checkout was path-validated but packaged mode remains selected; no external files were edited.'
        }

        return $observation
    }

    if ([string]::IsNullOrWhiteSpace([string]$enginePath)) {
        throw "Source engine mode requires an explicit externalRepositoryRoots.engine path."
    }

    if ($null -eq $compatibility) {
        throw 'Source engine mode requires an explicit engineCompatibility lock.'
    }

    $compatibilityRepository = Get-RequiredConfigProperty -Object $compatibility -Name 'repositoryId'
    $lockedCommit = Get-RequiredConfigProperty -Object $compatibility -Name 'commit'
    if ($lockedCommit -notmatch '^[0-9a-fA-F]{40}$') {
        throw "Source engine compatibility commit must be a 40-character SHA-1; got '$lockedCommit'."
    }

    $resolvedEngine = Resolve-ConfiguredPath -Path $enginePath -PropertyName 'externalRepositoryRoots.engine' -PathType Container
    $engine = Get-GitRepository -Root $resolvedEngine -PropertyName 'externalRepositoryRoots.engine' -ExpectedIdentity $compatibilityRepository
    if (-not [StringComparer]::OrdinalIgnoreCase.Equals($engine.head, $lockedCommit)) {
        throw "Source engine compatibility mismatch. Expected commit '$lockedCommit', got '$($engine.head)'."
    }

    $engine.compatibility = [ordered]@{
        repositoryId = $compatibilityRepository
        commit = $lockedCommit
    }
    $engine.mode = 'source'
    $engine.status = 'selected'
    $engine.note = 'The explicitly locked engine checkout was inspected read-only.'
    return $engine
}

try {
    $resolvedConfigPath = (Resolve-Path -LiteralPath $ConfigPath -ErrorAction Stop).Path
    if (-not (Test-Path -LiteralPath $resolvedConfigPath -PathType Leaf)) {
        throw "Configuration path is not a file: '$resolvedConfigPath'."
    }

    try {
        $config = Get-Content -LiteralPath $resolvedConfigPath -Raw -Encoding utf8 | ConvertFrom-Json
    }
    catch {
        throw "Could not parse environment configuration '$resolvedConfigPath': $($_.Exception.Message)"
    }

    $formatVersion = Get-ConfigProperty -Object $config -Name 'formatVersion'
    if ($formatVersion -ne 1) {
        throw "Unsupported environment configuration formatVersion '$formatVersion'; expected 1."
    }

    $repositoryId = Get-RequiredConfigProperty -Object $config -Name 'repositoryId'
    if ($repositoryId.ToString() -ne 'datasources') {
        throw "Configuration repositoryId must be 'datasources'; got '$repositoryId'."
    }

    $selectedTrack = Get-ConfigProperty -Object $config -Name 'selectedTrack'
    if ([string]::IsNullOrWhiteSpace([string]$selectedTrack)) {
        $selectedTrack = $ExpectedTrack
    }
    if ($selectedTrack.ToString() -ne $ExpectedTrack) {
        throw "Configuration selectedTrack '$selectedTrack' does not match expected track '$ExpectedTrack'."
    }

    $repositoryRoot = Resolve-ConfiguredPath -Path (Get-RequiredConfigProperty -Object $config -Name 'repositoryRoot') -PropertyName 'repositoryRoot' -PathType Container
    $repository = Get-GitRepository -Root $repositoryRoot -PropertyName 'repositoryRoot' -ExpectedIdentity $ExpectedRepository
    foreach ($requiredFile in @('Directory.Build.props', 'Musoq.DataSources.sln')) {
        if (-not (Test-Path -LiteralPath (Join-Path $repositoryRoot $requiredFile) -PathType Leaf)) {
            throw "repositoryRoot is not a Musoq.DataSources checkout: required file '$requiredFile' is missing."
        }
    }

    $actualPins = Get-PackagePins -RepositoryRoot $repositoryRoot
    $expectedPins = Get-ConfigProperty -Object $config -Name 'expectedPackagePins'
    $packageTrain = Assert-PackagePins -Actual $actualPins -Expected $expectedPins

    $blocked = [System.Collections.Generic.List[string]]::new()
    if ($packageTrain.status -eq 'blocked') {
        $blocked.Add('expectedPackagePins')
    }

    $engine = Get-EngineObservation -Config $config -ExpectedRepository $ExpectedRepository
    $roots = Get-ConfigProperty -Object $config -Name 'externalRepositoryRoots'
    $cliPath = if ($null -eq $roots) { $null } else { Get-ConfigProperty -Object $roots -Name 'cli' }
    $cli = Get-OptionalExecutableObservation -Path (Get-ConfigProperty -Object $config -Name 'cliExecutable') -PropertyName 'cliExecutable' -VersionArguments (Get-ConfigProperty -Object $config -Name 'cliVersionArguments')
    if ($cli.status -eq 'blocked') {
        $blocked.Add('cliExecutable')
    }
    if (-not [string]::IsNullOrWhiteSpace([string]$cliPath)) {
        $cliRepositoryId = Get-ConfigProperty -Object $config -Name 'cliRepositoryId'
        if ([string]::IsNullOrWhiteSpace([string]$cliRepositoryId)) {
            throw 'externalRepositoryRoots.cli requires an explicit cliRepositoryId before the checkout can be inspected.'
        }

        $cliRoot = Resolve-ConfiguredPath -Path $cliPath -PropertyName 'externalRepositoryRoots.cli' -PathType Container
        $cliRootObservation = Get-GitRepository -Root $cliRoot -PropertyName 'externalRepositoryRoots.cli' -ExpectedIdentity $cliRepositoryId
    }
    else {
        $cliRootObservation = [ordered]@{
            status = 'blocked'
            path = $null
            reason = 'externalRepositoryRoots.cli is unresolved; no checkout was inferred.'
        }
        $blocked.Add('externalRepositoryRoots.cli')
    }

    $dotnet = Get-ToolObservation -Name 'dotnet' -Arguments @('--version')
    $git = Get-ToolObservation -Name 'git' -Arguments @('--version')
    $globalJsonPath = Join-Path $repositoryRoot 'global.json'
    $globalJson = $null
    if (Test-Path -LiteralPath $globalJsonPath -PathType Leaf) {
        $globalJson = Get-Content -LiteralPath $globalJsonPath -Raw -Encoding utf8 | ConvertFrom-Json
    }

    [ordered]@{
        formatVersion = 1
        status = if ($blocked.Count -eq 0) { 'resolved' } else { 'resolved-with-blocked-prerequisites' }
        capturedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
        configPath = $resolvedConfigPath
        selectedTrack = $selectedTrack
        repository = $repository
        packageTrain = $packageTrain
        engine = $engine
        cli = $cli
        cliRepository = $cliRootObservation
        blockedPrerequisites = @($blocked)
        host = [ordered]@{
            osDescription = [System.Runtime.InteropServices.RuntimeInformation]::OSDescription
            osVersion = [Environment]::OSVersion.Version.ToString()
            architecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
            rid = [System.Runtime.InteropServices.RuntimeInformation]::RuntimeIdentifier
            powershellVersion = $PSVersionTable.PSVersion.ToString()
        }
        toolchain = [ordered]@{
            dotnet = $dotnet
            git = $git
            globalJson = $globalJson
        }
        validation = [ordered]@{
            readOnly = $true
            externalRepositoriesInspected = @(@($engine.path, $cliRootObservation.path) | Where-Object { $null -ne $_ })
            externalRepositoriesMutated = @()
            pathResolution = 'LiteralPath and direct executable invocation preserve spaces and Unicode.'
        }
    } | ConvertTo-Json -Depth 12
}
catch {
    Write-Error $_
    exit 1
}
