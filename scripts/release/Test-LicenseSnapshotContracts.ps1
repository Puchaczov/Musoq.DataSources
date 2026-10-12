#Requires -Version 7.2
[CmdletBinding()]
param(
    [string] $RepositoryRoot = (Join-Path $PSScriptRoot '../..')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'LicenseSnapshot.Common.psm1') -Force

function Assert-ContractTrue {
    param([bool] $Condition, [string] $Message)
    if (-not $Condition) { throw $Message }
}

function Assert-ContractThrows {
    param([scriptblock] $Action, [string] $Message)
    try {
        & $Action
    }
    catch {
        return
    }
    throw $Message
}

$root = [IO.Path]::GetFullPath($RepositoryRoot)
$definition = @(Get-DatasourcePackageDefinitions -RepositoryRoot $root | Where-Object packageId -eq 'Musoq.DataSources.Json')[0]
if ($null -eq $definition) { throw 'The synthetic snapshot contract requires Musoq.DataSources.Json in packages.json.' }
$workingRoot = Join-Path $root '.builds/license-snapshot-contracts'
$snapshotRoot = Join-Path $workingRoot ([guid]::NewGuid().ToString('N'))
$nugetConfigRoot = Join-Path $workingRoot ('nuget-config-' + [guid]::NewGuid().ToString('N'))
$snapshotDirectory = Join-Path $snapshotRoot $definition.packageId
$noticeDirectory = Join-Path $snapshotDirectory 'third-party-notices/Synthetic.Dependency'
$assertScript = Join-Path $PSScriptRoot 'Assert-LicenseSnapshots.ps1'
$gathererPath = Join-Path $root 'tools/dotnet/LicenseGatherer/Musoq.Cloud.LicensesGatherer.exe'
$gathererHash = (Get-FileHash -LiteralPath $gathererPath -Algorithm SHA256).Hash.ToLowerInvariant()

function Write-ContractManifest {
    $manifestPath = Join-Path $snapshotDirectory 'manifest.json'
    $manifest = [PSCustomObject][ordered]@{
        schemaVersion = 2
        packageId = $definition.packageId
        slug = $definition.slug
        version = $definition.version
        projectPath = $definition.projectPath
        entryProject = $definition.projectPath
        ownPackageId = $definition.packageId
        licenseTool = [PSCustomObject][ordered]@{ package = 'nuget-license'; version = '4.0.16' }
        licenseGatherer = [PSCustomObject][ordered]@{ path = 'tools/dotnet/LicenseGatherer/Musoq.Cloud.LicensesGatherer.exe'; sha256 = $gathererHash }
        dependencyInputs = @(Get-DatasourceDependencyInputManifest `
            -RepositoryRoot $root `
            -EntryProject $definition.projectPath `
            -AdditionalPath (Get-DatasourceSnapshotAdditionalInputs -RepositoryRoot $root))
        packageGraph = @([PSCustomObject][ordered]@{ id = 'Synthetic.Dependency'; version = '1.0.0'; direct = $true })
        files = @(Get-DatasourceSnapshotFileManifest -SnapshotDirectory $snapshotDirectory)
    }
    $json = (($manifest | ConvertTo-Json -Depth 100).Replace("`r`n", "`n").Replace("`r", "`n")) + "`n"
    [IO.File]::WriteAllText($manifestPath, $json, [Text.UTF8Encoding]::new($false))
}

function New-ValidContractSnapshot {
    if (Test-Path -LiteralPath $snapshotRoot) { Remove-Item -LiteralPath $snapshotRoot -Recurse -Force }
    New-Item -ItemType Directory -Path $noticeDirectory -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination (Join-Path $snapshotDirectory 'license.txt')
    $licenseText = ('Synthetic MIT license text. ' * 40).Trim() + "`n"
    [IO.File]::WriteAllText((Join-Path $noticeDirectory 'license.txt'), $licenseText, [Text.UTF8Encoding]::new($false))
    $report = @(
        [PSCustomObject]@{ PackageId = 'Synthetic.Dependency'; PackageVersion = '1.0.0'; License = 'MIT' },
        [PSCustomObject]@{ PackageId = $definition.packageId; PackageVersion = $definition.version; License = 'MIT' }
    )
    [IO.File]::WriteAllText(
        (Join-Path $snapshotDirectory 'third-party-notices/report.json'),
        (($report | ConvertTo-Json -Depth 20).Replace("`r`n", "`n").Replace("`r", "`n")) + "`n",
        [Text.UTF8Encoding]::new($false))
    Write-ContractManifest
}

function Invoke-ContractValidation {
    & $assertScript -PluginName $definition.packageId -RepositoryRoot $root -SnapshotRoot $snapshotRoot | Out-Null
}

try {
    New-Item -ItemType Directory -Path $nugetConfigRoot -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $nugetConfigRoot 'Test.csproj'), '<Project />', [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $nugetConfigRoot '.gitignore'), "/NuGet.config`n", [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $nugetConfigRoot 'NuGet.config'), '<configuration />', [Text.UTF8Encoding]::new($false))
    & git -C $nugetConfigRoot init --quiet
    if ($LASTEXITCODE -ne 0) { throw 'Could not initialize synthetic Git repository for NuGet config contract.' }
    & git -C $nugetConfigRoot add -- .gitignore Test.csproj
    if ($LASTEXITCODE -ne 0) { throw 'Could not stage synthetic project and ignore rule.' }
    $ignoredConfigInputs = @(Get-DatasourceDependencyInputFiles -RepositoryRoot $nugetConfigRoot -EntryProject 'Test.csproj')
    Assert-ContractTrue (@($ignoredConfigInputs | Where-Object { [IO.Path]::GetFileName($_) -ieq 'NuGet.config' }).Count -eq 0) 'Ignored local NuGet config must not become a committed snapshot input.'
    & git -C $nugetConfigRoot add -f -- NuGet.config
    if ($LASTEXITCODE -ne 0) { throw 'Could not stage synthetic tracked NuGet config.' }
    $trackedConfigInputs = @(Get-DatasourceDependencyInputFiles -RepositoryRoot $nugetConfigRoot -EntryProject 'Test.csproj')
    Assert-ContractTrue (@($trackedConfigInputs | Where-Object { [IO.Path]::GetFileName($_) -ieq 'NuGet.config' }).Count -eq 1) 'Tracked NuGet config must remain a snapshot input.'

    New-ValidContractSnapshot
    Invoke-ContractValidation

    $seedRoot = Join-Path $snapshotRoot 'seeds'
    New-Item -ItemType Directory -Path $seedRoot | Out-Null
    Copy-DatasourceVerifiedLicenseSeeds -RepositoryRoot $root -ExistingSnapshot $snapshotDirectory -Destination $seedRoot `
        -PackageGraph @([pscustomobject]@{id='Synthetic.Dependency'; version='1.0.0'})
    $seedPath = Join-Path $seedRoot 'Synthetic.Dependency.txt'
    Assert-ContractTrue (Test-Path -LiteralPath $seedPath) 'Exact package/version notice should seed the offline cache.'
    Assert-ContractTrue ((Get-FileHash -LiteralPath $seedPath).Hash -eq (Get-FileHash -LiteralPath (Join-Path $noticeDirectory 'license.txt')).Hash) 'Seed notice bytes must remain exact.'
    Remove-Item -LiteralPath $seedPath
    Copy-DatasourceVerifiedLicenseSeeds -RepositoryRoot $root -ExistingSnapshot $snapshotDirectory -Destination $seedRoot `
        -PackageGraph @([pscustomobject]@{id='Synthetic.Dependency'; version='2.0.0'})
    Assert-ContractTrue (!(Test-Path -LiteralPath $seedPath)) 'A different package version must never reuse a notice.'
    Add-Content -LiteralPath (Join-Path $noticeDirectory 'license.txt') -Value 'tampered' -NoNewline
    Assert-ContractThrows {
        Copy-DatasourceVerifiedLicenseSeeds -RepositoryRoot $root -ExistingSnapshot $snapshotDirectory -Destination $seedRoot `
            -PackageGraph @([pscustomobject]@{id='Synthetic.Dependency'; version='1.0.0'})
    } 'A tampered notice must never seed the cache.'

    New-ValidContractSnapshot
    Remove-Item -LiteralPath (Join-Path $noticeDirectory 'license.txt') -Force
    Assert-ContractThrows { Invoke-ContractValidation } 'A snapshot with missing license text must fail closed.'

    New-ValidContractSnapshot
    $staleManifest = Get-Content -LiteralPath (Join-Path $snapshotDirectory 'manifest.json') -Raw | ConvertFrom-Json -Depth 100
    $staleManifest.dependencyInputs[0].sha256 = ('0' * 64)
    [IO.File]::WriteAllText((Join-Path $snapshotDirectory 'manifest.json'), (($staleManifest | ConvertTo-Json -Depth 100) + "`n"), [Text.UTF8Encoding]::new($false))
    Assert-ContractThrows { Invoke-ContractValidation } 'A snapshot with stale dependency inputs must fail closed.'

    New-ValidContractSnapshot
    [IO.File]::WriteAllText((Join-Path $noticeDirectory 'license.txt'), 'MIT' + "`n", [Text.UTF8Encoding]::new($false))
    Write-ContractManifest
    Assert-ContractThrows { Invoke-ContractValidation } 'A snapshot with abbreviated license text must fail closed.'

    New-ValidContractSnapshot
    Add-Content -LiteralPath (Join-Path $noticeDirectory 'license.txt') -Value 'tampered' -NoNewline
    Assert-ContractThrows { Invoke-ContractValidation } 'A snapshot with tampered bytes must fail closed.'

    New-ValidContractSnapshot
    [IO.File]::WriteAllText((Join-Path $snapshotDirectory 'manifest.json'), '{', [Text.UTF8Encoding]::new($false))
    Assert-ContractThrows { Invoke-ContractValidation } 'A malformed snapshot manifest must fail closed.'

    Assert-ContractTrue $true 'Snapshot contract tests should reach completion.'
    Write-Host 'License snapshot contract tests passed.' -ForegroundColor Green
}
finally {
    if (Test-Path -LiteralPath $snapshotRoot) {
        Remove-Item -LiteralPath $snapshotRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path -LiteralPath $nugetConfigRoot) {
        Remove-Item -LiteralPath $nugetConfigRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
