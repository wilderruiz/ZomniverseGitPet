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
$marker = 'PATCH: GET INDEX LOCK HARDENING 2026-10-25'

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

    # String.Replace(old,new,StringComparison) is unavailable in Windows PowerShell 5.1.
    # Anchor count above already proves the exact case-sensitive target occurs once.
    return $Text.Replace($Old, $New)
}

if (Test-ContainsOrdinal $gitService $marker) {
    Write-Host 'PASS Get index-lock hardening already applied.'
    exit 0
}

# Read-only status must never refresh/write the Git index on Windows/WSL shared trees.
$gitService = Replace-ExactOnce $gitService @'
        var arguments = new List<string>
        {
            "status", "--porcelain=v2", "--branch", "--untracked-files=all"
        };
'@ @'
        var arguments = new List<string>
        {
            "--no-optional-locks", "status", "--porcelain=v2", "--branch", "--untracked-files=all"
        };
'@ 'GetStatus no-optional-locks'

$gitService = Replace-ExactOnce $gitService @'
            ["status", "--porcelain=v2", "--branch", "--untracked-files=no"],
'@ @'
            ["--no-optional-locks", "status", "--porcelain=v2", "--branch", "--untracked-files=no"],
'@ 'ValidateRepositoryState no-optional-locks'

if (-not (Test-ContainsOrdinal $gitService 'TryRecoverStaleIndexLockFile')) {
    $helperAnchor = @'
    internal IReadOnlyList<string> LaunchedGitSubcommands => _launchedGitSubcommands;

    public async Task<CommandResult> RunGitAsync(
'@

    $helperReplacement = @'
    internal IReadOnlyList<string> LaunchedGitSubcommands => _launchedGitSubcommands;

    // PATCH: GET INDEX LOCK HARDENING 2026-10-25
    internal sealed record IndexLockRecoveryResult(
        bool Success,
        bool Found,
        bool Removed,
        string Error = "");

    internal static IndexLockRecoveryResult TryRecoverStaleIndexLockFile(string lockPath)
    {
        if (string.IsNullOrWhiteSpace(lockPath))
            return new(false, false, false, "Git index lock path is empty.");
        if (!File.Exists(lockPath))
            return new(true, false, false);

        try
        {
            // FileShare.Delete permits this process to delete the exact file while
            // retaining exclusive read/write ownership. Another Git writer cannot
            // acquire the lock between ownership proof and removal.
            using var stream = new FileStream(
                lockPath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.Delete);
            File.Delete(lockPath);
            return new(true, true, true);
        }
        catch (FileNotFoundException)
        {
            return new(true, false, false);
        }
        catch (DirectoryNotFoundException)
        {
            return new(true, false, false);
        }
        catch (IOException ex)
        {
            return new(false, true, false, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return new(false, true, false, ex.Message);
        }
    }

    private async Task<CommandResult?> RecoverStaleIndexLockBeforeMutationAsync(
        string repositoryPath,
        string operation,
        CancellationToken token)
    {
        if (!OperatingSystem.IsWindows()) return null;

        var located = await RunProcessAsync(
            "git.exe",
            ["rev-parse", "--git-path", "index.lock"],
            repositoryPath,
            TimeSpan.FromSeconds(10),
            token);
        if (!located.Success || string.IsNullOrWhiteSpace(located.Output))
            return null;

        string lockPath;
        try
        {
            var reported = located.Output.Trim();
            lockPath = Path.IsPathRooted(reported)
                ? Path.GetFullPath(reported)
                : Path.GetFullPath(Path.Combine(
                    repositoryPath,
                    reported.Replace('/', Path.DirectorySeparatorChar)));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            await audit.WriteAsync("git_index_lock_path_invalid", new
            {
                repository = Path.GetFileName(Path.GetFullPath(repositoryPath)),
                operation,
                error = ex.Message
            });
            return new(-1,
                "GitPet could not safely resolve Git's index lock path. Nothing was deleted.\r\n\r\n" +
                ex.Message);
        }

        var recovery = TryRecoverStaleIndexLockFile(lockPath);
        if (!recovery.Found) return null;

        if (recovery.Success && recovery.Removed)
        {
            await audit.WriteAsync("stale_git_index_lock_recovered", new
            {
                repository = Path.GetFileName(Path.GetFullPath(repositoryPath)),
                operation,
                lockPath
            });
            return null;
        }

        await audit.WriteAsync("git_index_lock_active", new
        {
            repository = Path.GetFileName(Path.GetFullPath(repositoryPath)),
            operation,
            lockPath,
            error = recovery.Error
        });

        var retryAction = operation.Equals("pull", StringComparison.OrdinalIgnoreCase)
            ? "Get"
            : operation.Equals("add", StringComparison.OrdinalIgnoreCase)
                ? "Save"
                : "the Git operation";

        return new(-1,
            "GitPet found an existing Git index lock and verified that it is currently in use. " +
            "Nothing was deleted.\r\n\r\n" +
            lockPath + "\r\n\r\n" +
            $"Finish the other Git operation and try {retryAction} again.\r\n\r\n" +
            recovery.Error);
    }

    public async Task<CommandResult> RunGitAsync(
'@

    $gitService = Replace-ExactOnce $gitService $helperAnchor $helperReplacement 'index-lock recovery helpers'

    $runAnchor = @'
        var args = arguments.ToArray();
        await _gitGate.WaitAsync(cancellationToken);
        try
        {
            var started = Stopwatch.StartNew();
            GitProcessLaunchCount++;
            _launchedGitSubcommands.Add(GetGitSubcommand(args));
            var result = await RunProcessAsync("git.exe", args, repositoryPath,
                timeout ?? TimeSpan.FromSeconds(30), cancellationToken);
            await audit.WriteAsync("git_operation", new
            {
                operation = GetGitSubcommand(args),
                success = result.Success,
'@

    $runReplacement = @'
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
'@

    $gitService = Replace-ExactOnce $gitService $runAnchor $runReplacement 'central add/pull index-lock guard'
}
elseif (Test-ContainsOrdinal $gitService 'RecoverStaleIndexLockBeforeStagingAsync') {
    throw 'The older Save-only stale-index-lock patch is present. Apply scripts/apply-stale-index-lock-recovery.ps1 state must be reconciled before this hardening patch.'
}
else {
    throw 'An unknown index-lock recovery implementation is already present. No file was written.'
}

# Add real Get regressions to the existing Windows lock suite.
if (-not (Test-ContainsOrdinal $tests 'GitPullRecoversStaleIndexLockAsync')) {
    $runTestsAnchor = @'
        await GitAddRecoversStaleIndexLockAsync();
        await GitAddPreservesActiveIndexLockAsync();
        await RestartManagerFindsLockOwnerAsync();
'@
    $runTestsReplacement = @'
        await GitAddRecoversStaleIndexLockAsync();
        await GitAddPreservesActiveIndexLockAsync();
        await GitPullRecoversStaleIndexLockAsync();
        await GitPullPreservesActiveIndexLockAsync();
        await RestartManagerFindsLockOwnerAsync();
'@
    $tests = Replace-ExactOnce $tests $runTestsAnchor $runTestsReplacement 'Get index-lock regression registration'

    $methodAnchor = @'
    private static async Task RestartManagerFindsLockOwnerAsync()
'@
    $methodReplacement = @'
    private static async Task GitPullRecoversStaleIndexLockAsync()
    {
        var root = CreateTempRoot("GitPullStaleIndexLock");
        var repository = Path.Combine(root, "repo");
        Directory.CreateDirectory(repository);
        var service = CreateService(root);

        try
        {
            var init = await service.RunGitAsync(repository, ["init", "-b", "main"]);
            if (!init.Success)
                throw new InvalidOperationException("Could not initialize stale pull-lock fixture: " + init.Output);

            var remote = Path.Combine(root, "remote.git");
            var initRemote = await service.RunGitAsync(root, ["init", "--bare", remote]);
            if (!initRemote.Success)
                throw new InvalidOperationException("Could not initialize pull-lock remote fixture: " + initRemote.Output);

            await service.RunGitAsync(repository, ["config", "user.name", "GitPet Test"]);
            await service.RunGitAsync(repository, ["config", "user.email", "gitpet@example.invalid"]);
            File.WriteAllText(Path.Combine(repository, "fixture.txt"), "fixture");
            var add = await service.RunGitAsync(repository, ["add", "--", "fixture.txt"]);
            var commit = await service.RunGitAsync(repository, ["commit", "-m", "fixture"]);
            var remoteAdd = await service.RunGitAsync(repository, ["remote", "add", "origin", remote]);
            var push = await service.RunGitAsync(repository, ["push", "-u", "origin", "main"]);
            if (!add.Success || !commit.Success || !remoteAdd.Success || !push.Success)
                throw new InvalidOperationException("Could not prepare stale pull-lock fixture.");

            var lockPath = Path.Combine(repository, ".git", "index.lock");
            File.WriteAllBytes(lockPath, []);

            var pull = await service.PullFromOriginAsync(repository, "main");
            if (!pull.Success || File.Exists(lockPath))
                throw new InvalidOperationException(
                    "Get did not recover an unheld stale index.lock before pull. " + pull.Output);
        }
        finally
        {
            TryDelete(root);
        }
    }

    private static async Task GitPullPreservesActiveIndexLockAsync()
    {
        var root = CreateTempRoot("GitPullActiveIndexLock");
        var repository = Path.Combine(root, "repo");
        Directory.CreateDirectory(repository);
        var service = CreateService(root);

        try
        {
            var init = await service.RunGitAsync(repository, ["init", "-b", "main"]);
            if (!init.Success)
                throw new InvalidOperationException("Could not initialize active pull-lock fixture: " + init.Output);

            var lockPath = Path.Combine(repository, ".git", "index.lock");
            File.WriteAllText(lockPath, "active");
            using var holder = new FileStream(
                lockPath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None);

            var pull = await service.PullFromOriginAsync(repository, "main");
            if (
                pull.Success
                || !File.Exists(lockPath)
                || !pull.Output.Contains("currently in use", StringComparison.OrdinalIgnoreCase)
                || !pull.Output.Contains("Nothing was deleted", StringComparison.OrdinalIgnoreCase)
                || !pull.Output.Contains("try Get again", StringComparison.OrdinalIgnoreCase)
            )
            {
                throw new InvalidOperationException(
                    "Get did not preserve/report an actively held index.lock correctly. " + pull.Output);
            }
        }
        finally
        {
            TryDelete(root);
        }
    }

    private static async Task RestartManagerFindsLockOwnerAsync()
'@
    $tests = Replace-ExactOnce $tests $methodAnchor $methodReplacement 'Get index-lock regression methods'
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

Write-Host 'PASS ZGitPet Get index-lock hardening APPLIED.'
Write-Host 'PASS read-only status uses --no-optional-locks.'
Write-Host 'PASS Save/add and Get/pull share exclusive stale-lock recovery.'
Write-Host 'PASS actively held index.lock is preserved; only proven-stale locks are deleted.'
Write-Host 'PASS Windows regressions now cover stale + active Get locks.'
Write-Host 'NEXT: run scripts/validate-get-index-lock-hardening-2026-10-25.ps1, then the regression suite.'
