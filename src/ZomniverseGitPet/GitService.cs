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

    public Task<CommandResult> GetLastCommitAsync(string path, CancellationToken token = default) =>
        RunGitAsync(path, ["log", "-1", "--format=%H%x09%h%x09%ad%x09%s", "--date=iso-strict"], cancellationToken: token);

    public Task<CommandResult> GetRecentCommitsAsync(string path, CancellationToken token = default) =>
        RunGitAsync(path, ["log", "-12", "--date=short", "--pretty=format:%h  %ad  %s"], cancellationToken: token);

    public Task<CommandResult> GetDiffAsync(string path, string file, CancellationToken token = default) =>
        RunGitAsync(path, ["diff", "--no-ext-diff", "--", file], cancellationToken: token);

    public Task<CommandResult> HealthCheckAsync(string path, CancellationToken token = default) =>
        RunGitAsync(path, ["fsck", "--no-progress"], TimeSpan.FromMinutes(3), token);

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
        return new(true, $"Restore point created.\r\n\r\n{value}", value);
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

