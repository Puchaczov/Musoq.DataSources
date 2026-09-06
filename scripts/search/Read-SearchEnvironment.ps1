[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$RepositoryRoot,

    [string]$RepositoryId,

    [string]$ExpectedBranch,

    [string]$ExpectedCommit,

    [switch]$RequireClean
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

function Invoke-Git {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [switch]$AllowFailure
    )

    $output = @(& git -C $script:GitRoot @Arguments 2>&1)
    $exitCode = $LASTEXITCODE
    $text = ConvertTo-Text $output

    if (-not $AllowFailure -and $exitCode -ne 0) {
        $command = 'git -C "{0}" {1}' -f $script:GitRoot, ($Arguments -join ' ')
        throw "Command failed with exit code ${exitCode}: $command`n$text"
    }

    return [pscustomobject]@{
        ExitCode = $exitCode
        Text     = $text
    }
}

function Get-GitText {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    return (Invoke-Git -Arguments $Arguments).Text
}

function Get-GitOptionalText {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $result = Invoke-Git -Arguments $Arguments -AllowFailure
    if ($result.ExitCode -ne 0 -or [string]::IsNullOrWhiteSpace($result.Text)) {
        return $null
    }

    return $result.Text
}

function ConvertTo-SafeRemoteUrl {
    param(
        [AllowNull()]
        [string]$Url
    )

    if ([string]::IsNullOrWhiteSpace($Url)) {
        return $null
    }

    return [regex]::Replace($Url, '(?i)(https?://)[^/@]+@', '$1<redacted>@')
}

try {
    $resolvedInput = (Resolve-Path -LiteralPath $RepositoryRoot -ErrorAction Stop).Path
    if (-not (Test-Path -LiteralPath $resolvedInput -PathType Container)) {
        throw "Repository root is not a directory: $resolvedInput"
    }

    $script:GitRoot = $resolvedInput
    $isWorkTree = Get-GitText -Arguments @('rev-parse', '--is-inside-work-tree')
    if ($isWorkTree -ne 'true') {
        throw "Path is not inside a Git worktree: $resolvedInput"
    }

    $gitTopLevel = (Resolve-Path -LiteralPath (Get-GitText -Arguments @('rev-parse', '--show-toplevel')) -ErrorAction Stop).Path
    if (-not [StringComparer]::OrdinalIgnoreCase.Equals($gitTopLevel, $resolvedInput)) {
        throw "Repository root must be the worktree top-level. Requested '$resolvedInput', Git reported '$gitTopLevel'."
    }

    $branchResult = Invoke-Git -Arguments @('symbolic-ref', '--quiet', '--short', 'HEAD') -AllowFailure
    $branch = if ($branchResult.ExitCode -eq 0) { $branchResult.Text } else { $null }
    $head = Get-GitText -Arguments @('rev-parse', 'HEAD')
    $origin = ConvertTo-SafeRemoteUrl (Get-GitOptionalText -Arguments @('remote', 'get-url', 'origin'))
    $upstream = Get-GitOptionalText -Arguments @('rev-parse', '--abbrev-ref', '--symbolic-full-name', '@{upstream}')

    $ahead = $null
    $behind = $null
    if ($null -ne $upstream) {
        $divergence = Get-GitText -Arguments @('rev-list', '--left-right', '--count', 'HEAD...@{upstream}')
        $divergenceParts = $divergence -split '\s+'
        if ($divergenceParts.Count -ne 2) {
            throw "Unexpected Git divergence output: '$divergence'"
        }

        $ahead = [int]$divergenceParts[0]
        $behind = [int]$divergenceParts[1]
    }

    $statusText = Get-GitText -Arguments @('status', '--porcelain=v1', '--untracked-files=all')
    $statusLines = @(if ([string]::IsNullOrWhiteSpace($statusText)) {
        @()
    }
    else {
        @($statusText -split '\r?\n' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    })

    $untrackedCount = @($statusLines | Where-Object { $_.StartsWith('??') }).Count
    $trackedChangeCount = $statusLines.Count - $untrackedCount
    $isDirty = $statusLines.Count -gt 0

    if ($RequireClean -and $isDirty) {
        throw "Worktree is dirty ($($statusLines.Count) status entries); inspection is read-only and no checkout cleanup was performed."
    }

    if (-not [string]::IsNullOrWhiteSpace($ExpectedBranch) -and $branch -ne $ExpectedBranch) {
        throw "Expected branch '$ExpectedBranch', but found '$branch'."
    }

    if (-not [string]::IsNullOrWhiteSpace($ExpectedCommit) -and $head -ne $ExpectedCommit) {
        throw "Expected commit '$ExpectedCommit', but found '$head'."
    }

    [ordered]@{
        formatVersion   = 1
        capturedAtUtc   = (Get-Date).ToUniversalTime().ToString('o')
        repositoryId    = if ([string]::IsNullOrWhiteSpace($RepositoryId)) { $null } else { $RepositoryId }
        repositoryRoot  = $resolvedInput
        gitTopLevel     = $gitTopLevel
        branch          = $branch
        head            = $head
        origin          = $origin
        upstream        = $upstream
        ahead           = $ahead
        behind          = $behind
        worktree        = [ordered]@{
            isDirty            = $isDirty
            statusEntryCount   = $statusLines.Count
            trackedChangeCount = $trackedChangeCount
            untrackedCount     = $untrackedCount
        }
        expectations   = [ordered]@{
            expectedBranch = if ([string]::IsNullOrWhiteSpace($ExpectedBranch)) { $null } else { $ExpectedBranch }
            expectedCommit = if ([string]::IsNullOrWhiteSpace($ExpectedCommit)) { $null } else { $ExpectedCommit }
            requireClean   = [bool]$RequireClean
        }
    } | ConvertTo-Json -Depth 8
}
catch {
    Write-Error $_
    exit 1
}
