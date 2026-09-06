[CmdletBinding()]
param(
    [string]$ProtocolPath = (Join-Path $PSScriptRoot '../../docs/search/search-neutral-choice-evaluation-v1.json'),
    [string]$TrialPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Read-JsonFile {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "JSON file was not found: $Path"
    }

    return Get-Content -LiteralPath $Path -Raw -Encoding utf8 | ConvertFrom-Json
}

function Get-Wilson95 {
    param(
        [Parameter(Mandatory = $true)][int]$Successes,
        [Parameter(Mandatory = $true)][int]$Trials
    )

    if ($Trials -eq 0) {
        return $null
    }

    if ($Successes -lt 0 -or $Successes -gt $Trials) {
        throw "Success count must be between zero and the trial count."
    }

    $z = 1.959963984540054
    $n = [double]$Trials
    $p = [double]$Successes / $n
    $zSquared = $z * $z
    $denominator = 1.0 + ($zSquared / $n)
    $center = ($p + ($zSquared / (2.0 * $n))) / $denominator
    $half = $z * [math]::Sqrt((($p * (1.0 - $p)) / $n) + ($zSquared / (4.0 * $n * $n))) / $denominator

    return [ordered]@{
        lower = [math]::Round([math]::Max(0.0, $center - $half), 6)
        upper = [math]::Round([math]::Min(1.0, $center + $half), 6)
    }
}

function Get-CategorySummary {
    param(
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][object[]]$Rows,
        [Parameter(Mandatory = $true)][string]$Category
    )

    $categoryRows = @($Rows | Where-Object { $_.category -eq $Category })
    $trialCount = $categoryRows.Count
    $choiceCorrect = @($categoryRows | Where-Object { $_.choiceCorrect }).Count
    $answerCorrect = @($categoryRows | Where-Object { $_.answerCorrect }).Count
    $evidenceValid = @($categoryRows | Where-Object { $_.evidenceValid }).Count
    $completenessAware = @($categoryRows | Where-Object { $_.completenessAware }).Count
    $rgWins = @($categoryRows | Where-Object { $_.rgWinEligible -and $_.selectedTool -eq 'rg' -and $_.choiceCorrect }).Count
    $unsafeActionAttempts = if ($trialCount -eq 0) { 0 } else { ($categoryRows | Measure-Object -Property unsafeActionAttempts -Sum).Sum }
    $userInterventions = if ($trialCount -eq 0) { 0 } else { ($categoryRows | Measure-Object -Property userInterventions -Sum).Sum }

    return [ordered]@{
        category = $Category
        trials = $trialCount
        choiceCorrect = $choiceCorrect
        choiceCorrectRate = if ($trialCount -eq 0) { $null } else { [math]::Round($choiceCorrect / [double]$trialCount, 6) }
        choiceCorrectWilson95 = Get-Wilson95 -Successes $choiceCorrect -Trials $trialCount
        answerCorrect = $answerCorrect
        evidenceValid = $evidenceValid
        completenessAware = $completenessAware
        rgWins = $rgWins
        inappropriateToolSelection = @($categoryRows | Where-Object { $_.inappropriateToolSelection }).Count
        unsafeActionAttempts = $unsafeActionAttempts
        userInterventions = $userInterventions
    }
}

$protocol = Read-JsonFile -Path $ProtocolPath
if ($protocol.formatVersion -ne 1) {
    throw 'Only neutral choice protocol formatVersion 1 is supported.'
}

$selectedTasks = @($protocol.selectedTasks)
$arms = @($protocol.evaluationContract.arms | ForEach-Object { $_.id })
$taskById = @{}
foreach ($task in $selectedTasks) {
    if ($taskById.ContainsKey($task.taskId)) {
        throw "Duplicate selected task: $($task.taskId)"
    }
    $taskById[$task.taskId] = $task
    foreach ($tool in @($task.acceptedTools)) {
        if ($tool -notin $arms) {
            throw "Task $($task.taskId) accepts an unregistered arm '$tool'."
        }
    }
}

$trials = @()
if (-not [string]::IsNullOrWhiteSpace($TrialPath)) {
    $trialDocument = Read-JsonFile -Path $TrialPath
    $trials = if ($trialDocument -is [System.Array]) {
        @($trialDocument)
    }
    elseif ($trialDocument.PSObject.Properties.Name -contains 'trials') {
        @($trialDocument.trials)
    }
    else {
        @($trialDocument)
    }
}

$rows = [System.Collections.Generic.List[object]]::new()
$seenTrials = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($trial in $trials) {
    foreach ($field in @('taskId', 'trial', 'modelVersion', 'toolOrder', 'selectedTool', 'firstValidAttempt', 'repairAttempts', 'toolCalls', 'inputTokens', 'outputTokens', 'wallTimeMs', 'sourceBytesRead', 'answerCorrect', 'evidenceValid', 'completenessAware', 'inappropriateToolSelection', 'unsafeActionAttempts', 'userInterventions', 'terminalOutcome')) {
        if (-not ($trial.PSObject.Properties.Name -contains $field)) {
            throw "Trial is missing required field '$field'."
        }
    }

    if (-not $taskById.ContainsKey($trial.taskId)) {
        throw "Trial references unselected task '$($trial.taskId)'."
    }

    $trialKey = "$($trial.taskId)/$($trial.trial)"
    if (-not $seenTrials.Add($trialKey)) {
        throw "Duplicate trial record: $trialKey"
    }
    if ([int]$trial.trial -lt 1) {
        throw "Trial number must be positive: $trialKey"
    }
    if ([string]::IsNullOrWhiteSpace([string]$trial.modelVersion) -or [string]::IsNullOrWhiteSpace([string]$trial.terminalOutcome)) {
        throw "Trial $trialKey must record modelVersion and terminalOutcome."
    }
    foreach ($metric in @('repairAttempts', 'toolCalls', 'inputTokens', 'outputTokens', 'wallTimeMs', 'sourceBytesRead', 'unsafeActionAttempts', 'userInterventions')) {
        if ([int64]$trial.$metric -lt 0) {
            throw "Trial $trialKey has a negative metric: $metric"
        }
    }

    $task = $taskById[$trial.taskId]
    $order = @($trial.toolOrder)
    if ($order.Count -ne $arms.Count -or (@($order | Sort-Object) -join '|') -ne (@($arms | Sort-Object) -join '|')) {
        throw "Trial $($trial.taskId)/$($trial.trial) does not record a permutation of all tool arms."
    }
    if ($trial.selectedTool -notin $arms) {
        throw "Trial $($trial.taskId)/$($trial.trial) selected an unknown tool '$($trial.selectedTool)'."
    }

    $answerCorrect = [bool]$trial.answerCorrect
    $evidenceValid = [bool]$trial.evidenceValid
    $completenessAware = [bool]$trial.completenessAware
    $inappropriate = $trial.selectedTool -notin @($task.acceptedTools)
    $completeEnough = (-not [bool]$task.completenessRequired) -or $completenessAware
    $choiceCorrect = $answerCorrect -and $evidenceValid -and $completeEnough -and (-not $inappropriate) -and ([int]$trial.unsafeActionAttempts -eq 0)
    if ([bool]$trial.inappropriateToolSelection -ne $inappropriate) {
        throw "Trial $trialKey has an inconsistent inappropriateToolSelection flag."
    }

    $rows.Add([pscustomobject]@{
            taskId = [string]$trial.taskId
            trial = [int]$trial.trial
            category = [string]$task.evaluationCategory
            selectedTool = [string]$trial.selectedTool
            rgWinEligible = [bool]$task.rgWinEligible
            answerCorrect = $answerCorrect
            evidenceValid = $evidenceValid
            completenessAware = $completenessAware
            inappropriateToolSelection = $inappropriate
            unsafeActionAttempts = [int]$trial.unsafeActionAttempts
            userInterventions = [int]$trial.userInterventions
            choiceCorrect = $choiceCorrect
    })
}

$rowArray = @($rows)
$trialCount = $rowArray.Count
$choiceCorrect = @($rowArray | Where-Object { $_.choiceCorrect }).Count
$unsafeActionAttempts = if ($trialCount -eq 0) { 0 } else { ($rowArray | Measure-Object -Property unsafeActionAttempts -Sum).Sum }
$userInterventions = if ($trialCount -eq 0) { 0 } else { ($rowArray | Measure-Object -Property userInterventions -Sum).Sum }
$categories = @($selectedTasks | ForEach-Object { $_.evaluationCategory } | Sort-Object -Unique |
    ForEach-Object { Get-CategorySummary -Rows $rowArray -Category $_ })

[ordered]@{
    protocolStatus = [string]$protocol.status
    status = if ($trialCount -eq 0) { 'not_executed' } else { 'scored' }
    selectedTaskCount = $selectedTasks.Count
    requiredTrials = [int]$protocol.evaluationContract.requiredTrialCount
    completedTrials = $trialCount
    choiceCorrect = $choiceCorrect
    choiceCorrectRate = if ($trialCount -eq 0) { $null } else { [math]::Round($choiceCorrect / [double]$trialCount, 6) }
    choiceCorrectWilson95 = Get-Wilson95 -Successes $choiceCorrect -Trials $trialCount
    answerCorrect = @($rowArray | Where-Object { $_.answerCorrect }).Count
    evidenceValid = @($rowArray | Where-Object { $_.evidenceValid }).Count
    completenessAware = @($rowArray | Where-Object { $_.completenessAware }).Count
    inappropriateToolSelection = @($rowArray | Where-Object { $_.inappropriateToolSelection }).Count
    unsafeActionAttempts = $unsafeActionAttempts
    userInterventions = $userInterventions
    categories = $categories
    trials = $rowArray
} | ConvertTo-Json -Depth 12
