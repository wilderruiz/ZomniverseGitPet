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
    private static readonly Dictionary<GuardianForm, GuardianActionButton> Buttons = [];

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
            Buttons.Clear();
        };
    }

    private static void OnSyncChanged(object? sender, EventArgs e)
    {
        foreach (var guardian in Buttons.Keys.ToArray()) UpdateButton(guardian);
    }

    private static async Task TickAsync()
    {
        if (_tickRunning || _config is null || _git is null || _audit is null) return;
        _tickRunning = true;
        try
        {
            var guardians = Application.OpenForms
                .OfType<GuardianForm>()
                .Where(form => !form.IsDisposed)
                .ToArray();

            foreach (var stale in Buttons.Keys.Where(form => !guardians.Contains(form)).ToArray())
                Buttons.Remove(stale);

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
        await Task.CompletedTask;
    }

    private static void EnsureButton(GuardianForm guardian)
    {
        if (Buttons.ContainsKey(guardian)) return;
        var toolbar = FindToolbar(guardian);
        if (toolbar is null) return;

        var original = FindOriginalSend(toolbar);
        if (original is null) return;
        var index = toolbar.Controls.GetChildIndex(original);

        var button = new GuardianActionButton
        {
            Name = "StandaloneProjectSendButton",
            Text = "Send ↑",
            Width = 92,
            Kind = GuardianActionKind.Push,
            SyncStateAware = false,
            Visible = false,
            Enabled = false,
            Margin = original.Margin
        };
        button.Click += async (_, _) => await PublishAsync(guardian);
        toolbar.Controls.Add(button);
        toolbar.Controls.SetChildIndex(button, index);
        Buttons[guardian] = button;
        guardian.Disposed += (_, _) => Buttons.Remove(guardian);
    }

    private static void UpdateButton(GuardianForm guardian)
    {
        if (_config is null || !Buttons.TryGetValue(guardian, out var standaloneButton)) return;
        var toolbar = FindToolbar(guardian);
        if (toolbar is null) return;
        var original = FindOriginalSend(toolbar);
        if (original is null) return;

        var repositoryRoot = _config.RepositoryPath;
        var logical = !string.IsNullOrWhiteSpace(repositoryRoot) &&
                      StandaloneProjectPublishing.IsLogicalProject(_config, repositoryRoot);

        original.Visible = !logical;
        standaloneButton.Visible = logical;
        if (!logical) return;

        var snapshot = GuardianSyncState.Current;
        var linked = StandaloneProjectPublishing.GetLink(_config) is not null;
        var operationRunning = OperationInProgress(guardian);
        standaloneButton.Text = "Send ↑";
        standaloneButton.Width = 92;
        standaloneButton.Enabled = linked &&
                                   snapshot.HasRepository &&
                                   snapshot.HasRemote &&
                                   snapshot.OnlineReachable &&
                                   snapshot.Unsaved == 0 &&
                                   snapshot.Ahead > 0 &&
                                   !operationRunning;
        standaloneButton.Cursor = standaloneButton.Enabled ? Cursors.Hand : Cursors.Default;
    }

    private static async Task PublishAsync(GuardianForm guardian)
    {
        var config = _config;
        var git = _git;
        var audit = _audit;
        if (config is null || git is null || audit is null || string.IsNullOrWhiteSpace(config.RepositoryPath)) return;
        if (!StandaloneProjectPublishing.IsLogicalProject(config, config.RepositoryPath)) return;

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

        using var confirmation = new GuardianConfirmDialog(
            "Send project",
            "SEND PROJECT SCOPE ONLY",
            $"Publish the saved {project.DisplayName} project to:\r\n{link.RepositoryLabel}\r\n\r\n" +
            "GitPet will build an isolated copy containing ONLY this project's selected tracked files.\r\n\r\n" +
            "The larger parent repository and unrelated sibling folders will NOT be sent.",
            "Send project ↑",
            "Cancel");
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
                showCancel: false);
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
