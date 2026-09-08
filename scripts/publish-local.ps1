[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projectPath = Join-Path $repositoryRoot 'src\ZomniverseGitPet\ZomniverseGitPet.csproj'
$publishDirectory = Join-Path $repositoryRoot 'src\ZomniverseGitPet\bin\Release\net8.0-windows\win-x64\publish'
$repositoryParent = Split-Path -Parent $repositoryRoot
$releaseDirectory = Join-Path $repositoryParent 'ZomniverseGitPet_Releases\current'
$publishedExecutable = Join-Path $publishDirectory 'ZomniverseGitPet.exe'
$releaseExecutable = Join-Path $releaseDirectory 'ZomniverseGitPet.exe'

if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
    throw "ZomniverseGitPet project was not found at: $projectPath"
}

Write-Host 'Publishing ZomniverseGitPet (Release, win-x64, self-contained, single-file)...'
& dotnet publish $projectPath `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true

if ($LASTEXITCODE -ne 0) {
    throw "ZomniverseGitPet publishing failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path -LiteralPath $publishedExecutable -PathType Leaf)) {
    throw "Publishing completed without producing the expected executable: $publishedExecutable"
}

New-Item -ItemType Directory -Force -Path $releaseDirectory | Out-Null
try {
    Copy-Item -LiteralPath $publishedExecutable -Destination $releaseExecutable -Force
}
catch [System.IO.IOException] {
    throw "The current ZomniverseGitPet.exe is still running or locked. Exit it from the app, or end ZomniverseGitPet.exe in Task Manager, then run this script again. Target: $releaseExecutable"
}

if (-not (Test-Path -LiteralPath $releaseExecutable -PathType Leaf)) {
    throw "The published executable could not be placed at: $releaseExecutable"
}

$finalExecutable = (Get-Item -LiteralPath $releaseExecutable).FullName
Write-Host ''
Write-Host 'ZomniverseGitPet local development build is ready:'
Write-Host $finalExecutable

