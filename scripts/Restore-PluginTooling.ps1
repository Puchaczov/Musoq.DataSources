[CmdletBinding()]
param(
    [string]$ManifestPath,
    [string]$ToolPath
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    $ManifestPath = Join-Path $PSScriptRoot "../.config/dotnet-tools.json"
}

$resolvedManifestPath = (Resolve-Path -LiteralPath $ManifestPath -ErrorAction Stop).Path
$manifest = Get-Content -LiteralPath $resolvedManifestPath -Raw | ConvertFrom-Json
$tool = $manifest.tools.'nuget-license'
if ($null -eq $tool) {
    throw "The local tool manifest does not define the nuget-license tool: $resolvedManifestPath"
}

$version = [string]$tool.version
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "The nuget-license entry in the local tool manifest has no pinned version: $resolvedManifestPath"
}

if ([string]::IsNullOrWhiteSpace($ToolPath)) {
    $runnerTemp = if ([string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)) {
        [System.IO.Path]::GetTempPath()
    }
    else {
        $env:RUNNER_TEMP
    }
    $ToolPath = Join-Path $runnerTemp "musoq-plugin-tooling/nuget-license/$version"
}

$resolvedToolPath = [System.IO.Path]::GetFullPath($ToolPath)
New-Item -ItemType Directory -Path $resolvedToolPath -Force | Out-Null

$toolExecutable = Join-Path $resolvedToolPath "nuget-license.exe"
$needsInstall = $true
if (Test-Path -LiteralPath $toolExecutable -PathType Leaf) {
    $existingVersionOutput = & $toolExecutable --version 2>&1 | Out-String
    $needsInstall = $LASTEXITCODE -ne 0 -or $existingVersionOutput -notmatch "(^|[^0-9])$([regex]::Escape($version))([^0-9]|$)"
}

if ($needsInstall) {
    $toolOperation = if (Test-Path -LiteralPath $toolExecutable -PathType Leaf) { "update" } else { "install" }
    $installOutput = & dotnet tool $toolOperation `
        --tool-path $resolvedToolPath `
        nuget-license `
        --version $version 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to $toolOperation nuget-license $version into '$resolvedToolPath': $($installOutput -join [Environment]::NewLine)"
    }
}

$pathSeparator = [System.IO.Path]::PathSeparator
$env:PATH = "$resolvedToolPath$pathSeparator$env:PATH"
if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_PATH)) {
    Add-Content -LiteralPath $env:GITHUB_PATH -Value $resolvedToolPath
}

$command = Get-Command nuget-license -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
if ($null -eq $command) {
    throw "nuget-license $version was installed, but the command is not available on PATH. Expected tool path: $resolvedToolPath"
}

$versionOutput = & $command.Source --version 2>&1 | Out-String
if ($LASTEXITCODE -ne 0) {
    throw "nuget-license was found at '$($command.Source)' but --version failed: $($versionOutput.Trim())"
}

if ($versionOutput -notmatch "(^|[^0-9])$([regex]::Escape($version))([^0-9]|$)") {
    throw "nuget-license version mismatch. Expected $version, got: $($versionOutput.Trim())"
}

Write-Host "Provisioned nuget-license $version from $resolvedManifestPath at $resolvedToolPath"
