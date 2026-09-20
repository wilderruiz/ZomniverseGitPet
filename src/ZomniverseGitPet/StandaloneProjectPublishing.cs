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

        var remoteInspection = await InspectRemoteAsync(config, git, repositoryRoot, true, token);
        if (remoteInspection.OnlineReachable &&
            remoteInspection.RemoteBranchExists &&
            string.IsNullOrWhiteSpace(link.LastPublishedFingerprint))
        {
            return new(false,
                "This project-only online repository already has a main branch, but this local GitPet project has no publishing baseline yet.\r\n\r\n" +
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

        var workspace = GetWorkspacePath(project);
        var gitDirectory = Path.Combine(workspace, ".git");
        if (!Directory.Exists(gitDirectory))
        {
            var probe = await git.RunGitAsync(
                repositoryRoot,
                ["ls-remote", "--heads", link.RemoteUrl, "refs/heads/main"],
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
                ["fetch", "--quiet", "--prune", "origin", "main"],
                TimeSpan.FromMinutes(2),
                token);
            if (!fetch.Success)
                return new(false, false, [], fetch.Output);
        }

        var remoteHead = await git.RunGitAsync(
            workspace,
            ["rev-parse", "--verify", "-q", "refs/remotes/origin/main"],
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
            ["diff", "--name-status", "--find-renames", "HEAD", "refs/remotes/origin/main"],
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

        var workspace = GetWorkspacePath(project);
        var stagingRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ZomniverseGitPet",
            "Receiving",
            project.Id,
            Guid.NewGuid().ToString("N"));

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
                ["fetch", "--prune", "origin", "main"],
                TimeSpan.FromMinutes(5),
                token);
            if (!fetch.Success)
                return ReceiveFailure("GitPet could not fetch the project-only online repository.", fetch, workspace, link.RemoteUrl);

            var remoteHead = await git.RunGitAsync(
                workspace,
                ["rev-parse", "--verify", "refs/remotes/origin/main"],
                TimeSpan.FromSeconds(12),
                token);
            if (!remoteHead.Success)
                return ReceiveFailure("The standalone repository does not have a readable main branch.", remoteHead, workspace, link.RemoteUrl);

            var diff = await git.RunGitAsync(
                workspace,
                ["diff", "--name-status", "--find-renames", "HEAD", "refs/remotes/origin/main"],
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
                ["reset", "--hard", "refs/remotes/origin/main"],
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
                remote = link.RepositoryLabel
            });

            return new(true,
                $"Received {changes.Count} project-only change{(changes.Count == 1 ? "" : "s")} from {link.RepositoryLabel}.\r\n\r\n" +
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
