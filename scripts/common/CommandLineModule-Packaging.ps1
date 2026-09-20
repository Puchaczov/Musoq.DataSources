$script:MusoqCommandLineModulesDirectoryName = "CommandLineModules"
$script:MusoqCommandLineModuleManifestFileName = "CommandLineModule.json"
$script:MusoqCommandLineModuleDefinitionFileName = "CommandLineModule.package.json"
$script:MusoqCommandLineModuleManifestFormatVersion = 1

function Assert-MusoqCommandLineModuleDefinition {
    param(
        [Parameter(Mandatory=$true)]
        [object]$Definition,
        [string]$Context = "Command-line module definition"
    )

    if ([int]$Definition.formatVersion -ne $script:MusoqCommandLineModuleManifestFormatVersion) {
        throw "$Context has unsupported formatVersion '$($Definition.formatVersion)'."
    }
    if ([string]$Definition.moduleId -cnotmatch '^[a-z0-9]+(\.[a-z0-9-]+)+$') {
        throw "$Context has invalid moduleId '$($Definition.moduleId)'."
    }
    if ([string]$Definition.moduleVersion -cnotmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(-[0-9A-Za-z.-]+)?$') {
        throw "$Context has invalid moduleVersion '$($Definition.moduleVersion)'."
    }

    $entryAssembly = [string]$Definition.entryAssembly
    if ([string]::IsNullOrWhiteSpace($entryAssembly) -or
        $entryAssembly -cne [System.IO.Path]::GetFileName($entryAssembly) -or
        [System.IO.Path]::GetExtension($entryAssembly) -cne '.dll') {
        throw "$Context has invalid entryAssembly '$entryAssembly'."
    }

    if ([string]$Definition.framework.packageId -cne 'Musoq.CommandLine') {
        throw "$Context must target the Musoq.CommandLine host ABI."
    }
    if ([string]$Definition.framework.versionRange -cnotmatch '^\[[^,\[\]]+,[^,\[\]]+\)$') {
        throw "$Context has invalid framework versionRange '$($Definition.framework.versionRange)'."
    }

    $names = @{}
    foreach ($requirement in @($Definition.requiredInvocationItems)) {
        $name = [string]$requirement.name
        $contract = [string]$requirement.contract
        if ($name -cnotmatch '^[a-z0-9]+(\.[a-z0-9-]+)+$') {
            throw "$Context has invalid invocation item name '$name'."
        }
        if ($contract -cnotmatch '^[a-z0-9]+(-[a-z0-9]+)*$') {
            throw "$Context has invalid invocation item contract '$contract'."
        }
        if ($names.ContainsKey($name)) {
            throw "$Context repeats invocation item '$name'."
        }
        $names[$name] = $true
    }
}

function Read-MusoqCommandLineModuleDefinition {
    param(
        [Parameter(Mandatory=$true)]
        [string]$ProjectPath
    )

    $definitionPath = Join-Path (Split-Path -Parent $ProjectPath) $script:MusoqCommandLineModuleDefinitionFileName
    if (-not (Test-Path -LiteralPath $definitionPath -PathType Leaf)) {
        throw "Command-line module definition was not found: $definitionPath"
    }
    try {
        $definition = Get-Content -LiteralPath $definitionPath -Raw | ConvertFrom-Json -Depth 20
    }
    catch {
        throw "Command-line module definition is malformed: $definitionPath. $_"
    }
    Assert-MusoqCommandLineModuleDefinition -Definition $definition -Context $definitionPath
    return $definition
}

function Write-MusoqCommandLineModuleManifest {
    param(
        [Parameter(Mandatory=$true)]
        [object]$Definition,
        [Parameter(Mandatory=$true)]
        [string]$ModuleDirectory
    )

    Assert-MusoqCommandLineModuleDefinition -Definition $Definition
    $files = @(
        Get-ChildItem -LiteralPath $ModuleDirectory -File |
            Where-Object { $_.Name -cne $script:MusoqCommandLineModuleManifestFileName } |
            Sort-Object Name |
            ForEach-Object {
                [ordered]@{
                    path = $_.Name
                    sizeBytes = [int64]$_.Length
                    sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
                }
            }
    )
    if (@($files | Where-Object { $_.path -ceq [string]$Definition.entryAssembly }).Count -ne 1) {
        throw "Command-line module entry assembly '$($Definition.entryAssembly)' is missing from '$ModuleDirectory'."
    }

    $requiredItems = @(
        @($Definition.requiredInvocationItems) | ForEach-Object {
            [ordered]@{
                name = [string]$_.name
                contract = [string]$_.contract
            }
        }
    )
    $manifest = [ordered]@{
        formatVersion = $script:MusoqCommandLineModuleManifestFormatVersion
        moduleId = [string]$Definition.moduleId
        moduleVersion = [string]$Definition.moduleVersion
        entryAssembly = [string]$Definition.entryAssembly
        framework = [ordered]@{
            packageId = [string]$Definition.framework.packageId
            versionRange = [string]$Definition.framework.versionRange
        }
        requiredInvocationItems = $requiredItems
        files = $files
    }
    $manifestPath = Join-Path $ModuleDirectory $script:MusoqCommandLineModuleManifestFileName
    [System.IO.File]::WriteAllText(
        $manifestPath,
        ($manifest | ConvertTo-Json -Depth 20),
        [System.Text.UTF8Encoding]::new($false))
    return $manifest
}

function Read-MusoqCommandLineModuleManifest {
    param(
        [Parameter(Mandatory=$true)]
        [string]$ModuleDirectory
    )

    $manifestPath = Join-Path $ModuleDirectory $script:MusoqCommandLineModuleManifestFileName
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        throw "Command-line module manifest was not found: $manifestPath"
    }
    try {
        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json -Depth 20
    }
    catch {
        throw "Command-line module manifest is malformed: $manifestPath. $_"
    }
    Assert-MusoqCommandLineModuleDefinition -Definition $manifest -Context $manifestPath

    $declared = @{}
    foreach ($file in @($manifest.files)) {
        $path = [string]$file.path
        if ([string]::IsNullOrWhiteSpace($path) -or $path -cne [System.IO.Path]::GetFileName($path)) {
            throw "Command-line module manifest contains unsafe file path '$path'."
        }
        if ($declared.ContainsKey($path)) {
            throw "Command-line module manifest repeats file '$path'."
        }
        if ([int64]$file.sizeBytes -le 0 -or [string]$file.sha256 -cnotmatch '^[0-9a-f]{64}$') {
            throw "Command-line module manifest has invalid integrity metadata for '$path'."
        }
        $filePath = Join-Path $ModuleDirectory $path
        if (-not (Test-Path -LiteralPath $filePath -PathType Leaf)) {
            throw "Command-line module file '$path' is missing."
        }
        $actualSize = (Get-Item -LiteralPath $filePath).Length
        $actualHash = (Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actualSize -ne [int64]$file.sizeBytes -or $actualHash -cne [string]$file.sha256) {
            throw "Command-line module file '$path' failed its integrity check."
        }
        $declared[$path] = $true
    }

    $actualFiles = @(Get-ChildItem -LiteralPath $ModuleDirectory -File |
        Where-Object { $_.Name -cne $script:MusoqCommandLineModuleManifestFileName })
    if ($declared.Count -ne $actualFiles.Count) {
        throw "Command-line module directory contains files that are not declared by its manifest."
    }
    if (-not $declared.ContainsKey([string]$manifest.entryAssembly)) {
        throw "Command-line module entry assembly '$($manifest.entryAssembly)' is not declared."
    }
    return $manifest
}

function Publish-MusoqCommandLineModule {
    param(
        [Parameter(Mandatory=$true)]
        [string]$ProjectPath,
        [Parameter(Mandatory=$true)]
        [string]$DestinationRoot,
        [Parameter(Mandatory=$true)]
        [string[]]$HostOwnedAssemblyPatterns
    )

    $definition = Read-MusoqCommandLineModuleDefinition -ProjectPath $ProjectPath
    $temporary = Join-Path ([System.IO.Path]::GetTempPath()) "musoq-command-line-module-$([guid]::NewGuid().ToString('N'))"
    $publishDirectory = Join-Path $temporary "publish"
    $moduleDirectory = Join-Path $DestinationRoot ([string]$definition.moduleId)
    try {
        $publishOutput = & dotnet publish $ProjectPath `
            --configuration Release `
            --framework net10.0 `
            --no-self-contained `
            -p:CopyLocalLockFileAssemblies=false `
            --output $publishDirectory 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "Command-line module publish failed: $($publishOutput -join "`n")"
        }

        $forbiddenPatterns = @($HostOwnedAssemblyPatterns) + @('Musoq.CommandLine.dll')
        $forbidden = @(Get-ChildItem -LiteralPath $publishDirectory -Recurse -File | Where-Object {
            $fileName = $_.Name
            @($forbiddenPatterns | Where-Object { $fileName -like $_ }).Count -gt 0
        })
        if ($forbidden.Count -gt 0) {
            throw "Command-line module publish contains host-owned assemblies: $(@($forbidden.Name | Sort-Object -Unique) -join ', ')"
        }

        if (Test-Path -LiteralPath $moduleDirectory) {
            throw "Command-line module destination already exists: $moduleDirectory"
        }
        New-Item -ItemType Directory -Path $moduleDirectory -Force | Out-Null
        $publishFiles = @(Get-ChildItem -LiteralPath $publishDirectory -File)
        foreach ($file in $publishFiles) {
            Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $moduleDirectory $file.Name)
        }

        $manifest = Write-MusoqCommandLineModuleManifest -Definition $definition -ModuleDirectory $moduleDirectory
        Read-MusoqCommandLineModuleManifest -ModuleDirectory $moduleDirectory | Out-Null
        return $manifest
    }
    finally {
        if (Test-Path -LiteralPath $temporary) {
            Remove-Item -LiteralPath $temporary -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}
