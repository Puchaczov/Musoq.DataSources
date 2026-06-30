[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$Tag,
    [string]$ArtifactDirectory = "artifacts/release"
)

. (Join-Path $PSScriptRoot "Release.Common.ps1")

$release = Resolve-DatasourceReleaseTag -Tag $Tag
$repositoryRoot = Get-ReleaseRepositoryRoot
$resolvedArtifactDirectory = if ([System.IO.Path]::IsPathRooted($ArtifactDirectory)) {
    [System.IO.Path]::GetFullPath($ArtifactDirectory)
}
else {
    [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $ArtifactDirectory))
}

if (-not (Test-Path -LiteralPath $resolvedArtifactDirectory)) {
    throw "Artifact directory was not found: $resolvedArtifactDirectory"
}

$nupkgPath = Join-Path $resolvedArtifactDirectory "nupkgs/$($release.PackageId).$($release.Version).nupkg"
$snupkgPath = Join-Path $resolvedArtifactDirectory "nupkgs/$($release.PackageId).$($release.Version).snupkg"

if (-not (Test-Path -LiteralPath $nupkgPath)) {
    throw "NuGet package was not found: $nupkgPath"
}

if (-not (Test-Path -LiteralPath $snupkgPath)) {
    throw "Symbol package was not found: $snupkgPath"
}

function Test-NuGetPackage {
    param(
        [Parameter(Mandatory=$true)]
        [string]$PackagePath
    )

    $tempDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "musoq-nupkg-smoke-$([guid]::NewGuid().ToString('N'))"
    try {
        New-Item -ItemType Directory -Force -Path $tempDirectory | Out-Null
        Expand-Archive -LiteralPath $PackagePath -DestinationPath $tempDirectory -Force
        $nuspec = Get-ChildItem -LiteralPath $tempDirectory -Filter "*.nuspec" | Select-Object -First 1
        if (-not $nuspec) {
            throw "NuGet package is missing nuspec: $PackagePath"
        }

        [xml]$nuspecXml = Get-Content -LiteralPath $nuspec.FullName
        $id = [string]$nuspecXml.package.metadata.id
        $version = [string]$nuspecXml.package.metadata.version
        if ($id -ne $release.PackageId) {
            throw "NuGet package id '$id' does not match '$($release.PackageId)'."
        }

        if ($version -ne $release.Version) {
            throw "NuGet package version '$version' does not match '$($release.Version)'."
        }
    }
    finally {
        if (Test-Path -LiteralPath $tempDirectory) {
            Remove-Item -LiteralPath $tempDirectory -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

function Test-PluginPackage {
    param(
        [Parameter(Mandatory=$true)]
        [string]$PackagePath
    )

    $hostProvidedAssemblies = @(
        "Musoq.Schema.dll",
        "Musoq.Plugins.dll",
        "Musoq.Parser.dll",
        "Musoq.Converter.dll",
        "Musoq.Evaluator.dll"
    )

    $tempDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "musoq-plugin-smoke-$([guid]::NewGuid().ToString('N'))"
    $outerDirectory = Join-Path $tempDirectory "outer"
    $pluginDirectory = Join-Path $tempDirectory "plugin"

    try {
        New-Item -ItemType Directory -Force -Path $outerDirectory | Out-Null
        New-Item -ItemType Directory -Force -Path $pluginDirectory | Out-Null
        Expand-Archive -LiteralPath $PackagePath -DestinationPath $outerDirectory -Force

        $entryPointFile = Join-Path $outerDirectory "EntryPoint.txt"
        $libraryNameFile = Join-Path $outerDirectory "LibraryName.txt"
        $versionFile = Join-Path $outerDirectory "Version.txt"
        $pluginZipFile = Join-Path $outerDirectory "Plugin.zip"

        foreach ($requiredFile in @($entryPointFile, $libraryNameFile, $versionFile, $pluginZipFile)) {
            if (-not (Test-Path -LiteralPath $requiredFile)) {
                throw "Plugin package is missing required file '$([System.IO.Path]::GetFileName($requiredFile))': $PackagePath"
            }
        }

        $entryPoint = (Get-Content -LiteralPath $entryPointFile -Raw).Trim()
        $libraryName = (Get-Content -LiteralPath $libraryNameFile -Raw).Trim()
        $version = (Get-Content -LiteralPath $versionFile -Raw).Trim()

        if ($libraryName -ne $release.PackageId) {
            throw "LibraryName.txt '$libraryName' does not match '$($release.PackageId)'."
        }

        if ($version -ne $release.Version) {
            throw "Version.txt '$version' does not match '$($release.Version)'."
        }

        if ([string]::IsNullOrWhiteSpace($entryPoint)) {
            throw "EntryPoint.txt is empty: $PackagePath"
        }

        Expand-Archive -LiteralPath $pluginZipFile -DestinationPath $pluginDirectory -Force

        if (-not (Test-Path -LiteralPath (Join-Path $pluginDirectory $entryPoint))) {
            throw "Plugin.zip is missing entry point DLL '$entryPoint': $PackagePath"
        }

        $xmlDocumentation = [System.IO.Path]::ChangeExtension($entryPoint, ".xml")
        if (-not (Test-Path -LiteralPath (Join-Path $pluginDirectory $xmlDocumentation))) {
            throw "Plugin.zip is missing XML documentation '$xmlDocumentation': $PackagePath"
        }

        foreach ($assembly in $hostProvidedAssemblies) {
            if (Test-Path -LiteralPath (Join-Path $pluginDirectory $assembly)) {
                throw "Plugin.zip contains host-provided assembly '$assembly': $PackagePath"
            }
        }
    }
    finally {
        if (Test-Path -LiteralPath $tempDirectory) {
            Remove-Item -LiteralPath $tempDirectory -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

Test-NuGetPackage -PackagePath $nupkgPath

foreach ($artifactName in (Get-ArtifactNames -ProjectName $release.PackageId).Values) {
    $pluginPackagePath = Join-Path $resolvedArtifactDirectory "plugins/$artifactName"
    if (-not (Test-Path -LiteralPath $pluginPackagePath)) {
        throw "Plugin package was not found: $pluginPackagePath"
    }

    Test-PluginPackage -PackagePath $pluginPackagePath
}

Write-Host "Release smoke test passed for $Tag." -ForegroundColor Green
