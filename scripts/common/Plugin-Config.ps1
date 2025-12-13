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

$script:MaxStringLength = 500
$script:MaxDescriptionLength = 1000
$script:MaxTagLength = 50
$script:MaxTagCount = 20
$script:VersionPattern = '^\d+\.\d+\.\d+(-[\w\d]+)?$'
$script:PluginNamePattern = '^Musoq\.DataSources\.[A-Za-z][A-Za-z0-9]*$'
$script:ShortNamePattern = '^[a-z][a-z0-9]*$'
$script:SafeStringPattern = '^[a-zA-Z0-9\.\-_\s,]+$'

function Test-SafeString {
    param(
        [string]$Value,
        [int]$MaxLength = $script:MaxStringLength,
        [string]$Pattern = $script:SafeStringPattern,
        [switch]$AllowEmpty
    )
    
    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $AllowEmpty.IsPresent
    }
    
    if ($Value.Length -gt $MaxLength) {
        return $false
    }
    
    if ($Value -match '[<>|&;`$(){}\[\]\\\/\x00-\x1f]') {
        return $false
    }
    
    return $true
}

function Test-ValidVersion {
    param([string]$Version)
    
    if ([string]::IsNullOrWhiteSpace($Version)) {
        return $false
    }
    
    if ($Version.Length -gt 50) {
        return $false
    }
    
    if ($Version -notmatch $script:VersionPattern) {
        return $false
    }
    
    try {
        [void][version]($Version -replace '-.*$', '')
        return $true
    }
    catch {
        return $false
    }
}

function Test-ValidPluginName {
    param([string]$Name)
    
    if ([string]::IsNullOrWhiteSpace($Name)) {
        return $false
    }
    
    if ($Name.Length -gt 100) {
        return $false
    }
    
    return $Name -match $script:PluginNamePattern
}

function Test-ValidShortName {
    param([string]$ShortName)
    
    if ([string]::IsNullOrWhiteSpace($ShortName)) {
        return $false
    }
    
    if ($ShortName.Length -gt 50) {
        return $false
    }
    
    return $ShortName -match $script:ShortNamePattern
}

function Test-ValidDescription {
    param([string]$Description)
    
    if ([string]::IsNullOrEmpty($Description)) {
        return $true
    }
    
    if ($Description.Length -gt $script:MaxDescriptionLength) {
        return $false
    }
    
    if ($Description -match '[<>&;`$\x00-\x1f]') {
        return $false
    }
    
    return $true
}

function Test-ValidTags {
    param([array]$Tags)
    
    if ($null -eq $Tags -or $Tags.Count -eq 0) {
        return $true
    }
    
    if ($Tags.Count -gt $script:MaxTagCount) {
        return $false
    }
    
    foreach ($tag in $Tags) {
        if ([string]::IsNullOrWhiteSpace($tag)) {
            continue
        }
        
        if ($tag.Length -gt $script:MaxTagLength) {
            return $false
        }
        
        if ($tag -notmatch '^[a-z0-9\-]+$') {
            return $false
        }
    }
    
    return $true
}

function Test-ValidRepository {
    param([string]$Repository)
    
    if ([string]::IsNullOrWhiteSpace($Repository)) {
        return $false
    }
    
    if ($Repository -notmatch '^[a-zA-Z0-9\-_]+/[a-zA-Z0-9\.\-_]+$') {
        return $false
    }
    
    if ($Repository.Length -gt 200) {
        return $false
    }
    
    return $true
}

function Get-SanitizedString {
    param(
        [string]$Value,
        [int]$MaxLength = $script:MaxStringLength
    )
    
    if ([string]::IsNullOrEmpty($Value)) {
        return ""
    }
    
    $sanitized = $Value -replace '[<>|&;`$(){}\[\]\x00-\x1f]', ''
    
    if ($sanitized.Length -gt $MaxLength) {
        $sanitized = $sanitized.Substring(0, $MaxLength)
    }
    
    return $sanitized.Trim()
}

function Get-SolutionRoot {
    $root = Resolve-Path "$PSScriptRoot/../.."
    
    $solutionFile = Join-Path $root "Musoq.DataSources.sln"
    if (-not (Test-Path $solutionFile)) {
        throw "Invalid solution root: $root (solution file not found)"
    }
    
    return $root
}

function Get-PluginProjects {
    param(
        [string]$PluginName = "All"
    )
    
    $SolutionRoot = Get-SolutionRoot
    $Projects = Get-ChildItem -Path $SolutionRoot -Recurse -Filter "Musoq.DataSources.*.csproj"
    
    if ($PluginName -ne "All") {
        if (-not (Test-ValidPluginName -Name $PluginName)) {
            throw "Invalid plugin name format: $PluginName"
        }
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
    
    if (-not (Test-ValidPluginName -Name $ProjectName)) {
        throw "Invalid project name format: $ProjectName"
    }
    
    $shortName = $ProjectName -replace "^Musoq\.DataSources\.", ""
    $shortName = $shortName.ToLower()
    
    if (-not (Test-ValidShortName -ShortName $shortName)) {
        throw "Generated short name is invalid: $shortName"
    }
    
    return $shortName
}

function Get-ProjectMetadata {
    param(
        [System.IO.FileInfo]$Project
    )
    
    if (-not $Project -or -not (Test-Path $Project.FullName)) {
        throw "Project file not found: $($Project.FullName)"
    }
    
    if (-not (Test-ValidPluginName -Name $Project.BaseName)) {
        throw "Invalid plugin project name: $($Project.BaseName)"
    }
    
    [xml]$csproj = Get-Content $Project.FullName
    $PropertyGroup = $csproj.Project.PropertyGroup | Select-Object -First 1
    
    $Version = if ($PropertyGroup.Version) { $PropertyGroup.Version.Trim() } else { "1.0.0" }
    if (-not (Test-ValidVersion -Version $Version)) {
        throw "Invalid version format in $($Project.BaseName): $Version"
    }
    
    $Description = if ($PropertyGroup.Description) { 
        Get-SanitizedString -Value $PropertyGroup.Description -MaxLength $script:MaxDescriptionLength 
    } else { 
        "" 
    }
    if (-not (Test-ValidDescription -Description $Description)) {
        throw "Invalid description in $($Project.BaseName)"
    }
    
    $PackageTags = if ($PropertyGroup.PackageTags) { $PropertyGroup.PackageTags } else { "" }
    $TagsArray = @()
    if ($PackageTags) {
        $TagsArray = $PackageTags -split ",\s*" | ForEach-Object { 
            $_.Trim().ToLower() -replace '[^a-z0-9\-]', ''
        } | Where-Object { 
            $_ -and $_.Length -gt 0 -and $_ -ne "dotnet-core" -and $_ -ne "musoq" 
        } | Select-Object -First $script:MaxTagCount
    }
    if (-not (Test-ValidTags -Tags $TagsArray)) {
        throw "Invalid tags in $($Project.BaseName)"
    }
    
    $ShortName = Get-ShortName -ProjectName $Project.BaseName
    $ReleaseTag = "$Version-$($Project.BaseName)"
    
    if ($ReleaseTag.Length -gt 200 -or $ReleaseTag -match '[<>|&;`$\s]') {
        throw "Invalid release tag format: $ReleaseTag"
    }
    
    return @{
        Name = $Project.BaseName
        ShortName = $ShortName
        Version = $Version
        Description = $Description
        Tags = $TagsArray
        FullPath = $Project.FullName
        ReleaseTag = $ReleaseTag
    }
}

function Get-ArtifactNames {
    param(
        [string]$ProjectName
    )
    
    if (-not (Test-ValidPluginName -Name $ProjectName)) {
        throw "Invalid project name: $ProjectName"
    }
    
    $artifacts = @{}
    foreach ($target in $script:Targets) {
        $key = "$($target.Platform)-$($target.Architecture)"
        $artifactName = "$ProjectName-$($target.Platform)-$($target.Architecture).zip"
        
        if ($artifactName -match '[<>|&;`$\s\\\/]') {
            throw "Invalid artifact name generated: $artifactName"
        }
        
        $artifacts[$key] = $artifactName
    }
    return $artifacts
}
