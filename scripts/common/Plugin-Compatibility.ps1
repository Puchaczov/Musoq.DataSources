$script:MusoqPluginCompatibilityFileName = "MusoqPluginCompatibility.json"
$script:MusoqPluginCompatibilityFormatVersion = 1
$script:MusoqPluginRuntimeFamily = "musoq-runtime-v2"
$script:MusoqPluginTargetFramework = "net10.0"
$script:MusoqPluginAbiPackages = @("Musoq.Schema", "Musoq.Plugins")
$script:MusoqHostOwnedAssemblyPatterns = @(
    "Musoq.Schema.dll",
    "Musoq.Plugins.dll",
    "Musoq.Parser.dll",
    "Musoq.Converter.dll",
    "Musoq.Evaluator.dll",
    "Musoq.CommandLine.dll",
    "Musoq.Targets.*.dll"
)

. (Join-Path $PSScriptRoot "Plugin-Versioning.ps1")

function Get-MusoqCompatibilityProperty {
    param(
        [Parameter(Mandatory=$true)]
        [object]$Value,
        [Parameter(Mandatory=$true)]
        [string]$Name
    )

    if ($Value -is [System.Collections.IDictionary]) {
        return $Value[$Name]
    }

    return $Value.PSObject.Properties[$Name].Value
}

function New-MusoqPluginCompatibility {
    param(
        [Parameter(Mandatory=$true)]
        [string]$TargetFramework,
        [Parameter(Mandatory=$true)]
        [string]$SchemaVersion,
        [Parameter(Mandatory=$true)]
        [string]$PluginsVersion
    )

    if ($TargetFramework -ne $script:MusoqPluginTargetFramework) {
        throw "Unsupported plugin target framework '$TargetFramework'. Expected '$($script:MusoqPluginTargetFramework)'."
    }

    $schema = ConvertTo-MusoqSemVer -Version $SchemaVersion
    $plugins = ConvertTo-MusoqSemVer -Version $PluginsVersion
    if ($schema.Original -ne $plugins.Original) {
        throw "Musoq.Schema '$($schema.Original)' and Musoq.Plugins '$($plugins.Original)' must use the same ABI version."
    }

    if ($schema.Major -lt 1) {
        throw "Musoq ABI package major version must be positive."
    }

    $maximumVersionExclusive = "$($schema.Major + 1).0.0"
    return [ordered]@{
        formatVersion = $script:MusoqPluginCompatibilityFormatVersion
        runtimeFamily = $script:MusoqPluginRuntimeFamily
        targetFramework = $TargetFramework
        hostPackages = [ordered]@{
            "Musoq.Schema" = [ordered]@{
                minimumVersionInclusive = $schema.Original
                maximumVersionExclusive = $maximumVersionExclusive
            }
            "Musoq.Plugins" = [ordered]@{
                minimumVersionInclusive = $plugins.Original
                maximumVersionExclusive = $maximumVersionExclusive
            }
        }
    }
}

function Assert-MusoqPluginCompatibility {
    param(
        [Parameter(Mandatory=$true)]
        [object]$Compatibility
    )

    $formatVersion = Get-MusoqCompatibilityProperty -Value $Compatibility -Name "formatVersion"
    $runtimeFamily = Get-MusoqCompatibilityProperty -Value $Compatibility -Name "runtimeFamily"
    $targetFramework = Get-MusoqCompatibilityProperty -Value $Compatibility -Name "targetFramework"
    $hostPackages = Get-MusoqCompatibilityProperty -Value $Compatibility -Name "hostPackages"

    if ($formatVersion -ne $script:MusoqPluginCompatibilityFormatVersion) {
        throw "Unsupported plugin compatibility format version '$formatVersion'."
    }
    if ($runtimeFamily -ne $script:MusoqPluginRuntimeFamily) {
        throw "Unsupported plugin runtime family '$runtimeFamily'."
    }
    if ($targetFramework -ne $script:MusoqPluginTargetFramework) {
        throw "Unsupported plugin target framework '$targetFramework'."
    }
    if ($null -eq $hostPackages) {
        throw "Plugin compatibility hostPackages is required."
    }

    $minimumVersions = @()
    foreach ($packageName in $script:MusoqPluginAbiPackages) {
        $packageRange = Get-MusoqCompatibilityProperty -Value $hostPackages -Name $packageName
        if ($null -eq $packageRange) {
            throw "Plugin compatibility is missing required host package '$packageName'."
        }

        $minimum = [string](Get-MusoqCompatibilityProperty -Value $packageRange -Name "minimumVersionInclusive")
        $maximum = [string](Get-MusoqCompatibilityProperty -Value $packageRange -Name "maximumVersionExclusive")
        $minimumVersion = ConvertTo-MusoqSemVer -Version $minimum
        $maximumVersion = ConvertTo-MusoqSemVer -Version $maximum
        if ($minimumVersion.Major + 1 -ne $maximumVersion.Major -or $maximumVersion.Minor -ne 0 -or $maximumVersion.Patch -ne 0 -or $maximumVersion.IsPrerelease) {
            throw "Host package '$packageName' must use an exclusive next-major upper bound."
        }

        $minimumVersions += $minimumVersion.Original
    }

    if ($minimumVersions[0] -ne $minimumVersions[1]) {
        throw "Musoq.Schema and Musoq.Plugins compatibility minimums must match."
    }
}

function Get-MusoqPluginCompatibility {
    param(
        [Parameter(Mandatory=$true)]
        [string]$ProjectPath
    )

    $resolvedProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
    $arguments = @(
        "msbuild", $resolvedProjectPath,
        "-nologo",
        "-verbosity:quiet",
        "-getProperty:TargetFramework,MusoqSchemaVersion,MusoqPluginsVersion",
        "-getItem:PackageReference"
    )
    $evaluationOutput = & dotnet @arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Could not evaluate compatibility for '$resolvedProjectPath': $($evaluationOutput -join "`n")"
    }

    $evaluationText = ($evaluationOutput -join "`n").Trim()
    try {
        $evaluation = $evaluationText | ConvertFrom-Json -Depth 100
    }
    catch {
        throw "MSBuild returned malformed compatibility data for '$resolvedProjectPath': $_"
    }

    $targetFramework = [string]$evaluation.Properties.TargetFramework
    $configuredVersions = @{
        "Musoq.Schema" = [string]$evaluation.Properties.MusoqSchemaVersion
        "Musoq.Plugins" = [string]$evaluation.Properties.MusoqPluginsVersion
    }
    $references = @($evaluation.Items.PackageReference)
    $evaluatedVersions = @{}

    foreach ($packageName in $script:MusoqPluginAbiPackages) {
        $matchingReferences = @($references | Where-Object { $_.Identity -eq $packageName })
        if ($matchingReferences.Count -ne 1) {
            throw "Project '$resolvedProjectPath' must have exactly one evaluated '$packageName' PackageReference."
        }

        $reference = $matchingReferences[0]
        $version = [string]$reference.Version
        if ([string]::IsNullOrWhiteSpace($configuredVersions[$packageName]) -or $configuredVersions[$packageName] -ne $version) {
            throw "Evaluated '$packageName' version '$version' does not match the centralized version '$($configuredVersions[$packageName])'."
        }

        $excludedAssets = @([string]$reference.ExcludeAssets -split '[;,]' | ForEach-Object { $_.Trim().ToLowerInvariant() })
        if ($excludedAssets -notcontains "runtime") {
            throw "Project '$resolvedProjectPath' must exclude runtime assets for '$packageName'."
        }

        $evaluatedVersions[$packageName] = $version
    }

    $compatibility = New-MusoqPluginCompatibility `
        -TargetFramework $targetFramework `
        -SchemaVersion $evaluatedVersions["Musoq.Schema"] `
        -PluginsVersion $evaluatedVersions["Musoq.Plugins"]
    Assert-MusoqPluginCompatibility -Compatibility $compatibility
    return $compatibility
}

function ConvertTo-MusoqPluginCompatibilityJson {
    param(
        [Parameter(Mandatory=$true)]
        [object]$Compatibility
    )

    Assert-MusoqPluginCompatibility -Compatibility $Compatibility
    return ($Compatibility | ConvertTo-Json -Depth 10)
}

function Read-MusoqPluginCompatibilityManifest {
    param(
        [Parameter(Mandatory=$true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Plugin compatibility manifest was not found: $Path"
    }

    try {
        $compatibility = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -Depth 100
    }
    catch {
        throw "Plugin compatibility manifest is malformed: $Path. $_"
    }

    Assert-MusoqPluginCompatibility -Compatibility $compatibility
    return $compatibility
}
