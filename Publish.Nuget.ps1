param(
    [string]$unusedNugetExecutable,
    [Parameter(Mandatory=$true)]
    [string]$project,
    [Parameter(Mandatory=$true)]
    [string]$apiKey,
    [string]$Source = "https://api.nuget.org/v3/index.json"
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($apiKey)) {
    throw "NuGet API key is required."
}

$packages = @(Get-ChildItem -Path . -Filter "$project.*.nupkg" -File | Where-Object { $_.Name -notlike "*.symbols.nupkg" })
if ($packages.Count -eq 0) {
    throw "No NuGet package found for '$project' in $(Get-Location)."
}

foreach ($package in $packages) {
    Write-Host "Publishing $($package.Name)..." -ForegroundColor Cyan
    dotnet nuget push $package.FullName `
        --source $Source `
        --api-key $apiKey `
        --skip-duplicate

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet nuget push failed for $($package.Name)."
    }

    Write-Host "done." -ForegroundColor Green
}
