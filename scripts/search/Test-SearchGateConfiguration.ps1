[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ConfigPath,

    [Parameter(Mandatory = $true)]
    [string]$RepositoryRoot,

    [switch]$RequireIgnored
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

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

function Invoke-Git {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    & git -C $Root @Arguments 2>$null | Out-Null
    return $LASTEXITCODE
}

try {
    $root = (Resolve-Path -LiteralPath $RepositoryRoot -ErrorAction Stop).Path
    $config = (Resolve-Path -LiteralPath $ConfigPath -ErrorAction Stop).Path
    if (-not (Test-Path -LiteralPath $config -PathType Leaf)) {
        throw "Configuration path is not a file: '$config'."
    }

    $raw = Get-Content -LiteralPath $config -Raw -Encoding utf8
    try {
        $value = $raw | ConvertFrom-Json
    }
    catch {
        throw "Could not parse configuration '$config': $($_.Exception.Message)"
    }

    if ($value.formatVersion -ne 1) {
        throw "Configuration '$config' must use formatVersion 1."
    }

    $fullSuite = Get-ConfigProperty -Object $value -Name 'fullSuiteArgv'
    $expectedSuite = @('dotnet', 'test', '--configuration', 'Release')
    if ($null -eq $fullSuite -or $fullSuite -isnot [array] -or $fullSuite.Count -ne $expectedSuite.Count) {
        throw "Configuration '$config' must define the exact fullSuiteArgv vector: dotnet test --configuration Release."
    }
    for ($index = 0; $index -lt $expectedSuite.Count; $index++) {
        if ($fullSuite[$index].ToString() -ne $expectedSuite[$index]) {
            throw "Configuration '$config' has an invalid fullSuiteArgv at index $index; expected '$($expectedSuite[$index])'."
        }
    }

    $secretMatches = @([regex]::Matches($raw, '(?i)"[^"]*(password|secret|token|api[-_]?key|authorization|private[-_]?key|credential)[^"]*"\s*:'))
    if ($secretMatches.Count -gt 0) {
        throw "Configuration '$config' contains secret-like properties; credentials must not be stored in shared configuration."
    }
    if ($raw -match '(?i)(bearer\s+[A-Za-z0-9._-]+|gh[pousr]_[A-Za-z0-9_]{10,}|github_pat_[A-Za-z0-9_]{10,}|sk-[A-Za-z0-9]{10,}|-----BEGIN\s+[^-]+\s+PRIVATE\s+KEY-----)') {
        throw "Configuration '$config' contains a secret-like value; credentials must not be stored in shared configuration."
    }

    $relative = [IO.Path]::GetRelativePath($root, $config)
    if ($relative.StartsWith('..' + [IO.Path]::DirectorySeparatorChar) -or [IO.Path]::IsPathRooted($relative)) {
        throw "Configuration '$config' is outside repository root '$root'."
    }

    $ignored = $false
    if ($RequireIgnored) {
        $ignored = (Invoke-Git -Root $root -Arguments @('check-ignore', '--quiet', '--no-index', '--', $relative)) -eq 0
        if (-not $ignored) {
            throw "Configuration '$relative' is not ignored; machine-specific paths must not enter shared configuration."
        }
    }

    [ordered]@{
        formatVersion = 1
        status = 'passed'
        configuration = $relative.Replace('\', '/')
        fullSuiteArgv = @($fullSuite)
        secretLikeProperties = $secretMatches.Count
        requireIgnored = [bool]$RequireIgnored
        ignored = $ignored
        shellInterpolation = 'rejected by exact command-vector comparison'
    } | ConvertTo-Json -Depth 8
}
catch {
    Write-Error $_
    exit 1
}
