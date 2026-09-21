[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project = Join-Path $repositoryRoot 'experiments\LynxLab\LynxLab.csproj'

if (-not (Test-Path -LiteralPath $project -PathType Leaf)) {
    throw "Lynx Lab project was not found: $project"
}

Write-Host 'Starting Lynx Lab...'
& dotnet run --project $project
if ($LASTEXITCODE -ne 0) {
    throw "Lynx Lab exited with code $LASTEXITCODE."
}
