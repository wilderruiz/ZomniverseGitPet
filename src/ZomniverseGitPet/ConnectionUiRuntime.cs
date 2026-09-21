namespace ZomniverseGitPet;

/* ==========================================================================
   PATCH: FIRST-CLASS CONNECTION TOOLBAR
   DATE.TIME: 2026-09-11 11:38 +03:00
   Surface account state before Projects and guide authentication.
   ========================================================================== */
internal static class ConnectionUiRuntime
{
    private static AppConfig? _config;
    private static ConfigStore? _configStore;
    private static AuditLog? _audit;
    private static GitHubAccountService? _github;
    private static System.Windows.Forms.Timer? _timer;
    private static bool _tickRunning;
    private static DateTimeOffset _nextAccountPoll = DateTimeOffset.MinValue;
    private static GitHubAccountStatus? _status;
    private static readonly HashSet<FirstRunSetupForm> PendingFirstRunSignIns = [];

    public static void Initialize(AppConfig config, ConfigStore configStore, AuditLog audit)
    {
        if (_timer is not null) return;

        _config = config;
        _configStore = configStore;
        _audit = audit;
        _github = new GitHubAccountService(audit);

        _timer = new System.Windows.Forms.Timer { Interval = 900 };
        _timer.Tick += async (_, _) => await TickAsync();
        _timer.Start();
        Application.ApplicationExit += (_, _) =>
        {
            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;
        };
    }

    private static async Task TickAsync()
    {
        if (_tickRunning || _config is null || _configStore is null || _audit is null || _github is null)
            return;

        _tickRunning = true;
        try
        {
            var guardians = Application.OpenForms.OfType<GuardianForm>().Where(form => !form.IsDisposed).ToArray();
            foreach (var guardian in guardians)
            {
                RemoveLegacyConnectionMenu(guardian);
                EnsureConnectionToolbarButton(guardian);
            }

            var firstRuns = Application.OpenForms.OfType<FirstRunSetupForm>().Where(form => !form.IsDisposed && form.Visible).ToArray();
            ObserveFirstRunSignIns(firstRuns);

            var urgentAuthPoll = PendingFirstRunSignIns.Count > 0;
            if (urgentAuthPoll || DateTimeOffset.UtcNow >= _nextAccountPoll)
            {
                _status = await _github.GetStatusAsync();
                _nextAccountPoll = DateTimeOffset.UtcNow.AddSeconds(urgentAuthPoll ? 1 : 5);
                foreach (var guardian in guardians) UpdateConnectionToolbarButton(guardian);
            }

            if (_status is { Authenticated: true } && PendingFirstRunSignIns.Count > 0)
                AdvanceCompletedFirstRunSignIns();
        }
        catch
        {
            // Connection UI is advisory. Never interrupt repository work because a status probe failed.
        }
        finally
        {
            _tickRunning = false;
        }
    }

    private static void ObserveFirstRunSignIns(IEnumerable<FirstRunSetupForm> forms)
    {
        var visible = forms.ToHashSet();
        PendingFirstRunSignIns.RemoveWhere(form => !visible.Contains(form) || form.IsDisposed);

        foreach (var form in visible)
        {
            var labels = EnumerateControls(form).OfType<Label>().ToArray();
            if (labels.Any(label => label.Text.Contains("GitHub sign-in opened", StringComparison.OrdinalIgnoreCase) ||
                                    label.Text.Contains("Waiting for GitHub", StringComparison.OrdinalIgnoreCase)))
            {
                PendingFirstRunSignIns.Add(form);
            }

            if (labels.Any(label => label.Text.Contains("Local Git Only selected", StringComparison.OrdinalIgnoreCase)))
                PendingFirstRunSignIns.Remove(form);
        }
    }

    private static void AdvanceCompletedFirstRunSignIns()
    {
        foreach (var form in PendingFirstRunSignIns.ToArray())
        {
            if (form.IsDisposed || !form.Visible)
            {
                PendingFirstRunSignIns.Remove(form);
                continue;
            }

            var controls = EnumerateControls(form).ToArray();
            var statusLabel = controls.OfType<Label>().FirstOrDefault(label =>
                label.Text.Contains("GitHub sign-in opened", StringComparison.OrdinalIgnoreCase) ||
                label.Text.Contains("GitHub is connected", StringComparison.OrdinalIgnoreCase));
            var refresh = controls.OfType<OnboardingButton>().FirstOrDefault(button => button.Text == "↻");
            var connect = controls.OfType<OnboardingButton>().FirstOrDefault(button =>
                button.Text.Equals("Connect", StringComparison.OrdinalIgnoreCase));

            if (statusLabel is not null &&
                !statusLabel.Text.Contains("GitHub is connected", StringComparison.OrdinalIgnoreCase))
            {
                if (refresh is { Enabled: true }) refresh.PerformClick();
                continue;
            }

            if (connect is { Enabled: true })
            {
                connect.PerformClick();
                PendingFirstRunSignIns.Remove(form);
            }
        }
    }

    private static void EnsureConnectionToolbarButton(GuardianForm guardian)
    {
        var toolbar = EnumerateControls(guardian)
            .OfType<FlowLayoutPanel>()
            .FirstOrDefault(panel => panel.Controls.OfType<GuardianActionButton>()
                .Any(button => button.Text.StartsWith("Projects", StringComparison.OrdinalIgnoreCase)));
        if (toolbar is null) return;

        var existing = toolbar.Controls.Find("ConnectionToolbarButton", false).FirstOrDefault() as OnboardingButton;
        if (existing is null)
        {
            existing = new OnboardingButton
            {
                Name = "ConnectionToolbarButton",
                Text = "Connect",
                Width = 116,
                Height = 38,
                Margin = new Padding(0, 2, 7, 2),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                CornerRadius = 9,
                FillColor = GuardianTheme.SurfaceSoft,
                BorderColor = GuardianTheme.Border,
                HoverColor = GuardianTheme.SurfaceRaised,
                AccessibleName = "GitPet connection"
            };
            existing.Click += async (_, _) => await OpenConnectionManagerAsync(guardian);
            toolbar.Controls.Add(existing);
        }

        toolbar.Controls.SetChildIndex(existing, 0);
        UpdateConnectionToolbarButton(guardian);
    }

    private static void UpdateConnectionToolbarButton(GuardianForm guardian)
    {
        var button = EnumerateControls(guardian)
            .OfType<OnboardingButton>()
            .FirstOrDefault(control => control.Name == "ConnectionToolbarButton");
        if (button is null || _config is null) return;

        var githubMode = _config.ConnectionMode == GitPetConnectionModes.GitHub;
        var localMode = _config.ConnectionMode == GitPetConnectionModes.LocalGitOnly;
        var authenticated = _status is { Authenticated: true };

        if (localMode)
        {
            button.Text = "Local Git";
            button.FillColor = Color.FromArgb(89, 42, 31);
            button.BorderColor = Color.FromArgb(240, 80, 50);
            button.HoverColor = Color.FromArgb(112, 52, 37);
            button.Width = 116;
        }
        else if (githubMode && authenticated)
        {
            button.Text = "GitHub ✓";
            button.FillColor = GuardianTheme.HealthyFill;
            button.BorderColor = GuardianTheme.Healthy;
            button.HoverColor = Color.FromArgb(31, 78, 58);
            button.Width = 116;
        }
        else if (githubMode)
        {
            button.Text = "GitHub !";
            button.FillColor = GuardianTheme.SurfaceSoft;
            button.BorderColor = GuardianTheme.Warning;
            button.HoverColor = GuardianTheme.SurfaceRaised;
            button.Width = 116;
        }
        else
        {
            button.Text = authenticated ? "GitHub ✓" : "Connect";
            button.FillColor = authenticated ? GuardianTheme.HealthyFill : GuardianTheme.SurfaceSoft;
            button.BorderColor = authenticated ? GuardianTheme.Healthy : GuardianTheme.Border;
            button.HoverColor = authenticated ? Color.FromArgb(31, 78, 58) : GuardianTheme.SurfaceRaised;
            button.Width = 116;
        }

        button.Invalidate();
    }

    private static void RemoveLegacyConnectionMenu(GuardianForm guardian)
    {
        var menu = guardian.MainMenuStrip;
        if (menu is null) return;

        var item = menu.Items.Cast<ToolStripItem>().FirstOrDefault(candidate => candidate.Name == "ConnectionMenu");
        if (item is null) return;
        menu.Items.Remove(item);
        item.Dispose();
    }

    private static async Task OpenConnectionManagerAsync(GuardianForm guardian)
    {
        if (_config is null || _configStore is null || _audit is null || _github is null) return;

        var operationRunning = EnumerateControls(guardian)
            .OfType<GuardianActionButton>()
            .Any(button => button.Visible && button.Text.Equals("Cancel", StringComparison.OrdinalIgnoreCase));
        if (operationRunning)
        {
            using var info = new GuardianConfirmDialog(
                "GitPet connection",
                "FINISH THE CURRENT OPERATION",
                "Finish or cancel the current Guardian operation before changing connection settings.",
                "OK",
                showCancel: false);
            info.ShowDialog(guardian);
            return;
        }

        var pet = Application.OpenForms.OfType<PetForm>().FirstOrDefault(form => form.Visible && !form.IsDisposed);
        var guide = pet is null ? new Action<string>(_ => { }) : pet.ShowGuidance;
        guide("🔑 CONNECTION\nGitHub or local Git");

        using var form = new ConnectionSettingsForm(_config, _configStore, _audit, guide);
        form.ShowDialog(guardian);

        _status = form.CurrentStatus ?? await _github.GetStatusAsync();
        _nextAccountPoll = DateTimeOffset.UtcNow.AddSeconds(2);
        UpdateConnectionToolbarButton(guardian);
        try { await GuardianSyncState.RefreshAsync(true); } catch { }
    }

    private static IEnumerable<Control> EnumerateControls(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var descendant in EnumerateControls(child)) yield return descendant;
        }
    }
}
