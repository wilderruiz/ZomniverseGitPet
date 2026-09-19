using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace ZomniverseGitPet;

public sealed class GitService(AuditLog audit)
{
    private readonly SemaphoreSlim _gitGate = new(1, 1);
    private readonly HashSet<string> _longPathReadyRepositories = new(StringComparer.OrdinalIgnoreCase);
    internal int GitProcessLaunchCount { get; private set; }
    private readonly List<string> _launchedGitSubcommands = [];
    internal IReadOnlyList<string> LaunchedGitSubcommands => _launchedGitSubcommands;

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
            var started = Stopwatch.StartNew();
            GitProcessLaunchCount++;
            _launchedGitSubcommands.Add(GetGitSubcommand(args));
            var result = await RunProcessAsync("git.exe", args, repositoryPath,
                timeout ?? TimeSpan.FromSeconds(30), cancellationToken);
            await audit.WriteAsync("git_operation", new
            {
                operation = GetGitSubcommand(args),
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

    private async Task<CommandResult> RunGitWithInputAsync(
        string repositoryPath,
        IEnumerable<string> arguments,
        string standardInput,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var args = arguments.ToArray();
        await _gitGate.WaitAsync(cancellationToken);
        try
        {
            var started = Stopwatch.StartNew();
            GitProcessLaunchCount++;
            _launchedGitSubcommands.Add(GetGitSubcommand(args));
            var result = await RunProcessAsync("git.exe", args, repositoryPath, timeout,
                cancellationToken, standardInput);
            await audit.WriteAsync("git_operation", new
            {
                operation = GetGitSubcommand(args),
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

    private static string GetGitSubcommand(IReadOnlyList<string> arguments)
    {
        for (var index = 0; index < arguments.Count; index++)
        {
            if (arguments[index] == "-c") { index++; continue; }
            if (!arguments[index].StartsWith('-')) return arguments[index];
        }
        return arguments.FirstOrDefault() ?? "unknown";
    }

    public Task<RepositoryLongPathResult> EnsureRepositoryLongPathsAsync(
        string path,
        IProgress<GuardianActivityEvent>? progress = null,
        CancellationToken token = default) =>
        EnsureRepositoryLongPathsCoreAsync(path, OperatingSystem.IsWindows(), progress, token,
            arguments => RunGitAsync(path, arguments, TimeSpan.FromSeconds(20), token));

    internal async Task<RepositoryLongPathResult> EnsureRepositoryLongPathsCoreAsync(
        string path,
        bool isWindows,
        IProgress<GuardianActivityEvent>? progress,
        CancellationToken token,
        Func<IReadOnlyList<string>, Task<CommandResult>> runGit,
        bool forceRecheck = false)
    {
        if (!isWindows) return new(false, false, false);
        var repositoryKey = Path.GetFullPath(path);
        if (!forceRecheck && _longPathReadyRepositories.Contains(repositoryKey))
            return new(true, true, false);

        token.ThrowIfCancellationRequested();
        var started = Stopwatch.StartNew();
        progress?.Report(new(GuardianActivityKind.LongPathChecking,
            "Checking repository path support..."));
        var current = await runGit(["config", "--local", "--get", "core.longpaths"]);
        if (current.Success && string.Equals(current.Output.Trim(), "true", StringComparison.OrdinalIgnoreCase))
        {
            _longPathReadyRepositories.Add(repositoryKey);
            progress?.Report(new(GuardianActivityKind.LongPathAlreadyEnabled,
                "Git long-path support is enabled for this repository."));
            await audit.WriteAsync("repository_longpaths_checked", new
            {
                repository = Path.GetFileName(repositoryKey),
                enabled = true,
                elapsedMilliseconds = started.ElapsedMilliseconds
            });
            return new(true, true, false);
        }

        progress?.Report(new(GuardianActivityKind.LongPathEnabling,
            "Enabling Git long-path support for this repository..."));
        var configured = await runGit(["config", "--local", "core.longpaths", "true"]);
        if (configured.Success)
        {
            _longPathReadyRepositories.Add(repositoryKey);
            progress?.Report(new(GuardianActivityKind.LongPathAlreadyEnabled,
                "core.longpaths enabled locally."));
            await audit.WriteAsync("repository_longpaths_enabled", new
            {
                repository = Path.GetFileName(repositoryKey),
                elapsedMilliseconds = started.ElapsedMilliseconds
            });
            return new(true, true, true);
        }

        var error = "GitPet could not enable repository-local long-path support. " +
                    "Long paths may still prevent Save.\r\n" + configured.Output;
        progress?.Report(new(GuardianActivityKind.LongPathConfigurationFailed, error));
        await audit.WriteAsync("repository_longpaths_enable_failed", new
        {
            repository = Path.GetFileName(repositoryKey),
            elapsedMilliseconds = started.ElapsedMilliseconds,
            error = configured.Output
        });
        return new(true, false, false, error);
    }

    internal static bool IsLongPathFailure(CommandResult result) =>
        !result.Success && result.Output.Contains("filename too long", StringComparison.OrdinalIgnoreCase);

    public async Task<RepositoryStatus> GetStatusAsync(string repositoryPath, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
            return RepositoryStatus.Failure("Choose a Git repository to begin.");

        var arguments = new List<string>
        {
            "status", "--porcelain=v2", "--branch", "--untracked-files=all"
        };
        var pathspecs = LogicalProjectScopeRuntime.GetPathspecs(repositoryPath, includeRootGitIgnore: true);
        if (pathspecs.Count > 0)
        {
            arguments.Add("--");
            arguments.AddRange(pathspecs);
        }

        var result = await RunGitAsync(repositoryPath, arguments, TimeSpan.FromSeconds(20), token);
        if (!result.Success)
            return RepositoryStatus.Failure(result.Output.Length == 0 ? "Git status failed." : result.Output);

        return ParsePorcelainV2(result.Output);
    }

    public Task<CommandResult> ValidateRepositoryStateAsync(
        string repositoryPath,
        CancellationToken token = default) =>
        RunGitAsync(
            repositoryPath,
            ["status", "--porcelain=v2", "--branch", "--untracked-files=no"],
            TimeSpan.FromSeconds(20),
            token);

    internal static string DescribeRepositoryReadFailure(string? output)
    {
        var details = string.IsNullOrWhiteSpace(output)
            ? "Git could not read the repository state."
            : output.Trim();

        if (!details.Contains("bad object HEAD", StringComparison.OrdinalIgnoreCase))
            return details;

        return "Git cannot read this project's HEAD. HEAD refers to a Git object that is missing or unreadable " +
               "in this repository. GitPet will not activate or modify the project while that metadata is incomplete.\r\n\r\n" +
               "Restore the repository's .git object data from a known-good copy, or re-clone/recreate the repository, " +
               "then try opening the project again.\r\n\r\nGit reported:\r\n" + details;
    }

    public Task<CommandResult> GetGitVersionAsync(string path, CancellationToken token = default) =>
        RunGitAsync(path, ["--version"], cancellationToken: token);

    public async Task<CommandResult> GetRepositoryRootAsync(string path, CancellationToken token = default)
    {
        var result = await RunGitAsync(path, ["rev-parse", "--show-toplevel"], cancellationToken: token);
        if (result.Success)
        {
            await EnsureRepositoryLongPathsAsync(path, token: token);
            return result;
        }
        if (!IsDubiousOwnershipError(result.Output)) return result;

        using var trust = new SafeDirectorySetupForm(path, result.Output);
        if (trust.ShowDialog() != DialogResult.OK)
            return new(result.ExitCode,
                "Git's ownership safety check was not approved. No trust setting was changed.\r\n\r\n" + result.Output,
                result.TimedOut);

        var configured = await AddSafeDirectoryAsync(path, token);
        if (!configured.Success) return configured;

        var verified = await RunGitAsync(path, ["rev-parse", "--show-toplevel"], cancellationToken: token);
        if (verified.Success) await EnsureRepositoryLongPathsAsync(path, token: token);
        return verified;
    }

    public Task<CommandResult> InitializeRepositoryAsync(string path, CancellationToken token = default) =>
        RunGitAsync(path, ["init", "-b", "main"], TimeSpan.FromMinutes(1), token);

    internal static bool IsDubiousOwnershipError(string output) =>
        !string.IsNullOrWhiteSpace(output) &&
        output.Contains("detected dubious ownership in repository", StringComparison.OrdinalIgnoreCase) &&
        output.Contains("safe.directory", StringComparison.OrdinalIgnoreCase);

    private async Task<CommandResult> AddSafeDirectoryAsync(string path, CancellationToken token)
    {
        string normalized;
        try
        {
            normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)).Replace('\\', '/');
        }
        catch (Exception ex)
        {
            return new(-1, "GitPet could not normalize the selected project path.\r\n\r\n" + ex.Message);
        }

        var configWorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(configWorkingDirectory) || !Directory.Exists(configWorkingDirectory))
            configWorkingDirectory = path;

        var existing = await RunGitAsync(configWorkingDirectory,
            ["config", "--global", "--get-all", "safe.directory"], cancellationToken: token);
        if (!string.IsNullOrWhiteSpace(existing.Output))
        {
            var alreadyTrusted = existing.Output
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => value.Replace('\\', '/').TrimEnd('/'))
                .Any(value => value.Equals(normalized.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));
            if (alreadyTrusted)
                return new(0, "This exact project folder is already in your Git safe.directory list.");
        }

        var result = await RunGitAsync(configWorkingDirectory,
            ["config", "--global", "--add", "safe.directory", normalized], cancellationToken: token);
        await audit.WriteAsync("safe_directory_configured", new
        {
            scope = "exact_project",
            success = result.Success,
            result.ExitCode,
            result.TimedOut
        });

        return result.Success
            ? new(0, "This exact project folder is now trusted by Git for the current Windows user.")
            : new(result.ExitCode,
                "GitPet could not add the selected project to your personal Git safe.directory list.\r\n\r\n" + result.Output,
                result.TimedOut);
    }

    public Task<CommandResult> GetLastCommitAsync(string path, CancellationToken token = default) =>
        RunGitAsync(path, ["log", "-1", "--format=%H%x09%h%x09%ad%x09%s", "--date=iso-strict"], cancellationToken: token);

    // Interactive, read-only review must not queue behind background fetch/status work.
    // Pin both content and diff to the commit returned by this query.
    internal Task<CommandResult> GetReviewCommitAsync(string path, CancellationToken token) =>
        RunProcessAsync("git.exe",
            ["log", "-1", "--format=%H%x09%h%x09%ad%x09%s", "--date=iso-strict"],
            path, TimeSpan.FromSeconds(20), token);

    internal Task<CommandResult> GetReviewContentAsync(string path, string file, string commit, CancellationToken token) =>
        RunProcessAsync("git.exe", ["show", $"{commit}:{NormalizeGitRelativePath(file)}"],
            path, TimeSpan.FromSeconds(20), token);

    internal Task<CommandResult> GetReviewDiffAsync(string path, string file, string commit, CancellationToken token) =>
        RunProcessAsync("git.exe",
            ["diff", "--no-ext-diff", "--no-textconv", "--unified=0", commit, "--", NormalizeGitRelativePath(file)],
            path, TimeSpan.FromSeconds(20), token);

    public Task<CommandResult> GetRecentCommitsAsync(string path, CancellationToken token = default) =>
        RunGitAsync(path, ["log", "-12", "--date=short", "--pretty=format:%h  %ad  %s"], cancellationToken: token);

    public Task<CommandResult> GetDiffAsync(string path, string file, CancellationToken token = default) =>
        RunGitAsync(path, ["diff", "--no-ext-diff", "--", file], cancellationToken: token);

    public Task<CommandResult> HasHeadCommitAsync(string path, CancellationToken token = default) =>
        RunGitAsync(path, ["rev-parse", "--verify", "HEAD"], cancellationToken: token);

    public Task<CommandResult> GetFileAtHeadAsync(string path, string file, CancellationToken token = default)
    {
        var normalized = NormalizeGitRelativePath(file);
        if (string.IsNullOrWhiteSpace(normalized))
            return Task.FromResult(new CommandResult(-1, "The selected file path is empty."));

        return RunGitAsync(path, ["show", $"HEAD:{normalized}"], TimeSpan.FromSeconds(30), token);
    }

    public Task<CommandResult> GetDiffAgainstHeadAsync(string path, string file, CancellationToken token = default)
    {
        var normalized = NormalizeGitRelativePath(file);
        if (string.IsNullOrWhiteSpace(normalized))
            return Task.FromResult(new CommandResult(-1, "The selected file path is empty."));

        return RunGitAsync(path,
            ["diff", "--no-ext-diff", "--unified=0", "HEAD", "--", normalized],
            TimeSpan.FromSeconds(30), token);
    }

    public Task<CommandResult> HealthCheckAsync(string path, CancellationToken token = default) =>
        RunGitAsync(path, ["fsck", "--no-progress"], TimeSpan.FromMinutes(3), token);

    public Task<CommandResult> GetCurrentBranchAsync(string path, CancellationToken token = default) =>
        RunGitAsync(path, ["branch", "--show-current"], cancellationToken: token);

    public async Task<CommandResult> GetOriginUrlAsync(string path, CancellationToken token = default)
    {
        var existing = await RunGitAsync(path, ["remote", "get-url", "origin"], cancellationToken: token);
        if (existing.Success && !string.IsNullOrWhiteSpace(existing.Output)) return existing;

        var normalized = Path.TrimEndingDirectorySeparator(path);
        var projectName = Path.GetFileName(normalized);
        if (string.IsNullOrWhiteSpace(projectName)) projectName = normalized;

        using var setup = new RemoteSetupForm(projectName);
        if (setup.ShowDialog() != DialogResult.OK)
            return new(-1, "Remote setup cancelled. No remote was created or changed.");

        var added = await AddOriginRemoteAsync(path, setup.RemoteUrl, token);
        if (!added.Success) return added;

        return await RunGitAsync(path, ["remote", "get-url", "origin"], cancellationToken: token);
    }

    public async Task<CommandResult> AddOriginRemoteAsync(string path, string remoteUrl, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(remoteUrl) || remoteUrl.Contains('\r') || remoteUrl.Contains('\n'))
            return new(-1, "A valid remote URL is required.");

        var existing = await RunGitAsync(path, ["remote", "get-url", "origin"], cancellationToken: token);
        if (existing.Success && !string.IsNullOrWhiteSpace(existing.Output))
            return new(-1, "An 'origin' remote already exists. GitPet will not replace or change it automatically.");

        var result = await RunGitAsync(path, ["remote", "add", "origin", remoteUrl], cancellationToken: token);
        await audit.WriteAsync("remote_configured", new
        {
            remote = "origin",
            success = result.Success,
            result.ExitCode,
            result.TimedOut
        });

        return result.Success
            ? new(0, "Remote 'origin' connected.")
            : new(result.ExitCode, "GitPet could not connect the 'origin' remote.\r\n\r\n" + result.Output, result.TimedOut);
    }

    public Task<CommandResult> GetUserNameAsync(string path, CancellationToken token = default) =>
        RunGitAsync(path, ["config", "--get", "user.name"], cancellationToken: token);

    public Task<CommandResult> GetUserEmailAsync(string path, CancellationToken token = default) =>
        RunGitAsync(path, ["config", "--get", "user.email"], cancellationToken: token);

    public async Task<CommandResult> SetUserIdentityAsync(
        string path,
        string name,
        string email,
        bool global,
        CancellationToken token = default)
    {
        var scope = global ? "--global" : "--local";
        var setName = await RunGitAsync(path, ["config", scope, "user.name", name], cancellationToken: token);
        if (!setName.Success)
        {
            await audit.WriteAsync("git_identity_configured", new { scope = global ? "global" : "repository", success = false });
            return new(setName.ExitCode, "Git could not save the author name.\r\n\r\n" + setName.Output, setName.TimedOut);
        }

        var setEmail = await RunGitAsync(path, ["config", scope, "user.email", email], cancellationToken: token);
        await audit.WriteAsync("git_identity_configured", new
        {
            scope = global ? "global" : "repository",
            success = setEmail.Success
        });
        if (!setEmail.Success)
            return new(setEmail.ExitCode, "Git saved the author name but could not save the email.\r\n\r\n" + setEmail.Output, setEmail.TimedOut);

        return new(0, "Git identity saved.");
    }

    public async Task<CommandResult> PushToOriginAsync(string path, string branch, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(branch))
            return new(-1, "Cannot push because the current branch could not be determined.");

        var result = await RunGitAsync(path, ["push", "origin", branch], TimeSpan.FromMinutes(5), token);
        await audit.WriteAsync("manual_push", new
        {
            remote = "origin",
            branch,
            success = result.Success,
            result.ExitCode,
            result.TimedOut
        });
        return result;
    }

    public async Task<CommandResult> PullFromOriginAsync(string path, string branch, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(branch))
            return new(-1, "Cannot pull because the current branch could not be determined.");

        var result = await RunGitAsync(path, ["pull", "--ff-only", "origin", branch], TimeSpan.FromMinutes(5), token);
        await audit.WriteAsync("manual_pull", new
        {
            remote = "origin",
            branch,
            fastForwardOnly = true,
            success = result.Success,
            result.ExitCode,
            result.TimedOut
        });
        return result;
    }

    public async Task<CheckpointResult> CreateCheckpointAsync(string path, string message, CancellationToken token = default)
        => await CreateCheckpointAsync(path, message, stagePlan: null, token, progress: null);

    /* ==========================================================================
       PATCH: EXACT SAVE STAGING PLAN
       DATE: 2026-09-11

       Stage approved files without force-adding selected directories.
       ========================================================================== */
    public async Task<CheckpointResult> CreateCheckpointAsync(
        string path,
        string message,
        SaveStagePlan? stagePlan,
        CancellationToken token = default,
        IProgress<GuardianActivityEvent>? progress = null)
    {
        if (stagePlan is null)
        {
            var preflight = await GetSavePreflightAsync(path, token);
            if (!preflight.Success) return new(false, preflight.Error);
            if (preflight.IgnoredChangedFiles.Count > 0)
            {
                return new(false,
                    "Ignored project files require explicit approval in the manual Save window. " +
                    "Nothing was staged.");
            }
        }

        var pathspecs = LogicalProjectScopeRuntime.GetPathspecs(path, includeRootGitIgnore: true);
        if (pathspecs.Count > 0)
        {
            var staged = await RunGitAsync(path, ["diff", "--cached", "--name-only"], TimeSpan.FromSeconds(20), token);
            if (!staged.Success)
                return new(false, "GitPet could not verify the staged-file boundary before saving: " + staged.Output);

            var outside = staged.Output
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(file => !LogicalProjectScopeRuntime.ContainsPath(path, file))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (outside.Length > 0)
            {
                return new(false,
                    "Git already has staged changes outside this GitPet project's scope. GitPet stopped before creating a commit.\r\n\r\n" +
                    string.Join("\r\n", outside.Take(12)) +
                    (outside.Length > 12 ? $"\r\n… and {outside.Length - 12} more" : "") +
                    "\r\n\r\nSave or unstage those changes from their own project first.");
            }
        }

        var diagnosticPaths = stagePlan is null
            ? pathspecs
            : stagePlan.NormalFiles.Concat(stagePlan.ApprovedIgnoredFiles).ToArray();
        foreach (var relativePath in diagnosticPaths.Where(candidate =>
                     Path.GetFullPath(Path.Combine(path, candidate.Replace('/', Path.DirectorySeparatorChar))).Length >= 260))
            progress?.Report(new(GuardianActivityKind.Warning, "Long path detected", relativePath));

        await EnsureRepositoryLongPathsAsync(path, progress, token);
        progress?.Report(new(GuardianActivityKind.SaveStaging, "Staging files..."));

        if (stagePlan is null)
        {
            var stageArguments = new List<string> { "add", "-A" };
            if (pathspecs.Count > 0)
            {
                stageArguments.Add("--");
                stageArguments.AddRange(pathspecs);
            }

            var stage = await RunStageWithLongPathRecoveryAsync(path, stageArguments, progress, token);
            await audit.WriteAsync("git_stage", new
            {
                success = stage.Success,
                projectScoped = pathspecs.Count > 0,
                pathspecCount = pathspecs.Count
            });
            if (!stage.Success) return new(false, "Staging failed: " + stage.Output);
        }
        else
        {
            if (stagePlan.NormalFiles.Count == 0 && stagePlan.ApprovedIgnoredFiles.Count == 0)
            {
                var skipped = stagePlan.SkippedIgnoredFiles.Count;
                return new(true,
                    $"✓ 0 files saved\r\n○ {skipped} ignored project file{(skipped == 1 ? "" : "s")} skipped",
                    SkippedIgnoredCount: skipped);
            }

            var normalArguments = IgnoredFileSavePolicy.BuildStageArguments(stagePlan.NormalFiles, force: false);
            if (normalArguments.Count > 0)
            {
                var normalStage = await RunStageWithLongPathRecoveryAsync(path, normalArguments, progress, token);
                if (!normalStage.Success) return new(false, "Staging failed: " + normalStage.Output);
            }

            var forcedArguments = IgnoredFileSavePolicy.BuildStageArguments(stagePlan.ApprovedIgnoredFiles, force: true);
            if (forcedArguments.Count > 0)
            {
                var forcedStage = await RunStageWithLongPathRecoveryAsync(path, forcedArguments, progress, token);
                if (!forcedStage.Success) return new(false, "Force-track staging failed: " + forcedStage.Output);
            }

            await audit.WriteAsync("git_stage", new
            {
                success = true,
                exactNormalFiles = stagePlan.NormalFiles.Count,
                exactForceTrackedFiles = stagePlan.ApprovedIgnoredFiles.Count,
                skippedIgnoredFiles = stagePlan.SkippedIgnoredFiles.Count
            });
        }

        progress?.Report(new(GuardianActivityKind.SaveCreatingCheckpoint, "Creating local checkpoint..."));
        var commit = await RunGitAsync(path, ["commit", "-m", message], TimeSpan.FromMinutes(2), token);
        if (!commit.Success)
        {
            await audit.WriteAsync("checkpoint_failed", new { output = commit.Output });
            return new(false, "Commit failed: " + commit.Output);
        }

        var hash = await RunGitAsync(path, ["rev-parse", "HEAD"], cancellationToken: token);
        var value = hash.Success ? hash.Output.Trim() : null;
        await audit.WriteAsync("checkpoint_created", new
        {
            commitHash = value,
            message,
            project = LogicalProjectScopeRuntime.DisplayName,
            projectScoped = pathspecs.Count > 0
        });
        var savedCount = stagePlan is null ? 0 : stagePlan.NormalFiles.Count + stagePlan.ApprovedIgnoredFiles.Count;
        var skippedCount = stagePlan?.SkippedIgnoredFiles.Count ?? 0;
        var summary = stagePlan is null
            ? $"Checkpoint created.\r\n\r\n{value}"
            : $"✓ {savedCount} file{(savedCount == 1 ? "" : "s")} saved\r\n" +
              (skippedCount > 0 ? $"○ {skippedCount} ignored project file{(skippedCount == 1 ? "" : "s")} skipped\r\n" : "") +
              $"\r\n{value}";
        return new(true, summary, value, savedCount, skippedCount);
    }

    internal async Task<CommandResult> RunStageWithLongPathRecoveryAsync(
        string path,
        IReadOnlyList<string> arguments,
        IProgress<GuardianActivityEvent>? progress,
        CancellationToken token,
        bool? isWindowsOverride = null,
        Func<Task<CommandResult>>? runStageOverride = null,
        Func<Task<RepositoryLongPathResult>>? ensureOverride = null)
    {
        async Task<CommandResult> Stage() => runStageOverride is null
            ? await RunGitAsync(path, arguments, TimeSpan.FromMinutes(1), token)
            : await runStageOverride();

        var first = await Stage();
        if (!(isWindowsOverride ?? OperatingSystem.IsWindows()) || !IsLongPathFailure(first)) return first;

        var started = Stopwatch.StartNew();
        progress?.Report(new(GuardianActivityKind.LongPathRetrying,
            "Staging encountered a Windows long-path error. Rechecking repository support..."));
        await audit.WriteAsync("staging_longpath_retry", new
        {
            repository = Path.GetFileName(Path.GetFullPath(path))
        });
        if (ensureOverride is null)
            await EnsureRepositoryLongPathsCoreAsync(path, true, progress, token,
                command => RunGitAsync(path, command, TimeSpan.FromSeconds(20), token), forceRecheck: true);
        else
            await ensureOverride();

        progress?.Report(new(GuardianActivityKind.LongPathRetrying, "Retrying staging once..."));
        var retry = await Stage();
        if (retry.Success)
        {
            progress?.Report(new(GuardianActivityKind.LongPathRetrySucceeded, "Staging completed."));
            await audit.WriteAsync("staging_longpath_retry_succeeded", new
            {
                repository = Path.GetFileName(Path.GetFullPath(path)),
                elapsedMilliseconds = started.ElapsedMilliseconds
            });
        }
        else
        {
            progress?.Report(new(GuardianActivityKind.LongPathRetryFailed,
                "Staging still failed after enabling long-path support.\r\n" + retry.Output));
            await audit.WriteAsync("staging_longpath_retry_failed", new
            {
                repository = Path.GetFileName(Path.GetFullPath(path)),
                elapsedMilliseconds = started.ElapsedMilliseconds,
                error = retry.Output
            });
        }
        return retry;
    }

    /* ==========================================================================
       PATCH: READ-ONLY IGNORE PREFLIGHT
       DATE: 2026-09-11

       Discover exact ignored files before changing Git's index.
       ========================================================================== */
    public async Task<SavePreflightResult> GetSavePreflightAsync(
        string path,
        CancellationToken token = default,
        IProgress<GuardianActivityEvent>? progress = null,
        RepositoryStatus? knownStatus = null)
    {
        var started = Stopwatch.StartNew();
        progress?.Report(new(GuardianActivityKind.PhaseStarted, "Reading project changes..."));
        var status = knownStatus ?? await GetStatusAsync(path, token);
        if (!status.Healthy) return new(false, [], [], status.Error);

        var normal = status.Files.Select(file => NormalizeGitRelativePath(file.Path))
            .Where(file => file.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var pathspecs = LogicalProjectScopeRuntime.GetPathspecs(path, includeRootGitIgnore: false);
        if (pathspecs.Count == 0) return new(true, normal, []);

        var arguments = new List<string> { "ls-files", "--others", "--ignored", "--exclude-standard", "-z" };
        if (pathspecs.Count > 0)
        {
            arguments.Add("--");
            arguments.AddRange(pathspecs);
        }

        var ignoredList = await RunGitAsync(path, arguments, TimeSpan.FromMinutes(1), token);
        if (!ignoredList.Success) return new(false, normal, [], "Ignored-file preflight failed: " + ignoredList.Output);

        var candidates = ignoredList.Output.Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeGitRelativePath)
            .Where(candidate => candidate.Length > 0 &&
                !Directory.Exists(Path.Combine(path, candidate.Replace('/', Path.DirectorySeparatorChar))))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        progress?.Report(new(GuardianActivityKind.PhaseStarted,
            $"Checking ignore rules for {candidates.Length} files...", Total: candidates.Length));
        foreach (var candidate in candidates)
            progress?.Report(new(GuardianActivityKind.FilePending, "Pending", candidate, Total: candidates.Length));

        IReadOnlyList<IgnoredProjectFile> ignored = [];
        if (candidates.Length > 0)
        {
            var input = string.Join('\0', candidates) + '\0';
            var provenance = await RunGitWithInputAsync(path,
                ["-c", "core.quotePath=false", "check-ignore", "-v", "-z", "--stdin"],
                input, TimeSpan.FromMinutes(1), token);
            if (provenance.ExitCode != 0)
            {
                var message = "Ignored-file provenance failed: " + provenance.Output;
                progress?.Report(new(GuardianActivityKind.Error, message));
                return new(false, normal, [], message);
            }
            if (!IgnoredFileSavePolicy.TryParseCheckIgnoreBatch(provenance.Output, out ignored, out var parseError))
            {
                progress?.Report(new(GuardianActivityKind.Error, parseError));
                return new(false, normal, [], parseError);
            }

            var completed = 0;
            foreach (var item in ignored)
            {
                completed++;
                progress?.Report(new(GuardianActivityKind.FileCompleted, "Ignored rule found",
                    item.Path, completed, candidates.Length));
            }
        }

        await audit.WriteAsync("save_preflight", new
        {
            success = true,
            normalFiles = normal.Length,
            ignoredCandidates = candidates.Length,
            ignoredFiles = ignored.Count,
            elapsedMilliseconds = started.ElapsedMilliseconds
        });

        return new(true, normal, ignored
            .GroupBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray());
    }

    public async Task<CommandResult> RunTestCommandAsync(string path, string command, CancellationToken token = default)
    {
        var workingDirectory = LogicalProjectScopeRuntime.GetWorkingDirectory(path);
        var result = await RunProcessAsync("cmd.exe", ["/d", "/s", "/c", command], workingDirectory,
            TimeSpan.FromMinutes(10), token);
        await audit.WriteAsync("test_run", new
        {
            command,
            workingDirectory,
            success = result.Success,
            result.ExitCode,
            result.TimedOut
        });
        return result;
    }

    internal static RepositoryStatus ParsePorcelainV2(string output)
    {
        var files = new List<ChangedFile>();
        var branch = "?";
        var ahead = 0;
        var behind = 0;
        var hasTrackingInformation = false;

        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith("# branch.head ", StringComparison.Ordinal))
                branch = line[14..].Trim();
            else if (line.StartsWith("# branch.ab ", StringComparison.Ordinal))
            {
                var fields = line[12..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (fields.Length >= 2 &&
                    fields[0].StartsWith('+') &&
                    fields[1].StartsWith('-') &&
                    int.TryParse(fields[0][1..], out var parsedAhead) &&
                    int.TryParse(fields[1][1..], out var parsedBehind))
                {
                    ahead = Math.Max(0, parsedAhead);
                    behind = Math.Max(0, parsedBehind);
                    hasTrackingInformation = true;
                }
            }
            else if (line.StartsWith("? ", StringComparison.Ordinal))
                files.Add(new("??", line[2..]));
            else if (line.StartsWith("1 ", StringComparison.Ordinal) || line.StartsWith("2 ", StringComparison.Ordinal))
            {
                var fields = line.Split(' ', line[0] == '1' ? 9 : 10);
                if (fields.Length >= 2)
                    files.Add(new(fields[1], fields[^1].Split('\t')[0]));
            }
            else if (line.StartsWith("u ", StringComparison.Ordinal))
            {
                var fields = line.Split(' ', 11);
                if (fields.Length >= 2) files.Add(new(fields[1], fields[^1]));
            }
        }

        return new(true, branch, files,
            Ahead: ahead,
            Behind: behind,
            HasTrackingInformation: hasTrackingInformation);
    }

    public static IReadOnlyList<string> FindSuspiciousPaths(IEnumerable<ChangedFile> files, IEnumerable<string> patterns) =>
        files.Select(file => file.Path.Replace('\\', '/'))
            .Where(path => patterns.Any(pattern => Regex.IsMatch(path, pattern, RegexOptions.IgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase).Order().ToArray();

    private static string NormalizeGitRelativePath(string file) =>
        (file ?? "").Replace('\\', '/').TrimStart('/');

    private static async Task<CommandResult> RunProcessAsync(
        string fileName, IEnumerable<string> arguments, string workingDirectory,
        TimeSpan timeout, CancellationToken cancellationToken, string? standardInput = null)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = standardInput is not null,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                CreateNoWindow = true
            }
        };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);

        try
        {
            process.Start();
            var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
            var stdin = standardInput is null
                ? Task.CompletedTask
                : WriteStandardInputAsync(process, standardInput, cancellationToken);
            using var timeoutSource = new CancellationTokenSource(timeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
            await process.WaitForExitAsync(linked.Token);
            await stdin;
            var output = JoinOutput(await stdout, await stderr);
            return new(process.ExitCode, output);
        }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(true); } catch { }
            if (cancellationToken.IsCancellationRequested) throw;
            return new(-1, $"Operation timed out after {timeout.TotalSeconds:0} seconds.", true);
        }
        catch (Exception ex)
        {
            return new(-1, ex.Message);
        }
    }

    private static async Task WriteStandardInputAsync(
        Process process, string input, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        await process.StandardInput.BaseStream.WriteAsync(bytes, cancellationToken);
        await process.StandardInput.BaseStream.FlushAsync(cancellationToken);
        process.StandardInput.Close();
    }

    private static string JoinOutput(string stdout, string stderr)
    {
        var builder = new StringBuilder(stdout.TrimEnd());
        if (!string.IsNullOrWhiteSpace(stderr))
        {
            if (builder.Length > 0) builder.AppendLine();
            builder.Append(stderr.TrimEnd());
        }
        return builder.ToString();
    }
}
