param(
    [string]$PluginName = "All",
    [string]$OutputDirectory = "$PSScriptRoot/../artifacts"
)

$ErrorActionPreference = "Stop"

. "$PSScriptRoot/common/Plugin-Compatibility.ps1"
. "$PSScriptRoot/common/Plugin-LicensePackaging.ps1"
. "$PSScriptRoot/common/CommandLineModule-Packaging.ps1"
Import-Module "$PSScriptRoot/release/LicenseSnapshot.Common.psm1" -Force

$Targets = @(
    @{ Rid = "win-x64";        Platform = "windows"; Architecture = "x64" },
    @{ Rid = "linux-x64";      Platform = "linux";   Architecture = "x64" },
    @{ Rid = "osx-arm64";      Platform = "macos";   Architecture = "arm64" },
    @{ Rid = "linux-musl-x64"; Platform = "alpine";  Architecture = "x64" }
)

if ([string]::IsNullOrWhiteSpace($PluginName)) {
    $PluginName = "All"
}

if (-not (Test-Path $OutputDirectory)) {
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
}
$OutputDirectory = (Resolve-Path $OutputDirectory).Path

$SolutionRoot = (Resolve-Path "$PSScriptRoot/..").Path
$SnapshotAssertionScript = Join-Path $SolutionRoot "scripts/release/Assert-LicenseSnapshots.ps1"

$Definitions = @(Get-DatasourcePackageDefinition -RepositoryRoot $SolutionRoot -Selector $PluginName)
if ($Definitions.Count -eq 0) {
    throw "No registered datasource packages matched '$PluginName'."
}

$Projects = @($Definitions | ForEach-Object {
    [PSCustomObject]@{
        Definition = $_
        FullName = $_.fullProjectPath
        BaseName = $_.packageId
    }
})

$ProjectLicenseMap = @{}

foreach ($Project in $Projects) {
    $definition = $Project.Definition
    Write-Host "Validating committed license snapshot: $($definition.packageId)" -ForegroundColor Cyan

    Write-Host "  Restoring NuGet packages..." -ForegroundColor Gray
    & dotnet restore $definition.fullProjectPath --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "NuGet restore failed for $($definition.packageId)."
    }

    & $SnapshotAssertionScript `
        -PluginName $definition.packageId `
        -RepositoryRoot $SolutionRoot `
        -ValidatePackageGraph | Out-Null

    $snapshotDirectory = Join-Path $SolutionRoot "licenses/release/$($definition.packageId)"
    $projectLicensesDir = Join-Path $snapshotDirectory "third-party-notices"
    Add-MusoqPluginLicenseMapEntry `
        -LicenseMap $ProjectLicenseMap `
        -ProjectPath $Project.FullName `
        -LicenseDirectory $projectLicensesDir
}

Write-Host "Starting Build..." -ForegroundColor Cyan

$BuildScriptBlock = {
    param($ProjectFullName, $ProjectBaseName, $ProjectVersion, $OutputDirectory, $Targets, $HostOwnedAssemblyPatterns, $CompatibilityJson, $CompatibilityFileName, $ProjectLicensesDir, $CommandLineModuleProjectPath)
    
    $ErrorActionPreference = "Stop"
    $MinPluginZipSizeBytes = 1000
    
    $Results = @()
    foreach ($Target in $Targets) {
        $Rid = $Target.Rid
        $TempDir = Join-Path $OutputDirectory "temp_${ProjectBaseName}_$Rid"
        $PublishDir = Join-Path $TempDir "publish"
        $PackageDir = Join-Path $TempDir "package"
        $ZipName = "${ProjectBaseName}-$($Target.Platform)-$($Target.Architecture).zip"
        
        try {
            $PublishArgs = @(
                "publish", $ProjectFullName,
                "-c", "Release",
                "-f", "net10.0",
                "-r", $Rid,
                "--no-self-contained",
                "-p:CopyLocalLockFileAssemblies=false",
                "-o", $PublishDir
            )
            $PublishOutput = dotnet @PublishArgs 2>&1
            if ($LASTEXITCODE -ne 0) {
                throw "dotnet publish failed with exit code $LASTEXITCODE. Output: $($PublishOutput -join "`n")"
            }

            New-Item -ItemType Directory -Path $PackageDir -Force | Out-Null
            
            $hostOwnedAssemblies = @(Get-ChildItem -LiteralPath $PublishDir -Recurse -File | Where-Object {
                $fileName = $_.Name
                @($HostOwnedAssemblyPatterns | Where-Object { $fileName -like $_ }).Count -gt 0
            })
            foreach ($hostOwnedAssembly in $hostOwnedAssemblies) {
                Remove-Item -LiteralPath $hostOwnedAssembly.FullName -Force
            }

            $remainingHostOwnedAssemblies = @(Get-ChildItem -LiteralPath $PublishDir -Recurse -File | Where-Object {
                $fileName = $_.Name
                @($HostOwnedAssemblyPatterns | Where-Object { $fileName -like $_ }).Count -gt 0
            })
            if ($remainingHostOwnedAssemblies.Count -gt 0) {
                $names = @($remainingHostOwnedAssemblies | ForEach-Object { $_.Name } | Sort-Object -Unique) -join ", "
                throw "Could not remove host-owned Musoq assemblies from publish output: $names"
            }

            $embeddedCommandModules = @(Get-ChildItem -LiteralPath $PublishDir -Recurse -File | Where-Object {
                $_.Name -like '*.CommandLineArguments.*' -or $_.Name -eq 'Musoq.CommandLine.dll'
            })
            if ($embeddedCommandModules.Count -gt 0) {
                throw "Plugin publish contains command-line module or host ABI files: $(@($embeddedCommandModules.Name | Sort-Object -Unique) -join ', ')"
            }

            $compatibilityPath = Join-Path $PublishDir $CompatibilityFileName
            [System.IO.File]::WriteAllText(
                $compatibilityPath,
                $CompatibilityJson,
                [System.Text.UTF8Encoding]::new($false))

            if ([string]::IsNullOrWhiteSpace($ProjectLicensesDir)) {
                throw "No validated license notices directory was supplied for $ProjectBaseName."
            }
            if (-not (Test-Path -LiteralPath $ProjectLicensesDir -PathType Container)) {
                throw "Validated license notices directory is missing for ${ProjectBaseName}: $ProjectLicensesDir"
            }

            $DestLicensesDir = Join-Path $PublishDir "third-party-notices"
            Copy-Item -LiteralPath $ProjectLicensesDir -Destination $DestLicensesDir -Recurse -Force
            Assert-MusoqPluginLicenseNotices `
                -PluginDirectory $PublishDir `
                -Context "Published plugin '$ProjectBaseName' for RID '$Rid'" | Out-Null

            if (-not (Test-Path $PublishDir)) {
                throw "Publish directory does not exist: $PublishDir"
            }
            
            $PublishContents = Get-ChildItem -Path $PublishDir -Force
            if ($PublishContents.Count -eq 0) {
                throw "Publish directory is empty: $PublishDir"
            }

            # Validate that entry point DLL exists in publish directory
            $EntryPointDll = "${ProjectBaseName}.dll"
            $EntryPointPath = Join-Path $PublishDir $EntryPointDll
            if (-not (Test-Path $EntryPointPath)) {
                throw "Entry point DLL '$EntryPointDll' not found in publish directory: $PublishDir"
            }

            $EntryPointXml = "${ProjectBaseName}.xml"
            $EntryPointXmlPath = Join-Path $PublishDir $EntryPointXml
            if (-not (Test-Path $EntryPointXmlPath)) {
                throw "XML documentation '$EntryPointXml' not found in publish directory: $PublishDir"
            }

            $InnerZipPath = Join-Path $PackageDir "Plugin.zip"
            $FilesToCompress = Get-ChildItem -Path $PublishDir -Force | Select-Object -ExpandProperty FullName
            Compress-Archive -Path $FilesToCompress -DestinationPath $InnerZipPath -Force
            
            if (-not (Test-Path $InnerZipPath)) {
                throw "Failed to create Plugin.zip"
            }
            $PluginZipSize = (Get-Item $InnerZipPath).Length
            if ($PluginZipSize -lt $MinPluginZipSizeBytes) {
                throw "Plugin.zip is suspiciously small ($PluginZipSize bytes), publish may have failed"
            }

            Set-Content -Path "$PackageDir\EntryPoint.txt" -Value "${ProjectBaseName}.dll"
            Set-Content -Path "$PackageDir\LibraryName.txt" -Value $ProjectBaseName
            Set-Content -Path "$PackageDir\Version.txt" -Value $ProjectVersion
            Set-Content -Path "$PackageDir\Platform.txt" -Value $Target.Platform
            Set-Content -Path "$PackageDir\Architecture.txt" -Value $Target.Architecture

            if ($CommandLineModuleProjectPath) {
                $commandLineModulesRoot = Join-Path $PackageDir $script:MusoqCommandLineModulesDirectoryName
                Publish-MusoqCommandLineModule `
                    -ProjectPath $CommandLineModuleProjectPath `
                    -DestinationRoot $commandLineModulesRoot `
                    -HostOwnedAssemblyPatterns $HostOwnedAssemblyPatterns | Out-Null
            }

            $ZipPath = Join-Path $OutputDirectory $ZipName
            $PackageContents = Get-ChildItem -Path $PackageDir -Force | Select-Object -ExpandProperty FullName
            Compress-Archive -Path $PackageContents -DestinationPath $ZipPath -Force
            
            $Results += "    -> Created: $ZipName"
        }
        catch {
            $Results += "    -> Error building $ZipName : $_"
        }
        finally {
            if (Test-Path $TempDir) { Remove-Item $TempDir -Recurse -Force -ErrorAction SilentlyContinue }
        }
    }
    return $Results
}

foreach ($Project in $Projects) {
    Write-Host "Building: $($Project.BaseName)" -ForegroundColor Gray

    $Compatibility = Get-MusoqPluginCompatibility -ProjectPath $Project.FullName
    $CompatibilityJson = ConvertTo-MusoqPluginCompatibilityJson -Compatibility $Compatibility
    $commandLineModuleProjectPath = Join-Path `
        $SolutionRoot `
        "$($Project.BaseName).CommandLineArguments/$($Project.BaseName).CommandLineArguments.csproj"
    if (-not (Test-Path -LiteralPath $commandLineModuleProjectPath -PathType Leaf)) {
        $commandLineModuleProjectPath = $null
    }
    
    $JobParams = @(
        $Project.FullName,
        $Project.BaseName,
        $Project.Definition.version,
        $OutputDirectory,
        $Targets,
        $script:MusoqHostOwnedAssemblyPatterns,
        $CompatibilityJson,
        $script:MusoqPluginCompatibilityFileName,
        $ProjectLicenseMap[$Project.FullName],
        $commandLineModuleProjectPath
    )
    
    $Results = & $BuildScriptBlock @JobParams
    $Results | Write-Host
    
    # Check for error messages in results  
    $ErrorResults = $Results | Where-Object { $_ -match "Error building" }
    if ($ErrorResults) {
        throw "Build failed for $($Project.BaseName)"
    }
}
