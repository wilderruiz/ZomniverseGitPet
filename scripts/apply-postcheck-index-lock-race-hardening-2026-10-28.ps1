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
$marker = 'PATCH: POST-CHECK INDEX LOCK RACE HARDENING 2026-10-28'

function Test-ContainsOrdinal {
    param([string]$Text, [string]$Needle)
    return $Text.IndexOf($Needle, [System.StringComparison]::Ordinal) -ge 0
}

function Replace-ExactOnce {
    param(
        [string]$Text,
        [string]$Old,
        [string]$New,
        [string]$Label
    )

    $matches = [System.Text.RegularExpressions.Regex]::Matches(
        $Text,
        [System.Text.RegularExpressions.Regex]::Escape($Old)
    )
    if ($matches.Count -ne 1) {
        throw "Expected exactly one anchor for $Label; found $($matches.Count). No file was written."
    }

    return $Text.Replace($Old, $New)
}

if (Test-ContainsOrdinal $gitService $marker) {
    Write-Host 'PASS post-check index-lock race hardening already applied.'
    exit 0
}

if (-not (Test-ContainsOrdinal $gitService 'PATCH: GET INDEX LOCK HARDENING 2026-10-25')) {
    throw 'The Oct 25 Get index-lock hardening prerequisite is missing. No file was written.'
}

$helperAnchor = @'
    public async Task<CommandResult> RunGitAsync(
'@

$helperReplacement = @'
    // PATCH: POST-CHECK INDEX LOCK RACE HARDENING 2026-10-28
    internal static bool IsIndexLockCollision(CommandResult result)
    {
        if (result.Success || string.IsNullOrWhiteSpace(result.Output)) return false;

        return result.Output.Contains("index.lock", StringComparison.OrdinalIgnoreCase) &&
               (result.Output.Contains("Unable to create", StringComparison.OrdinalIgnoreCase) ||
                result.Output.Contains("File exists", StringComparison.OrdinalIgnoreCase) ||
                result.Output.Contains("another git process seems to be running", StringComparison.OrdinalIgnoreCase));
    }

    internal async Task<CommandResult> RetryIndexLockCollisionOnceAsync(
        string repositoryPath,
        string operation,
        CommandResult firstResult,
        Func<Task<CommandResult>> retry,
        CancellationToken token)
    {
        if (!OperatingSystem.IsWindows() || !IsIndexLockCollision(firstResult))
            return firstResult;

        var lockFailure = await RecoverStaleIndexLockBeforeMutationAsync(
            repositoryPath,
            operation,
            token);
        if (lockFailure is not null)
            return lockFailure;

        await audit.WriteAsync("git_index_lock_collision_retry", new
        {
            repository = Path.GetFileName(Path.GetFullPath(repositoryPath)),
            operation
        });

        var retried = await retry();
        await audit.WriteAsync(
            retried.Success
                ? "git_index_lock_collision_retry_succeeded"
                : "git_index_lock_collision_retry_failed",
            new
            {
                repository = Path.GetFileName(Path.GetFullPath(repositoryPath)),
                operation,
                retried.ExitCode,
                retried.TimedOut,
                error = retried.Success ? "" : retried.Output
            });
        return retried;
    }

    public async Task<CommandResult> RunGitAsync(
'@

$gitService = Replace-ExactOnce $gitService $helperAnchor $helperReplacement 'post-check index-lock helpers'

$runOld = @'
    public async Task<CommandResult> RunGitAsync(
        string repositoryPath,
        IEnumerable<string> arguments,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var args = arguments.ToArray();
        await _gitGate.WaitAsync(cancellationToken);
        try
        {
            var operation = GetGitSubcommand(args);
            if (operation.Equals("add", StringComparison.OrdinalIgnoreCase) ||
                operation.Equals("pull", StringComparison.OrdinalIgnoreCase))
            {
                var lockFailure = await RecoverStaleIndexLockBeforeMutationAsync(
                    repositoryPath,
                    operation,
                    cancellationToken);
                if (lockFailure is not null) return lockFailure;
            }

            var started = Stopwatch.StartNew();
            GitProcessLaunchCount++;
            _launchedGitSubcommands.Add(operation);
            var result = await RunProcessAsync("git.exe", args, repositoryPath,
                timeout ?? TimeSpan.FromSeconds(30), cancellationToken);
            await audit.WriteAsync("git_operation", new
            {
                operation,
                success = result.Success,
                result.ExitCode,
                result.TimedOut,
                elapsedMilliseconds = started.ElapsedMilliseconds
            });
            return result;
        }
        finally
        {
            _gitGate.Release();
        }
    }
'@

$runNew = @'
    public async Task<CommandResult> RunGitAsync(
        string repositoryPath,
        IEnumerable<string> arguments,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var args = arguments.ToArray();
        await _gitGate.WaitAsync(cancellationToken);
        try
        {
            var operation = GetGitSubcommand(args);
            var guardedIndexMutation =
                operation.Equals("add", StringComparison.OrdinalIgnoreCase) ||
                operation.Equals("pull", StringComparison.OrdinalIgnoreCase);
            if (guardedIndexMutation)
            {
                var lockFailure = await RecoverStaleIndexLockBeforeMutationAsync(
                    repositoryPath,
                    operation,
                    cancellationToken);
                if (lockFailure is not null) return lockFailure;
            }

            var started = Stopwatch.StartNew();
            var commandTimeout = timeout ?? TimeSpan.FromSeconds(30);

            async Task<CommandResult> ExecuteOnceAsync()
            {
                GitProcessLaunchCount++;
                _launchedGitSubcommands.Add(operation);
                return await RunProcessAsync(
                    "git.exe",
                    args,
                    repositoryPath,
                    commandTimeout,
                    cancellationToken);
            }

            var result = await ExecuteOnceAsync();
            if (guardedIndexMutation && IsIndexLockCollision(result))
            {
                result = await RetryIndexLockCollisionOnceAsync(
                    repositoryPath,
                    operation,
                    result,
                    ExecuteOnceAsync,
                    cancellationToken);
            }

            await audit.WriteAsync("git_operation", new
            {
                operation,
                success = result.Success,
                result.ExitCode,
                result.TimedOut,
                elapsedMilliseconds = started.ElapsedMilliseconds
            });
            return result;
        }
        finally
        {
            _gitGate.Release();
        }
    }
'@

$gitService = Replace-ExactOnce $gitService $runOld $runNew 'RunGitAsync post-check retry'

if (-not (Test-ContainsOrdinal $tests 'IndexLockCollisionClassification')) {
    $registrationOld = @'
        await GitPullRecoversStaleIndexLockAsync();
        await GitPullPreservesActiveIndexLockAsync();
        await RestartManagerFindsLockOwnerAsync();
'@
    $registrationNew = @'
        await GitPullRecoversStaleIndexLockAsync();
        await GitPullPreservesActiveIndexLockAsync();
        IndexLockCollisionClassification();
        await PostCheckStaleIndexLockRetriesOnceAsync();
        await PostCheckActiveIndexLockDoesNotRetryAsync();
        await PostCheckVanishedIndexLockRetriesOnceAsync();
        await PostCheckRetryFailureStopsAfterOneRetryAsync();
        await RestartManagerFindsLockOwnerAsync();
'@
    $tests = Replace-ExactOnce $tests $registrationOld $registrationNew 'post-check race regression registration'

    $methodsAnchor = @'
    private static async Task RestartManagerFindsLockOwnerAsync()
'@
    $methodsReplacement = @'
    private static void IndexLockCollisionClassification()
    {
        var collision = new CommandResult(
            128,
            "fatal: Unable to create 'C:/repo/.git/index.lock': File exists.");
        var unrelated = new CommandResult(
            1,
            "fatal: unable to access 'https://example.invalid/repo': network unavailable");
        var success = new CommandResult(0, "index.lock File exists");

        if (!GitService.IsIndexLockCollision(collision) ||
            GitService.IsIndexLockCollision(unrelated) ||
            GitService.IsIndexLockCollision(success))
        {
            throw new InvalidOperationException(
                "Post-check index-lock collision classification is not deterministic.");
        }
    }

    private static async Task PostCheckStaleIndexLockRetriesOnceAsync()
    {
        var root = CreateTempRoot("PostCheckStaleIndexLock");
        var service = CreateService(root);

        try
        {
            var init = await service.RunGitAsync(root, ["init", "-b", "main"]);
            if (!init.Success)
                throw new InvalidOperationException("Could not initialize post-check stale-lock fixture: " + init.Output);

            var lockPath = Path.Combine(root, ".git", "index.lock");
            File.WriteAllBytes(lockPath, []);
            var retries = 0;
            var first = new CommandResult(
                128,
                $"fatal: Unable to create '{lockPath}': File exists.");

            var result = await service.RetryIndexLockCollisionOnceAsync(
                root,
                "pull",
                first,
                () =>
                {
                    retries++;
                    return Task.FromResult(new CommandResult(0, "retry succeeded"));
                },
                CancellationToken.None);

            if (!result.Success || retries != 1 || File.Exists(lockPath))
                throw new InvalidOperationException(
                    "A stale post-check index.lock was not recovered and retried exactly once. " + result.Output);
        }
        finally
        {
            TryDelete(root);
        }
    }

    private static async Task PostCheckActiveIndexLockDoesNotRetryAsync()
    {
        var root = CreateTempRoot("PostCheckActiveIndexLock");
        var service = CreateService(root);

        try
        {
            var init = await service.RunGitAsync(root, ["init", "-b", "main"]);
            if (!init.Success)
                throw new InvalidOperationException("Could not initialize post-check active-lock fixture: " + init.Output);

            var lockPath = Path.Combine(root, ".git", "index.lock");
            File.WriteAllText(lockPath, "active");
            using var holder = new FileStream(
                lockPath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None);
            var retries = 0;
            var first = new CommandResult(
                128,
                $"fatal: Unable to create '{lockPath}': File exists.");

            var result = await service.RetryIndexLockCollisionOnceAsync(
                root,
                "pull",
                first,
                () =>
                {
                    retries++;
                    return Task.FromResult(new CommandResult(0, "must not run"));
                },
                CancellationToken.None);

            if (result.Success || retries != 0 || !File.Exists(lockPath) ||
                !result.Output.Contains("currently in use", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "An actively held post-check index.lock was deleted or retried. " + result.Output);
            }
        }
        finally
        {
            TryDelete(root);
        }
    }

    private static async Task PostCheckVanishedIndexLockRetriesOnceAsync()
    {
        var root = CreateTempRoot("PostCheckVanishedIndexLock");
        var service = CreateService(root);

        try
        {
            var init = await service.RunGitAsync(root, ["init", "-b", "main"]);
            if (!init.Success)
                throw new InvalidOperationException("Could not initialize vanished-lock fixture: " + init.Output);

            var retries = 0;
            var first = new CommandResult(
                128,
                "fatal: Unable to create '.git/index.lock': File exists.");
            var result = await service.RetryIndexLockCollisionOnceAsync(
                root,
                "add",
                first,
                () =>
                {
                    retries++;
                    return Task.FromResult(new CommandResult(0, "competing writer finished"));
                },
                CancellationToken.None);

            if (!result.Success || retries != 1)
                throw new InvalidOperationException(
                    "A vanished post-check index.lock did not permit exactly one retry. " + result.Output);
        }
        finally
        {
            TryDelete(root);
        }
    }

    private static async Task PostCheckRetryFailureStopsAfterOneRetryAsync()
    {
        var root = CreateTempRoot("PostCheckRetryStops");
        var service = CreateService(root);

        try
        {
            var init = await service.RunGitAsync(root, ["init", "-b", "main"]);
            if (!init.Success)
                throw new InvalidOperationException("Could not initialize retry-stop fixture: " + init.Output);

            var retries = 0;
            var first = new CommandResult(
                128,
                "fatal: Unable to create '.git/index.lock': File exists.");
            var result = await service.RetryIndexLockCollisionOnceAsync(
                root,
                "pull",
                first,
                () =>
                {
                    retries++;
                    return Task.FromResult(new CommandResult(
                        128,
                        "fatal: Unable to create '.git/index.lock': File exists."));
                },
                CancellationToken.None);

            if (result.Success || retries != 1 || !GitService.IsIndexLockCollision(result))
                throw new InvalidOperationException(
                    "Post-check index-lock retry did not stop after exactly one failed retry.");
        }
        finally
        {
            TryDelete(root);
        }
    }

    private static async Task RestartManagerFindsLockOwnerAsync()
'@
    $tests = Replace-ExactOnce $tests $methodsAnchor $methodsReplacement 'post-check race regression methods'
}

[System.IO.File]::WriteAllText(
    $gitServicePath,
    $gitService,
    [System.Text.UTF8Encoding]::new($false)
)
[System.IO.File]::WriteAllText(
    $testPath,
    $tests,
    [System.Text.UTF8Encoding]::new($false)
)

Write-Host 'PASS post-check index-lock race hardening APPLIED.'
Write-Host 'PASS add/pull still perform the existing pre-check.'
Write-Host 'PASS a Git-reported post-check index.lock collision is re-inspected safely.'
Write-Host 'PASS stale/vanished lock permits one retry; active lock permits zero retries.'
Write-Host 'PASS a failed retry stops immediately; no retry loop exists.'
Write-Host 'NEXT: run scripts/validate-postcheck-index-lock-race-hardening-2026-10-28.ps1, then the regression suite.'