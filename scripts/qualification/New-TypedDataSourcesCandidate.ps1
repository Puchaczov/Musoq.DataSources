[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $AcceptedQualification,
    [Parameter(Mandatory)][string] $CoreCandidateManifest,
    [Parameter(Mandatory)][string] $OutputDirectory,
    [string] $RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
)
$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)
$qualification = Get-Content -LiteralPath $AcceptedQualification -Raw | ConvertFrom-Json
$core = Get-Content -LiteralPath $CoreCandidateManifest -Raw | ConvertFrom-Json
$commit = (git -C $repositoryRoot rev-parse HEAD).Trim()
$tree = (git -C $repositoryRoot rev-parse 'HEAD^{tree}').Trim()
if ($qualification.Status -ne 'accepted-local-commit' -or $qualification.Repository -ne 'DataSources' -or
    $qualification.Commit -ne $commit -or $qualification.QualifiedTree -ne $tree) {
    throw 'Local packages require the exact accepted DataSources commit and qualified tree.'
}
if (@(git -C $repositoryRoot status --porcelain).Count -ne 0) { throw 'The accepted working tree must be clean.' }
foreach ($package in $core.Packages) {
    if ((Get-FileHash -LiteralPath $package.Path -Algorithm SHA256).Hash -ne $package.Sha256) { throw 'Core package bytes changed.' }
}
$outputRoot = [System.IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $outputRoot) { throw 'Candidate output exists; immutable identities must never be replaced.' }
$version = "1.0.0-alpha.typedsource12c.$commit.r$([DateTime]::UtcNow.ToString('yyyyMMddHHmmssfffffff'))"
New-Item -ItemType Directory -Path $outputRoot | Out-Null
$projects = @(Get-ChildItem -LiteralPath $repositoryRoot -Directory -Filter 'Musoq.*' | ForEach-Object {
    $name = $_.Name
    if ($name -match 'Tests|Benchmark|Testing|Playground') { return }
    $projectPath = Join-Path $_.FullName "$name.csproj"
    if (!(Test-Path -LiteralPath $projectPath)) { return }
    $project = [xml](Get-Content -LiteralPath $projectPath -Raw)
    if (@($project.SelectNodes("//*[local-name()='IsPackable']") | Where-Object InnerText -EQ 'false').Count -gt 0) { return }
    $idNode = $project.SelectSingleNode("//*[local-name()='PackageId']")
    $id = if ($null -eq $idNode) { $name } else { $idNode.InnerText }
    [pscustomobject]@{ Id=$id; Path=$projectPath }
})
foreach ($project in $projects) {
    dotnet pack $project.Path -c Release --no-build --no-restore --nologo --verbosity quiet "-p:PackageVersion=$version" "-p:RepositoryCommit=$commit" -o $outputRoot *> (Join-Path $outputRoot "$($project.Id)-pack.log")
    if ($LASTEXITCODE -ne 0) { throw "Local pack failed: $($project.Id); evidence retained." }
}
$assemblies = @{}
$packages = foreach ($project in $projects) {
    $path = Join-Path $outputRoot "$($project.Id).$version.nupkg"
    $archive = [System.IO.Compression.ZipFile]::OpenRead($path)
    try {
        $reader = [System.IO.StreamReader]::new($archive.GetEntry("$($project.Id).nuspec").Open())
        try { $specification = [xml]$reader.ReadToEnd() } finally { $reader.Dispose() }
        $metadata = $specification.package.metadata
        if ($metadata.id -ne $project.Id -or $metadata.version -ne $version -or $metadata.repository.commit -ne $commit) { throw 'Package source identity differs.' }
        foreach ($dependency in $specification.SelectNodes("//*[local-name()='dependency']")) {
            if ($dependency.id -in @('Musoq.Schema','Musoq.Plugins','Musoq.Parser','Musoq.Evaluator','Musoq.Converter') -and
                $dependency.version -ne $core.Version) { throw "Core dependency identity differs: $($dependency.id)" }
            if ($dependency.id -like 'Musoq.DataSources.*' -and $dependency.version -ne $version) { throw "Internal package identity differs: $($dependency.id)" }
        }
        foreach ($assembly in $archive.Entries | Where-Object FullName -Like 'lib/net10.0/*.dll') {
            $name = [System.IO.Path]::GetFileName($assembly.FullName)
            if ($name -like 'Musoq.*' -and $name -notlike 'Musoq.DataSources.*') { throw "Host assembly bundled into provider package: $name" }
            $stream = $assembly.Open()
            try { $hash = [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($stream)) } finally { $stream.Dispose() }
            if ($assemblies.ContainsKey($name) -and $assemblies[$name] -ne $hash) { throw "Assembly bytes differ across packages: $name" }
            $assemblies[$name] = $hash
        }
    } finally { $archive.Dispose() }
    [ordered]@{ Path=$path; Sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
    $symbols = Join-Path $outputRoot "$($project.Id).$version.snupkg"
    if (Test-Path -LiteralPath $symbols) { [ordered]@{ Path=$symbols; Sha256=(Get-FileHash -LiteralPath $symbols -Algorithm SHA256).Hash } }
}
if ($projects.Count -ne 19 -or $assemblies.Count -lt 19) { throw 'The provider package inventory is incomplete.' }
$manifest = [ordered]@{ Status='created-local'; Commit=$commit; Tree=$tree; Version=$version; PackageDirectory=$outputRoot;
    CoreCommit=$core.Commit; CoreVersion=$core.Version; CoreCandidateManifest=[System.IO.Path]::GetFullPath($CoreCandidateManifest);
    Qualification=[System.IO.Path]::GetFullPath($AcceptedQualification); Packages=@($packages);
    Assemblies=@($assemblies.GetEnumerator() | Sort-Object Name | ForEach-Object { [ordered]@{ Name=$_.Key; Sha256=$_.Value } }) }
$manifestPath = Join-Path $outputRoot 'candidate.json'
$manifest | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath $manifestPath -Encoding utf8
Write-Output $manifestPath
