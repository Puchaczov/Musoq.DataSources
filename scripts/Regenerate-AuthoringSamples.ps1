[CmdletBinding()]
param([string]$OutputDirectory = (Join-Path $PSScriptRoot '../artifacts/generated-code-samples'))
$ErrorActionPreference = 'Stop'
$previous = $env:MUSOQ_AUTHORING_SAMPLES
try {
    $env:MUSOQ_AUTHORING_SAMPLES = [IO.Path]::GetFullPath($OutputDirectory)
    dotnet test (Join-Path $PSScriptRoot '../Musoq.DataSources.RepresentativeTests') -c Release --no-build --nologo --verbosity quiet --filter 'FullyQualifiedName~NativePluginAuthoringTests' --logger 'console;verbosity=minimal'
    if ($LASTEXITCODE -ne 0) { throw 'Authoring sample execution failed.' }
    $root = $env:MUSOQ_AUTHORING_SAMPLES
    $entries = @(Get-ChildItem -LiteralPath $root -Recurse -Filter '*.cs' | Sort-Object FullName | ForEach-Object {
        @{ Path = [IO.Path]::GetRelativePath($root, $_.FullName).Replace('\', '/'); Sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    })
    $entries | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $root 'manifest.json') -Encoding utf8
} finally {
    $env:MUSOQ_AUTHORING_SAMPLES = $previous
}
