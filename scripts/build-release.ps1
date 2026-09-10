[CmdletBinding()]
param(
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$solutionPath = Join-Path $repositoryRoot 'ZomniverseGitPet.sln'
$projectPath = Join-Path $repositoryRoot 'src\ZomniverseGitPet\ZomniverseGitPet.csproj'
$testsPath = Join-Path $repositoryRoot 'tests\ZomniverseGitPet.Tests\ZomniverseGitPet.Tests.csproj'
$installerScript = Join-Path $repositoryRoot 'installer\ZomniverseGitPet.iss'
$repositoryParent = Split-Path -Parent $repositoryRoot

if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
    throw "Project file not found: $projectPath"
}
if (-not (Test-Path -LiteralPath $installerScript -PathType Leaf)) {
    throw "Installer definition not found: $installerScript"
}

[xml]$projectXml = Get-Content -LiteralPath $projectPath -Raw
$version = [string]($projectXml.Project.PropertyGroup.Version | Select-Object -First 1)
if ([string]::IsNullOrWhiteSpace($version)) {
    throw 'The project Version property is missing.'
}

$releaseRoot = Join-Path $repositoryParent ("ZomniverseGitPet_Releases\packages\{0}" -f $version)
$publishDirectory = Join-Path $releaseRoot 'publish'
$installerDirectory = Join-Path $releaseRoot 'installer'
$publishedExecutable = Join-Path $publishDirectory 'ZomniverseGitPet.exe'
$portableFileName = "ZomniverseGitPet-$version-win-x64.exe"
$portableExecutable = Join-Path $releaseRoot $portableFileName
$installerFileName = "ZomniverseGitPet-Setup-$version.exe"
$installerExecutable = Join-Path $installerDirectory $installerFileName

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory)] [string]$FilePath,
        [Parameter(ValueFromRemainingArguments)] [string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FilePath failed with exit code $LASTEXITCODE."
    }
}

function Find-InnoCompiler {
    $candidates = New-Object System.Collections.Generic.List[string]

    if (-not [string]::IsNullOrWhiteSpace($env:ISCC_EXE)) {
        $candidates.Add($env:ISCC_EXE)
    }

    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        $candidates.Add($command.Source)
    }

    <#
    PATCH: PER-USER INNO SETUP DISCOVERY
    DATE.TIME: 2026-09-10 14:35 +03:00
    Find Winget per-user Inno Setup installations automatically.
    #>
    if (-not [string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) {
        $candidates.Add((Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'))
        $candidates.Add((Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup\ISCC.exe'))
    }

    if (-not [string]::IsNullOrWhiteSpace(${env:ProgramFiles(x86)})) {
        $candidates.Add((Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'))
    }

    if (-not [string]::IsNullOrWhiteSpace($env:ProgramFiles)) {
        $candidates.Add((Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'))
    }

    $registryRoots = @(
        'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1',
        'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1',
        'HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1'
    )

    foreach ($registryRoot in $registryRoots) {
        try {
            $installLocation = (Get-ItemProperty -LiteralPath $registryRoot -ErrorAction Stop).InstallLocation
            if (-not [string]::IsNullOrWhiteSpace($installLocation)) {
                $candidates.Add((Join-Path $installLocation 'ISCC.exe'))
            }
        }
        catch {
        }
    }

    foreach ($candidate in $candidates | Select-Object -Unique) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return (Get-Item -LiteralPath $candidate).FullName
        }
    }

    throw @"
Inno Setup 6 compiler (ISCC.exe) was not found.
Install Inno Setup once on this development PC, then rerun this script.
You may also set ISCC_EXE to the full path of ISCC.exe.
End users will NOT need Inno Setup or the .NET SDK.
"@
}

Write-Host "Building ZomniverseGitPet $version release package..."
Write-Host ''

if (-not $SkipTests) {
    Write-Host '1/4  Building solution...'
    Invoke-CheckedCommand dotnet 'build' $solutionPath '-c' 'Release'

    Write-Host ''
    Write-Host '2/4  Running regression suite...'
    Invoke-CheckedCommand dotnet 'run' '--project' $testsPath '-c' 'Release'
}
else {
    Write-Host '1/4  Build/tests skipped by request.'
    Write-Host '2/4  Regression suite skipped by request.'
}

Write-Host ''
Write-Host '3/4  Publishing self-contained win-x64 executable...'
if (Test-Path -LiteralPath $publishDirectory) {
    Remove-Item -LiteralPath $publishDirectory -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $publishDirectory | Out-Null

Invoke-CheckedCommand dotnet `
    'publish' $projectPath `
    '--configuration' 'Release' `
    '--runtime' 'win-x64' `
    '--self-contained' 'true' `
    '-p:PublishSingleFile=true' `
    '--output' $publishDirectory

if (-not (Test-Path -LiteralPath $publishedExecutable -PathType Leaf)) {
    throw "Publish completed without the expected executable: $publishedExecutable"
}

Copy-Item -LiteralPath $publishedExecutable -Destination $portableExecutable -Force

Write-Host ''
Write-Host '4/4  Building Windows installer...'
$iscc = Find-InnoCompiler
Write-Host "Using Inno Setup compiler: $iscc"
New-Item -ItemType Directory -Force -Path $installerDirectory | Out-Null

$isccArguments = @(
    "/DMyAppVersion=$version",
    "/DSourceExe=$publishedExecutable",
    "/DOutputDir=$installerDirectory",
    "/DRepoRoot=$repositoryRoot",
    $installerScript
)
Invoke-CheckedCommand $iscc @isccArguments

if (-not (Test-Path -LiteralPath $installerExecutable -PathType Leaf)) {
    throw "Installer build completed without the expected file: $installerExecutable"
}

$portableHash = (Get-FileHash -LiteralPath $portableExecutable -Algorithm SHA256).Hash.ToLowerInvariant()
$installerHash = (Get-FileHash -LiteralPath $installerExecutable -Algorithm SHA256).Hash.ToLowerInvariant()

$manifest = [ordered]@{
    schemaVersion = 1
    version = $version
    channel = 'stable'
    releaseTag = "v$version"
    releasePage = "https://github.com/wilderruiz/ZomniverseGitPet/releases/tag/v$version"
    installer = [ordered]@{
        fileName = $installerFileName
        sha256 = $installerHash
        downloadUrl = "https://github.com/wilderruiz/ZomniverseGitPet/releases/download/v$version/$installerFileName"
    }
    portable = [ordered]@{
        fileName = $portableFileName
        sha256 = $portableHash
        downloadUrl = "https://github.com/wilderruiz/ZomniverseGitPet/releases/download/v$version/$portableFileName"
    }
}

$manifestPath = Join-Path $releaseRoot 'release-manifest.json'
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding UTF8

$checksumsPath = Join-Path $releaseRoot 'SHA256SUMS.txt'
@(
    "$installerHash  $installerFileName",
    "$portableHash  $portableFileName"
) | Set-Content -LiteralPath $checksumsPath -Encoding ASCII

Write-Host ''
Write-Host 'Release package is ready:'
Write-Host "  Installer : $installerExecutable"
Write-Host "  Portable  : $portableExecutable"
Write-Host "  Manifest  : $manifestPath"
Write-Host "  Checksums : $checksumsPath"
Write-Host ''
Write-Host 'The installer is self-contained; end users do not need the .NET SDK or runtime.'
