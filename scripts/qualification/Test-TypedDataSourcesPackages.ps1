[CmdletBinding()]
param([Parameter(Mandatory)][string] $CandidateManifest, [Parameter(Mandatory)][string] $EvidenceDirectory)
$ErrorActionPreference = 'Stop'
$candidate = Get-Content -LiteralPath $CandidateManifest -Raw | ConvertFrom-Json
$core = Get-Content -LiteralPath $candidate.CoreCandidateManifest -Raw | ConvertFrom-Json
foreach ($package in @($candidate.Packages) + @($core.Packages)) {
    if ((Get-FileHash -LiteralPath $package.Path -Algorithm SHA256).Hash -ne $package.Sha256) { throw 'Immutable package hash differs.' }
}
$evidenceRoot = [System.IO.Path]::GetFullPath($EvidenceDirectory)
if (Test-Path -LiteralPath $evidenceRoot) { throw 'Consumer evidence already exists; use a new identity.' }
New-Item -ItemType Directory -Path $evidenceRoot | Out-Null
$consumerRoot = Join-Path $evidenceRoot 'consumer'
New-Item -ItemType Directory -Path $consumerRoot | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'TypedDataSourcesConsumer/Program.cs') -Destination $consumerRoot
$escape = { param($text) [System.Security.SecurityElement]::Escape([string]$text) }
$cache = (dotnet nuget locals global-packages --list | Select-Object -First 1) -replace '^[^:]+:\s*', ''
$providerSource = & $escape $candidate.PackageDirectory
$coreSource = & $escape $core.PackageDirectory
$externalSource = & $escape $cache.Trim()
$consumerCache = & $escape (Join-Path $evidenceRoot 'packages')
$configuration = @"
<configuration>
 <config><add key="globalPackagesFolder" value="$consumerCache" /></config>
 <packageSources><clear />
  <add key="typed-providers" value="$providerSource" /><add key="typed-core" value="$coreSource" />
  <add key="external-cache" value="$externalSource" />
 </packageSources>
 <packageSourceMapping><clear />
  <packageSource key="typed-providers"><package pattern="Musoq.DataSources.*" /></packageSource>
  <packageSource key="typed-core"><package pattern="Musoq.Converter" /><package pattern="Musoq.Evaluator" /><package pattern="Musoq.Parser" /><package pattern="Musoq.Plugins" /><package pattern="Musoq.Schema" /></packageSource>
  <packageSource key="external-cache"><package pattern="*" /></packageSource>
 </packageSourceMapping>
</configuration>
"@
$configPath = Join-Path $consumerRoot 'NuGet.Config'
Set-Content -LiteralPath $configPath -Value $configuration -Encoding utf8
$references = @($candidate.Assemblies | Where-Object Name -Like 'Musoq.DataSources.*.dll' | ForEach-Object {
    $id = [System.IO.Path]::GetFileNameWithoutExtension($_.Name)
    "<PackageReference Include=`"$id`" Version=`"$($candidate.Version)`" />"
})
$references += "<PackageReference Include=`"Musoq.Converter`" Version=`"$($core.Version)`" />"
$project = "<Project Sdk=`"Microsoft.NET.Sdk`"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings></PropertyGroup><ItemGroup>$($references -join [Environment]::NewLine)</ItemGroup></Project>"
$projectPath = Join-Path $consumerRoot 'TypedDataSourcesConsumer.csproj'
Set-Content -LiteralPath $projectPath -Value $project -Encoding utf8
dotnet restore $projectPath --configfile $configPath --nologo --verbosity quiet *> (Join-Path $evidenceRoot 'restore.log')
if ($LASTEXITCODE -ne 0) { throw 'Package consumer restore failed; evidence retained.' }
dotnet build $projectPath -c Release --no-restore --nologo --verbosity quiet *> (Join-Path $evidenceRoot 'build.log')
if ($LASTEXITCODE -ne 0) { throw 'Package consumer build failed; evidence retained.' }
dotnet run --project $projectPath -c Release --no-build -- (Join-Path $evidenceRoot 'fixtures') *> (Join-Path $evidenceRoot 'execution.log')
if ($LASTEXITCODE -ne 0 -or !(Select-String -LiteralPath (Join-Path $evidenceRoot 'execution.log') -SimpleMatch 'TYPED_DATASOURCES_CONSUMER_OK')) { throw 'Package consumer execution failed; evidence retained.' }
$binaryDirectory = Join-Path $consumerRoot 'bin/Release/net10.0'
$checked = foreach ($assembly in @($candidate.Assemblies) + @($core.Assemblies)) {
    $path = Join-Path $binaryDirectory $assembly.Name
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $assembly.Sha256) { throw "Consumed assembly differs: $($assembly.Name)" }
    $assembly.Name
}
$record = [ordered]@{Status='passed-local-consumer'; Commit=$candidate.Commit; CoreCommit=$core.Commit; Version=$candidate.Version; CoreVersion=$core.Version; CheckedAssemblies=@($checked); SqlContracts=3}
$record | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $evidenceRoot 'qualification.json') -Encoding utf8
Write-Output (Join-Path $evidenceRoot 'qualification.json')
