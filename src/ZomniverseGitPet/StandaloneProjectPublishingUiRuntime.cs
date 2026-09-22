namespace ZomniverseGitPet;

/* ========================================================================== 
   PATCH: PROJECT-ONLY SEND ROUTER
   DATE.TIME: 2026-09-11 20:28 +03:00
   Prevent logical projects from pushing the parent repository.
   ========================================================================== */
internal static class StandaloneProjectPublishingUiRuntime
{
    private static AppConfig? _config;
    private static GitService? _git;
    private static AuditLog? _audit;
    private static System.Windows.Forms.Timer? _timer;
    private static bool _tickRunning;
    private static readonly Dictionary<GuardianForm, GuardianActionButton> SendButtons = [];
    private static readonly Dictionary<GuardianForm, GuardianActionButton> GetButtons = [];
    private static readonly Dictionary<GuardianForm, GuardianActionButton> BranchButtons = [];

    public static void Initialize(AppConfig config, GitService git, AuditLog audit)
    {
        if (_timer is not null) return;
        _config = config;
        _git = git;
        _audit = audit;

        _timer = new System.Windows.Forms.Timer { Interval = 650 };
        _timer.Tick += async (_, _) => await TickAsync();
        _timer.Start();
        GuardianSyncState.Changed += OnSyncChanged;
        Application.ApplicationExit += (_, _) =>
        {
            GuardianSyncState.Changed -= OnSyncChanged;
            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;
            SendButtons.Clear();
            GetButtons.Clear();
            BranchButtons.Clear();
        };
    }

    private static void OnSyncChanged(object? sender, EventArgs e)
    {
        foreach (var guardian in SendButtons.Keys
                     .Concat(GetButtons.Keys)
                     .Concat(BranchButtons.Keys)
                     .Distinct()
                     .ToArray())
            UpdateButton(guardian);
    }

    private static Task TickAsync()
    {
        if (_tickRunning || _config is null || _git is null || _audit is null) return Task.CompletedTask;
        _tickRunning = true;
        try
        {
            var guardians = Application.OpenForms
                .OfType<GuardianForm>()
                .Where(form => !form.IsDisposed)
                .ToArray();

            foreach (var stale in SendButtons.Keys.Where(form => !guardians.Contains(form)).ToArray())
                SendButtons.Remove(stale);
            foreach (var stale in GetButtons.Keys.Where(form => !guardians.Contains(form)).ToArray())
                GetButtons.Remove(stale);
            foreach (var stale in BranchButtons.Keys.Where(form => !guardians.Contains(form)).ToArray())
                BranchButtons.Remove(stale);

            foreach (var guardian in guardians)
            {
                EnsureButton(guardian);
                UpdateButton(guardian);
            }
        }
        finally
        {
            _tickRunning = false;
        }
        return Task.CompletedTask;
    }

    private static void EnsureButton(GuardianForm guardian)
    {
        if (SendButtons.ContainsKey(guardian) &&
            GetButtons.ContainsKey(guardian) &&
            BranchButtons.ContainsKey(guardian)) return;

        var toolbar = FindToolbar(guardian);
        if (toolbar is null) return;

        if (!BranchButtons.ContainsKey(guardian))
        {
            var originalGet = FindOriginalGet(toolbar);
            if (originalGet is not null)
            {
                var index = toolbar.Controls.GetChildIndex(originalGet);
                var branchButton = new GuardianActionButton
                {
                    Name = "StandaloneProjectBranchButton",
                    Text = "Branch: main ▾",
                    Width = 190,
                    Kind = GuardianActionKind.Standard,
                    SyncStateAware = false,
                    Visible = false,
                    Enabled = false,
                    Margin = originalGet.Margin
                };
                branchButton.Click += async (_, _) => await SelectBranchAsync(guardian);
                toolbar.Controls.Add(branchButton);
                toolbar.Controls.SetChildIndex(branchButton, index);
                BranchButtons[guardian] = branchButton;
            }
        }

        if (!GetButtons.ContainsKey(guardian))
        {
            var originalGet = FindOriginalGet(toolbar);
            if (originalGet is not null)
            {
                var index = toolbar.Controls.GetChildIndex(originalGet);
                var getButton = new GuardianActionButton
                {
                    Name = "StandaloneProjectGetButton",
                    Text = "Get ↓",
                    Width = 92,
                    Kind = GuardianActionKind.Pull,
                    SyncStateAware = false,
                    Visible = false,
                    Enabled = false,
                    Margin = originalGet.Margin
                };
                getButton.Click += async (_, _) => await ReceiveAsync(guardian);
                toolbar.Controls.Add(getButton);
                toolbar.Controls.SetChildIndex(getButton, index);
                GetButtons[guardian] = getButton;
            }
        }

        if (!SendButtons.ContainsKey(guardian))
        {
            var originalSend = FindOriginalSend(toolbar);
            if (originalSend is not null)
            {
                var index = toolbar.Controls.GetChildIndex(originalSend);
                var sendButton = new GuardianActionButton
                {
                    Name = "StandaloneProjectSendButton",
                    Text = "Send ↑",
                    Width = 92,
                    Kind = GuardianActionKind.Push,
                    SyncStateAware = false,
                    Visible = false,
                    Enabled = false,
                    Margin = originalSend.Margin
                };
                sendButton.Click += async (_, _) => await PublishAsync(guardian);
                toolbar.Controls.Add(sendButton);
                toolbar.Controls.SetChildIndex(sendButton, index);
                SendButtons[guardian] = sendButton;
            }
        }

        guardian.Disposed += (_, _) =>
        {
            SendButtons.Remove(guardian);
            GetButtons.Remove(guardian);
            BranchButtons.Remove(guardian);
        };
    }

    private static void UpdateButton(GuardianForm guardian)
    {
        if (_config is null) return;
        var toolbar = FindToolbar(guardian);
        if (toolbar is null) return;

        var originalSend = FindOriginalSend(toolbar);
        var originalGet = FindOriginalGet(toolbar);
        SendButtons.TryGetValue(guardian, out var standaloneSend);
        GetButtons.TryGetValue(guardian, out var standaloneGet);
        BranchButtons.TryGetValue(guardian, out var branchButton);
        if (originalSend is null || originalGet is null ||
            standaloneSend is null || standaloneGet is null || branchButton is null) return;

        var repositoryRoot = _config.RepositoryPath;
        var logical = !string.IsNullOrWhiteSpace(repositoryRoot) &&
                      StandaloneProjectPublishing.IsLogicalProject(_config, repositoryRoot);

        originalSend.Visible = !logical;
        originalGet.Visible = !logical;
        standaloneSend.Visible = logical;
        standaloneGet.Visible = logical;
        branchButton.Visible = logical;
        if (!logical) return;

        var snapshot = GuardianSyncState.Current;
        var link = StandaloneProjectPublishing.GetLink(_config);
        var linked = link is not null;
        var noLocalBaseline = link is not null && string.IsNullOrWhiteSpace(link.LastPublishedFingerprint);
        var onlineMode = _config.ConnectionMode != GitPetConnectionModes.LocalGitOnly;
        var operationRunning = OperationInProgress(guardian);

        branchButton.Text = linked
            ? $"Branch: {link!.Branch} ▾"
            : "Branch: —";
        branchButton.Width = Math.Clamp(TextRenderer.MeasureText(branchButton.Text, branchButton.Font).Width + 34, 150, 280);
        branchButton.Enabled = onlineMode &&
                               linked &&
                               snapshot.HasRepository &&
                               snapshot.Unsaved == 0 &&
                               snapshot.Ahead == 0 &&
                               snapshot.Behind == 0 &&
                               !snapshot.ReconciliationPending &&
                               !operationRunning;
        branchButton.Cursor = branchButton.Enabled ? Cursors.Hand : Cursors.Default;

        var reconcile = ShouldOfferReconcile(
            snapshot,
            onlineMode,
            linked,
            operationRunning);
        standaloneGet.Text = reconcile ? "Reconcile ↕" : "Get ↓";
        standaloneGet.Width = reconcile ? 120 : 92;
        standaloneGet.Enabled = reconcile || ShouldEnableGet(
            snapshot,
            onlineMode,
            linked,
            noLocalBaseline,
            operationRunning);
        standaloneGet.Cursor = standaloneGet.Enabled ? Cursors.Hand : Cursors.Default;

        standaloneSend.Text = "Send ↑";
        standaloneSend.Width = 92;
        standaloneSend.Enabled = onlineMode &&
                                 linked &&
                                 snapshot.HasRepository &&
                                 snapshot.HasRemote &&
                                 snapshot.OnlineReachable &&
                                 snapshot.Unsaved == 0 &&
                                 snapshot.Ahead > 0 &&
                                 snapshot.Behind == 0 &&
                                 !operationRunning;
        standaloneSend.Cursor = standaloneSend.Enabled ? Cursors.Hand : Cursors.Default;
    }

    internal static bool ShouldOfferReconcile(
        GuardianSyncSnapshot snapshot,
        bool onlineMode,
        bool linked,
        bool operationRunning) =>
        onlineMode &&
        linked &&
        snapshot.HasRepository &&
        snapshot.OnlineReachable &&
        snapshot.Unsaved == 0 &&
        snapshot.Ahead > 0 &&
        snapshot.Behind > 0 &&
        !snapshot.ReconciliationPending &&
        !operationRunning;

    internal static bool ShouldEnableGet(
        GuardianSyncSnapshot snapshot,
        bool onlineMode,
        bool linked,
        bool noLocalBaseline,
        bool operationRunning) =>
        onlineMode &&
        linked &&
        snapshot.HasRepository &&
        snapshot.OnlineReachable &&
        snapshot.Unsaved == 0 &&
        (snapshot.Ahead == 0 || noLocalBaseline) &&
        (snapshot.Behind > 0 || (noLocalBaseline && snapshot.RemoteBranchExists)) &&
        !operationRunning;

    private static async Task SelectBranchAsync(GuardianForm guardian)
    {
        var config = _config;
        var git = _git;
        var audit = _audit;
        if (config is null || git is null || audit is null ||
            string.IsNullOrWhiteSpace(config.RepositoryPath)) return;
        if (!StandaloneProjectPublishing.IsLogicalProject(config, config.RepositoryPath)) return;

        var link = StandaloneProjectPublishing.GetLink(config);
        if (link is null)
        {
            using var missing = new GuardianConfirmDialog(
                "Project branch",
                "CONNECT PROJECT FIRST",
                "Connect this logical project to its standalone online repository before choosing a branch.",
                "OK",
                showCancel: false);
            missing.ShowDialog(guardian);
            return;
        }

        var snapshot = GuardianSyncState.Current;
        var operationRunning = OperationInProgress(guardian);
        if (operationRunning ||
            snapshot.Unsaved > 0 ||
            snapshot.Ahead > 0 ||
            snapshot.Behind > 0 ||
            snapshot.ReconciliationPending)
        {
            var reasons = new List<string>();
            if (operationRunning) reasons.Add("another GitPet operation is still running");
            if (snapshot.Unsaved > 0) reasons.Add("unsaved project changes are waiting for review");
            if (snapshot.Ahead > 0) reasons.Add("saved project updates are waiting to Send");
            if (snapshot.Behind > 0) reasons.Add("online project updates are waiting to Get");
            if (snapshot.ReconciliationPending) reasons.Add("the project is in reconciliation");

            using var blocked = new GuardianConfirmDialog(
                "Project branch",
                "FINISH CURRENT PROJECT STATE FIRST",
                "GitPet will not switch the standalone branch while this logical project has unresolved state.\r\n\r\n" +
                string.Join("\r\n", reasons.Select(reason => "• " + reason)) +
                "\r\n\r\nThe parent repository branch has not been changed.",
                "OK",
                showCancel: false,
                dialogSize: new Size(720, 470));
            blocked.ShowDialog(guardian);
            return;
        }

        guardian.UseWaitCursor = true;
        IReadOnlyList<string> branches;
        try
        {
            branches = await StandaloneProjectPublishing.ListRemoteBranchesAsync(
                config,
                git,
                config.RepositoryPath);
        }
        finally
        {
            guardian.UseWaitCursor = false;
        }

        if (branches.Count == 0)
        {
            using var empty = new GuardianConfirmDialog(
                "Project branch",
                "NO REMOTE BRANCHES FOUND",
                $"GitPet could not find any existing branches at {link.RepositoryLabel}.\r\n\r\n" +
                "Refresh the GitHub connection or create the branch online first. This selector never creates or force-updates branches.",
                "OK",
                showCancel: false);
            empty.ShowDialog(guardian);
            return;
        }

        using var selector = new StandaloneProjectBranchForm(
            link.RepositoryLabel,
            link.Branch,
            branches);
        if (selector.ShowDialog(guardian) != DialogResult.OK) return;

        var selected = selector.SelectedBranch;
        if (string.IsNullOrWhiteSpace(selected) ||
            selected.Equals(link.Branch, StringComparison.OrdinalIgnoreCase))
            return;

        using var confirmation = new GuardianConfirmDialog(
            "Project branch",
            "SWITCH STANDALONE PROJECT BRANCH",
            $"Change only this logical project's standalone remote branch?\r\n\r\n" +
            $"{link.RepositoryLabel}\r\n" +
            $"{link.Branch}  →  {selected}\r\n\r\n" +
            "The parent repository branch and history stay exactly where they are. " +
            "GitPet will re-check GET and SEND state against the selected standalone branch.",
            "Use branch",
            "Cancel",
            confirmWidth: 140,
            dialogSize: new Size(760, 470));
        if (confirmation.ShowDialog(guardian) != DialogResult.Yes) return;

        StandaloneProjectPublishing.SetBranch(config, selected);
        await audit.WriteAsync("standalone_project_branch_changed", new
        {
            projectId = config.GetActiveProject()?.Id,
            project = config.GetActiveProject()?.DisplayName,
            remote = link.RepositoryLabel,
            from = link.Branch,
            to = selected
        });

        try { await GuardianSyncState.RefreshAsync(true); } catch { }
        try { await guardian.RefreshAsync(); } catch { }
        try { await GuardianWorkboardRuntime.RefreshNowAsync(); } catch { }
        UpdateButton(guardian);

        var pet = Application.OpenForms
            .OfType<PetForm>()
            .FirstOrDefault(form => form.Visible && !form.IsDisposed);
        pet?.ShowGuidance($"⑂ PROJECT BRANCH\n{selected}");
    }

    private static async Task ReceiveAsync(GuardianForm guardian)
    {
        var config = _config;
        var git = _git;
        var audit = _audit;
        if (config is null || git is null || audit is null || string.IsNullOrWhiteSpace(config.RepositoryPath)) return;
        if (!StandaloneProjectPublishing.IsLogicalProject(config, config.RepositoryPath)) return;

        if (config.ConnectionMode == GitPetConnectionModes.LocalGitOnly)
        {
            using var localOnly = new GuardianConfirmDialog(
                "Get project",
                "LOCAL GIT MODE",
                "GitPet is currently keeping this project local. Switch the GitHub connection back on before getting project-only updates.",
                "OK",
                showCancel: false);
            localOnly.ShowDialog(guardian);
            return;
        }

        var project = config.GetActiveProject();
        var link = StandaloneProjectPublishing.GetLink(config);
        if (project is null || link is null)
        {
            await GuardianSyncState.ConnectOriginAsync(guardian);
            return;
        }

        var snapshot = GuardianSyncState.Current;
        if (snapshot.Unsaved > 0)
        {
            using var saveFirst = new GuardianConfirmDialog(
                "Get project",
                "SAVE OR DISCARD LOCAL CHANGES FIRST",
                "GitPet found local changes inside this logical project's selected scope.\r\n\r\n" +
                "Review and Save them, or discard them intentionally, before getting the standalone online copy.",
                "OK",
                showCancel: false);
            saveFirst.ShowDialog(guardian);
            return;
        }

        if (ShouldOfferReconcile(
                snapshot,
                onlineMode: true,
                linked: true,
                operationRunning: OperationInProgress(guardian)))
        {
            await GuardianReconciliation.BeginAsync(guardian);
            try { await GuardianSyncState.RefreshAsync(true); } catch { }
            try { await guardian.RefreshAsync(); } catch { }
            try { await GuardianWorkboardRuntime.RefreshNowAsync(); } catch { }
            UpdateButton(guardian);
            return;
        }

        if (snapshot.Ahead > 0 && !string.IsNullOrWhiteSpace(link.LastPublishedFingerprint))
        {
            using var sendFirst = new GuardianConfirmDialog(
                "Get project",
                "SEND SAVED PROJECT UPDATES FIRST",
                "This project has saved local updates that have not been sent to its project-only online home.\r\n\r\n" +
                "Send those updates first, then Get. GitPet will not guess how to combine independent local and standalone histories.",
                "OK",
                showCancel: false);
            sendFirst.ShowDialog(guardian);
            return;
        }

        using var confirmation = new GuardianConfirmDialog(
            "Get project",
            "GET PROJECT SCOPE ONLY",
            $"Check {link.RepositoryLabel} / {link.Branch} for project-only updates?\r\n\r\n" +
            "GitPet will fetch into its isolated project workspace, verify every changed path is inside this project's configured scope, " +
            "then copy only those project files into the local working tree.\r\n\r\n" +
            "Incoming files will remain UNSAVED so you can review them before Save. The parent repository history will not be pulled.",
            "Get project ↓",
            "Cancel",
            dialogSize: new Size(800, 560),
            resizable: true,
            scrollable: true,
            confirmWidth: 160);
        if (confirmation.ShowDialog(guardian) != DialogResult.Yes) return;

        var pet = Application.OpenForms
            .OfType<PetForm>()
            .FirstOrDefault(form => form.Visible && !form.IsDisposed);
        pet?.BeginGuidanceHold("↓ GETTING PROJECT\nChecking project-only remote");

        var toolbar = FindToolbar(guardian);
        if (toolbar is not null) toolbar.Enabled = false;
        guardian.UseWaitCursor = true;
        try
        {
            var result = await StandaloneProjectPublishing.ReceiveAsync(config, git, audit);
            using var done = new GuardianConfirmDialog(
                "Get project",
                result.Success ? (result.ChangedFileCount > 0 ? "PROJECT UPDATES RECEIVED  ✓" : "PROJECT ALREADY CURRENT  ✓") : "GET NEEDS ATTENTION",
                result.Message,
                "OK",
                showCancel: false,
                dialogSize: result.Success ? new Size(760, 460) : new Size(760, 520));
            done.ShowDialog(guardian);

            pet?.ShowGuidance(result.Success
                ? result.ChangedFileCount > 0
                    ? $"↓ {result.ChangedFileCount} PROJECT CHANGE{(result.ChangedFileCount == 1 ? "" : "S")}\nReview before Save"
                    : "✓ PROJECT CURRENT\nNothing new to Get"
                : "⚠ GET NEEDS HELP\nNo parent history was pulled");
        }
        finally
        {
            guardian.UseWaitCursor = false;
            if (toolbar is not null) toolbar.Enabled = true;
            pet?.EndGuidanceHold();
        }

        try { await GuardianSyncState.RefreshAsync(true); } catch { }
        try { await guardian.RefreshAsync(); } catch { }
        try { await GuardianWorkboardRuntime.RefreshNowAsync(); } catch { }
        UpdateButton(guardian);
    }

    private static async Task PublishAsync(GuardianForm guardian)
    {
        var config = _config;
        var git = _git;
        var audit = _audit;
        if (config is null || git is null || audit is null || string.IsNullOrWhiteSpace(config.RepositoryPath)) return;
        if (!StandaloneProjectPublishing.IsLogicalProject(config, config.RepositoryPath)) return;

        if (config.ConnectionMode == GitPetConnectionModes.LocalGitOnly)
        {
            using var localOnly = new GuardianConfirmDialog(
                "Send project",
                "LOCAL GIT MODE",
                "GitPet is currently keeping this project local. Switch the GitHub connection back on before publishing.",
                "OK",
                showCancel: false);
            localOnly.ShowDialog(guardian);
            return;
        }

        var project = config.GetActiveProject();
        var link = StandaloneProjectPublishing.GetLink(config);
        if (project is null || link is null)
        {
            await GuardianSyncState.ConnectOriginAsync(guardian);
            return;
        }

        var snapshot = GuardianSyncState.Current;
        if (snapshot.Unsaved > 0)
        {
            using var saveFirst = new GuardianConfirmDialog(
                "Send project",
                "SAVE THIS PROJECT FIRST",
                "GitPet found changes inside this logical project's selected scope.\r\n\r\nSave them before sending the standalone project online.",
                "OK",
                showCancel: false);
            saveFirst.ShowDialog(guardian);
            return;
        }

        /* ==========================================================================
           PATCH: AUTOMATIC ALLOW-LIST SEND FIREWALL
           DATE.TIME: 2026-09-11 21:18 +03:00
           Block standalone Send when its saved allow-list contract mismatches.
           ========================================================================== */
        var allowList = new ProjectAllowListStore().Load(
            project.Id,
            project.RepositoryRoot,
            project.Path,
            project.DisplayName);
        if (!string.IsNullOrWhiteSpace(allowList))
        {
            var boundary = await ProjectPublishBoundary.CompareAsync(
                config,
                git,
                config.RepositoryPath,
                allowList);
            if (!boundary.ExactMatch)
            {
                using var review = new ProjectPublishBoundaryDialog(boundary, publishingGate: true);
                review.ShowDialog(guardian);

                var blockedPet = Application.OpenForms
                    .OfType<PetForm>()
                    .FirstOrDefault(form => form.Visible && !form.IsDisposed);
                blockedPet?.ShowGuidance("🛡 SEND BLOCKED\nProject boundary mismatch");
                await audit.WriteAsync("standalone_project_publish_boundary_blocked", new
                {
                    projectId = project.Id,
                    project = project.DisplayName,
                    matched = boundary.Matched.Count,
                    allowListOnly = boundary.AllowListOnly.Count,
                    sendOnly = boundary.SendOnly.Count,
                    issues = boundary.Issues.Count,
                    boundary.FailureMessage
                });
                return;
            }
        }

        using var confirmation = new GuardianConfirmDialog(
            "Send project",
            "SEND PROJECT SCOPE ONLY",
            $"Publish the saved {project.DisplayName} project to:\r\n{link.RepositoryLabel}\r\nBranch: {link.Branch}\r\n\r\n" +
            "GitPet will build an isolated copy containing ONLY this project's selected tracked files.\r\n\r\n" +
            "The larger parent repository and unrelated sibling folders will NOT be sent.",
            "Send project ↑",
            "Cancel",
            confirmWidth: 160);
        if (confirmation.ShowDialog(guardian) != DialogResult.Yes) return;

        var pet = Application.OpenForms
            .OfType<PetForm>()
            .FirstOrDefault(form => form.Visible && !form.IsDisposed);
        pet?.BeginGuidanceHold("📦 PUBLISHING PROJECT\nOnly your selected scope");

        var toolbar = FindToolbar(guardian);
        if (toolbar is not null) toolbar.Enabled = false;
        guardian.UseWaitCursor = true;
        try
        {
            var result = await StandaloneProjectPublishing.PublishAsync(config, git, audit);
            using var done = new GuardianConfirmDialog(
                "Send project",
                result.Success ? "PROJECT SENT  ✓" : "SEND NEEDS ATTENTION",
                result.Success
                    ? result.Message + "\r\n\r\nStandalone workspace:\r\n" + result.WorkspacePath
                    : result.Message,
                "OK",
                showCancel: false,
                dialogSize: result.Success ? new Size(760, 460) : new Size(760, 500));
            done.ShowDialog(guardian);
            pet?.ShowGuidance(result.Success
                ? "✓ PROJECT SENT\nParent repo stayed private"
                : "⚠ SEND NEEDS HELP\nParent repo was not sent");
        }
        finally
        {
            guardian.UseWaitCursor = false;
            if (toolbar is not null) toolbar.Enabled = true;
            pet?.EndGuidanceHold();
        }

        try { await GuardianSyncState.RefreshAsync(true); } catch { }
        try { await guardian.RefreshAsync(); } catch { }
        UpdateButton(guardian);
    }

    private static FlowLayoutPanel? FindToolbar(Control root) =>
        EnumerateControls(root)
            .OfType<FlowLayoutPanel>()
            .FirstOrDefault(panel => panel.Controls.OfType<GuardianActionButton>()
                .Any(button => button.Text.StartsWith("Projects", StringComparison.OrdinalIgnoreCase)));

    private static GuardianActionButton? FindOriginalGet(FlowLayoutPanel toolbar) =>
        toolbar.Controls
            .OfType<GuardianActionButton>()
            .FirstOrDefault(button =>
                button.Name != "StandaloneProjectGetButton" &&
                button.Text.StartsWith("Get", StringComparison.OrdinalIgnoreCase));

    private static GuardianActionButton? FindOriginalSend(FlowLayoutPanel toolbar) =>
        toolbar.Controls
            .OfType<GuardianActionButton>()
            .FirstOrDefault(button =>
                button.Name != "StandaloneProjectSendButton" &&
                button.Text.StartsWith("Send", StringComparison.OrdinalIgnoreCase));

    private static bool OperationInProgress(Control root) =>
        EnumerateControls(root)
            .OfType<GuardianActionButton>()
            .Any(button => button.Visible && button.Text.Equals("Cancel", StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<Control> EnumerateControls(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var descendant in EnumerateControls(child)) yield return descendant;
        }
    }
}
