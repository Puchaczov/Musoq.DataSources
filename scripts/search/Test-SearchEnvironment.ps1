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
        [string]$RepositoryRoot
    )

    New-Item -ItemType Directory -Path $RepositoryRoot -Force | Out-Null
    Invoke-Git -RepositoryRoot $RepositoryRoot -Arguments @('init', '--quiet') | Out-Null
    Invoke-Git -RepositoryRoot $RepositoryRoot -Arguments @('config', 'user.name', 'Search Bootstrap Test') | Out-Null
    Invoke-Git -RepositoryRoot $RepositoryRoot -Arguments @('config', 'user.email', 'search-bootstrap@example.invalid') | Out-Null
    Invoke-Git -RepositoryRoot $RepositoryRoot -Arguments @('checkout', '--quiet', '-b', 'main') | Out-Null
    Set-Content -LiteralPath (Join-Path $RepositoryRoot 'README.md') -Value 'bootstrap fixture' -Encoding utf8NoBOM
    Invoke-Git -RepositoryRoot $RepositoryRoot -Arguments @('add', 'README.md') | Out-Null
    Invoke-Git -RepositoryRoot $RepositoryRoot -Arguments @('commit', '--quiet', '-m', 'fixture: initial') | Out-Null
    return Invoke-Git -RepositoryRoot $RepositoryRoot -Arguments @('rev-parse', 'HEAD')
}

function Invoke-Bootstrap {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot,

        [string]$ExpectedBranch,

        [string]$ExpectedCommit,

        [switch]$RequireClean
    )

    $reader = Join-Path $PSScriptRoot 'Read-SearchEnvironment.ps1'
    $pwsh = (Get-Command pwsh -CommandType Application -ErrorAction Stop).Source
    $arguments = @(
        '-NoProfile',
        '-NonInteractive',
        '-File',
        $reader,
        '-RepositoryRoot',
        $RepositoryRoot,
        '-RepositoryId',
        'fixture/search-bootstrap',
        '-ExpectedBranch',
        $ExpectedBranch,
        '-ExpectedCommit',
        $ExpectedCommit
    )

    if ($RequireClean) {
        $arguments += '-RequireClean'
    }

    $output = @(& $pwsh @arguments 2>&1)
    return [pscustomobject]@{
        ExitCode = $LASTEXITCODE
        Text     = ConvertTo-Text $output
    }
}

function ConvertFrom-BootstrapJson {
    param(
        [Parameter(Mandatory = $true)]
        [pscustomobject]$Invocation
    )

    Assert-Equal -Expected 0 -Actual $Invocation.ExitCode -Message "Bootstrap invocation should succeed. Output: $($Invocation.Text)"
    return $Invocation.Text | ConvertFrom-Json
}

$script:assertionCount = 0
$scenarioCount = 0
$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) ('musoq-search-bootstrap-' + [Guid]::NewGuid().ToString('N'))
$scenarios = [System.Collections.Generic.List[object]]::new()

try {
    $cleanRoot = Join-Path $temporaryRoot 'clean'
    $cleanCommit = New-TestRepository -RepositoryRoot $cleanRoot
    $cleanInvocation = Invoke-Bootstrap -RepositoryRoot $cleanRoot -ExpectedBranch 'main' -ExpectedCommit $cleanCommit
    $clean = ConvertFrom-BootstrapJson -Invocation $cleanInvocation
    Assert-Equal -Expected 'main' -Actual $clean.branch -Message 'Clean fixture branch should be captured.'
    Assert-Equal -Expected $cleanCommit -Actual $clean.head -Message 'Clean fixture commit should be captured.'
    Assert-True -Condition (-not $clean.worktree.isDirty) -Message 'Clean fixture should be reported clean.'
    $scenarioCount++
    $scenarios.Add([ordered]@{ name = 'clean'; result = 'passed'; assertions = 3 })

    $dirtyMarker = Join-Path $cleanRoot 'untracked.marker'
    Set-Content -LiteralPath $dirtyMarker -Value 'must survive' -Encoding utf8NoBOM
    $dirtyInvocation = Invoke-Bootstrap -RepositoryRoot $cleanRoot -ExpectedBranch 'main' -ExpectedCommit $cleanCommit
    $dirty = ConvertFrom-BootstrapJson -Invocation $dirtyInvocation
    Assert-True -Condition $dirty.worktree.isDirty -Message 'Dirty fixture should be reported dirty.'
    Assert-Equal -Expected 1 -Actual $dirty.worktree.untrackedCount -Message 'Dirty fixture should report one untracked entry.'
    Assert-True -Condition (Test-Path -LiteralPath $dirtyMarker -PathType Leaf) -Message 'Dirty inspection must not delete the marker.'

    $strictDirtyInvocation = Invoke-Bootstrap -RepositoryRoot $cleanRoot -ExpectedBranch 'main' -ExpectedCommit $cleanCommit -RequireClean
    Assert-True -Condition ($strictDirtyInvocation.ExitCode -ne 0) -Message 'RequireClean should reject a dirty fixture.'
    Assert-True -Condition (Test-Path -LiteralPath $dirtyMarker -PathType Leaf) -Message 'RequireClean rejection must not clean the fixture.'
    Assert-Equal -Expected $cleanCommit -Actual (Invoke-Git -RepositoryRoot $cleanRoot -Arguments @('rev-parse', 'HEAD')) -Message 'Dirty inspection must not move HEAD.'
    $scenarioCount++
    $scenarios.Add([ordered]@{ name = 'dirty'; result = 'passed'; assertions = 5 })

    $originalRoot = Join-Path $temporaryRoot 'relocated-source'
    $relocatedRoot = Join-Path $temporaryRoot 'relocated-checkout'
    $movedBranchBaseCommit = New-TestRepository -RepositoryRoot $originalRoot
    Invoke-Git -RepositoryRoot $originalRoot -Arguments @('checkout', '--quiet', '-b', 'search-bootstrap') | Out-Null
    Set-Content -LiteralPath (Join-Path $originalRoot 'branch.marker') -Value 'advanced branch' -Encoding utf8NoBOM
    Invoke-Git -RepositoryRoot $originalRoot -Arguments @('add', 'branch.marker') | Out-Null
    Invoke-Git -RepositoryRoot $originalRoot -Arguments @('commit', '--quiet', '-m', 'fixture: advance branch') | Out-Null
    $movedBranchCommit = Invoke-Git -RepositoryRoot $originalRoot -Arguments @('rev-parse', 'HEAD')
    Move-Item -LiteralPath $originalRoot -Destination $relocatedRoot

    $staleCommitInvocation = Invoke-Bootstrap -RepositoryRoot $relocatedRoot -ExpectedBranch 'search-bootstrap' -ExpectedCommit $movedBranchBaseCommit
    Assert-True -Condition ($staleCommitInvocation.ExitCode -ne 0) -Message 'A stale expected commit must be rejected after a branch advances.'
    $relocatedInvocation = Invoke-Bootstrap -RepositoryRoot $relocatedRoot -ExpectedBranch 'search-bootstrap' -ExpectedCommit $movedBranchCommit
    $relocated = ConvertFrom-BootstrapJson -Invocation $relocatedInvocation
    Assert-Equal -Expected 'search-bootstrap' -Actual $relocated.branch -Message 'Advanced branch should be captured after relocation.'
    Assert-Equal -Expected $movedBranchCommit -Actual $relocated.head -Message 'Advanced branch commit should be captured after relocation.'
    Assert-Equal -Expected $relocatedRoot -Actual $relocated.gitTopLevel -Message 'Relocated checkout top-level should be resolved.'
    Assert-True -Condition (-not $relocated.worktree.isDirty) -Message 'Relocated fixture should remain clean.'
    $scenarioCount++
    $scenarios.Add([ordered]@{ name = 'advanced-branch-and-relocated-checkout'; result = 'passed'; assertions = 5 })

    [ordered]@{
        formatVersion = 1
        test = 'Search environment bootstrap'
        status = 'passed'
        scenarios = $scenarios
        scenarioCount = $scenarioCount
        assertions = $script:assertionCount
        temporaryFixturePolicy = 'All fixtures were created below the system temporary directory and removed in finally; the target checkout was never mutated.'
    } | ConvertTo-Json -Depth 8
}
finally {
    if (Test-Path -LiteralPath $temporaryRoot) {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
    }
}
