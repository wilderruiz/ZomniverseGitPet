$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$target = Join-Path $root 'src\ZomniverseGitPet\GitService.cs'

if (-not (Test-Path -LiteralPath $target)) {
    throw "Missing GitService.cs at $target"
}

$content = [System.IO.File]::ReadAllText($target)

function Replace-ExactOnce {
    param(
        [string]$Text,
        [string]$Old,
        [string]$New,
        [string]$Label
    )

    if ($Text.Contains($New, [System.StringComparison]::Ordinal)) {
        Write-Host "PASS already applied: $Label"
        return $Text
    }

    $matches = [System.Text.RegularExpressions.Regex]::Matches(
        $Text,
        [System.Text.RegularExpressions.Regex]::Escape($Old)
    )
    if ($matches.Count -ne 1) {
        throw "Expected exactly one anchor for $Label; found $($matches.Count). No file was written."
    }

    return $Text.Replace($Old, $New, [System.StringComparison]::Ordinal)
}

$helperAnchor = @'
    internal IReadOnlyList<string> LaunchedGitSubcommands => _launchedGitSubcommands;

    public async Task<CommandResult> RunGitAsync(
'@

$helperReplacement = @'
    internal IReadOnlyList<string> LaunchedGitSubcommands => _launchedGitSubcommands;

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
            // FileShare.Delete keeps read/write access exclusive while allowing this
            // process to delete the exact file through a second Windows handle. This
            // closes the check/delete race: another Git writer cannot acquire the
            // lock between our ownership proof and removal.
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

    private async Task<CommandResult?> RecoverStaleIndexLockBeforeStagingAsync(
        string repositoryPath,
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
                lockPath
            });
            return null;
        }

        await audit.WriteAsync("git_index_lock_active", new
        {
            repository = Path.GetFileName(Path.GetFullPath(repositoryPath)),
            lockPath,
            error = recovery.Error
        });
        return new(-1,
            "GitPet found an existing Git index lock and verified that it is currently in use. " +
            "Nothing was deleted.\r\n\r\n" +
            lockPath + "\r\n\r\n" +
            "Finish the other Git operation and try Save again.\r\n\r\n" +
            recovery.Error);
    }

    public async Task<CommandResult> RunGitAsync(
'@

$content = Replace-ExactOnce $content $helperAnchor $helperReplacement 'index-lock recovery helpers'

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
            if (operation.Equals("add", StringComparison.OrdinalIgnoreCase))
            {
                var lockFailure = await RecoverStaleIndexLockBeforeStagingAsync(
                    repositoryPath,
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

$content = Replace-ExactOnce $content $runAnchor $runReplacement 'central git-add guard'

[System.IO.File]::WriteAllText(
    $target,
    $content,
    [System.Text.UTF8Encoding]::new($false)
)

Write-Host 'PASS stale Git index-lock recovery APPLIED.'
Write-Host 'PASS every ZGitPet git add now checks the exact Git index.lock before staging.'
Write-Host 'PASS an exclusively openable stale lock is deleted while ownership is held.'
Write-Host 'PASS an actively held/permission-blocked lock is preserved and reported.'
Write-Host 'NEXT: run the regression suite before rebuilding DEV-ZGitPet.'
