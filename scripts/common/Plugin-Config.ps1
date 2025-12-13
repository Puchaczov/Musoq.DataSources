# Common configuration and utilities for plugin management scripts

$script:IgnorePatterns = @(
    "Tests$",
    "\.Tests", 
    "\.Benchmarks", 
    "Helpers$", 
    "\.Common$", 
    "\.CommandLineArguments$"
)

$script:Targets = @(
    @{ Rid = "win-x64";        Platform = "windows"; Architecture = "x64" },
    @{ Rid = "linux-x64";      Platform = "linux";   Architecture = "x64" },
    @{ Rid = "osx-arm64";      Platform = "macos";   Architecture = "arm64" },
    @{ Rid = "linux-musl-x64"; Platform = "alpine";  Architecture = "x64" }
)

$script:RegistryReleaseTag = "plugin-registry"
$script:RegistryFileName = "plugin-registry.json"

function Get-SolutionRoot {
    return Resolve-Path "$PSScriptRoot/../.."
}

function Get-PluginProjects {
    param(
        [string]$PluginName = "All"
    )
    
    $SolutionRoot = Get-SolutionRoot
    $Projects = Get-ChildItem -Path $SolutionRoot -Recurse -Filter "Musoq.DataSources.*.csproj"
    
    if ($PluginName -ne "All") {
        $Projects = $Projects | Where-Object { $_.BaseName -eq $PluginName }
    } else {
        foreach ($Pattern in $script:IgnorePatterns) {
            $Projects = $Projects | Where-Object { $_.BaseName -notmatch $Pattern }
        }
    }
    
    return $Projects
}

function Get-ShortName {
    param(
        [string]$ProjectName
    )
    
    # Extract short name from Musoq.DataSources.XXX -> xxx (lowercase)
    $shortName = $ProjectName -replace "^Musoq\.DataSources\.", ""
    return $shortName.ToLower()
}

function Get-ProjectMetadata {
    param(
        [System.IO.FileInfo]$Project
    )
    
    [xml]$csproj = Get-Content $Project.FullName
    $PropertyGroup = $csproj.Project.PropertyGroup | Select-Object -First 1
    
    $Version = if ($PropertyGroup.Version) { $PropertyGroup.Version } else { "1.0.0" }
    $Description = if ($PropertyGroup.Description) { $PropertyGroup.Description } else { "" }
    $PackageTags = if ($PropertyGroup.PackageTags) { $PropertyGroup.PackageTags } else { "" }
    
    # Parse tags into array
    $TagsArray = @()
    if ($PackageTags) {
        $TagsArray = $PackageTags -split ",\s*" | ForEach-Object { $_.Trim().ToLower() } | Where-Object { $_ -and $_ -ne "dotnet-core" -and $_ -ne "musoq" }
    }
    
    $ShortName = Get-ShortName -ProjectName $Project.BaseName
    
    return @{
        Name = $Project.BaseName
        ShortName = $ShortName
        Version = $Version
        Description = $Description
        Tags = $TagsArray
        FullPath = $Project.FullName
        ReleaseTag = "$Version-$($Project.BaseName)"
    }
}

function Get-ArtifactNames {
    param(
        [string]$ProjectName
    )
    
    $artifacts = @{}
    foreach ($target in $script:Targets) {
        $key = "$($target.Platform)-$($target.Architecture)"
        $artifacts[$key] = "$ProjectName-$($target.Platform)-$($target.Architecture).zip"
    }
    return $artifacts
}
