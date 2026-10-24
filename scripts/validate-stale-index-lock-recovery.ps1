$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$gitServicePath = Join-Path $root 'src\ZomniverseGitPet\GitService.cs'
$testPath = Join-Path $root 'tests\ZomniverseGitPet.Tests\WindowsFileLockRegression.cs'

foreach ($path in @($gitServicePath, $testPath)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing required file: $path"
    }
}

$gitService = [System.IO.File]::ReadAllText($gitServicePath)
$tests = [System.IO.File]::ReadAllText($testPath)

function Assert-Contains {
    param([string]$Text, [string]$Needle, [string]$Message)
    if (-not $Text.Contains($Needle, [System.StringComparison]::Ordinal)) {
        throw $Message
    }
}

Assert-Contains $gitService 'TryRecoverStaleIndexLockFile' 'Missing stale index-lock recovery helper.'
Assert-Contains $gitService 'FileShare.Delete' 'Index-lock recovery no longer holds exclusive read/write ownership through deletion.'
Assert-Contains $gitService 'File.Delete(lockPath);' 'Stale index-lock removal is missing.'
Assert-Contains $gitService 'RecoverStaleIndexLockBeforeStagingAsync' 'Missing pre-staging index-lock guard.'
Assert-Contains $gitService 'operation.Equals("add", StringComparison.OrdinalIgnoreCase)' 'Central git add path no longer invokes the index-lock guard.'
Assert-Contains $gitService 'Nothing was deleted.' 'Active-lock safety diagnostic is missing.'
Assert-Contains $gitService 'stale_git_index_lock_recovered' 'Recovered-lock audit event is missing.'
Assert-Contains $gitService 'git_index_lock_active' 'Active-lock audit event is missing.'

Assert-Contains $tests 'StaleIndexLockIsRemoved' 'Missing stale-lock removal regression.'
Assert-Contains $tests 'ActiveIndexLockIsPreserved' 'Missing active-lock preservation regression.'
Assert-Contains $tests 'GitAddRecoversStaleIndexLockAsync' 'Missing git add stale-lock integration regression.'
Assert-Contains $tests 'GitAddPreservesActiveIndexLockAsync' 'Missing git add active-lock integration regression.'

Write-Host 'PASS stale Git index-lock recovery structural contract.'
Write-Host 'PASS every git add is guarded centrally before staging.'
Write-Host 'PASS stale locks are removable only after exclusive ownership proof.'
Write-Host 'PASS active locks are preserved and surfaced instead of deleted.'
Write-Host 'PASS regression coverage includes helper + real git add behavior.'
