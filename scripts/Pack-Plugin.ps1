param(
    [string]$PluginName = "All",
    [string]$OutputDirectory = "$PSScriptRoot/../artifacts"
)

$ErrorActionPreference = "Stop"

. "$PSScriptRoot/common/Plugin-Compatibility.ps1"
. "$PSScriptRoot/common/CommandLineModule-Packaging.ps1"

$Targets = @(
    @{ Rid = "win-x64";        Platform = "windows"; Architecture = "x64" },
    @{ Rid = "linux-x64";      Platform = "linux";   Architecture = "x64" },
    @{ Rid = "osx-arm64";      Platform = "macos";   Architecture = "arm64" },
    @{ Rid = "linux-musl-x64"; Platform = "alpine";  Architecture = "x64" }
)

$IgnorePatterns = @(
    "Tests$",
    "\.Tests", 
    "\.Benchmarks", 
    "Helpers$", 
    "\.Common$", 
    "\.CommandLineArguments$",
    "AsyncRowsSource$"
)

if (-not (Test-Path $OutputDirectory)) {
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
}
$OutputDirectory = Resolve-Path $OutputDirectory

$SolutionRoot = Resolve-Path "$PSScriptRoot/.."

$LicenseGathererTool = Join-Path $SolutionRoot "tools/dotnet/LicenseGatherer/Musoq.Cloud.LicensesGatherer.exe"
$LinksCacheFile = Join-Path $SolutionRoot "LinksCache.json"
$LinksManualFile = Join-Path $SolutionRoot "LinksManual.json"
$LicensesCacheDir = Join-Path $SolutionRoot ".licenses-cache"
$DownloadedLicensesDir = Join-Path $SolutionRoot "licenses"

if (-not (Test-Path $LinksManualFile)) {
    Set-Content -Path $LinksManualFile -Value "{}"
}

function Test-IsValidMusoqPlugin {
    param([string]$ProjectPath)
    
    $ProjectDir = Split-Path -Parent $ProjectPath
    $CsFiles = Get-ChildItem -Path $ProjectDir -Filter "*.cs" -Recurse -ErrorAction SilentlyContinue
    
    foreach ($CsFile in $CsFiles) {
        $Content = Get-Content -Path $CsFile.FullName -Raw -ErrorAction SilentlyContinue
        if (($Content -match ":\s*.*\bSchemaBase\b") -or ($Content -match ":\s*.*\bISchema\b")) {
            return $true
        }
    }
    return $false
}

$Projects = Get-ChildItem -Path $SolutionRoot -Recurse -Filter "Musoq.DataSources.*.csproj"

if ($PluginName -ne "All") {
    $Projects = $Projects | Where-Object { $_.BaseName -eq $PluginName }
} else {
    foreach ($Pattern in $IgnorePatterns) {
        $Projects = $Projects | Where-Object { $_.BaseName -notmatch $Pattern }
    }
}

$ValidProjects = @()
$InvalidProjects = @()
foreach ($Project in $Projects) {
    if (Test-IsValidMusoqPlugin -ProjectPath $Project.FullName) {
        $ValidProjects += $Project
    } else {
        $InvalidProjects += $Project
    }
}

if ($InvalidProjects.Count -gt 0) {
    Write-Host "Skipping non-plugin projects (no SchemaBase/ISchema implementation found):" -ForegroundColor Yellow
    foreach ($InvalidProject in $InvalidProjects) {
        Write-Host "  - $($InvalidProject.BaseName)" -ForegroundColor Yellow
    }
}

$Projects = $ValidProjects

if ($Projects.Count -eq 0) {
    Write-Error "No matching plugin projects found."
}

function Get-ProjectVersion {
    param([System.IO.FileInfo]$Project)

    [xml]$csproj = Get-Content $Project.FullName
    $PropertyGroup = $csproj.Project.PropertyGroup | Select-Object -First 1

    if ($PropertyGroup.Version) {
        return $PropertyGroup.Version.Trim()
    }

    return "1.0.0"
}

$ProjectLicenseMap = @{}

foreach ($Project in $Projects) {
    Write-Host "Gathering Licenses: $($Project.BaseName)" -ForegroundColor Cyan

    $LicenseTempDir = Join-Path $OutputDirectory "temp_licenses_$($Project.BaseName)"
    $ProjectLicensesDir = Join-Path $LicenseTempDir "third-party-notices"
    $OwnPackageJsonPath = Join-Path $LicenseTempDir "OwnPackage.json"
    
    New-Item -ItemType Directory -Path $LicenseTempDir -Force | Out-Null

    try {
        Write-Host "  Restoring NuGet packages..." -ForegroundColor Gray
        $RestoreArgs = @(
            "restore", $Project.FullName
        )
        $RestoreOutput = dotnet @RestoreArgs 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "NuGet restore failed for $($Project.BaseName): $($RestoreOutput -join "`n")"
        }
        
        [xml]$csproj = Get-Content $Project.FullName
        $PropertyGroup = $csproj.Project.PropertyGroup | Select-Object -First 1
        
        $PackageId = if ($PropertyGroup.PackageId) { $PropertyGroup.PackageId } else { $Project.BaseName }
        $Version = if ($PropertyGroup.Version) { $PropertyGroup.Version } else { "1.0.0" }
        $ProjectUrl = if ($PropertyGroup.PackageProjectUrl) { $PropertyGroup.PackageProjectUrl } else { "https://github.com/Puchaczov/Musoq.DataSources" }
        
        $OwnPackage = @{
            PackageId = $PackageId
            PackageVersion = $Version
            PackageProjectUrl = $ProjectUrl
            License = "MIT"
            LicenseUrl = "$ProjectUrl/blob/main/LICENSE"
        }
        
        $OwnPackage | ConvertTo-Json | Set-Content -Path $OwnPackageJsonPath
        
        $GatherArgs = @(
            "retrieve",
            "--solution-or-cs-project-file-path", $Project.FullName,
            "--own-package-file-path", $OwnPackageJsonPath,
            "--licenses-folder", $ProjectLicensesDir,
            "--links-cache-file-path", $LinksCacheFile,
            "--manual-links-file-path", $LinksManualFile,
            "--licenses-cache-folder", $LicensesCacheDir,
            "--downloaded-licenses-folder", $DownloadedLicensesDir
        )
        
        & $LicenseGathererTool @GatherArgs | Out-Null
        $ProjectLicenseMap[$Project.FullName] = $ProjectLicensesDir
    }
    catch {
        Write-Warning "License gathering failed for $($Project.BaseName): $_"
    }
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

            if ($ProjectLicensesDir -and (Test-Path $ProjectLicensesDir)) {
                $DestLicensesDir = Join-Path $PublishDir "third-party-notices"
                Copy-Item -Path $ProjectLicensesDir -Destination $DestLicensesDir -Recurse -Force
            }

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
        (Get-ProjectVersion -Project $Project),
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

foreach ($Project in $Projects) {
    $LicenseTempDir = Join-Path $OutputDirectory "temp_licenses_$($Project.BaseName)"
    if (Test-Path $LicenseTempDir) { Remove-Item $LicenseTempDir -Recurse -Force -ErrorAction SilentlyContinue }
}
