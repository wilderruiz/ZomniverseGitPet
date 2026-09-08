using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace ZomniverseGitPet;

public sealed class GitService(AuditLog audit)
{
    private readonly SemaphoreSlim _gitGate = new(1, 1);

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
            var result = await RunProcessAsync("git.exe", args, repositoryPath,
                timeout ?? TimeSpan.FromSeconds(30), cancellationToken);
            await audit.WriteAsync("git_operation", new
            {
                operation = args.FirstOrDefault() ?? "unknown",
                success = result.Success,
                result.ExitCode,
                result.TimedOut
            });
            return result;
        }
        finally
        {
            _gitGate.Release();
        }
    }

    public async Task<RepositoryStatus> GetStatusAsync(string repositoryPath, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
            return RepositoryStatus.Failure("Choose a Git repository to begin.");

        var result = await RunGitAsync(repositoryPath,
            ["status", "--porcelain=v2", "--branch", "--untracked-files=normal"],
            TimeSpan.FromSeconds(20), token);
        if (!result.Success)
            return RepositoryStatus.Failure(result.Output.Length == 0 ? "Git status failed." : result.Output);

        return ParsePorcelainV2(result.Output);
    }

    public Task<CommandResult> GetGitVersionAsync(string path, CancellationToken token = default) =>
        RunGitAsync(path, ["--version"], cancellationToken: token);

    public Task<CommandResult> GetRepositoryRootAsync(string path, CancellationToken token = default) =>
        RunGitAsync(path, ["rev-parse", "--show-toplevel"], cancellationToken: token);

    public Task<CommandResult> InitializeRepositoryAsync(string path, CancellationToken token = default) =>
        RunGitAsync(path, ["init", "-b", "main"], TimeSpan.FromMinutes(1), token);

    public Task<CommandResult> GetLastCommitAsync(string path, CancellationToken token = default) =>
        RunGitAsync(path, ["log", "-1", "--format=%H%x09%h%x09%ad%x09%s", "--date=iso-strict"], cancellationToken: token);

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
    {
        var stage = await RunGitAsync(path, ["add", "-A"], TimeSpan.FromMinutes(1), token);
        await audit.WriteAsync("git_stage", new { success = stage.Success });
        if (!stage.Success) return new(false, "Staging failed: " + stage.Output);

        var commit = await RunGitAsync(path, ["commit", "-m", message], TimeSpan.FromMinutes(2), token);
        if (!commit.Success)
        {
            await audit.WriteAsync("checkpoint_failed", new { output = commit.Output });
            return new(false, "Commit failed: " + commit.Output);
        }

        var hash = await RunGitAsync(path, ["rev-parse", "HEAD"], cancellationToken: token);
        var value = hash.Success ? hash.Output.Trim() : null;
        await audit.WriteAsync("checkpoint_created", new { commitHash = value, message });
        return new(true, $"Checkpoint created.\r\n\r\n{value}", value);
    }

    public async Task<CommandResult> RunTestCommandAsync(string path, string command, CancellationToken token = default)
    {
        var result = await RunProcessAsync("cmd.exe", ["/d", "/s", "/c", command], path,
            TimeSpan.FromMinutes(10), token);
        await audit.WriteAsync("test_run", new { command, success = result.Success, result.ExitCode, result.TimedOut });
        return result;
    }

    internal static RepositoryStatus ParsePorcelainV2(string output)
    {
        var files = new List<ChangedFile>();
        var branch = "?";
        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith("# branch.head ", StringComparison.Ordinal))
                branch = line[14..].Trim();
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
        return new(true, branch, files);
    }

    public static IReadOnlyList<string> FindSuspiciousPaths(IEnumerable<ChangedFile> files, IEnumerable<string> patterns) =>
        files.Select(file => file.Path.Replace('\\', '/'))
            .Where(path => patterns.Any(pattern => Regex.IsMatch(path, pattern, RegexOptions.IgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase).Order().ToArray();

    private static string NormalizeGitRelativePath(string file) =>
        (file ?? "").Replace('\\', '/').TrimStart('/');

    private static async Task<CommandResult> RunProcessAsync(
        string fileName, IEnumerable<string> arguments, string workingDirectory,
        TimeSpan timeout, CancellationToken cancellationToken)
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
                CreateNoWindow = true
            }
        };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);

        try
        {
            process.Start();
            var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
            using var timeoutSource = new CancellationTokenSource(timeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
            await process.WaitForExitAsync(linked.Token);
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
