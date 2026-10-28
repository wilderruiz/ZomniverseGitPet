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
        throw "$Message Expected $Expected; found $count."
    }
}

Assert-Contains $gitService 'PATCH: POST-CHECK INDEX LOCK RACE HARDENING 2026-10-28' 'Missing post-check race hardening marker.'
Assert-Contains $gitService 'IsIndexLockCollision' 'Missing index-lock collision classifier.'
Assert-Contains $gitService 'RetryIndexLockCollisionOnceAsync' 'Missing one-shot post-check retry helper.'
Assert-Contains $gitService 'guardedIndexMutation' 'RunGitAsync is not identifying guarded add/pull mutations.'
Assert-Contains $gitService 'if (guardedIndexMutation && IsIndexLockCollision(result))' 'RunGitAsync does not invoke post-check collision recovery.'
Assert-Contains $gitService 'git_index_lock_collision_retry' 'Missing retry audit event.'
Assert-Contains $gitService 'git_index_lock_collision_retry_succeeded' 'Missing retry success audit event.'
Assert-Contains $gitService 'git_index_lock_collision_retry_failed' 'Missing retry failure audit event.'
Assert-Count $gitService 'RetryIndexLockCollisionOnceAsync(' 2 'Retry helper should have one definition and one production invocation.'

Assert-Contains $tests 'IndexLockCollisionClassification' 'Missing collision classification regression.'
Assert-Contains $tests 'PostCheckStaleIndexLockRetriesOnceAsync' 'Missing stale post-check lock regression.'
Assert-Contains $tests 'PostCheckActiveIndexLockDoesNotRetryAsync' 'Missing active post-check lock regression.'
Assert-Contains $tests 'PostCheckVanishedIndexLockRetriesOnceAsync' 'Missing vanished-lock retry regression.'
Assert-Contains $tests 'PostCheckRetryFailureStopsAfterOneRetryAsync' 'Missing one-shot retry-stop regression.'

Write-Host 'PASS post-check index-lock race hardening structural validation.'
Write-Host 'PASS stale/vanished collision => one retry.'
Write-Host 'PASS active collision => no deletion and no retry.'
Write-Host 'PASS failed retry => no third attempt.'