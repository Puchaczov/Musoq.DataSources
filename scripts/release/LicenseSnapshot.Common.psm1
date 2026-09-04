Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function ConvertTo-DatasourceFullPath {
    param(
        [Parameter(Mandatory)] [string] $Path
    )

    return [IO.Path]::GetFullPath($Path)
}

function Test-DatasourcePathUnderRoot {
    param(
        [Parameter(Mandatory)] [string] $Root,
        [Parameter(Mandatory)] [string] $Path
    )

    $rootPath = ConvertTo-DatasourceFullPath -Path $Root
    $candidate = ConvertTo-DatasourceFullPath -Path $Path
    $rootWithSeparator = $rootPath.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    return $candidate.Equals($rootPath, [StringComparison]::OrdinalIgnoreCase) -or
        $candidate.StartsWith($rootWithSeparator, [StringComparison]::OrdinalIgnoreCase)
}

function Assert-DatasourcePathUnderRoot {
    param(
        [Parameter(Mandatory)] [string] $Root,
        [Parameter(Mandatory)] [string] $Path,
        [Parameter(Mandatory)] [string] $Description
    )

    if (-not (Test-DatasourcePathUnderRoot -Root $Root -Path $Path)) {
        throw "$Description '$Path' escapes repository root '$Root'."
    }
}

function Get-DatasourceRelativePath {
    param(
        [Parameter(Mandatory)] [string] $Root,
        [Parameter(Mandatory)] [string] $Path
    )

    Assert-DatasourcePathUnderRoot -Root $Root -Path $Path -Description 'Path'
    return [IO.Path]::GetRelativePath((ConvertTo-DatasourceFullPath $Root), (ConvertTo-DatasourceFullPath $Path)).Replace('\', '/')
}

function Get-DatasourceRegistryPath {
    param([Parameter(Mandatory)] [string] $RepositoryRoot)

    return Join-Path (ConvertTo-DatasourceFullPath $RepositoryRoot) 'scripts/release/packages.json'
}

function Get-DatasourcePackageDefinitions {
    param(
        [Parameter(Mandatory)] [string] $RepositoryRoot
    )

    $root = ConvertTo-DatasourceFullPath $RepositoryRoot
    $registryPath = Get-DatasourceRegistryPath -RepositoryRoot $root
    if (-not [IO.File]::Exists($registryPath)) {
        throw "Datasource package registry was not found: $registryPath"
    }

    try { $registry = [IO.File]::ReadAllText($registryPath) | ConvertFrom-Json -Depth 20 }
    catch { throw "Datasource package registry is invalid JSON: $($_.Exception.Message)" }
    if ($null -eq $registry.packages -or @($registry.packages).Count -eq 0) {
        throw "Datasource package registry contains no packages."
    }

    $seenIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $seenSlugs = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $definitions = @()
    foreach ($entry in @($registry.packages)) {
        $slug = [string]$entry.slug
        $packageId = [string]$entry.packageId
        $version = [string]$entry.version
        $projectPath = [string]$entry.projectPath
        if ($slug -notmatch '^[a-z][a-z0-9]*$') { throw "Invalid datasource package slug '$slug'." }
        if ($packageId -notmatch '^Musoq\.DataSources\.[A-Za-z][A-Za-z0-9]*$') { throw "Invalid datasource package id '$packageId'." }
        if ($version -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?$') { throw "Invalid datasource package version '$version'." }
        if ([string]::IsNullOrWhiteSpace($projectPath) -or [IO.Path]::IsPathRooted($projectPath) -or $projectPath -match '(^|[\\/])\.\.([\\/]|$)') {
            throw "Datasource project path must be repository-relative: '$projectPath'."
        }
        if (-not $seenIds.Add($packageId)) { throw "Duplicate datasource package id '$packageId'." }
        if (-not $seenSlugs.Add($slug)) { throw "Duplicate datasource package slug '$slug'." }

        $fullProjectPath = ConvertTo-DatasourceFullPath (Join-Path $root $projectPath)
        Assert-DatasourcePathUnderRoot -Root $root -Path $fullProjectPath -Description 'Datasource project path'
        if (-not [IO.File]::Exists($fullProjectPath)) { throw "Datasource project was not found: $projectPath" }

        try { [xml]$project = [IO.File]::ReadAllText($fullProjectPath) }
        catch { throw "Datasource project '$projectPath' is not valid XML: $($_.Exception.Message)" }
        $packageIdNode = $project.SelectSingleNode("//*[local-name()='PackageId']")
        $versionNode = $project.SelectSingleNode("//*[local-name()='Version']")
        $projectId = if ($null -ne $packageIdNode) { ([string]$packageIdNode.InnerText).Trim() } else { [IO.Path]::GetFileNameWithoutExtension($fullProjectPath) }
        $projectVersion = if ($null -ne $versionNode) { ([string]$versionNode.InnerText).Trim() } else { '' }
        if ($projectId -ne $packageId) { throw "Registry package id '$packageId' does not match project id '$projectId'." }
        if ($projectVersion -ne $version) { throw "Registry package '$packageId' version '$version' does not match project version '$projectVersion'." }

        $definitions += [PSCustomObject][ordered]@{
            slug = $slug
            packageId = $packageId
            version = $version
            projectPath = $projectPath.Replace('\', '/')
            fullProjectPath = $fullProjectPath
        }
    }

    return @($definitions)
}

function Get-DatasourcePackageDefinition {
    param(
        [Parameter(Mandatory)] [string] $RepositoryRoot,
        [Parameter(Mandatory)] [string] $Selector
    )

    $definitions = @(Get-DatasourcePackageDefinitions -RepositoryRoot $RepositoryRoot)
    if ($Selector -eq 'All') { return $definitions }
    $matches = @($definitions | Where-Object {
        $_.packageId -eq $Selector -or $_.slug -eq $Selector
    })
    if ($matches.Count -ne 1) { throw "Datasource selector '$Selector' did not identify exactly one registered package." }
    return $matches[0]
}

function Get-DatasourceDependencyInputFiles {
    param(
        [Parameter(Mandatory)] [string] $RepositoryRoot,
        [Parameter(Mandatory)] [string] $EntryProject,
        [string[]] $AdditionalPath = @()
    )

    $root = ConvertTo-DatasourceFullPath $RepositoryRoot
    $entryPath = if ([IO.Path]::IsPathRooted($EntryProject)) { ConvertTo-DatasourceFullPath $EntryProject } else { ConvertTo-DatasourceFullPath (Join-Path $root $EntryProject) }
    Assert-DatasourcePathUnderRoot -Root $root -Path $entryPath -Description 'Entry project'
    if (-not [IO.File]::Exists($entryPath)) { throw "Entry project does not exist: $EntryProject" }

    $pending = [Collections.Generic.Queue[string]]::new()
    $pending.Enqueue($entryPath)
    $projects = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $inputs = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)

    while ($pending.Count -gt 0) {
        $projectPath = ConvertTo-DatasourceFullPath $pending.Dequeue()
        Assert-DatasourcePathUnderRoot -Root $root -Path $projectPath -Description 'Project reference'
        if (-not $projects.Add($projectPath)) { continue }
        if (-not [IO.File]::Exists($projectPath)) { throw "Referenced project does not exist: $projectPath" }
        [void]$inputs.Add($projectPath)

        try { [xml]$document = [IO.File]::ReadAllText($projectPath) }
        catch { throw "Project '$projectPath' is not valid XML: $($_.Exception.Message)" }

        foreach ($reference in @($document.SelectNodes("//*[local-name()='ProjectReference']"))) {
            $include = [string]$reference.Include
            if ([string]::IsNullOrWhiteSpace($include)) { continue }
            if ($include.Contains('$(')) { throw "Project '$projectPath' contains unevaluated ProjectReference '$include'." }
            $pending.Enqueue((ConvertTo-DatasourceFullPath (Join-Path ([IO.Path]::GetDirectoryName($projectPath)) $include)))
        }

        foreach ($import in @($document.SelectNodes("//*[local-name()='Import']"))) {
            $include = [string]$import.Project
            if ([string]::IsNullOrWhiteSpace($include) -or $include.Contains('$(') -or $include.Contains('*')) { continue }
            $candidate = ConvertTo-DatasourceFullPath (Join-Path ([IO.Path]::GetDirectoryName($projectPath)) $include)
            if ([IO.File]::Exists($candidate)) {
                Assert-DatasourcePathUnderRoot -Root $root -Path $candidate -Description 'Imported project file'
                [void]$inputs.Add($candidate)
            }
        }

        $projectDirectory = [IO.Path]::GetDirectoryName($projectPath)
        $lockFile = Join-Path $projectDirectory 'packages.lock.json'
        if ([IO.File]::Exists($lockFile)) { [void]$inputs.Add((ConvertTo-DatasourceFullPath $lockFile)) }
    }

    foreach ($name in @('Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props', 'NuGet.config', 'global.json', '.gitattributes')) {
        $candidate = Join-Path $root $name
        if ([IO.File]::Exists($candidate)) { [void]$inputs.Add((ConvertTo-DatasourceFullPath $candidate)) }
    }

    foreach ($relativePath in @($AdditionalPath)) {
        if ([string]::IsNullOrWhiteSpace($relativePath)) { continue }
        $candidate = if ([IO.Path]::IsPathRooted($relativePath)) { ConvertTo-DatasourceFullPath $relativePath } else { ConvertTo-DatasourceFullPath (Join-Path $root $relativePath) }
        Assert-DatasourcePathUnderRoot -Root $root -Path $candidate -Description 'Dependency input'
        if (-not [IO.File]::Exists($candidate)) { throw "Dependency input does not exist: $relativePath" }
        [void]$inputs.Add($candidate)
    }

    return @($inputs | Sort-Object)
}

function Get-DatasourceDependencyInputManifest {
    param(
        [Parameter(Mandatory)] [string] $RepositoryRoot,
        [Parameter(Mandatory)] [string] $EntryProject,
        [string[]] $AdditionalPath = @()
    )

    $root = ConvertTo-DatasourceFullPath $RepositoryRoot
    return @(Get-DatasourceDependencyInputFiles -RepositoryRoot $root -EntryProject $EntryProject -AdditionalPath $AdditionalPath | ForEach-Object {
        $content = [IO.File]::ReadAllText($_).Replace("`r`n", "`n").Replace("`r", "`n")
        $digest = [Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($content))
        [PSCustomObject][ordered]@{
            path = Get-DatasourceRelativePath -Root $root -Path $_
            sha256 = [Convert]::ToHexString($digest).ToLowerInvariant()
        }
    })
}

function Invoke-DatasourceDotNetJsonCommand {
    param(
        [Parameter(Mandatory)] [string] $WorkingDirectory,
        [Parameter(Mandatory)] [string[]] $Arguments
    )

    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = 'dotnet'
    $start.WorkingDirectory = ConvertTo-DatasourceFullPath $WorkingDirectory
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in $Arguments) { [void]$start.ArgumentList.Add([string]$argument) }

    $process = [Diagnostics.Process]::Start($start)
    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $($process.ExitCode): $stderr"
    }
    if ([string]::IsNullOrWhiteSpace($stdout)) { throw 'dotnet package graph command returned empty output.' }
    try { return $stdout | ConvertFrom-Json -Depth 100 }
    catch { throw "dotnet package graph output is not valid JSON: $($_.Exception.Message)" }
}

function Get-DatasourcePackageGraph {
    param(
        [Parameter(Mandatory)] [string] $RepositoryRoot,
        [Parameter(Mandatory)] [string] $EntryProject
    )

    $document = Invoke-DatasourceDotNetJsonCommand -WorkingDirectory $RepositoryRoot -Arguments @(
        'list', $EntryProject, 'package', '--include-transitive', '--format', 'json'
    )
    $packages = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($project in @($document.projects)) {
        foreach ($framework in @($project.frameworks)) {
            foreach ($package in @($framework.topLevelPackages)) {
                $id = [string]$package.id
                $version = [string]$package.resolvedVersion
                if ([string]::IsNullOrWhiteSpace($id) -or [string]::IsNullOrWhiteSpace($version)) { throw 'Package graph contains a package without an id or resolved version.' }
                # The bundled Cloud gatherer filters Microsoft runtime packages before resolving licenses.
                if ($id.StartsWith('runtime.', [StringComparison]::OrdinalIgnoreCase)) { continue }
                $key = "$id|$version"
                $packages[$key] = [PSCustomObject][ordered]@{ id = $id; version = $version; direct = $true }
            }
            foreach ($package in @($framework.transitivePackages)) {
                $id = [string]$package.id
                $version = [string]$package.resolvedVersion
                if ([string]::IsNullOrWhiteSpace($id) -or [string]::IsNullOrWhiteSpace($version)) { throw 'Package graph contains a package without an id or resolved version.' }
                if ($id.StartsWith('runtime.', [StringComparison]::OrdinalIgnoreCase)) { continue }
                $key = "$id|$version"
                if (-not $packages.ContainsKey($key)) {
                    $packages[$key] = [PSCustomObject][ordered]@{ id = $id; version = $version; direct = $false }
                }
            }
        }
    }
    return @($packages.Values | Sort-Object id, version)
}

function Get-DatasourceSnapshotFileManifest {
    param([Parameter(Mandatory)] [string] $SnapshotDirectory)

    $root = ConvertTo-DatasourceFullPath $SnapshotDirectory
    if (-not [IO.Directory]::Exists($root)) { throw "Snapshot directory does not exist: $SnapshotDirectory" }
    return @(Get-ChildItem -LiteralPath $root -Recurse -File |
        Where-Object Name -ne 'manifest.json' |
        Sort-Object FullName |
        ForEach-Object {
            [PSCustomObject][ordered]@{
                path = Get-DatasourceRelativePath -Root $root -Path $_.FullName
                size = [long]$_.Length
                sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            }
        })
}

function Get-DatasourceSnapshotAdditionalInputs {
    param(
        [Parameter(Mandatory)] [string] $RepositoryRoot
    )

    $root = ConvertTo-DatasourceFullPath $RepositoryRoot
    $paths = @(
        'scripts/release/packages.json',
        '.config/dotnet-tools.json',
        'LinksManual.json',
        'LICENSE'
    )
    $staticLicenseRoot = Join-Path $root 'licenses'
    if ([IO.Directory]::Exists($staticLicenseRoot)) {
        $paths += @(Get-ChildItem -LiteralPath $staticLicenseRoot -File -Filter '*.txt' | ForEach-Object {
            Get-DatasourceRelativePath -Root $root -Path $_.FullName
        })
    }
    return @($paths)
}

Export-ModuleMember -Function @(
    'Assert-DatasourcePathUnderRoot',
    'Test-DatasourcePathUnderRoot',
    'Get-DatasourcePackageDefinitions',
    'Get-DatasourcePackageDefinition',
    'Get-DatasourceRelativePath',
    'Get-DatasourceDependencyInputFiles',
    'Get-DatasourceDependencyInputManifest',
    'Get-DatasourcePackageGraph',
    'Get-DatasourceSnapshotFileManifest',
    'Get-DatasourceSnapshotAdditionalInputs'
)
