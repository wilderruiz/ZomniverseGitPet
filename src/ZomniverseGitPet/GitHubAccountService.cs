using System.Diagnostics;
using System.Text;

namespace ZomniverseGitPet;

internal sealed record GitHubAccountStatus(
    bool CliAvailable,
    bool Authenticated,
    string Login,
    string Message);

internal sealed record GitHubRepositoryCandidate(
    string NameWithOwner,
    string Url,
    DateTimeOffset? UpdatedAt,
    int MatchScore);

internal sealed record GitHubRepositoryCreationResult(
    bool Success,
    string RepositoryUrl,
    string Message);

internal sealed class GitHubAccountService
{
    private readonly AuditLog _audit;

    public GitHubAccountService(AuditLog audit)
    {
        _audit = audit;
    }

    public async Task<GitHubAccountStatus> GetStatusAsync(CancellationToken token = default)
    {
        var executable = FindGitHubCliExecutable();
        if (string.IsNullOrWhiteSpace(executable))
        {
            return new(false, false, string.Empty,
                "GitHub CLI is not installed yet. GitPet can install it for you.");
        }

        var version = await RunProcessAsync(
            executable,
            ["--version"],
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            TimeSpan.FromSeconds(15),
            token);
        if (!version.Success)
        {
            return new(false, false, string.Empty,
                "GitHub CLI could not be started. GitPet can reinstall it for you.");
        }

        var auth = await RunProcessAsync(
            executable,
            ["auth", "status", "--hostname", "github.com"],
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            TimeSpan.FromSeconds(20),
            token);
        if (!auth.Success)
        {
            return new(true, false, string.Empty,
                "GitHub CLI is installed, but no GitHub account is connected yet.");
        }

        var login = await RunProcessAsync(
            executable,
            ["api", "user", "--jq", ".login"],
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            TimeSpan.FromSeconds(20),
            token);
        var user = login.Success ? login.Output.Trim() : string.Empty;
        var message = string.IsNullOrWhiteSpace(user)
            ? "GitHub is connected ✓"
            : $"GitHub is connected as {user} ✓";

        return new(true, true, user, message);
    }

    /* ==========================================================================
       PATCH: GITHUB REPOSITORY DISCOVERY
       DATE.TIME: 2026-09-11 18:13 +03:00
       Find likely repository matches for the active GitPet project.
       ========================================================================== */
    public async Task<IReadOnlyList<GitHubRepositoryCandidate>> FindRepositoriesAsync(
        string login,
        IEnumerable<string> hints,
        CancellationToken token = default)
    {
        var executable = FindGitHubCliExecutable();
        if (string.IsNullOrWhiteSpace(executable)) return [];
        if (string.IsNullOrWhiteSpace(login)) return [];

        var result = await RunProcessAsync(
            executable,
            [
                "repo", "list", login.Trim(),
                "--limit", "200",
                "--json", "nameWithOwner,url,updatedAt",
                "--jq", ".[] | [.nameWithOwner, .url, .updatedAt] | @tsv"
            ],
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            TimeSpan.FromSeconds(45),
            token);

        if (!result.Success)
        {
            await _audit.WriteAsync("github_repository_discovery", new { success = false });
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(result.Output)
                    ? "GitHub did not return the repository list."
                    : result.Output);
        }

        var normalizedHints = hints
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(NormalizeRepositoryHint)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var candidates = new List<GitHubRepositoryCandidate>();
        foreach (var line in result.Output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split('\t');
            if (parts.Length < 2) continue;
            var nameWithOwner = parts[0].Trim();
            var url = parts[1].Trim();
            if (nameWithOwner.Length == 0 || url.Length == 0) continue;

            DateTimeOffset? updated = null;
            if (parts.Length > 2 && DateTimeOffset.TryParse(parts[2].Trim(), out var parsed)) updated = parsed;
            var repositoryName = nameWithOwner.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? nameWithOwner;
            var score = ScoreRepositoryCandidate(repositoryName, normalizedHints);
            candidates.Add(new(nameWithOwner, url, updated, score));
        }

        var ordered = candidates
            .OrderByDescending(item => item.MatchScore)
            .ThenByDescending(item => item.UpdatedAt ?? DateTimeOffset.MinValue)
            .Take(12)
            .ToArray();

        await _audit.WriteAsync("github_repository_discovery", new
        {
            success = true,
            candidates = ordered.Length
        });
        return ordered;
    }

    /* ==========================================================================
       PATCH: EMPTY GITHUB REPOSITORY CREATION
       DATE.TIME: 2026-09-11 18:13 +03:00
       Create an online home without pushing local work automatically.
       ========================================================================== */
    public async Task<GitHubRepositoryCreationResult> CreateRepositoryAsync(
        string owner,
        string repositoryName,
        bool isPrivate,
        string description,
        CancellationToken token = default)
    {
        var executable = FindGitHubCliExecutable();
        if (string.IsNullOrWhiteSpace(executable))
            return new(false, string.Empty, "GitHub CLI could not be located.");

        owner = owner.Trim();
        var name = SuggestRepositoryName(repositoryName);
        if (owner.Length == 0 || name.Length == 0)
            return new(false, string.Empty, "A GitHub owner and repository name are required.");

        var arguments = new List<string>
        {
            "repo", "create", $"{owner}/{name}", isPrivate ? "--private" : "--public"
        };
        if (!string.IsNullOrWhiteSpace(description))
        {
            arguments.Add("--description");
            arguments.Add(description.Trim());
        }

        var result = await RunProcessAsync(
            executable,
            arguments,
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            TimeSpan.FromMinutes(2),
            token);

        var url = $"https://github.com/{owner}/{name}.git";
        await _audit.WriteAsync("github_repository_created", new
        {
            owner,
            repository = name,
            visibility = isPrivate ? "private" : "public",
            success = result.Success
        });

        return result.Success
            ? new(true, url, "Repository created. Nothing has been pushed.")
            : new(false, string.Empty,
                string.IsNullOrWhiteSpace(result.Output)
                    ? "GitHub could not create the repository."
                    : result.Output);
    }

    internal static string SuggestRepositoryName(string? value)
    {
        var input = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (input.Length == 0) return string.Empty;

        var builder = new StringBuilder(input.Length);
        var pendingDash = false;
        foreach (var character in input)
        {
            if (char.IsLetterOrDigit(character) || character is '.' or '_')
            {
                if (pendingDash && builder.Length > 0 && builder[^1] != '-') builder.Append('-');
                builder.Append(character);
                pendingDash = false;
            }
            else if (character == '-')
            {
                if (builder.Length > 0 && builder[^1] != '-') builder.Append('-');
                pendingDash = false;
            }
            else
            {
                pendingDash = true;
            }
        }

        return builder.ToString().Trim('-', '.');
    }

    internal static int ScoreRepositoryCandidate(string repositoryName, IEnumerable<string> normalizedHints)
    {
        var candidate = NormalizeRepositoryHint(repositoryName);
        if (candidate.Length == 0) return 0;

        var score = 0;
        foreach (var hint in normalizedHints)
        {
            if (hint.Length == 0) continue;
            if (candidate.Equals(hint, StringComparison.OrdinalIgnoreCase))
                score = Math.Max(score, 100);
            else if (candidate.Contains(hint, StringComparison.OrdinalIgnoreCase) ||
                     hint.Contains(candidate, StringComparison.OrdinalIgnoreCase))
                score = Math.Max(score, 70);
            else
            {
                var common = candidate.Zip(hint).TakeWhile(pair => pair.First == pair.Second).Count();
                if (common >= Math.Min(4, Math.Min(candidate.Length, hint.Length)))
                    score = Math.Max(score, 30 + common);
            }
        }
        return score;
    }

    private static string NormalizeRepositoryHint(string value) =>
        new(value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    public bool LaunchInstall(IWin32Window? owner = null)
    {
        const string command =
            "winget install --id GitHub.cli -e; " +
            "Write-Host ''; Write-Host 'When installation finishes, close this window and return to GitPet.'";
        return LaunchPowerShell(command, owner, "GitHub setup");
    }

    public bool LaunchSignIn(IWin32Window? owner = null)
    {
        var executable = FindGitHubCliExecutable();
        if (string.IsNullOrWhiteSpace(executable))
        {
            MessageBox.Show(owner,
                "GitHub CLI could not be located yet. Install it first, then try again.",
                "GitHub setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        var quotedExecutable = executable.Replace("'", "''");
        var command =
            "& '" + quotedExecutable + "' auth login --hostname github.com --web --git-protocol https; " +
            "if ($LASTEXITCODE -eq 0) { & '" + quotedExecutable + "' auth setup-git --hostname github.com }; " +
            "Write-Host ''; Write-Host 'GitHub sign-in finished. You can close this window.'";
        return LaunchPowerShell(command, owner, "GitHub sign-in");
    }

    /* ==========================================================================
       PATCH: AUTOMATIC GITHUB AUTH DETECTION
       DATE.TIME: 2026-09-11 11:24 +03:00
       Detect completed browser sign-in without manual refresh.
       ========================================================================== */
    public async Task<GitHubAccountStatus> WaitForAuthenticationAsync(
        TimeSpan timeout,
        CancellationToken token = default)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        GitHubAccountStatus last = await GetStatusAsync(token);

        while (!last.Authenticated && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), token);
            last = await GetStatusAsync(token);
        }

        if (last.Authenticated)
            await _audit.WriteAsync("github_authenticated", new { login = last.Login });

        return last;
    }

    /* ==========================================================================
       PATCH: GITHUB ACCOUNT DISCONNECT
       DATE.TIME: 2026-09-11 11:24 +03:00
       Disconnect one GitHub login without touching repositories.
       ========================================================================== */
    public async Task<CommandResult> SignOutAsync(string login, CancellationToken token = default)
    {
        var executable = FindGitHubCliExecutable();
        if (string.IsNullOrWhiteSpace(executable))
            return new(-1, "GitHub CLI could not be located.");

        var arguments = new List<string> { "auth", "logout", "--hostname", "github.com" };
        if (!string.IsNullOrWhiteSpace(login))
        {
            arguments.Add("--user");
            arguments.Add(login.Trim());
        }

        var result = await RunProcessAsync(
            executable,
            arguments,
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            TimeSpan.FromSeconds(30),
            token);

        await _audit.WriteAsync("github_disconnected", new
        {
            login = string.IsNullOrWhiteSpace(login) ? null : login,
            success = result.Success
        });
        return result;
    }

    public async Task RecordModeAsync(string mode)
    {
        await _audit.WriteAsync("connection_mode_selected", new { mode });
    }

    internal static string? FindGitHubCliExecutable()
    {
        var candidates = new List<string>();

        var configured = Environment.GetEnvironmentVariable("GH_EXE");
        if (!string.IsNullOrWhiteSpace(configured)) candidates.Add(configured);

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrWhiteSpace(programFiles))
            candidates.Add(Path.Combine(programFiles, "GitHub CLI", "gh.exe"));

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(local))
        {
            candidates.Add(Path.Combine(local, "Programs", "GitHub CLI", "gh.exe"));
            candidates.Add(Path.Combine(local, "Microsoft", "WinGet", "Links", "gh.exe"));
        }

        return candidates.FirstOrDefault(File.Exists);
    }

    private static bool LaunchPowerShell(string command, IWin32Window? owner, string title)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = true,
                Arguments = "-NoExit -ExecutionPolicy Bypass -Command \"" + command.Replace("\"", "`\"") + "\""
            });
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(owner,
                "GitPet could not open PowerShell.\r\n\r\n" + ex.Message,
                title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
    }

    private static async Task<CommandResult> RunProcessAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        TimeSpan timeout,
        CancellationToken token)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = !string.IsNullOrWhiteSpace(workingDirectory) && Directory.Exists(workingDirectory)
                    ? workingDirectory
                    : Environment.CurrentDirectory
            };
            foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

            using var process = new Process { StartInfo = startInfo };
            if (!process.Start()) return new(-1, "The process could not be started.");

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeoutCts.CancelAfter(timeout);

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
                if (token.IsCancellationRequested) throw;
                return new(-1, $"Operation timed out after {timeout.TotalSeconds:0} seconds.", true);
            }

            var stdout = await outputTask;
            var stderr = await errorTask;
            var output = string.Join(Environment.NewLine,
                new[] { stdout.Trim(), stderr.Trim() }.Where(value => value.Length > 0));
            return new(process.ExitCode, output);
        }
        catch (Exception ex)
        {
            return new(-1, ex.Message);
        }
    }
}
