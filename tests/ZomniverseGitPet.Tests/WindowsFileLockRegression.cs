using System.Diagnostics;
using ZomniverseGitPet;

internal static class WindowsFileLockRegression
{
    public static async Task RunAsync()
    {
        if (!OperatingSystem.IsWindows()) return;

        StaleIndexLockIsRemoved();
        ActiveIndexLockIsPreserved();
        await GitAddRecoversStaleIndexLockAsync();
        await GitAddPreservesActiveIndexLockAsync();
        await GitPullRecoversStaleIndexLockAsync();
        await GitPullPreservesActiveIndexLockAsync();
        IndexLockCollisionClassification();
        await PostCheckStaleIndexLockRetriesOnceAsync();
        await PostCheckActiveIndexLockDoesNotRetryAsync();
        await PostCheckVanishedIndexLockRetriesOnceAsync();
        await PostCheckRetryFailureStopsAfterOneRetryAsync();
        await RestartManagerFindsLockOwnerAsync();

        Console.WriteLine("Windows file-lock + Git index-lock regressions passed.");
    }

    private static void StaleIndexLockIsRemoved()
    {
        var root = CreateTempRoot("StaleIndexLock");
        var lockPath = Path.Combine(root, "index.lock");
        File.WriteAllBytes(lockPath, []);

        try
        {
            var result = GitService.TryRecoverStaleIndexLockFile(lockPath);
            if (!result.Success || !result.Found || !result.Removed || File.Exists(lockPath))
                throw new InvalidOperationException("An unheld stale index.lock was not removed safely.");
        }
        finally
        {
            TryDelete(root);
        }
    }

    private static void ActiveIndexLockIsPreserved()
    {
        var root = CreateTempRoot("ActiveIndexLock");
        var lockPath = Path.Combine(root, "index.lock");
        File.WriteAllText(lockPath, "active");

        try
        {
            using var holder = new FileStream(
                lockPath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None);
            var result = GitService.TryRecoverStaleIndexLockFile(lockPath);
            if (result.Success || !result.Found || result.Removed || !File.Exists(lockPath))
                throw new InvalidOperationException("An actively held index.lock was deleted or treated as stale.");
        }
        finally
        {
            TryDelete(root);
        }
    }

    private static async Task GitAddRecoversStaleIndexLockAsync()
    {
        var root = CreateTempRoot("GitAddStaleIndexLock");
        var service = CreateService(root);

        try
        {
            var init = await service.RunGitAsync(root, ["init", "-b", "main"]);
            if (!init.Success)
                throw new InvalidOperationException("Could not initialize stale-index-lock fixture: " + init.Output);

            File.WriteAllText(Path.Combine(root, "fixture.txt"), "fixture");
            var lockPath = Path.Combine(root, ".git", "index.lock");
            File.WriteAllBytes(lockPath, []);

            var add = await service.RunGitAsync(root, ["add", "--", "fixture.txt"]);
            if (!add.Success || File.Exists(lockPath))
                throw new InvalidOperationException(
                    "git add did not recover an unheld stale index.lock before staging. " + add.Output);
        }
        finally
        {
            TryDelete(root);
        }
    }

    private static async Task GitAddPreservesActiveIndexLockAsync()
    {
        var root = CreateTempRoot("GitAddActiveIndexLock");
        var service = CreateService(root);

        try
        {
            var init = await service.RunGitAsync(root, ["init", "-b", "main"]);
            if (!init.Success)
                throw new InvalidOperationException("Could not initialize active-index-lock fixture: " + init.Output);

            File.WriteAllText(Path.Combine(root, "fixture.txt"), "fixture");
            var lockPath = Path.Combine(root, ".git", "index.lock");
            File.WriteAllText(lockPath, "active");

            using var holder = new FileStream(
                lockPath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None);
            var add = await service.RunGitAsync(root, ["add", "--", "fixture.txt"]);
            if (
                add.Success
                || !File.Exists(lockPath)
                || !add.Output.Contains("currently in use", StringComparison.OrdinalIgnoreCase)
                || !add.Output.Contains("Nothing was deleted", StringComparison.OrdinalIgnoreCase)
            )
            {
                throw new InvalidOperationException(
                    "git add did not preserve/report an actively held index.lock correctly. " + add.Output);
            }
        }
        finally
        {
            TryDelete(root);
        }
    }

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
    {
        var root = CreateTempRoot("LockTest");
        var path = Path.Combine(root, "locked.txt");
        File.WriteAllText(path, "locked");
        Process? holder = null;

        try
        {
            var escapedPath = path.Replace("'", "''", StringComparison.Ordinal);
            holder = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -NonInteractive -Command \"$s=[IO.File]::Open('" + escapedPath +
                            "',[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None); " +
                            "Write-Output READY; [Console]::Out.Flush(); Start-Sleep -Seconds 20\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }) ?? throw new InvalidOperationException("Could not start the lock-holder process.");

            var ready = await holder.StandardOutput.ReadLineAsync();
            if (!string.Equals(ready, "READY", StringComparison.Ordinal))
                throw new InvalidOperationException("The lock-holder process did not become ready.");

            var blockers = WindowsFileLockService.FindLockingProcesses(root, ["locked.txt"]);
            if (!blockers.Any(process => process.ProcessId == holder.Id))
                throw new InvalidOperationException("Restart Manager did not identify the process holding the file.");
        }
        finally
        {
            try
            {
                if (holder is { HasExited: false }) holder.Kill(entireProcessTree: true);
            }
            catch { }
            holder?.Dispose();
            TryDelete(root);
        }
    }

    private static string CreateTempRoot(string name)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "ZomniverseGitPet." + name,
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static GitService CreateService(string root) => new(new AuditLog(
        Path.Combine(root, "audit.jsonl")));

    private static void TryDelete(string root)
    {
        try { Directory.Delete(root, true); } catch { }
    }
}
