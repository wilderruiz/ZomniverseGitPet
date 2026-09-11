using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ZomniverseGitPet;

/* ========================================================================== 
   PATCH: STANDALONE LOGICAL PROJECT PUBLISHING
   DATE.TIME: 2026-09-11 20:22 +03:00
   Publish only the selected logical-project scope.
   ========================================================================== */
internal sealed class StandaloneProjectPublishingEntry
{
    public string ProjectId { get; set; } = "";
    public string RemoteUrl { get; set; } = "";
    public string RepositoryLabel { get; set; } = "";
    public string LastPublishedSourceCommit { get; set; } = "";
    public string LastPublishedFingerprint { get; set; } = "";
    public DateTimeOffset? LastPublishedUtc { get; set; }
}

internal sealed record StandaloneProjectSnapshot(
    string SourceCommit,
    string Fingerprint,
    IReadOnlyList<string> Files,
    IReadOnlyList<string> Pathspecs);

internal sealed record StandaloneProjectPublishResult(
    bool Success,
    string Message,
    int PublishedFileCount = 0,
    string WorkspacePath = "",
    string RemoteUrl = "",
    bool CreatedCommit = false);

internal static class StandaloneProjectPublishing
{
    private static readonly object StoreGate = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static bool IsLogicalProject(AppConfig config, string repositoryRoot)
    {
        var project = config.GetActiveProject();
        if (project is null || !PathEquals(project.RepositoryRoot, repositoryRoot)) return false;
        return !project.TrackEverything || !PathEquals(project.Path, project.RepositoryRoot);
    }

    public static IReadOnlyList<string> GetPublishingPathspecs(AppConfig config, string repositoryRoot)
    {
        var project = config.GetActiveProject();
        if (project is null || !PathEquals(project.RepositoryRoot, repositoryRoot)) return [];
        if (!IsLogicalProject(config, repositoryRoot)) return [];

        if (project.TrackEverything)
        {
            var nested = LogicalProjectScopeRuntime.TryGetRelativePath(repositoryRoot, project.Path);
            return string.IsNullOrWhiteSpace(nested) ? [] : [NormalizeRelative(nested)];
        }

        return LogicalProjectScopeRuntime.GetScopeEntries(repositoryRoot)
            .Select(entry => NormalizeRelative(entry.RelativePath))
            .Where(path => path.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static StandaloneProjectPublishingEntry? GetLink(AppConfig config)
    {
        var project = config.GetActiveProject();
        if (project is null) return null;
        return LoadEntries().FirstOrDefault(entry =>
            string.Equals(entry.ProjectId, project.Id, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(entry.RemoteUrl));
    }

    public static StandaloneProjectPublishingEntry SetLink(AppConfig config, string remoteUrl)
    {
        var project = config.GetActiveProject()
            ?? throw new InvalidOperationException("Choose a GitPet project before connecting a standalone publishing repository.");
        if (string.IsNullOrWhiteSpace(remoteUrl))
            throw new ArgumentException("A publishing repository URL is required.", nameof(remoteUrl));

        lock (StoreGate)
        {
            var entries = LoadEntriesUnsafe();
            var entry = entries.FirstOrDefault(item =>
                string.Equals(item.ProjectId, project.Id, StringComparison.OrdinalIgnoreCase));
            var repositoryLabel = DescribeRemote(remoteUrl);

            if (entry is null)
            {
                entry = new StandaloneProjectPublishingEntry { ProjectId = project.Id };
                entries.Add(entry);
            }

            if (!RemoteEquals(entry.RemoteUrl, remoteUrl))
            {
                entry.LastPublishedSourceCommit = "";
                entry.LastPublishedFingerprint = "";
                entry.LastPublishedUtc = null;
            }

            entry.RemoteUrl = remoteUrl.Trim();
            entry.RepositoryLabel = repositoryLabel;
            SaveEntriesUnsafe(entries);
            return Clone(entry);
        }
    }

    public static void MarkPublished(
        AppConfig config,
        string sourceCommit,
        string fingerprint)
    {
        var project = config.GetActiveProject();
        if (project is null) return;

        lock (StoreGate)
        {
            var entries = LoadEntriesUnsafe();
            var entry = entries.FirstOrDefault(item =>
                string.Equals(item.ProjectId, project.Id, StringComparison.OrdinalIgnoreCase));
            if (entry is null) return;

            entry.LastPublishedSourceCommit = sourceCommit;
            entry.LastPublishedFingerprint = fingerprint;
            entry.LastPublishedUtc = DateTimeOffset.UtcNow;
            SaveEntriesUnsafe(entries);
        }
    }

    public static string GetWorkspacePath(RecentRepositoryEntry project)
    {
        var safeId = new string((project.Id ?? "project")
            .Where(character => char.IsLetterOrDigit(character) || character is '-' or '_')
            .ToArray());
        if (string.IsNullOrWhiteSpace(safeId)) safeId = "project";

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ZomniverseGitPet",
            "Publishing",
            safeId);
    }

    public static async Task<StandaloneProjectSnapshot?> BuildSnapshotAsync(
        AppConfig config,
        GitService git,
        string repositoryRoot,
        CancellationToken token = default)
    {
        if (!IsLogicalProject(config, repositoryRoot)) return null;
        var pathspecs = GetPublishingPathspecs(config, repositoryRoot);
        if (pathspecs.Count == 0) return null;

        var head = await git.RunGitAsync(
            repositoryRoot,
            ["rev-parse", "HEAD"],
            TimeSpan.FromSeconds(12), token);
        if (!head.Success || string.IsNullOrWhiteSpace(head.Output)) return null;

        /* ==========================================================================
           PATCH: CONTENT-AWARE PROJECT FINGERPRINT
           DATE.TIME: 2026-09-11 20:41 +03:00
           Include blob identities so same-path edits trigger Send.
           ========================================================================== */
        var arguments = new List<string> { "ls-tree", "-r", "--full-tree", "HEAD", "--" };
        arguments.AddRange(pathspecs);
        var tracked = await git.RunGitAsync(
            repositoryRoot,
            arguments,
            TimeSpan.FromSeconds(30), token);
        if (!tracked.Success) return null;

        var files = tracked.Output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line =>
            {
                var tab = line.IndexOf('\t');
                return tab >= 0 && tab + 1 < line.Length ? line[(tab + 1)..] : string.Empty;
            })
            .Select(NormalizeRelative)
            .Where(path => path.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var fingerprintInput = string.Join('\n', pathspecs.OrderBy(path => path, StringComparer.OrdinalIgnoreCase)) +
                               "\n---\n" + tracked.Output.Replace("\r\n", "\n");
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintInput))).ToLowerInvariant();
        return new StandaloneProjectSnapshot(head.Output.Trim(), fingerprint, files, pathspecs);
    }

    public static async Task<bool> HasPendingPublishAsync(
        AppConfig config,
        GitService git,
        string repositoryRoot,
        CancellationToken token = default)
    {
        var link = GetLink(config);
        if (link is null) return false;
        var snapshot = await BuildSnapshotAsync(config, git, repositoryRoot, token);
        if (snapshot is null) return false;
        return string.IsNullOrWhiteSpace(link.LastPublishedFingerprint) ||
               !string.Equals(link.LastPublishedFingerprint, snapshot.Fingerprint, StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<IReadOnlyList<string>> GetTrackedScopeFilesAsync(
        AppConfig config,
        GitService git,
        string repositoryRoot,
        CancellationToken token = default)
    {
        var snapshot = await BuildSnapshotAsync(config, git, repositoryRoot, token);
        return snapshot?.Files ?? [];
    }

    public static async Task<StandaloneProjectPublishResult> PublishAsync(
        AppConfig config,
        GitService git,
        AuditLog audit,
        CancellationToken token = default)
    {
        var project = config.GetActiveProject();
        var repositoryRoot = config.RepositoryPath;
        if (project is null || string.IsNullOrWhiteSpace(repositoryRoot) || !Directory.Exists(repositoryRoot))
            return new(false, "Choose a logical GitPet project before publishing.");
        if (!IsLogicalProject(config, repositoryRoot))
            return new(false, "This is the whole Git repository, so normal Send should be used instead.");

        var link = GetLink(config);
        if (link is null)
            return new(false, "Connect this GitPet project to its own online repository first.");

        var status = await git.GetStatusAsync(repositoryRoot, token);
        if (!status.Healthy)
            return new(false, "GitPet could not verify the project before publishing.\r\n\r\n" + status.Error);
        if (status.Files.Count > 0)
            return new(false, "Save this project's changes before sending its standalone copy online.");

        var snapshot = await BuildSnapshotAsync(config, git, repositoryRoot, token);
        if (snapshot is null || snapshot.Files.Count == 0)
            return new(false, "The selected project scope does not contain any tracked files in the latest saved version.");

        if (!string.IsNullOrWhiteSpace(link.LastPublishedFingerprint) &&
            string.Equals(link.LastPublishedFingerprint, snapshot.Fingerprint, StringComparison.OrdinalIgnoreCase))
        {
            return new(true,
                "Everything in this logical project is already published.",
                snapshot.Files.Count,
                GetWorkspacePath(project),
                link.RemoteUrl);
        }

        var workspace = GetWorkspacePath(project);
        try
        {
            Directory.CreateDirectory(workspace);
            CleanPublishingWorkspace(workspace);

            foreach (var relative in snapshot.Files)
            {
                token.ThrowIfCancellationRequested();
                var source = ResolveInside(repositoryRoot, relative);
                if (!File.Exists(source))
                    return new(false, $"A saved project file is missing from the working tree:\r\n{relative}");

                var destination = ResolveInside(workspace, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(source, destination, true);
            }

            var gitDirectory = Path.Combine(workspace, ".git");
            if (!Directory.Exists(gitDirectory))
            {
                var init = await git.RunGitAsync(workspace, ["init", "-b", "main"], TimeSpan.FromMinutes(1), token);
                if (!init.Success) return Failure("GitPet could not initialize its isolated publishing workspace.", init, workspace, link.RemoteUrl);
            }

            var name = await git.GetUserNameAsync(repositoryRoot, token);
            var email = await git.GetUserEmailAsync(repositoryRoot, token);
            if (!name.Success || string.IsNullOrWhiteSpace(name.Output) ||
                !email.Success || string.IsNullOrWhiteSpace(email.Output))
            {
                return new(false,
                    "GitPet needs the Git author name and email already used by this source repository before it can create the standalone publishing commit.",
                    WorkspacePath: workspace,
                    RemoteUrl: link.RemoteUrl);
            }

            var setName = await git.RunGitAsync(workspace, ["config", "user.name", name.Output.Trim()], cancellationToken: token);
            if (!setName.Success) return Failure("GitPet could not configure the publishing author name.", setName, workspace, link.RemoteUrl);
            var setEmail = await git.RunGitAsync(workspace, ["config", "user.email", email.Output.Trim()], cancellationToken: token);
            if (!setEmail.Success) return Failure("GitPet could not configure the publishing author email.", setEmail, workspace, link.RemoteUrl);

            var remote = await git.RunGitAsync(workspace, ["remote", "get-url", "origin"], cancellationToken: token);
            CommandResult remoteResult;
            if (!remote.Success || string.IsNullOrWhiteSpace(remote.Output))
            {
                remoteResult = await git.RunGitAsync(workspace, ["remote", "add", "origin", link.RemoteUrl], cancellationToken: token);
            }
            else if (!RemoteEquals(remote.Output.Trim(), link.RemoteUrl))
            {
                remoteResult = await git.RunGitAsync(workspace, ["remote", "set-url", "origin", link.RemoteUrl], cancellationToken: token);
            }
            else
            {
                remoteResult = new CommandResult(0, remote.Output);
            }
            if (!remoteResult.Success) return Failure("GitPet could not configure the standalone publishing destination.", remoteResult, workspace, link.RemoteUrl);

            /* ==========================================================================
               PATCH: ISOLATED WORKSPACE EXACTNESS
               DATE.TIME: 2026-09-11 20:41 +03:00
               Keep explicitly selected tracked files despite copied ignore rules.
               ========================================================================== */
            var stage = await git.RunGitAsync(workspace, ["add", "-f", "-A"], TimeSpan.FromMinutes(1), token);
            if (!stage.Success) return Failure("GitPet could not stage the isolated project snapshot.", stage, workspace, link.RemoteUrl);

            var hasHead = await git.RunGitAsync(workspace, ["rev-parse", "--verify", "HEAD"], cancellationToken: token);
            var createdCommit = false;
            var shouldCommit = !hasHead.Success;
            if (hasHead.Success)
            {
                var changed = await git.RunGitAsync(workspace, ["diff", "--cached", "--quiet"], cancellationToken: token);
                if (changed.ExitCode == 1) shouldCommit = true;
                else if (changed.ExitCode != 0)
                    return Failure("GitPet could not compare the isolated publishing snapshot.", changed, workspace, link.RemoteUrl);
            }

            if (shouldCommit)
            {
                var shortSource = snapshot.SourceCommit.Length > 8 ? snapshot.SourceCommit[..8] : snapshot.SourceCommit;
                var message = $"publish: {project.DisplayName} from {shortSource}";
                var commit = await git.RunGitAsync(workspace, ["commit", "-m", message], TimeSpan.FromMinutes(1), token);
                if (!commit.Success) return Failure("GitPet could not create the standalone project commit.", commit, workspace, link.RemoteUrl);
                createdCommit = true;
            }

            var push = await git.RunGitAsync(
                workspace,
                ["push", "-u", "origin", "main"],
                TimeSpan.FromMinutes(5), token);
            if (!push.Success)
                return Failure(
                    "GitPet could not publish the isolated project. The parent repository was not pushed or changed.",
                    push,
                    workspace,
                    link.RemoteUrl);

            MarkPublished(config, snapshot.SourceCommit, snapshot.Fingerprint);
            await audit.WriteAsync("standalone_project_published", new
            {
                projectId = project.Id,
                project = project.DisplayName,
                fileCount = snapshot.Files.Count,
                sourceCommit = snapshot.SourceCommit,
                workspace,
                createdCommit
            });

            return new(true,
                $"Published {snapshot.Files.Count} project file{(snapshot.Files.Count == 1 ? "" : "s")} from the selected GitPet scope.\r\n\r\n" +
                "The larger parent repository was not sent.",
                snapshot.Files.Count,
                workspace,
                link.RemoteUrl,
                createdCommit);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await audit.WriteAsync("standalone_project_publish_error", new
            {
                projectId = project.Id,
                project = project.DisplayName,
                error = ex.Message
            });
            return new(false,
                "GitPet could not prepare the isolated publishing workspace.\r\n\r\n" + ex.Message,
                WorkspacePath: workspace,
                RemoteUrl: link.RemoteUrl);
        }
    }

    public static bool RemoteEquals(string? left, string? right)
    {
        static string Normalize(string? value)
        {
            var text = (value ?? "").Trim().TrimEnd('/');
            if (text.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) text = text[..^4];
            return text.Replace('\\', '/');
        }
        return string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);
    }

    public static string DescribeRemote(string remoteUrl)
    {
        if (GitRepositoryAddressParser.TryParse(remoteUrl, out var address))
        {
            var source = address.CloneSource.TrimEnd('/');
            if (source.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) source = source[..^4];
            var marker = source.IndexOf("github.com/", StringComparison.OrdinalIgnoreCase);
            if (marker >= 0) return source[(marker + "github.com/".Length)..].Replace(':', '/');
            var colon = source.LastIndexOf(':');
            if (source.StartsWith("git@", StringComparison.OrdinalIgnoreCase) && colon >= 0)
                return source[(colon + 1)..];
            return source;
        }
        return remoteUrl.Trim();
    }

    private static IReadOnlyList<StandaloneProjectPublishingEntry> LoadEntries()
    {
        lock (StoreGate) return LoadEntriesUnsafe().Select(Clone).ToArray();
    }

    private static List<StandaloneProjectPublishingEntry> LoadEntriesUnsafe()
    {
        try
        {
            var path = StorePath;
            if (!File.Exists(path)) return [];
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<StandaloneProjectPublishingEntry>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static void SaveEntriesUnsafe(List<StandaloneProjectPublishingEntry> entries)
    {
        var path = StorePath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(entries, JsonOptions));
        File.Move(temp, path, true);
    }

    private static string StorePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ZomniverseGitPet",
        "standalone-publishing.json");

    private static StandaloneProjectPublishingEntry Clone(StandaloneProjectPublishingEntry entry) => new()
    {
        ProjectId = entry.ProjectId,
        RemoteUrl = entry.RemoteUrl,
        RepositoryLabel = entry.RepositoryLabel,
        LastPublishedSourceCommit = entry.LastPublishedSourceCommit,
        LastPublishedFingerprint = entry.LastPublishedFingerprint,
        LastPublishedUtc = entry.LastPublishedUtc
    };

    private static void CleanPublishingWorkspace(string workspace)
    {
        foreach (var file in Directory.EnumerateFiles(workspace))
        {
            try { File.SetAttributes(file, FileAttributes.Normal); } catch { }
            File.Delete(file);
        }

        foreach (var directory in Directory.EnumerateDirectories(workspace))
        {
            if (Path.GetFileName(directory).Equals(".git", StringComparison.OrdinalIgnoreCase)) continue;
            Directory.Delete(directory, true);
        }
    }

    private static string ResolveInside(string root, string relative)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var candidate = Path.GetFullPath(Path.Combine(
            normalizedRoot,
            relative.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = normalizedRoot + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A project publishing path resolved outside its repository boundary: " + relative);
        return candidate;
    }

    private static StandaloneProjectPublishResult Failure(
        string prefix,
        CommandResult result,
        string workspace,
        string remoteUrl) =>
        new(false,
            prefix + (string.IsNullOrWhiteSpace(result.Output) ? "" : "\r\n\r\n" + result.Output),
            WorkspacePath: workspace,
            RemoteUrl: remoteUrl);

    private static string NormalizeRelative(string path) =>
        (path ?? "").Replace('\\', '/').Trim().TrimStart('/').TrimEnd('/');

    private static bool PathEquals(string left, string right)
    {
        try
        {
            left = Path.TrimEndingDirectorySeparator(Path.GetFullPath(left));
            right = Path.TrimEndingDirectorySeparator(Path.GetFullPath(right));
        }
        catch
        {
        }
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }
}
