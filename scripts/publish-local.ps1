[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projectPath = Join-Path $repositoryRoot 'src\ZomniverseGitPet\ZomniverseGitPet.csproj'
$publishDirectory = Join-Path $repositoryRoot 'src\ZomniverseGitPet\bin\Release\net8.0-windows\win-x64\publish'
$repositoryParent = Split-Path -Parent $repositoryRoot

<#
PATCH: DISTINCT DEVELOPMENT BUILD IDENTITY
DATE.TIME: 2026-09-10 19:10 +03:00
Keep development builds unmistakable from installed releases.
#>
$publishedExecutable = Join-Path $publishDirectory 'ZomniverseGitPet.exe'

<#
PATCH: LOCAL DEV RUNTIME OUTSIDE DROPBOX
DATE.TIME: 2026-09-11 08:44 +03:00
Keep the running DEV executable outside cloud-synced folders.
#>
if ([string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) {
    throw 'LOCALAPPDATA is unavailable. GitPet cannot choose a safe local DEV runtime folder.'
}

$releaseDirectory = Join-Path $env:LOCALAPPDATA 'ZomniverseGitPet\DEV'
$releaseExecutable = Join-Path $releaseDirectory 'DEV-ZomniverseGitPet.exe'

<#
PATCH: FUTURE-PROOF DEV SHORTCUT
DATE.TIME: 2026-09-10 19:20 +03:00
Create a permanent DEV launcher and remove legacy ambiguity.
#>
$startMenuPrograms = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
$devShortcut = Join-Path $startMenuPrograms 'DEV-ZGitPet.lnk'

# Previous DEV runtime locations lived beside the Dropbox repository.
# Clean only these known historical DEV artifacts after the safe local copy succeeds.
$legacyDevArtifacts = @(
    (Join-Path $repositoryParent 'ZomniverseGitPet_Releases\current\ZomniverseGitPet.exe'),
    (Join-Path $repositoryParent 'ZomniverseGitPet_Releases\DEV-current\DEV-ZomniverseGitPet.exe')
)

if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
    throw "ZomniverseGitPet project was not found at: $projectPath"
}

Write-Host 'Publishing ZomniverseGitPet DEV build (Release, win-x64, self-contained, single-file)...'
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
    throw "The DEV-ZomniverseGitPet.exe build is still running or locked. Exit DEV-ZGitPet, or end DEV-ZomniverseGitPet.exe in Task Manager, then run this script again. Target: $releaseExecutable"
}

if (-not (Test-Path -LiteralPath $releaseExecutable -PathType Leaf)) {
    throw "The published DEV executable could not be placed at: $releaseExecutable"
}

# Always recreate the shortcut so it follows the machine-local DEV runtime location.
try {
    New-Item -ItemType Directory -Force -Path $startMenuPrograms | Out-Null
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($devShortcut)
    $shortcut.TargetPath = $releaseExecutable
    $shortcut.WorkingDirectory = $releaseDirectory
    $shortcut.IconLocation = "$releaseExecutable,0"
    $shortcut.Description = 'ZomniverseGitPet development build'
    $shortcut.Save()
}
catch {
    Write-Warning "DEV build succeeded, but the Start Menu shortcut could not be refreshed: $($_.Exception.Message)"
}

foreach ($legacyDevExecutable in $legacyDevArtifacts) {
    if (-not (Test-Path -LiteralPath $legacyDevExecutable -PathType Leaf)) {
        continue
    }

    try {
        $legacyDevDirectory = Split-Path -Parent $legacyDevExecutable
        Remove-Item -LiteralPath $legacyDevExecutable -Force
        if ((Get-ChildItem -LiteralPath $legacyDevDirectory -Force | Measure-Object).Count -eq 0) {
            Remove-Item -LiteralPath $legacyDevDirectory -Force
        }
    }
    catch {
        Write-Warning "Legacy DEV executable could not be removed. It may still be running: $legacyDevExecutable"
    }
}

$finalExecutable = (Get-Item -LiteralPath $releaseExecutable).FullName
Write-Host ''
Write-Host 'ZomniverseGitPet DEV build is ready:'
Write-Host $finalExecutable
Write-Host ''
Write-Host 'Start Menu shortcut: DEV-ZGitPet'
Write-Host $devShortcut
