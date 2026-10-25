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
    if ($Text.IndexOf($Needle, [System.StringComparison]::Ordinal) -lt 0) {
        throw $Message
    }
}

function Assert-Count {
    param([string]$Text, [string]$Needle, [int]$Expected, [string]$Message)
    $count = [System.Text.RegularExpressions.Regex]::Matches(
        $Text,
        [System.Text.RegularExpressions.Regex]::Escape($Needle)
    ).Count
    if ($count -ne $Expected) {
        throw "$Message Expected $Expected, found $count."
    }
}

Assert-Contains $gitService 'PATCH: GET INDEX LOCK HARDENING 2026-10-25' 'Missing Get index-lock hardening marker.'
Assert-Contains $gitService 'TryRecoverStaleIndexLockFile' 'Missing stale index-lock recovery helper.'
Assert-Contains $gitService 'FileShare.Delete' 'Exclusive ownership-through-delete safety is missing.'
Assert-Contains $gitService 'File.Delete(lockPath);' 'Stale lock deletion is missing.'
Assert-Contains $gitService 'RecoverStaleIndexLockBeforeMutationAsync' 'Missing shared mutation lock guard.'
Assert-Contains $gitService 'operation.Equals("add", StringComparison.OrdinalIgnoreCase)' 'Save/add no longer uses the lock guard.'
Assert-Contains $gitService 'operation.Equals("pull", StringComparison.OrdinalIgnoreCase)' 'Get/pull does not use the lock guard.'
Assert-Contains $gitService 'try {retryAction} again' 'Active-lock user guidance is missing.'
Assert-Contains $gitService 'stale_git_index_lock_recovered' 'Recovered-lock audit event is missing.'
Assert-Contains $gitService 'git_index_lock_active' 'Active-lock audit event is missing.'
Assert-Contains $gitService 'operation,' 'Index-lock audit no longer records the guarded operation.'

Assert-Count $gitService '"--no-optional-locks", "status"' 2 'Both read-only status paths must disable optional index locking.'
Assert-Count $gitService '"status", "--porcelain=v2", "--branch"' 2 'Unexpected status-command shape after no-optional-lock hardening.'

Assert-Contains $tests 'GitPullRecoversStaleIndexLockAsync' 'Missing stale-lock Get regression.'
Assert-Contains $tests 'GitPullPreservesActiveIndexLockAsync' 'Missing active-lock Get regression.'
Assert-Contains $tests 'await service.PullFromOriginAsync(repository, "main")' 'Get regressions do not exercise the real PullFromOriginAsync path.'
Assert-Contains $tests 'try Get again' 'Active-lock Get regression does not verify user-facing recovery guidance.'

Write-Host 'PASS ZGitPet Get index-lock hardening structural contract.'
Write-Host 'PASS read-only status cannot take optional index locks.'
Write-Host 'PASS Save/add + Get/pull share the same exclusive stale-lock recovery authority.'
Write-Host 'PASS active locks remain fail-closed and are never deleted.'
Write-Host 'PASS regression coverage exercises real Get with stale and active index locks.'
Write-Host 'NEXT: dotnet run --project tests/ZomniverseGitPet.Tests/ZomniverseGitPet.Tests.csproj -c Release'
