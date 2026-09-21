using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ZomniverseGitPet;

/* ========================================================================== 
   PATCH: STANDALONE LOGICAL PROJECT PUBLISHING
   DATE.TIME: 2026-09-11 20:22 +03:00
   Publish only the selected logical-project scope.
   ========================================================================== */
internal sealed class StandaloneProjectBranchState
{
    public string LastPublishedSourceCommit { get; set; } = "";
    public string LastPublishedFingerprint { get; set; } = "";
    public DateTimeOffset? LastPublishedUtc { get; set; }
}

internal sealed class StandaloneProjectPublishingEntry
{
    public string ProjectId { get; set; } = "";
    public string RemoteUrl { get; set; } = "";
    public string RepositoryLabel { get; set; } = "";
    public string Branch { get; set; } = "main";
    public Dictionary<string, StandaloneProjectBranchState> BranchStates { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    // Legacy single-branch mirror. Old standalone-publishing.json records migrate
    // into BranchStates["main"], while existing callers keep working unchanged.
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

internal sealed record StandaloneProjectRemoteChange(
    string Status,
    string Path,
    string PreviousPath = "");

internal sealed record StandaloneProjectReceiveResult(
    bool Success,
    string Message,
    int ChangedFileCount = 0,
    string WorkspacePath = "",
    string RemoteUrl = "");

internal sealed record StandaloneProjectReconcileResult(
    bool Success,
    string Message,
    int AppliedOnlineCount = 0,
    int KeptLocalCount = 0,
    string WorkspacePath = "",
    string RemoteUrl = "");

internal sealed record StandaloneProjectRemoteInspection(
    bool OnlineReachable,
    bool RemoteBranchExists,
    IReadOnlyList<StandaloneProjectRemoteChange> Changes,
    string Message = "")
{
    public int IncomingChangeCount => Changes.Count;
}

internal static class StandaloneProjectPublishing
{
    private static readonly object StoreGate = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> WorkspaceGates =
        new(StringComparer.OrdinalIgnoreCase);

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
                entry.Branch = "main";
                entry.BranchStates = new Dictionary<string, StandaloneProjectBranchState>(StringComparer.OrdinalIgnoreCase);
                entry.LastPublishedSourceCommit = "";
                entry.LastPublishedFingerprint = "";
                entry.LastPublishedUtc = null;
            }

            entry.RemoteUrl = remoteUrl.Trim();
            entry.RepositoryLabel = repositoryLabel;
            NormalizeEntry(entry);
            SaveEntriesUnsafe(entries);
            return Clone(entry);
        }
    }

    public static StandaloneProjectPublishingEntry SetBranch(AppConfig config, string branch)
    {
        var project = config.GetActiveProject()
            ?? throw new InvalidOperationException("Choose a GitPet project before selecting a standalone branch.");
        var normalizedBranch = NormalizeBranchName(branch);
        if (!IsSafeBranchName(normalizedBranch))
            throw new ArgumentException("The standalone branch name is not a safe Git branch name.", nameof(branch));

        lock (StoreGate)
        {
            var entries = LoadEntriesUnsafe();
            var entry = entries.FirstOrDefault(item =>
                string.Equals(item.ProjectId, project.Id, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("Connect this GitPet project to its own online repository first.");

            entry.Branch = normalizedBranch;
            ProjectActiveBranchState(entry);
            SaveEntriesUnsafe(entries);
            return Clone(entry);
        }
    }

    public static async Task<IReadOnlyList<string>> ListRemoteBranchesAsync(
        AppConfig config,
        GitService git,
        string repositoryRoot,
        CancellationToken token = default)
    {
        var link = GetLink(config);
        if (link is null || !IsLogicalProject(config, repositoryRoot)) return [];

        var result = await git.RunGitAsync(
            repositoryRoot,
            ["ls-remote", "--heads", link.RemoteUrl],
            TimeSpan.FromSeconds(30),
            token);
        if (!result.Success) return [];

        return ParseRemoteBranches(result.Output);
    }

    internal static IReadOnlyList<string> ParseRemoteBranches(string output) =>
        (output ?? "")
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line =>
            {
                var marker = line.IndexOf("refs/heads/", StringComparison.Ordinal);
                return marker < 0 ? "" : line[(marker + "refs/heads/".Length)..].Trim();
            })
            .Where(IsSafeBranchName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name.Equals("main", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    internal static bool IsSafeBranchName(string? branch)
    {
        var value = NormalizeBranchName(branch);
        if (value.Length == 0 || value.Length > 255) return false;
        if (value.StartsWith("refs/", StringComparison.OrdinalIgnoreCase)) return false;
        if (value.StartsWith("-", StringComparison.Ordinal) ||
            value.StartsWith(".", StringComparison.Ordinal) ||
            value.EndsWith(".", StringComparison.Ordinal) ||
            value.EndsWith("/", StringComparison.Ordinal) ||
            value.EndsWith(".lock", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("..", StringComparison.Ordinal) ||
            value.Contains("//", StringComparison.Ordinal) ||
            value.Contains("@{", StringComparison.Ordinal))
            return false;

        foreach (var ch in value)
        {
            if (char.IsControl(ch) || char.IsWhiteSpace(ch) ||
                ch is '~' or '^' or ':' or '?' or '*' or '[' or '\\')
                return false;
        }
        return true;
    }

    internal static string NormalizeBranchName(string? branch) =>
        string.IsNullOrWhiteSpace(branch) ? "main" : branch.Trim();

    internal static string RemoteTrackingRef(string branch) =>
        "refs/remotes/origin/" + NormalizeBranchName(branch);

    internal static string[] BuildFetchArguments(string branch, bool quiet) =>
        quiet
            ? ["fetch", "--quiet", "--prune", "origin", NormalizeBranchName(branch)]
            : ["fetch", "--prune", "origin", NormalizeBranchName(branch)];

    internal static string[] BuildPushArguments(string branch) =>
        ["push", "-u", "origin", $"HEAD:refs/heads/{NormalizeBranchName(branch)}"];

    internal static string[] BuildBranchProbeArguments(string remoteUrl, string branch) =>
        ["ls-remote", "--heads", remoteUrl, $"refs/heads/{NormalizeBranchName(branch)}"];

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

            NormalizeEntry(entry);
            var state = GetOrCreateBranchState(entry, entry.Branch);
            state.LastPublishedSourceCommit = sourceCommit;
            state.LastPublishedFingerprint = fingerprint;
            state.LastPublishedUtc = DateTimeOffset.UtcNow;
            ProjectActiveBranchState(entry);
            SaveEntriesUnsafe(entries);
        }
    }

    public static string GetWorkspacePath(RecentRepositoryEntry project) =>
        GetWorkspacePath(project, "main");

    public static string GetWorkspacePath(RecentRepositoryEntry project, string branch)
    {
        var safeId = new string((project.Id ?? "project")
            .Where(character => char.IsLetterOrDigit(character) || character is '-' or '_')
            .ToArray());
        if (string.IsNullOrWhiteSpace(safeId)) safeId = "project";

        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ZomniverseGitPet",
            "Publishing",
            safeId);

        var normalizedBranch = NormalizeBranchName(branch);
        if (normalizedBranch.Equals("main", StringComparison.OrdinalIgnoreCase))
            return root;

        var branchKey = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(normalizedBranch)))
            .ToLowerInvariant()[..16];
        return Path.Combine(root, "branches", branchKey);
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

        var remoteInspection = await InspectRemoteAsync(config, git, repositoryRoot, true, token);
        if (remoteInspection.OnlineReachable &&
            remoteInspection.RemoteBranchExists &&
            string.IsNullOrWhiteSpace(link.LastPublishedFingerprint))
        {
            return new(false,
                $"The project-only online repository already has branch '{link.Branch}', but this local GitPet project has no publishing baseline for that branch yet.\r\n\r\n" +
                "Use Get first so GitPet can establish the project-only baseline without overwriting online work.",
                RemoteUrl: link.RemoteUrl);
        }
        if (remoteInspection.IncomingChangeCount > 0)
        {
            return new(false,
                $"The project-only online repository has {remoteInspection.IncomingChangeCount} incoming change{(remoteInspection.IncomingChangeCount == 1 ? "" : "s")}.\r\n\r\n" +
                "Use Get and review those changes before sending local project updates.",
                RemoteUrl: link.RemoteUrl);
        }

        var snapshot = await BuildSnapshotAsync(config, git, repositoryRoot, token);
        if (snapshot is null || snapshot.Files.Count == 0)
            return new(false, "The selected project scope does not contain any tracked files in the latest saved version.");

        if (!string.IsNullOrWhiteSpace(link.LastPublishedFingerprint) &&
            string.Equals(link.LastPublishedFingerprint, snapshot.Fingerprint, StringComparison.OrdinalIgnoreCase))
        {
            return new(true,
                "Everything in this logical project is already published.",
                snapshot.Files.Count,
                GetWorkspacePath(project, link.Branch),
                link.RemoteUrl);
        }

        var workspace = GetWorkspacePath(project, link.Branch);
        using var workspaceLease = await EnterWorkspaceAsync(workspace, token);
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
                BuildPushArguments(link.Branch),
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
                branch = link.Branch,
                workspace,
                createdCommit
            });

            return new(true,
                $"Published {snapshot.Files.Count} project file{(snapshot.Files.Count == 1 ? "" : "s")} from the selected GitPet scope.\r\n\r\n" +
                $"The larger parent repository was not sent. Standalone branch: {link.Branch}.",
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

    public static async Task<StandaloneProjectRemoteInspection> InspectRemoteAsync(
        AppConfig config,
        GitService git,
        string repositoryRoot,
        bool fetchRemote = true,
        CancellationToken token = default)
    {
        var project = config.GetActiveProject();
        var link = GetLink(config);
        if (project is null || link is null || !IsLogicalProject(config, repositoryRoot))
            return new(false, false, [], "No standalone project remote is connected.");

        var workspace = GetWorkspacePath(project, link.Branch);
        using var workspaceLease = await EnterWorkspaceAsync(workspace, token);
        var gitDirectory = Path.Combine(workspace, ".git");
        if (!Directory.Exists(gitDirectory))
        {
            var probe = await git.RunGitAsync(
                repositoryRoot,
                BuildBranchProbeArguments(link.RemoteUrl, link.Branch),
                TimeSpan.FromSeconds(30),
                token);
            return new(
                probe.Success,
                probe.Success && !string.IsNullOrWhiteSpace(probe.Output),
                [],
                probe.Success ? "" : probe.Output);
        }

        var remote = await git.RunGitAsync(workspace, ["remote", "get-url", "origin"], cancellationToken: token);
        CommandResult remoteResult;
        if (!remote.Success || string.IsNullOrWhiteSpace(remote.Output))
            remoteResult = await git.RunGitAsync(workspace, ["remote", "add", "origin", link.RemoteUrl], cancellationToken: token);
        else if (!RemoteEquals(remote.Output.Trim(), link.RemoteUrl))
            remoteResult = await git.RunGitAsync(workspace, ["remote", "set-url", "origin", link.RemoteUrl], cancellationToken: token);
        else
            remoteResult = new CommandResult(0, remote.Output);

        if (!remoteResult.Success)
            return new(false, false, [], remoteResult.Output);

        if (fetchRemote)
        {
            var fetch = await git.RunGitAsync(
                workspace,
                BuildFetchArguments(link.Branch, quiet: true),
                TimeSpan.FromMinutes(2),
                token);
            if (!fetch.Success)
                return new(false, false, [], fetch.Output);
        }

        var remoteHead = await git.RunGitAsync(
            workspace,
            ["rev-parse", "--verify", "-q", RemoteTrackingRef(link.Branch)],
            TimeSpan.FromSeconds(10),
            token);
        if (!remoteHead.Success)
            return new(true, false, []);

        var localHead = await git.RunGitAsync(
            workspace,
            ["rev-parse", "--verify", "-q", "HEAD"],
            TimeSpan.FromSeconds(10),
            token);
        if (!localHead.Success)
            return new(true, true, []);

        var diff = await git.RunGitAsync(
            workspace,
            ["diff", "--name-status", "--find-renames", "HEAD", RemoteTrackingRef(link.Branch)],
            TimeSpan.FromSeconds(20),
            token);
        if (!diff.Success)
            return new(false, true, [], diff.Output);

        return new(true, true, ParseRemoteChanges(diff.Output));
    }

    /* ==========================================================================
       PATCH: STANDALONE LOGICAL PROJECT GET
       DATE.TIME: 2026-09-20 17:40 +03:00
       Receive project-only remote changes through the isolated workspace.
       Never pull standalone history into the parent repository.
       ========================================================================== */
    public static async Task<StandaloneProjectReceiveResult> ReceiveAsync(
        AppConfig config,
        GitService git,
        AuditLog audit,
        CancellationToken token = default)
    {
        var project = config.GetActiveProject();
        var repositoryRoot = config.RepositoryPath;
        if (project is null || string.IsNullOrWhiteSpace(repositoryRoot) || !Directory.Exists(repositoryRoot))
            return new(false, "Choose a logical GitPet project before getting project-only updates.");
        if (!IsLogicalProject(config, repositoryRoot))
            return new(false, "This is the whole Git repository, so normal Get should be used instead.");

        var link = GetLink(config);
        if (link is null)
            return new(false, "Connect this GitPet project to its own online repository first.");

        var status = await git.GetStatusAsync(repositoryRoot, token);
        if (!status.Healthy)
            return new(false, "GitPet could not verify the project before Get.\r\n\r\n" + status.Error);
        if (status.Files.Count > 0)
            return new(false, "Save or discard this project's current local changes before getting its standalone online copy.");

        if (!string.IsNullOrWhiteSpace(link.LastPublishedFingerprint) &&
            await HasPendingPublishAsync(config, git, repositoryRoot, token))
        {
            return new(false,
                "This project has saved local updates that have not been sent yet.\r\n\r\n" +
                "Send those project-only updates first, then use Get so GitPet never guesses how to combine two independent histories.",
                RemoteUrl: link.RemoteUrl);
        }

        var snapshot = await BuildSnapshotAsync(config, git, repositoryRoot, token);
        if (snapshot is null || snapshot.Files.Count == 0)
            return new(false, "The selected project scope does not contain any tracked files in the latest saved version.");

        var workspace = GetWorkspacePath(project, link.Branch);
        var stagingRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ZomniverseGitPet",
            "Receiving",
            project.Id,
            Guid.NewGuid().ToString("N"));

        using var workspaceLease = await EnterWorkspaceAsync(workspace, token);
        try
        {
            Directory.CreateDirectory(workspace);

            var gitDirectory = Path.Combine(workspace, ".git");
            var hasWorkspaceGit = Directory.Exists(gitDirectory);
            if (!hasWorkspaceGit)
            {
                CleanPublishingWorkspace(workspace);
                foreach (var relative in snapshot.Files)
                {
                    token.ThrowIfCancellationRequested();
                    var source = ResolveInside(repositoryRoot, relative);
                    if (!File.Exists(source))
                        return new(false, $"A saved project file is missing from the working tree:\r\n{relative}",
                            WorkspacePath: workspace, RemoteUrl: link.RemoteUrl);

                    var destination = ResolveInside(workspace, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    File.Copy(source, destination, true);
                }

                var init = await git.RunGitAsync(workspace, ["init", "-b", "main"], TimeSpan.FromMinutes(1), token);
                if (!init.Success)
                    return ReceiveFailure("GitPet could not initialize its isolated Get workspace.", init, workspace, link.RemoteUrl);
            }
            else
            {
                var workspaceHead = await git.RunGitAsync(
                    workspace, ["rev-parse", "--verify", "HEAD"], cancellationToken: token);
                if (workspaceHead.Success)
                {
                    var reset = await git.RunGitAsync(
                        workspace, ["reset", "--hard", "HEAD"], TimeSpan.FromMinutes(1), token);
                    if (!reset.Success)
                        return ReceiveFailure("GitPet could not reset its isolated Get workspace.", reset, workspace, link.RemoteUrl);
                    var clean = await git.RunGitAsync(
                        workspace, ["clean", "-fdx"], TimeSpan.FromMinutes(1), token);
                    if (!clean.Success)
                        return ReceiveFailure("GitPet could not clean its isolated Get workspace.", clean, workspace, link.RemoteUrl);
                }
                else
                {
                    CleanPublishingWorkspace(workspace);
                    foreach (var relative in snapshot.Files)
                    {
                        token.ThrowIfCancellationRequested();
                        var source = ResolveInside(repositoryRoot, relative);
                        if (!File.Exists(source))
                            return new(false, $"A saved project file is missing from the working tree:\r\n{relative}",
                                WorkspacePath: workspace, RemoteUrl: link.RemoteUrl);

                        var destination = ResolveInside(workspace, relative);
                        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                        File.Copy(source, destination, true);
                    }
                }
            }

            var name = await git.GetUserNameAsync(repositoryRoot, token);
            var email = await git.GetUserEmailAsync(repositoryRoot, token);
            if (!name.Success || string.IsNullOrWhiteSpace(name.Output) ||
                !email.Success || string.IsNullOrWhiteSpace(email.Output))
            {
                return new(false,
                    "GitPet needs the Git author name and email already used by this source repository before it can establish the isolated Get baseline.",
                    WorkspacePath: workspace,
                    RemoteUrl: link.RemoteUrl);
            }

            var setName = await git.RunGitAsync(workspace, ["config", "user.name", name.Output.Trim()], cancellationToken: token);
            if (!setName.Success)
                return ReceiveFailure("GitPet could not configure the isolated Get author name.", setName, workspace, link.RemoteUrl);
            var setEmail = await git.RunGitAsync(workspace, ["config", "user.email", email.Output.Trim()], cancellationToken: token);
            if (!setEmail.Success)
                return ReceiveFailure("GitPet could not configure the isolated Get author email.", setEmail, workspace, link.RemoteUrl);

            var remote = await git.RunGitAsync(workspace, ["remote", "get-url", "origin"], cancellationToken: token);
            CommandResult remoteResult;
            if (!remote.Success || string.IsNullOrWhiteSpace(remote.Output))
                remoteResult = await git.RunGitAsync(workspace, ["remote", "add", "origin", link.RemoteUrl], cancellationToken: token);
            else if (!RemoteEquals(remote.Output.Trim(), link.RemoteUrl))
                remoteResult = await git.RunGitAsync(workspace, ["remote", "set-url", "origin", link.RemoteUrl], cancellationToken: token);
            else
                remoteResult = new CommandResult(0, remote.Output);
            if (!remoteResult.Success)
                return ReceiveFailure("GitPet could not configure the standalone Get source.", remoteResult, workspace, link.RemoteUrl);

            var localHead = await git.RunGitAsync(workspace, ["rev-parse", "--verify", "HEAD"], cancellationToken: token);
            if (!localHead.Success)
            {
                var stageBaseline = await git.RunGitAsync(workspace, ["add", "-f", "-A"], TimeSpan.FromMinutes(1), token);
                if (!stageBaseline.Success)
                    return ReceiveFailure("GitPet could not stage the isolated Get baseline.", stageBaseline, workspace, link.RemoteUrl);

                var baselineCommit = await git.RunGitAsync(
                    workspace,
                    ["commit", "-m", $"baseline: {project.DisplayName} before standalone Get"],
                    TimeSpan.FromMinutes(1),
                    token);
                if (!baselineCommit.Success)
                    return ReceiveFailure("GitPet could not create the isolated Get baseline.", baselineCommit, workspace, link.RemoteUrl);
            }

            var fetch = await git.RunGitAsync(
                workspace,
                BuildFetchArguments(link.Branch, quiet: false),
                TimeSpan.FromMinutes(5),
                token);
            if (!fetch.Success)
                return ReceiveFailure("GitPet could not fetch the project-only online repository.", fetch, workspace, link.RemoteUrl);

            var remoteHead = await git.RunGitAsync(
                workspace,
                ["rev-parse", "--verify", RemoteTrackingRef(link.Branch)],
                TimeSpan.FromSeconds(12),
                token);
            if (!remoteHead.Success)
                return ReceiveFailure($"The standalone repository does not have a readable '{link.Branch}' branch.", remoteHead, workspace, link.RemoteUrl);

            var diff = await git.RunGitAsync(
                workspace,
                ["diff", "--name-status", "--find-renames", "HEAD", RemoteTrackingRef(link.Branch)],
                TimeSpan.FromSeconds(30),
                token);
            if (!diff.Success)
                return ReceiveFailure("GitPet could not compare the local project package with its online copy.", diff, workspace, link.RemoteUrl);

            var changes = ParseRemoteChanges(diff.Output);
            if (changes.Count == 0)
            {
                return new(true,
                    "The project-only online repository is already synchronized with this local project.",
                    0,
                    workspace,
                    link.RemoteUrl);
            }

            var escaped = changes
                .SelectMany(change => string.IsNullOrWhiteSpace(change.PreviousPath)
                    ? new[] { change.Path }
                    : new[] { change.PreviousPath, change.Path })
                .Where(path => !IsPathInsideProjectScope(config, repositoryRoot, path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (escaped.Length > 0)
            {
                await audit.WriteAsync("standalone_project_get_boundary_blocked", new
                {
                    projectId = project.Id,
                    project = project.DisplayName,
                    escapedPaths = escaped
                });
                return new(false,
                    "Get blocked: the standalone repository contains incoming changes outside this project's configured scope.\r\n\r\n" +
                    string.Join("\r\n", escaped),
                    WorkspacePath: workspace,
                    RemoteUrl: link.RemoteUrl);
            }

            var resetRemote = await git.RunGitAsync(
                workspace,
                ["reset", "--hard", RemoteTrackingRef(link.Branch)],
                TimeSpan.FromMinutes(1),
                token);
            if (!resetRemote.Success)
                return ReceiveFailure("GitPet could not materialize the online project snapshot in its isolated workspace.", resetRemote, workspace, link.RemoteUrl);
            var cleanRemote = await git.RunGitAsync(workspace, ["clean", "-fdx"], TimeSpan.FromMinutes(1), token);
            if (!cleanRemote.Success)
                return ReceiveFailure("GitPet could not clean the isolated online project snapshot.", cleanRemote, workspace, link.RemoteUrl);

            Directory.CreateDirectory(stagingRoot);
            foreach (var change in changes)
            {
                token.ThrowIfCancellationRequested();
                if (change.Status.StartsWith("D", StringComparison.OrdinalIgnoreCase)) continue;

                var remoteSource = ResolveInside(workspace, change.Path);
                if (!File.Exists(remoteSource))
                {
                    return new(false,
                        $"The online project change could not be materialized as a normal file:\r\n{change.Path}",
                        WorkspacePath: workspace,
                        RemoteUrl: link.RemoteUrl);
                }

                var staged = ResolveInside(stagingRoot, change.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(staged)!);
                File.Copy(remoteSource, staged, true);
            }

            foreach (var change in changes)
            {
                token.ThrowIfCancellationRequested();

                if (!string.IsNullOrWhiteSpace(change.PreviousPath) &&
                    !change.PreviousPath.Equals(change.Path, StringComparison.OrdinalIgnoreCase) &&
                    change.Status.StartsWith("R", StringComparison.OrdinalIgnoreCase))
                {
                    var previous = ResolveInside(repositoryRoot, change.PreviousPath);
                    if (File.Exists(previous)) File.Delete(previous);
                }

                if (change.Status.StartsWith("D", StringComparison.OrdinalIgnoreCase))
                {
                    var deleted = ResolveInside(repositoryRoot, change.Path);
                    if (File.Exists(deleted)) File.Delete(deleted);
                    continue;
                }

                var staged = ResolveInside(stagingRoot, change.Path);
                var destination = ResolveInside(repositoryRoot, change.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(staged, destination, true);
            }

            await audit.WriteAsync("standalone_project_received", new
            {
                projectId = project.Id,
                project = project.DisplayName,
                changedFileCount = changes.Count,
                remote = link.RepositoryLabel,
                branch = link.Branch
            });

            return new(true,
                $"Received {changes.Count} project-only change{(changes.Count == 1 ? "" : "s")} from {link.RepositoryLabel} / {link.Branch}.\r\n\r\n" +
                "They were copied only into this project's configured scope and remain UNSAVED locally so you can review them before Save.",
                changes.Count,
                workspace,
                link.RemoteUrl);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await audit.WriteAsync("standalone_project_get_error", new
            {
                projectId = project.Id,
                project = project.DisplayName,
                error = ex.Message
            });
            return new(false,
                "GitPet could not receive the standalone project safely.\r\n\r\n" + ex.Message,
                WorkspacePath: workspace,
                RemoteUrl: link.RemoteUrl);
        }
        finally
        {
            try
            {
                if (Directory.Exists(stagingRoot)) Directory.Delete(stagingRoot, true);
            }
            catch
            {
            }
        }
    }

    public static async Task<StandaloneProjectReconcileResult> ReconcileFilesAsync(
        AppConfig config,
        GitService git,
        IReadOnlyDictionary<string, ReconcileChoice> choices,
        CancellationToken token = default)
    {
        var project = config.GetActiveProject();
        var repositoryRoot = config.RepositoryPath;
        if (project is null || string.IsNullOrWhiteSpace(repositoryRoot) || !Directory.Exists(repositoryRoot))
            return new(false, "Choose a logical GitPet project before reconciling project files.");
        if (!IsLogicalProject(config, repositoryRoot))
            return new(false, "This project uses shared Git history, so standard reconciliation should be used instead.");

        var link = GetLink(config);
        if (link is null)
            return new(false, "Connect this GitPet project to its own online repository first.");

        var status = await git.GetStatusAsync(repositoryRoot, token);
        if (!status.Healthy)
            return new(false, "GitPet could not verify the project before file reconciliation.\r\n\r\n" + status.Error);
        if (status.Files.Count > 0)
            return new(false, "Save or discard current local changes before reconciling project-only online files.");

        var workspace = GetWorkspacePath(project, link.Branch);
        var gitDirectory = Path.Combine(workspace, ".git");
        if (!Directory.Exists(gitDirectory))
        {
            return new(false,
                "The standalone publishing workspace has no local baseline yet.\r\n\r\nUse Get first to establish the project-only baseline.",
                WorkspacePath: workspace,
                RemoteUrl: link.RemoteUrl);
        }

        var stagingRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ZomniverseGitPet",
            "Receiving",
            project.Id,
            "reconcile-" + Guid.NewGuid().ToString("N"));
        var backupRoot = stagingRoot + "-backup";

        using var workspaceLease = await EnterWorkspaceAsync(workspace, token);
        string? originalWorkspaceHead = null;

        try
        {
            var remote = await git.RunGitAsync(workspace, ["remote", "get-url", "origin"], cancellationToken: token);
            CommandResult remoteResult;
            if (!remote.Success || string.IsNullOrWhiteSpace(remote.Output))
                remoteResult = await git.RunGitAsync(workspace, ["remote", "add", "origin", link.RemoteUrl], cancellationToken: token);
            else if (!RemoteEquals(remote.Output.Trim(), link.RemoteUrl))
                remoteResult = await git.RunGitAsync(workspace, ["remote", "set-url", "origin", link.RemoteUrl], cancellationToken: token);
            else
                remoteResult = new CommandResult(0, remote.Output);

            if (!remoteResult.Success)
                return new(false, "GitPet could not configure the standalone reconciliation source.\r\n\r\n" + remoteResult.Output,
                    WorkspacePath: workspace, RemoteUrl: link.RemoteUrl);

            var head = await git.RunGitAsync(workspace, ["rev-parse", "--verify", "HEAD"], cancellationToken: token);
            if (!head.Success || string.IsNullOrWhiteSpace(head.Output))
                return new(false, "GitPet could not read the standalone reconciliation baseline.",
                    WorkspacePath: workspace, RemoteUrl: link.RemoteUrl);
            originalWorkspaceHead = head.Output.Trim();

            var fetch = await git.RunGitAsync(
                workspace,
                BuildFetchArguments(link.Branch, quiet: false),
                TimeSpan.FromMinutes(5),
                token);
            if (!fetch.Success)
                return new(false, "GitPet could not fetch the project-only online repository.\r\n\r\n" + fetch.Output,
                    WorkspacePath: workspace, RemoteUrl: link.RemoteUrl);

            var remoteRef = RemoteTrackingRef(link.Branch);
            var remoteHead = await git.RunGitAsync(
                workspace,
                ["rev-parse", "--verify", remoteRef],
                TimeSpan.FromSeconds(12),
                token);
            if (!remoteHead.Success)
                return new(false, $"The standalone repository does not have a readable '{link.Branch}' branch.",
                    WorkspacePath: workspace, RemoteUrl: link.RemoteUrl);

            var diff = await git.RunGitAsync(
                workspace,
                ["diff", "--name-status", "--find-renames", "HEAD", remoteRef],
                TimeSpan.FromSeconds(30),
                token);
            if (!diff.Success)
                return new(false, "GitPet could not compare the standalone baseline with the online project.\r\n\r\n" + diff.Output,
                    WorkspacePath: workspace, RemoteUrl: link.RemoteUrl);

            var changes = ParseRemoteChanges(diff.Output);
            if (changes.Count == 0)
                return new(true, "The project-only online repository no longer has incoming file changes.",
                    WorkspacePath: workspace, RemoteUrl: link.RemoteUrl);

            var escaped = changes
                .SelectMany(change => string.IsNullOrWhiteSpace(change.PreviousPath)
                    ? new[] { change.Path }
                    : new[] { change.PreviousPath, change.Path })
                .Where(path => !IsPathInsideProjectScope(config, repositoryRoot, path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (escaped.Length > 0)
            {
                return new(false,
                    "Reconciliation blocked: the standalone repository contains incoming changes outside this project's configured scope.\r\n\r\n" +
                    string.Join("\r\n", escaped),
                    WorkspacePath: workspace,
                    RemoteUrl: link.RemoteUrl);
            }

            var unresolved = changes
                .Select(change => change.Path)
                .Where(path => !choices.ContainsKey(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (unresolved.Length > 0)
            {
                return new(false,
                    "GitPet needs a local/online choice for every incoming standalone file.\r\n\r\n" +
                    string.Join("\r\n", unresolved),
                    WorkspacePath: workspace,
                    RemoteUrl: link.RemoteUrl);
            }

            var resetRemote = await git.RunGitAsync(
                workspace,
                ["reset", "--hard", remoteRef],
                TimeSpan.FromMinutes(1),
                token);
            if (!resetRemote.Success)
                return new(false, "GitPet could not materialize the standalone online snapshot.\r\n\r\n" + resetRemote.Output,
                    WorkspacePath: workspace, RemoteUrl: link.RemoteUrl);

            var cleanRemote = await git.RunGitAsync(
                workspace,
                ["clean", "-fdx"],
                TimeSpan.FromMinutes(1),
                token);
            if (!cleanRemote.Success)
                return new(false, "GitPet could not clean the standalone online snapshot.\r\n\r\n" + cleanRemote.Output,
                    WorkspacePath: workspace, RemoteUrl: link.RemoteUrl);

            Directory.CreateDirectory(stagingRoot);
            Directory.CreateDirectory(backupRoot);

            var affectedPaths = changes
                .SelectMany(change => string.IsNullOrWhiteSpace(change.PreviousPath)
                    ? new[] { change.Path }
                    : new[] { change.PreviousPath, change.Path })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var originallyMissing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var relative in affectedPaths)
            {
                token.ThrowIfCancellationRequested();
                var localPath = ResolveInside(repositoryRoot, relative);
                if (!File.Exists(localPath))
                {
                    originallyMissing.Add(relative);
                    continue;
                }

                var backupPath = ResolveInside(backupRoot, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
                File.Copy(localPath, backupPath, true);
            }

            foreach (var change in changes)
            {
                token.ThrowIfCancellationRequested();
                if (choices[change.Path] != ReconcileChoice.Online) continue;
                if (change.Status.StartsWith("D", StringComparison.OrdinalIgnoreCase)) continue;

                var remoteSource = ResolveInside(workspace, change.Path);
                if (!File.Exists(remoteSource))
                    throw new IOException("The selected online file could not be materialized: " + change.Path);

                var staged = ResolveInside(stagingRoot, change.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(staged)!);
                File.Copy(remoteSource, staged, true);
            }

            var appliedOnline = 0;
            var keptLocal = 0;

            try
            {
                foreach (var change in changes)
                {
                    token.ThrowIfCancellationRequested();

                    if (choices[change.Path] == ReconcileChoice.Local)
                    {
                        keptLocal++;
                        continue;
                    }

                    appliedOnline++;

                    if (!string.IsNullOrWhiteSpace(change.PreviousPath) &&
                        !change.PreviousPath.Equals(change.Path, StringComparison.OrdinalIgnoreCase) &&
                        change.Status.StartsWith("R", StringComparison.OrdinalIgnoreCase))
                    {
                        var previous = ResolveInside(repositoryRoot, change.PreviousPath);
                        if (File.Exists(previous)) File.Delete(previous);
                    }

                    var destination = ResolveInside(repositoryRoot, change.Path);
                    if (change.Status.StartsWith("D", StringComparison.OrdinalIgnoreCase))
                    {
                        if (File.Exists(destination)) File.Delete(destination);
                        continue;
                    }

                    var staged = ResolveInside(stagingRoot, change.Path);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    File.Copy(staged, destination, true);
                }
            }
            catch
            {
                foreach (var relative in affectedPaths)
                {
                    var localPath = ResolveInside(repositoryRoot, relative);
                    var backupPath = ResolveInside(backupRoot, relative);

                    if (File.Exists(backupPath))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
                        File.Copy(backupPath, localPath, true);
                    }
                    else if (originallyMissing.Contains(relative) && File.Exists(localPath))
                    {
                        File.Delete(localPath);
                    }
                }

                if (!string.IsNullOrWhiteSpace(originalWorkspaceHead))
                {
                    await git.RunGitAsync(
                        workspace,
                        ["reset", "--hard", originalWorkspaceHead],
                        TimeSpan.FromMinutes(1),
                        CancellationToken.None);
                }

                throw;
            }

            return new(true,
                $"Standalone file reconciliation prepared.\r\n\r\n" +
                $"Online versions applied: {appliedOnline}\r\n" +
                $"Local versions kept: {keptLocal}\r\n\r\n" +
                "No Git histories were merged. The resulting project files are local working-tree changes for Review and Save.",
                appliedOnline,
                keptLocal,
                workspace,
                link.RemoteUrl);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrWhiteSpace(originalWorkspaceHead) && Directory.Exists(gitDirectory))
            {
                try
                {
                    await git.RunGitAsync(
                        workspace,
                        ["reset", "--hard", originalWorkspaceHead],
                        TimeSpan.FromMinutes(1),
                        CancellationToken.None);
                }
                catch
                {
                }
            }

            return new(false,
                "GitPet could not reconcile the standalone project files safely.\r\n\r\n" + ex.Message,
                WorkspacePath: workspace,
                RemoteUrl: link.RemoteUrl);
        }
        finally
        {
            foreach (var temp in new[] { stagingRoot, backupRoot })
            {
                try
                {
                    if (Directory.Exists(temp)) Directory.Delete(temp, true);
                }
                catch
                {
                }
            }
        }
    }

    internal static IReadOnlyList<StandaloneProjectRemoteChange> ParseRemoteChanges(string output)
    {
        var changes = new List<StandaloneProjectRemoteChange>();
        foreach (var line in (output ?? "").Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.Split('\t');
            if (fields.Length < 2) continue;

            var status = fields[0].Trim();
            if (status.Length == 0) continue;

            var renameOrCopy = (status.StartsWith("R", StringComparison.OrdinalIgnoreCase) ||
                                status.StartsWith("C", StringComparison.OrdinalIgnoreCase)) &&
                               fields.Length >= 3;
            var previous = renameOrCopy ? NormalizeRelative(fields[1]) : "";
            var path = NormalizeRelative(renameOrCopy ? fields[2] : fields[1]);
            if (path.Length == 0) continue;

            changes.Add(new StandaloneProjectRemoteChange(status, path, previous));
        }
        return changes;
    }

    internal static bool IsPathInsideProjectScope(
        AppConfig config,
        string repositoryRoot,
        string relativePath)
    {
        var project = config.GetActiveProject();
        if (project is null || !PathEquals(project.RepositoryRoot, repositoryRoot)) return false;

        var candidate = NormalizeRelative(relativePath);
        if (candidate.Length == 0) return false;

        if (project.TrackEverything)
        {
            var projectRelative = LogicalProjectScopeRuntime.TryGetRelativePath(repositoryRoot, project.Path);
            if (string.IsNullOrWhiteSpace(projectRelative)) return false;
            var scope = NormalizeRelative(projectRelative);
            return candidate.Equals(scope, StringComparison.OrdinalIgnoreCase) ||
                   candidate.StartsWith(scope + "/", StringComparison.OrdinalIgnoreCase);
        }

        foreach (var entry in project.ScopeEntries ?? [])
        {
            var scope = NormalizeRelative(entry.RelativePath);
            if (scope.Length == 0) continue;
            if (entry.IsDirectory)
            {
                if (candidate.Equals(scope, StringComparison.OrdinalIgnoreCase) ||
                    candidate.StartsWith(scope + "/", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            else if (candidate.Equals(scope, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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
            var entries = JsonSerializer.Deserialize<List<StandaloneProjectPublishingEntry>>(json) ?? [];
            foreach (var entry in entries) NormalizeEntry(entry);
            return entries;
        }
        catch
        {
            return [];
        }
    }

    private static void SaveEntriesUnsafe(List<StandaloneProjectPublishingEntry> entries)
    {
        foreach (var entry in entries) NormalizeEntry(entry);
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

    private static StandaloneProjectPublishingEntry Clone(StandaloneProjectPublishingEntry entry)
    {
        NormalizeEntry(entry);
        return new StandaloneProjectPublishingEntry
        {
            ProjectId = entry.ProjectId,
            RemoteUrl = entry.RemoteUrl,
            RepositoryLabel = entry.RepositoryLabel,
            Branch = entry.Branch,
            BranchStates = entry.BranchStates.ToDictionary(
                pair => pair.Key,
                pair => new StandaloneProjectBranchState
                {
                    LastPublishedSourceCommit = pair.Value.LastPublishedSourceCommit,
                    LastPublishedFingerprint = pair.Value.LastPublishedFingerprint,
                    LastPublishedUtc = pair.Value.LastPublishedUtc
                },
                StringComparer.OrdinalIgnoreCase),
            LastPublishedSourceCommit = entry.LastPublishedSourceCommit,
            LastPublishedFingerprint = entry.LastPublishedFingerprint,
            LastPublishedUtc = entry.LastPublishedUtc
        };
    }

    internal static void NormalizeEntry(StandaloneProjectPublishingEntry entry)
    {
        entry.Branch = NormalizeBranchName(entry.Branch);
        if (!IsSafeBranchName(entry.Branch)) entry.Branch = "main";
        entry.BranchStates ??= new Dictionary<string, StandaloneProjectBranchState>(StringComparer.OrdinalIgnoreCase);

        if (entry.BranchStates.Comparer != StringComparer.OrdinalIgnoreCase)
            entry.BranchStates = new Dictionary<string, StandaloneProjectBranchState>(
                entry.BranchStates,
                StringComparer.OrdinalIgnoreCase);

        if (!entry.BranchStates.ContainsKey("main") &&
            (!string.IsNullOrWhiteSpace(entry.LastPublishedSourceCommit) ||
             !string.IsNullOrWhiteSpace(entry.LastPublishedFingerprint) ||
             entry.LastPublishedUtc is not null))
        {
            entry.BranchStates["main"] = new StandaloneProjectBranchState
            {
                LastPublishedSourceCommit = entry.LastPublishedSourceCommit,
                LastPublishedFingerprint = entry.LastPublishedFingerprint,
                LastPublishedUtc = entry.LastPublishedUtc
            };
        }

        ProjectActiveBranchState(entry);
    }

    internal static StandaloneProjectBranchState GetOrCreateBranchState(
        StandaloneProjectPublishingEntry entry,
        string branch)
    {
        var normalized = NormalizeBranchName(branch);
        if (!entry.BranchStates.TryGetValue(normalized, out var state))
        {
            state = new StandaloneProjectBranchState();
            entry.BranchStates[normalized] = state;
        }
        return state;
    }

    internal static void ProjectActiveBranchState(StandaloneProjectPublishingEntry entry)
    {
        var state = GetOrCreateBranchState(entry, entry.Branch);
        entry.LastPublishedSourceCommit = state.LastPublishedSourceCommit;
        entry.LastPublishedFingerprint = state.LastPublishedFingerprint;
        entry.LastPublishedUtc = state.LastPublishedUtc;
    }

    internal static async Task<IDisposable> EnterWorkspaceAsync(
        string workspace,
        CancellationToken token = default)
    {
        var key = Path.TrimEndingDirectorySeparator(Path.GetFullPath(workspace));
        var gate = WorkspaceGates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(token);
        return new WorkspaceLease(gate);
    }

    internal static void CleanPublishingWorkspace(string workspace)
    {
        /*
         * The isolated workspace is a disposable cache, but its top-level .git
         * directory is intentionally preserved between Send/Get operations.
         *
         * Git for Windows can leave metadata inside that preserved .git tree
         * with the ReadOnly bit set (for example objects/info/commit-graphs/
         * commit-graph-chain). A background remote inspection can also briefly
         * hold those files open. Workspace-level serialization prevents GitPet
         * operations from racing one another; bounded retries cover short-lived
         * Windows/AV handles that can outlive a process by a moment.
         */
        MakePublishingTreeWritable(workspace);

        foreach (var file in Directory.EnumerateFiles(workspace))
            DeleteFileWithRetry(file);

        foreach (var directory in Directory.EnumerateDirectories(workspace))
        {
            if (Path.GetFileName(directory).Equals(".git", StringComparison.OrdinalIgnoreCase))
                continue;

            DeleteDirectoryWithRetry(directory);
        }
    }

    private static void MakePublishingTreeWritable(string workspace)
    {
        var pending = new Stack<string>();
        pending.Push(workspace);

        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            ClearReadOnlyAttributeWithRetry(directory);

            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                var attributes = GetAttributesWithRetry(entry);
                var isDirectory = (attributes & FileAttributes.Directory) != 0;
                var isReparsePoint = (attributes & FileAttributes.ReparsePoint) != 0;

                if ((attributes & FileAttributes.ReadOnly) != 0)
                {
                    RetryPublishingIo(() =>
                        File.SetAttributes(entry, attributes & ~FileAttributes.ReadOnly));
                }

                if (isDirectory && !isReparsePoint)
                    pending.Push(entry);
            }
        }
    }

    private static FileAttributes GetAttributesWithRetry(string path)
    {
        FileAttributes attributes = default;
        RetryPublishingIo(() => attributes = File.GetAttributes(path));
        return attributes;
    }

    private static void ClearReadOnlyAttributeWithRetry(string path)
    {
        RetryPublishingIo(() =>
        {
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReadOnly) != 0)
                File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
        });
    }

    private static void DeleteFileWithRetry(string path) =>
        RetryPublishingIo(() => File.Delete(path));

    private static void DeleteDirectoryWithRetry(string path) =>
        RetryPublishingIo(() => Directory.Delete(path, true));

    private static void RetryPublishingIo(Action action)
    {
        const int maxAttempts = 7;

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch (Exception ex) when (
                attempt < maxAttempts &&
                (ex is IOException || ex is UnauthorizedAccessException))
            {
                Thread.Sleep(50 * attempt);
            }
        }
    }

    private sealed class WorkspaceLease : IDisposable
    {
        private SemaphoreSlim? _gate;

        public WorkspaceLease(SemaphoreSlim gate) => _gate = gate;

        public void Dispose()
        {
            var gate = Interlocked.Exchange(ref _gate, null);
            gate?.Release();
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

    private static StandaloneProjectReceiveResult ReceiveFailure(
        string prefix,
        CommandResult result,
        string workspace,
        string remoteUrl) =>
        new(false,
            prefix + (string.IsNullOrWhiteSpace(result.Output) ? "" : "\r\n\r\n" + result.Output),
            WorkspacePath: workspace,
            RemoteUrl: remoteUrl);

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
