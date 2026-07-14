[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$Tag,
    [string]$ArtifactDirectory = "artifacts/release"
)

. (Join-Path $PSScriptRoot "Release.Common.ps1")
. (Join-Path $PSScriptRoot "../common/Plugin-Compatibility.ps1")
. (Join-Path $PSScriptRoot "../common/Plugin-ArtifactIntegrity.ps1")
. (Join-Path $PSScriptRoot "../common/CommandLineModule-Packaging.ps1")

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
            if (@(Get-ChildItem -LiteralPath $pluginDirectory -Recurse -File -Filter $assembly).Count -gt 0) {
                throw "Plugin.zip contains host-provided assembly '$assembly': $PackagePath"
            }
        }

        $targetsAssemblies = @(Get-ChildItem -LiteralPath $pluginDirectory -Recurse -File -Filter "Musoq.Targets.*.dll")
        if ($targetsAssemblies.Count -gt 0) {
            throw "Plugin.zip contains host-provided Musoq.Targets assembly '$($targetsAssemblies[0].Name)': $PackagePath"
        }
        $embeddedCommandLineFiles = @(Get-ChildItem -LiteralPath $pluginDirectory -Recurse -File | Where-Object {
            $_.Name -eq 'Musoq.CommandLine.dll' -or $_.Name -like '*.CommandLineArguments.*'
        })
        if ($embeddedCommandLineFiles.Count -gt 0) {
            throw "Plugin.zip contains command-line module or host ABI files: $PackagePath"
        }

        if ($release.PackageId -eq 'Musoq.DataSources.Roslyn') {
            $modulesRoot = Join-Path $outerDirectory $script:MusoqCommandLineModulesDirectoryName
            if (-not (Test-Path -LiteralPath $modulesRoot -PathType Container)) {
                throw "Roslyn package is missing $($script:MusoqCommandLineModulesDirectoryName): $PackagePath"
            }
            $moduleDirectories = @(Get-ChildItem -LiteralPath $modulesRoot -Directory)
            if ($moduleDirectories.Count -ne 1 -or $moduleDirectories[0].Name -ne 'musoq.datasource.roslyn') {
                throw "Roslyn package must contain exactly the musoq.datasource.roslyn command module: $PackagePath"
            }
            $moduleManifest = Read-MusoqCommandLineModuleManifest -ModuleDirectory $moduleDirectories[0].FullName
            if ($moduleManifest.entryAssembly -ne 'Musoq.DataSources.Roslyn.CommandLineArguments.dll' -or
                $moduleManifest.framework.versionRange -ne '[0.0.1,0.1.0)' -or
                @($moduleManifest.requiredInvocationItems).Count -ne 1 -or
                $moduleManifest.requiredInvocationItems[0].name -ne 'musoq.datasource.http-request.v1' -or
                $moduleManifest.requiredInvocationItems[0].contract -ne 'http-request-v1') {
                throw "Roslyn command-line module manifest has an unexpected contract: $PackagePath"
            }
        }

        $compatibilityPath = Join-Path $pluginDirectory $script:MusoqPluginCompatibilityFileName
        $compatibility = Read-MusoqPluginCompatibilityManifest -Path $compatibilityPath
        $schemaRange = $compatibility.hostPackages.'Musoq.Schema'
        $pluginsRange = $compatibility.hostPackages.'Musoq.Plugins'
        if ($schemaRange.minimumVersionInclusive -ne "17.0.2-alpha.2" -or
            $pluginsRange.minimumVersionInclusive -ne "17.0.2-alpha.2" -or
            $schemaRange.maximumVersionExclusive -ne "18.0.0" -or
            $pluginsRange.maximumVersionExclusive -ne "18.0.0") {
            throw "Plugin compatibility manifest does not match the supported runtime-v2 ABI: $PackagePath"
        }
    }
    finally {
        if (Test-Path -LiteralPath $tempDirectory) {
            Remove-Item -LiteralPath $tempDirectory -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

Test-NuGetPackage -PackagePath $nupkgPath

$releaseManifestPath = Join-Path $resolvedArtifactDirectory "release-artifacts.json"
if (-not (Test-Path -LiteralPath $releaseManifestPath)) {
    throw "Release artifact manifest was not found: $releaseManifestPath"
}

$releaseManifest = Get-Content -LiteralPath $releaseManifestPath -Raw | ConvertFrom-Json -Depth 100
$metadataPath = [string]$releaseManifest.pluginReleaseMetadata
if ([string]::IsNullOrWhiteSpace($metadataPath)) {
    throw "Release artifact manifest is missing pluginReleaseMetadata."
}

$metadata = Read-MusoqPluginReleaseMetadata -Path $metadataPath
if ($metadata.plugin -ne $release.PackageId -or $metadata.version -ne $release.Version -or $metadata.releaseTag -ne $release.Tag) {
    throw "Plugin release metadata identity does not match release '$Tag'."
}
if ($null -eq $releaseManifest.pluginArtifactIntegrity -or $null -eq $releaseManifest.runtimeCompatibility) {
    throw "Release artifact manifest is missing compatibility or integrity metadata."
}
if (-not (Test-MusoqCompatibilityEqual -Left $metadata.runtimeCompatibility -Right $releaseManifest.runtimeCompatibility)) {
    throw "Release artifact manifest compatibility differs from public release metadata."
}

foreach ($artifactEntry in (Get-ArtifactNames -ProjectName $release.PackageId).GetEnumerator()) {
    $artifactName = $artifactEntry.Value
    $pluginPackagePath = Join-Path $resolvedArtifactDirectory "plugins/$artifactName"
    if (-not (Test-Path -LiteralPath $pluginPackagePath)) {
        throw "Plugin package was not found: $pluginPackagePath"
    }

    Test-PluginPackage -PackagePath $pluginPackagePath
    $artifactRecord = $metadata.artifacts.($artifactEntry.Key)
    $localManifestRecord = $releaseManifest.pluginArtifactIntegrity.($artifactEntry.Key)
    foreach ($propertyName in @("fileName", "sizeBytes", "md5", "sha256")) {
        if ($artifactRecord.$propertyName -cne $localManifestRecord.$propertyName) {
            throw "Release artifact manifest $($artifactEntry.Key) $propertyName differs from public release metadata."
        }
    }
    Assert-MusoqArtifactMatchesRecord `
        -Path $pluginPackagePath `
        -Expected $artifactRecord `
        -Context "$($release.PackageId) $($release.Version) $($artifactEntry.Key)" | Out-Null

    $embeddedCompatibility = Get-MusoqPluginPackageCompatibility -PackagePath $pluginPackagePath
    if (-not (Test-MusoqCompatibilityEqual -Left $metadata.runtimeCompatibility -Right $embeddedCompatibility)) {
        throw "Plugin package compatibility differs from release metadata: $pluginPackagePath"
    }
}

Write-Host "Release smoke test passed for $Tag." -ForegroundColor Green
