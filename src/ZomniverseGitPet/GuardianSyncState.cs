namespace ZomniverseGitPet;

internal sealed record GuardianSyncSnapshot(
    bool HasRepository,
    string Branch,
    int Unsaved,
    int Ahead,
    int Behind,
    bool HasRemote,
    bool OnlineReachable,
    bool RemoteBranchExists,
    bool ReconciliationPending)
{
    public static readonly GuardianSyncSnapshot Empty = new(
        false, "?", 0, 0, 0, false, false, false, false);

    public bool Diverged => Ahead > 0 && Behind > 0;
}

internal static class GuardianSyncState
{
    private static readonly object Gate = new();
    private static readonly SemaphoreSlim RefreshGate = new(1, 1);
    private static AppConfig? _config;
    private static GitService? _git;
    private static GuardianSyncSnapshot _current = GuardianSyncSnapshot.Empty;

    public static event EventHandler? Changed;

    public static GuardianSyncSnapshot Current
    {
        get
        {
            lock (Gate) return _current;
        }
    }

    internal static AppConfig? Config => _config;
    internal static GitService? Git => _git;

    public static void Initialize(AppConfig config, GitService git)
    {
        _config = config;
        _git = git;
        Publish(GuardianSyncSnapshot.Empty);
    }

    public static void PublishLocal(RepositoryStatus status)
    {
        if (_config is null || string.IsNullOrWhiteSpace(_config.RepositoryPath)) return;

        var current = Current;
        var sameBranch = string.Equals(current.Branch, status.Branch, StringComparison.OrdinalIgnoreCase);
        var ahead = status.HasTrackingInformation
            ? status.Ahead
            : sameBranch ? current.Ahead : 0;
        var behind = status.HasTrackingInformation
            ? status.Behind
            : sameBranch ? current.Behind : 0;

        Publish(new GuardianSyncSnapshot(
            true,
            status.Branch,
            status.Files.Count,
            ahead,
            behind,
            sameBranch && current.HasRemote,
            sameBranch && current.OnlineReachable,
            sameBranch && current.RemoteBranchExists,
            sameBranch && current.ReconciliationPending));
    }

    public static async Task RefreshAsync(bool fetchRemote = true, CancellationToken token = default)
    {
        var config = _config;
        var git = _git;
        if (config is null || git is null) return;

        await RefreshGate.WaitAsync(token);
        try
        {
            var repositoryPath = config.RepositoryPath;
            if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
            {
                Publish(GuardianSyncSnapshot.Empty);
                return;
            }

            var status = await git.GetStatusAsync(repositoryPath, token);
            if (!status.Healthy)
            {
                Publish(new GuardianSyncSnapshot(
                    true, status.Branch, 0, 0, 0, false, false, false, false));
                return;
            }

            /* ==========================================================================
               PATCH: LOGICAL PROJECT REMOTE BOUNDARY
               DATE.TIME: 2026-09-11 20:24 +03:00
               Never treat the parent repository remote as project publishing.
               ========================================================================== */
            if (StandaloneProjectPublishing.IsLogicalProject(config, repositoryPath))
            {
                var link = StandaloneProjectPublishing.GetLink(config);
                if (link is null)
                {
                    Publish(new GuardianSyncSnapshot(
                        true,
                        status.Branch,
                        status.Files.Count,
                        0,
                        0,
                        false,
                        false,
                        false,
                        false));
                    return;
                }

                var pending = await StandaloneProjectPublishing.HasPendingPublishAsync(
                    config,
                    git,
                    repositoryPath,
                    token);

                Publish(new GuardianSyncSnapshot(
                    true,
                    status.Branch,
                    status.Files.Count,
                    pending ? 1 : 0,
                    0,
                    true,
                    true,
                    !string.IsNullOrWhiteSpace(link.LastPublishedFingerprint),
                    false));
                return;
            }

            var mergeHead = await git.RunGitAsync(
                repositoryPath,
                ["rev-parse", "--verify", "-q", "MERGE_HEAD"],
                TimeSpan.FromSeconds(8), token);
            var reconciliationPending = mergeHead.Success && !string.IsNullOrWhiteSpace(mergeHead.Output);

            var origin = await git.RunGitAsync(
                repositoryPath,
                ["remote", "get-url", "origin"],
                TimeSpan.FromSeconds(8), token);
            if (!origin.Success || string.IsNullOrWhiteSpace(origin.Output))
            {
                Publish(new GuardianSyncSnapshot(
                    true,
                    status.Branch,
                    status.Files.Count,
                    status.HasTrackingInformation ? status.Ahead : 0,
                    status.HasTrackingInformation ? status.Behind : 0,
                    false,
                    false,
                    false,
                    reconciliationPending));
                return;
            }

            if (reconciliationPending)
            {
                var previous = Current;
                Publish(new GuardianSyncSnapshot(
                    true,
                    status.Branch,
                    status.Files.Count,
                    status.HasTrackingInformation ? status.Ahead : previous.Ahead,
                    status.HasTrackingInformation ? status.Behind : previous.Behind,
                    true,
                    previous.OnlineReachable,
                    previous.RemoteBranchExists,
                    true));
                return;
            }

            var branch = status.Branch;
            if (string.IsNullOrWhiteSpace(branch) || branch == "?" || branch.StartsWith("(", StringComparison.Ordinal))
            {
                Publish(new GuardianSyncSnapshot(
                    true, branch, status.Files.Count, 0, 0, true, false, false, false));
                return;
            }

            if (fetchRemote)
            {
                var fetch = await git.RunGitAsync(
                    repositoryPath,
                    ["fetch", "--quiet", "origin"],
                    TimeSpan.FromSeconds(25), token);
                if (!fetch.Success)
                {
                    var previous = Current;
                    Publish(new GuardianSyncSnapshot(
                        true,
                        branch,
                        status.Files.Count,
                        status.HasTrackingInformation ? status.Ahead : previous.Ahead,
                        status.HasTrackingInformation ? status.Behind : previous.Behind,
                        true,
                        false,
                        previous.RemoteBranchExists,
                        false));
                    return;
                }
            }

            var remoteRef = $"refs/remotes/origin/{branch}";
            var remoteBranch = await git.RunGitAsync(
                repositoryPath,
                ["rev-parse", "--verify", "-q", remoteRef],
                TimeSpan.FromSeconds(8), token);

            if (!remoteBranch.Success)
            {
                var count = await git.RunGitAsync(
                    repositoryPath,
                    ["rev-list", "--count", "HEAD"],
                    TimeSpan.FromSeconds(10), token);
                var ahead = count.Success && int.TryParse(count.Output.Trim(), out var parsed)
                    ? Math.Max(0, parsed)
                    : 0;

                Publish(new GuardianSyncSnapshot(
                    true, branch, status.Files.Count, ahead, 0, true, true, false, false));
                return;
            }

            var divergence = await git.RunGitAsync(
                repositoryPath,
                ["rev-list", "--left-right", "--count", $"HEAD...origin/{branch}"],
                TimeSpan.FromSeconds(12), token);
            var (aheadCount, behindCount) = ParseDivergence(divergence);

            Publish(new GuardianSyncSnapshot(
                true,
                branch,
                status.Files.Count,
                aheadCount,
                behindCount,
                true,
                divergence.Success,
                true,
                false));
        }
        finally
        {
            RefreshGate.Release();
        }
    }

    /* ==========================================================================
       PATCH: FRIENDLY PROJECT CONNECTION FLOW
       DATE.TIME: 2026-09-11 18:15 +03:00
       Separate GitHub identity from the repository's online home.
       ========================================================================== */
    public static async Task ConnectOriginAsync(Form? owner)
    {
        var config = _config;
        var git = _git;
        if (config is null || git is null || string.IsNullOrWhiteSpace(config.RepositoryPath)) return;

        var repositoryPath = config.RepositoryPath;
        var standalone = StandaloneProjectPublishing.IsLogicalProject(config, repositoryPath);
        if (standalone)
        {
            var existingLink = StandaloneProjectPublishing.GetLink(config);
            if (existingLink is not null)
            {
                await RefreshAsync(true);
                if (owner is GuardianForm existingGuardian) await existingGuardian.RefreshAsync();
                return;
            }
        }
        else
        {
            var existing = await git.RunGitAsync(
                repositoryPath,
                ["remote", "get-url", "origin"],
                TimeSpan.FromSeconds(8));
            if (existing.Success && !string.IsNullOrWhiteSpace(existing.Output))
            {
                await RefreshAsync(true);
                if (owner is GuardianForm existingGuardian) await existingGuardian.RefreshAsync();
                return;
            }
        }

        var github = new GitHubAccountService(new AuditLog());
        var account = await github.GetStatusAsync();
        var projectName = LogicalProjectScopeRuntime.DisplayName;
        var projectPath = LogicalProjectScopeRuntime.GetWorkingDirectory(repositoryPath);
        string remoteUrl;

        if (account.Authenticated && !string.IsNullOrWhiteSpace(account.Login))
        {
            var pet = Application.OpenForms
                .OfType<PetForm>()
                .FirstOrDefault(form => form.Visible && !form.IsDisposed);
            pet?.BeginGuidanceHold(standalone
                ? "📦 PROJECT-ONLY HOME\nOnly this scope will publish"
                : "🏠 ONLINE HOME\nLet's connect this project");

            try
            {
                using var wizard = new RepositoryConnectionWizardForm(
                    projectName,
                    projectPath,
                    account,
                    github,
                    standalonePublishing: standalone);
                if (wizard.ShowDialog(owner) != DialogResult.OK || string.IsNullOrWhiteSpace(wizard.RemoteUrl))
                    return;
                remoteUrl = wizard.RemoteUrl;
            }
            finally
            {
                pet?.EndGuidanceHold();
            }
        }
        else if (standalone)
        {
            using var setup = new RemoteSetupForm(projectName);
            if (setup.ShowDialog(owner) != DialogResult.OK || string.IsNullOrWhiteSpace(setup.RemoteUrl)) return;
            remoteUrl = setup.RemoteUrl;
        }
        else
        {
            var result = await git.GetOriginUrlAsync(repositoryPath);
            if (!result.Success || string.IsNullOrWhiteSpace(result.Output)) return;
            await RefreshAsync(true);
            if (owner is GuardianForm guardian) await guardian.RefreshAsync();
            return;
        }

        if (standalone)
        {
            StandaloneProjectPublishing.SetLink(config, remoteUrl);

            // Migrate the connection created by older GitPet builds only when it points
            // to this exact logical-project destination. The parent repository must not
            // retain the same origin or a later whole-repository push could leak siblings.
            var legacyOrigin = await git.RunGitAsync(
                repositoryPath,
                ["remote", "get-url", "origin"],
                TimeSpan.FromSeconds(8));
            var removedLegacyOrigin = false;
            if (legacyOrigin.Success &&
                StandaloneProjectPublishing.RemoteEquals(legacyOrigin.Output, remoteUrl))
            {
                var removed = await git.RunGitAsync(
                    repositoryPath,
                    ["remote", "remove", "origin"],
                    TimeSpan.FromSeconds(12));
                removedLegacyOrigin = removed.Success;
            }

            using var connected = new GuardianConfirmDialog(
                "Connect project",
                "PROJECT-ONLY HOME READY  ✓",
                $"{projectName} will publish only its selected GitPet scope to:\r\n{remoteUrl}\r\n\r\n" +
                "The larger parent repository will not be sent.\r\n" +
                "Nothing was downloaded or published automatically." +
                (removedLegacyOrigin
                    ? "\r\n\r\nGitPet also removed the old matching origin from the shared parent repository."
                    : ""),
                "OK",
                showCancel: false);
            connected.ShowDialog(owner);
        }
        else
        {
            var result = await git.AddOriginRemoteAsync(repositoryPath, remoteUrl);
            if (!result.Success)
            {
                using var problem = new GuardianConfirmDialog(
                    "Connect project",
                    "CONNECTION NEEDS ATTENTION",
                    result.Output,
                    "OK",
                    showCancel: false);
                problem.ShowDialog(owner);
                return;
            }

            using var connected = new GuardianConfirmDialog(
                "Connect project",
                "PROJECT CONNECTED  ✓",
                $"{projectName} now has an online repository:\r\n{remoteUrl}\r\n\r\n" +
                "Nothing was downloaded or sent automatically.\r\n" +
                "Get ↓ and Send ↑ remain under your control.",
                "OK",
                showCancel: false);
            connected.ShowDialog(owner);
        }

        await RefreshAsync(true);
        if (owner is GuardianForm refreshedGuardian) await refreshedGuardian.RefreshAsync();
    }

    internal static void PublishReconciliationPending(RepositoryStatus status)
    {
        var previous = Current;
        Publish(new GuardianSyncSnapshot(
            true,
            status.Branch,
            status.Files.Count,
            previous.Ahead,
            previous.Behind,
            true,
            previous.OnlineReachable,
            previous.RemoteBranchExists,
            true));
    }

    private static (int Ahead, int Behind) ParseDivergence(CommandResult result)
    {
        if (!result.Success || string.IsNullOrWhiteSpace(result.Output)) return (0, 0);
        var fields = result.Output
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (fields.Length < 2 || !int.TryParse(fields[0], out var ahead) || !int.TryParse(fields[1], out var behind))
            return (0, 0);
        return (Math.Max(0, ahead), Math.Max(0, behind));
    }

    private static void Publish(GuardianSyncSnapshot snapshot)
    {
        lock (Gate) _current = snapshot;
        Changed?.Invoke(null, EventArgs.Empty);
    }
}
