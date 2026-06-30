Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. "$PSScriptRoot/../common/Plugin-Config.ps1"

$script:ReleasePackageRegistryPath = Join-Path $PSScriptRoot "packages.json"

function Get-ReleaseRepositoryRoot {
    return (Get-SolutionRoot).Path
}

function Resolve-ReleaseRepositoryPath {
    param(
        [Parameter(Mandatory=$true)]
        [string]$RelativePath
    )

    if ([System.IO.Path]::IsPathRooted($RelativePath)) {
        throw "Release registry paths must be repository-relative."
    }

    if ($RelativePath -match '(^|[\\/])\.\.([\\/]|$)') {
        throw "Release registry paths must not contain parent directory segments."
    }

    $repositoryRoot = Get-ReleaseRepositoryRoot
    $fullPath = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $RelativePath))
    $rootWithSeparator = $repositoryRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

    if (-not $fullPath.StartsWith($rootWithSeparator, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Release registry path resolves outside the repository."
    }

    return $fullPath
}

function Test-ReleasePluginProject {
    param(
        [Parameter(Mandatory=$true)]
        [string]$ProjectPath
    )

    $projectDirectory = Split-Path -Parent $ProjectPath
    foreach ($sourceFile in Get-ChildItem -Path $projectDirectory -Filter "*.cs" -Recurse -ErrorAction SilentlyContinue) {
        $content = Get-Content -LiteralPath $sourceFile.FullName -Raw -ErrorAction SilentlyContinue
        if (($content -match ':\s*.*\bSchemaBase\b') -or ($content -match ':\s*.*\bISchema\b')) {
            return $true
        }
    }

    return $false
}

function Get-ReleasePackages {
    if (-not (Test-Path -LiteralPath $script:ReleasePackageRegistryPath)) {
        throw "Release package registry was not found."
    }

    $registry = Get-Content -LiteralPath $script:ReleasePackageRegistryPath -Raw | ConvertFrom-Json
    if ($null -eq $registry.packages -or $registry.packages.Count -eq 0) {
        throw "Release package registry does not contain any packages."
    }

    $seenSlugs = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    $seenPackageIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    $packages = @()

    foreach ($package in $registry.packages) {
        $slug = [string]$package.slug
        $packageId = [string]$package.packageId
        $projectPath = [string]$package.projectPath

        if ($slug -notmatch '^[a-z][a-z0-9]*$') {
            throw "Release package registry contains an invalid package slug: $slug"
        }

        if (-not $seenSlugs.Add($slug)) {
            throw "Release package registry contains a duplicate package slug: $slug"
        }

        if (-not (Test-ValidPluginName -Name $packageId)) {
            throw "Release package registry contains an invalid package id: $packageId"
        }

        if (-not $seenPackageIds.Add($packageId)) {
            throw "Release package registry contains a duplicate package id: $packageId"
        }

        $fullProjectPath = Resolve-ReleaseRepositoryPath -RelativePath $projectPath
        if (-not (Test-Path -LiteralPath $fullProjectPath)) {
            throw "Release package project was not found: $projectPath"
        }

        if (-not (Test-ReleasePluginProject -ProjectPath $fullProjectPath)) {
            throw "Release package '$packageId' is not a datasource plugin project."
        }

        $metadata = Get-ProjectMetadata -Project (Get-Item -LiteralPath $fullProjectPath)
        if ($metadata.Name -ne $packageId) {
            throw "Release package id '$packageId' does not match project '$($metadata.Name)'."
        }

        $packages += [PSCustomObject]@{
            Slug = $slug
            PackageId = $packageId
            ProjectPath = $projectPath
            FullProjectPath = $fullProjectPath
            ShortName = $metadata.ShortName
            Description = $metadata.Description
            Tags = @($metadata.Tags)
        }
    }

    return @($packages)
}

function Get-ReleasePackageByPackageId {
    param(
        [Parameter(Mandatory=$true)]
        [string]$PackageId
    )

    $matches = @(Get-ReleasePackages | Where-Object { $_.PackageId -eq $PackageId })
    if ($matches.Count -ne 1) {
        throw "Release tag targets package '$PackageId', which is not configured as a unified datasource release package."
    }

    return $matches[0]
}

function Get-MsBuildProperty {
    param(
        [Parameter(Mandatory=$true)]
        [string]$ProjectPath,
        [Parameter(Mandatory=$true)]
        [string]$PropertyName
    )

    $output = & dotnet msbuild $ProjectPath "-getProperty:$PropertyName" -nologo -v:quiet 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to evaluate MSBuild property '$PropertyName'. $($output -join [Environment]::NewLine)"
    }

    $value = $output |
        ForEach-Object { [string]$_ } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -Last 1

    if ($null -eq $value) {
        return ""
    }

    return $value.Trim()
}

function Resolve-DatasourceReleaseTag {
    param(
        [Parameter(Mandatory=$true)]
        [string]$Tag
    )

    if ([string]::IsNullOrWhiteSpace($Tag) -or $Tag -ne $Tag.Trim()) {
        throw "Invalid release tag format."
    }

    if ($Tag -match '[\s;&|`$<>(){}\[\]''"\\/]') {
        throw "Invalid release tag format."
    }

    $parsedTag = ConvertFrom-MusoqPluginReleaseTag -ReleaseTag $Tag
    if (-not $parsedTag) {
        throw "Invalid release tag format. Expected '{semver}-Musoq.DataSources.{Name}'."
    }

    $package = Get-ReleasePackageByPackageId -PackageId $parsedTag.PluginName
    $declaredVersion = Get-MsBuildProperty -ProjectPath $package.FullProjectPath -PropertyName "Version"

    if ($declaredVersion -ne $parsedTag.Version) {
        throw "Project version '$declaredVersion' does not match release tag version '$($parsedTag.Version)'."
    }

    $metadata = Get-ProjectMetadata -Project (Get-Item -LiteralPath $package.FullProjectPath)
    $expectedTag = New-MusoqPluginReleaseTag -Version $declaredVersion -PluginName $metadata.Name
    if ($expectedTag -ne $Tag) {
        throw "Release tag '$Tag' does not match expected tag '$expectedTag'."
    }

    return [PSCustomObject]@{
        Tag = $Tag
        Version = $parsedTag.Version
        Channel = $parsedTag.Channel
        IsPrerelease = $parsedTag.IsPrerelease
        Package = $package
        PackageId = $package.PackageId
        Slug = $package.Slug
        ProjectPath = $package.ProjectPath
        FullProjectPath = $package.FullProjectPath
        ShortName = $package.ShortName
        Description = $package.Description
        Tags = @($package.Tags)
    }
}

function New-DatasourceReleaseSummary {
    param(
        [Parameter(Mandatory=$true)]
        [pscustomobject]$Release
    )

    return [PSCustomObject]@{
        tag = $Release.Tag
        version = $Release.Version
        channel = $Release.Channel
        isPrerelease = $Release.IsPrerelease
        slug = $Release.Slug
        packageId = $Release.PackageId
        projectPath = $Release.ProjectPath
    }
}

function Invoke-ReleaseCommand {
    param(
        [Parameter(Mandatory=$true)]
        [string]$FilePath,
        [Parameter(Mandatory=$true)]
        [string[]]$Arguments,
        [string]$WorkingDirectory = (Get-ReleaseRepositoryRoot)
    )

    Push-Location -LiteralPath $WorkingDirectory
    try {
        & $FilePath @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "Command failed: $FilePath"
        }
    }
    finally {
        Pop-Location
    }
}
