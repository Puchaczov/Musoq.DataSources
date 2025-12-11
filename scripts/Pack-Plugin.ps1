param(
    [string]$PluginName = "All",
    [string]$OutputDirectory = "$PSScriptRoot/../artifacts"
)

$ErrorActionPreference = "Stop"

$Targets = @(
    @{ Rid = "win-x64";        Platform = "windows"; Architecture = "x64" },
    @{ Rid = "linux-x64";      Platform = "linux";   Architecture = "x64" },
    @{ Rid = "osx-arm64";      Platform = "macos";   Architecture = "arm64" },
    @{ Rid = "linux-musl-x64"; Platform = "alpine";  Architecture = "x64" }
)

$ExcludedAssemblies = @("Musoq.Schema.dll", "Musoq.Parser.dll", "Musoq.Plugins.dll")

$IgnorePatterns = @(
    "\.Tests", 
    "\.Benchmarks", 
    "Helpers$", 
    "\.Common$", 
    "\.CommandLineArguments$"
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

$Projects = Get-ChildItem -Path $SolutionRoot -Recurse -Filter "Musoq.DataSources.*.csproj"

if ($PluginName -ne "All") {
    $Projects = $Projects | Where-Object { $_.BaseName -eq $PluginName }
} else {
    foreach ($Pattern in $IgnorePatterns) {
        $Projects = $Projects | Where-Object { $_.BaseName -notmatch $Pattern }
    }
}

if ($Projects.Count -eq 0) {
    Write-Error "No matching plugin projects found."
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
        dotnet @RestoreArgs | Out-Null
        
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

Write-Host "Starting Parallel Build..." -ForegroundColor Cyan

$BuildScriptBlock = {
    param($ProjectFullName, $ProjectBaseName, $OutputDirectory, $Targets, $ExcludedAssemblies, $ProjectLicensesDir)
    
    $Results = @()
    foreach ($Target in $Targets) {
        $Rid = $Target.Rid
        $TempDir = Join-Path $OutputDirectory "temp_${ProjectBaseName}_$Rid"
        $PublishDir = Join-Path $TempDir "publish"
        $PackageDir = Join-Path $TempDir "package"
        
        try {
            $PublishArgs = @(
                "publish", $ProjectFullName,
                "-c", "Release",
                "-r", $Rid,
                "--no-self-contained",
                "-o", $PublishDir
            )
            dotnet @PublishArgs | Out-Null

            New-Item -ItemType Directory -Path $PackageDir -Force | Out-Null
            
            foreach ($Dll in $ExcludedAssemblies) {
                Remove-Item (Join-Path $PublishDir $Dll) -ErrorAction SilentlyContinue
            }

            if ($ProjectLicensesDir -and (Test-Path $ProjectLicensesDir)) {
                $DestLicensesDir = Join-Path $PublishDir "third-party-notices"
                Copy-Item -Path $ProjectLicensesDir -Destination $DestLicensesDir -Recurse -Force
            }

            $InnerZipPath = Join-Path $PackageDir "Plugin.zip"
            Compress-Archive -Path "$PublishDir\*" -DestinationPath $InnerZipPath -Force

            Set-Content -Path "$PackageDir\EntryPoint.txt" -Value "${ProjectBaseName}.dll"
            Set-Content -Path "$PackageDir\Platform.txt" -Value $Target.Platform
            Set-Content -Path "$PackageDir\Architecture.txt" -Value $Target.Architecture

            $ZipName = "${ProjectBaseName}-$($Target.Platform)-$($Target.Architecture).zip"
            $ZipPath = Join-Path $OutputDirectory $ZipName
            Compress-Archive -Path "$PackageDir\*" -DestinationPath $ZipPath -Force
            
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

$MaxConcurrency = [Environment]::ProcessorCount
$RunningJobs = @()

foreach ($Project in $Projects) {
    while ($RunningJobs.Count -ge $MaxConcurrency) {
        $Finished = $RunningJobs | Where-Object { $_.State -ne 'Running' }
        foreach ($Job in $Finished) {
            Receive-Job -Job $Job | Write-Host
            Remove-Job -Job $Job
        }
        $RunningJobs = $RunningJobs | Where-Object { $_.State -eq 'Running' }
        if ($RunningJobs.Count -ge $MaxConcurrency) { Start-Sleep -Milliseconds 200 }
    }
    
    Write-Host "Queueing Build: $($Project.BaseName)" -ForegroundColor Gray
    
    $JobParams = @(
        $Project.FullName,
        $Project.BaseName,
        $OutputDirectory,
        $Targets,
        $ExcludedAssemblies,
        $ProjectLicenseMap[$Project.FullName]
    )
    
    $RunningJobs += Start-Job -ScriptBlock $BuildScriptBlock -ArgumentList $JobParams
}

$RunningJobs | Wait-Job | Receive-Job | Write-Host

foreach ($Project in $Projects) {
    $LicenseTempDir = Join-Path $OutputDirectory "temp_licenses_$($Project.BaseName)"
    if (Test-Path $LicenseTempDir) { Remove-Item $LicenseTempDir -Recurse -Force -ErrorAction SilentlyContinue }
}

