namespace ZomniverseGitPet;

/* ==========================================================================
   PATCH: FIRST-CLASS CONNECTION MANAGER
   DATE.TIME: 2026-09-11 11:31 +03:00
   Manage GitHub accounts without touching local repositories.
   ========================================================================== */
internal sealed class ConnectionSettingsForm : Form
{
    private static readonly Color GitOrange = Color.FromArgb(240, 80, 50);
    private static readonly Color GitOrangeDark = Color.FromArgb(89, 42, 31);

    private readonly AppConfig _config;
    private readonly ConfigStore _configStore;
    private readonly GitHubAccountService _github;
    private readonly Action<string> _guidePet;
    private readonly CancellationTokenSource _lifetime = new();

    private readonly Label _status = new();
    private readonly Label _mode = new();
    private readonly OnboardingButton _primary = new();
    private readonly OnboardingButton _changeAccount = new();
    private readonly OnboardingButton _disconnect = new();
    private readonly OnboardingButton _local = new();
    private readonly OnboardingButton _refresh = new();
    private readonly OnboardingButton _close = new();

    private GitHubAccountStatus? _githubStatus;
    private bool _busy;

    public ConnectionSettingsForm(
        AppConfig config,
        ConfigStore configStore,
        AuditLog audit,
        Action<string> guidePet)
    {
        _config = config;
        _configStore = configStore;
        _github = new GitHubAccountService(audit);
        _guidePet = guidePet;

        Text = "GitPet connection";
        Icon = AppIconProvider.Icon;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(720, 520);
        Size = new Size(820, 590);
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        BackColor = GuardianTheme.Window;
        ForeColor = GuardianTheme.Ink;
        Font = new Font("Segoe UI", 9.5f);
        WindowChrome.ApplyGuardianChrome(this);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = GuardianTheme.Window
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildStatusCard(), 0, 1);
        root.Controls.Add(BuildActions(), 0, 2);
        root.Controls.Add(BuildFooter(), 0, 3);
        Controls.Add(root);

        Shown += async (_, _) =>
        {
            _guidePet("🔑 CONNECTION\nChoose GitHub or local Git");
            await RefreshAsync();
        };
    }

    public GitHubAccountStatus? CurrentStatus => _githubStatus;

    private Control BuildHeader()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(30, 18, 30, 14),
            BackColor = GuardianTheme.SurfaceRaised
        };
        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 42,
            Text = "◇  GITPET CONNECTION",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        var subtitle = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Use your own GitHub account, switch accounts, or keep GitPet completely local.",
            ForeColor = GuardianTheme.MutedInk,
            Font = new Font("Segoe UI", 9.5f),
            TextAlign = ContentAlignment.TopLeft
        };
        panel.Controls.Add(subtitle);
        panel.Controls.Add(title);
        return panel;
    }

    private Control BuildStatusCard()
    {
        var outer = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(28, 14, 28, 8),
            BackColor = GuardianTheme.Window
        };
        var card = new OnboardingSurfacePanel
        {
            Dock = DockStyle.Fill,
            FillColor = GuardianTheme.SurfaceRaised,
            BackColor = GuardianTheme.SurfaceRaised,
            BorderColor = GuardianTheme.Border,
            CornerRadius = 10,
            Padding = new Padding(18, 12, 18, 10)
        };
        _status.Dock = DockStyle.Top;
        _status.Height = 38;
        _status.Text = "Checking GitHub…";
        _status.ForeColor = GuardianTheme.MutedInk;
        _status.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _status.AutoEllipsis = true;

        _mode.Dock = DockStyle.Fill;
        _mode.ForeColor = GuardianTheme.FaintInk;
        _mode.Font = new Font("Segoe UI", 8.8f);
        _mode.TextAlign = ContentAlignment.TopLeft;

        card.Controls.Add(_mode);
        card.Controls.Add(_status);
        outer.Controls.Add(card);
        return outer;
    }

    private Control BuildActions()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(28, 18, 28, 16),
            BackColor = GuardianTheme.Window
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        ConfigureButton(_primary, GuardianTheme.Violet, GuardianTheme.HotPink);
        ConfigureButton(_changeAccount, GuardianTheme.SurfaceSoft, GuardianTheme.Border);
        ConfigureButton(_disconnect, Color.FromArgb(78, 31, 42), GuardianTheme.Warning);
        ConfigureButton(_local, GitOrangeDark, GitOrange);

        _primary.Click += async (_, _) => await UseOrConnectGitHubAsync();
        _changeAccount.Click += async (_, _) => await ChangeAccountAsync();
        _disconnect.Click += async (_, _) => await DisconnectAsync();
        _local.Click += async (_, _) => await UseLocalAsync();

        panel.Controls.Add(_primary, 0, 0);
        panel.Controls.Add(_local, 1, 0);
        panel.Controls.Add(_changeAccount, 0, 1);
        panel.Controls.Add(_disconnect, 1, 1);

        var explanation = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Disconnecting GitHub never deletes a project, commit, remote, file, or GitHub repository. " +
                   "Local Git mode keeps review, Save, History, Tests and repository health available on this PC.",
            ForeColor = GuardianTheme.MutedInk,
            Font = new Font("Segoe UI", 9),
            TextAlign = ContentAlignment.TopLeft,
            Padding = new Padding(6, 14, 6, 0)
        };
        panel.SetColumnSpan(explanation, 2);
        panel.Controls.Add(explanation, 0, 2);
        return panel;
    }

    private Control BuildFooter()
    {
        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(18, 18, 24, 14),
            BackColor = GuardianTheme.SurfaceRaised
        };

        ConfigureButton(_close, GuardianTheme.SurfaceSoft, GuardianTheme.Border, 116);
        ConfigureButton(_refresh, GuardianTheme.SurfaceSoft, GuardianTheme.Border, 116);
        _close.Text = "Close";
        _refresh.Text = "Refresh";
        _close.Click += (_, _) => Close();
        _refresh.Click += async (_, _) => await RefreshAsync();
        footer.Controls.Add(_close);
        footer.Controls.Add(_refresh);
        return footer;
    }

    private async Task RefreshAsync()
    {
        if (_busy) return;
        SetBusy(true);
        try
        {
            _githubStatus = await _github.GetStatusAsync(_lifetime.Token);
            RenderState();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            _status.ForeColor = GuardianTheme.Warning;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task UseOrConnectGitHubAsync()
    {
        if (_busy) return;
        _githubStatus ??= await _github.GetStatusAsync(_lifetime.Token);

        if (!_githubStatus.CliAvailable)
        {
            _guidePet("🧰 ONE-TIME SETUP\nInstall GitHub CLI first");
            if (_github.LaunchInstall(this))
            {
                _status.Text = "GitHub CLI installation opened. Return here when it finishes.";
                _status.ForeColor = GuardianTheme.Changes;
            }
            return;
        }

        if (!_githubStatus.Authenticated)
        {
            _guidePet("🔐 YOUR TURN\nSign in with GitHub");
            if (!_github.LaunchSignIn(this)) return;

            _status.Text = "Waiting for GitHub browser sign-in…";
            _status.ForeColor = GuardianTheme.Changes;
            SetBusy(true);
            try
            {
                _githubStatus = await _github.WaitForAuthenticationAsync(
                    TimeSpan.FromMinutes(5), _lifetime.Token);
                if (!_githubStatus.Authenticated)
                {
                    _status.Text = "GitHub sign-in is still not visible. You can retry or use Refresh.";
                    _status.ForeColor = GuardianTheme.Warning;
                    return;
                }

                SetMode(GitPetConnectionModes.GitHub);
                _guidePet("✅ GITHUB CONNECTED\nYou're ready to work");
                RenderState();
            }
            catch (OperationCanceledException) { }
            finally
            {
                SetBusy(false);
            }
            return;
        }

        SetMode(GitPetConnectionModes.GitHub);
        _guidePet("✅ GITHUB MODE\nOnline tools are ready");
        RenderState();
    }

    private async Task ChangeAccountAsync()
    {
        if (_busy || _githubStatus is not { Authenticated: true }) return;
        var current = string.IsNullOrWhiteSpace(_githubStatus.Login) ? "the current GitHub account" : _githubStatus.Login;
        using var confirm = new GuardianConfirmDialog(
            "Change GitHub account",
            "CHANGE GITHUB ACCOUNT",
            $"Disconnect {current} from GitHub CLI and sign in with another account?\r\n\r\n" +
            "Your local projects, commits, remotes and files will not be changed.",
            "Change account",
            "Cancel");
        if (confirm.ShowDialog(this) != DialogResult.Yes) return;

        SetBusy(true);
        try
        {
            var logout = await _github.SignOutAsync(_githubStatus.Login, _lifetime.Token);
            if (!logout.Success)
            {
                _status.Text = "GitHub could not disconnect the current account. " + logout.Output;
                _status.ForeColor = GuardianTheme.Warning;
                return;
            }

            SetMode(GitPetConnectionModes.LocalGitOnly);
            _githubStatus = await _github.GetStatusAsync(_lifetime.Token);
            _guidePet("🔐 SWITCH ACCOUNT\nSign in with GitHub");
            if (!_github.LaunchSignIn(this)) return;

            _status.Text = "Waiting for the new GitHub account…";
            _status.ForeColor = GuardianTheme.Changes;
            _githubStatus = await _github.WaitForAuthenticationAsync(
                TimeSpan.FromMinutes(5), _lifetime.Token);
            if (_githubStatus.Authenticated)
            {
                SetMode(GitPetConnectionModes.GitHub);
                _guidePet("✅ ACCOUNT CHANGED\nGitPet is connected");
            }
            RenderState();
        }
        catch (OperationCanceledException) { }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task DisconnectAsync()
    {
        if (_busy || _githubStatus is not { Authenticated: true }) return;
        var current = string.IsNullOrWhiteSpace(_githubStatus.Login) ? "this GitHub account" : _githubStatus.Login;
        using var confirm = new GuardianConfirmDialog(
            "Disconnect GitHub",
            "DISCONNECT GITHUB",
            $"Disconnect {current} from GitHub CLI?\r\n\r\n" +
            "GitPet will switch to Local Git mode. Nothing in your projects or on GitHub will be deleted.",
            "Disconnect",
            "Cancel");
        if (confirm.ShowDialog(this) != DialogResult.Yes) return;

        SetBusy(true);
        try
        {
            var logout = await _github.SignOutAsync(_githubStatus.Login, _lifetime.Token);
            if (!logout.Success)
            {
                _status.Text = "GitHub disconnect failed. " + logout.Output;
                _status.ForeColor = GuardianTheme.Warning;
                return;
            }

            SetMode(GitPetConnectionModes.LocalGitOnly);
            _githubStatus = await _github.GetStatusAsync(_lifetime.Token);
            _guidePet("🧡 LOCAL GIT MODE\nGitHub disconnected safely");
            RenderState();
        }
        catch (OperationCanceledException) { }
        finally
        {
            SetBusy(false);
        }
    }

    private Task UseLocalAsync()
    {
        SetMode(GitPetConnectionModes.LocalGitOnly);
        _guidePet("🧡 LOCAL GIT MODE\nNothing needs to leave this PC");
        RenderState();
        return Task.CompletedTask;
    }

    private void SetMode(string mode)
    {
        _config.ConnectionMode = GitPetConnectionModes.Normalize(mode);
        _config.OnboardingCompleted = true;
        _configStore.Save(_config);
        _ = _github.RecordModeAsync(_config.ConnectionMode);
    }

    private void RenderState()
    {
        var connected = _githubStatus is { Authenticated: true };
        _status.Text = _githubStatus?.Message ?? "GitHub status is not available yet.";
        _status.ForeColor = connected ? GuardianTheme.Healthy : GuardianTheme.Warning;

        _mode.Text = _config.ConnectionMode == GitPetConnectionModes.LocalGitOnly
            ? "Current GitPet mode: Local Git only"
            : _config.ConnectionMode == GitPetConnectionModes.GitHub
                ? connected
                    ? $"Current GitPet mode: GitHub · {_githubStatus!.Login}"
                    : "Current GitPet mode: GitHub · sign-in required"
                : "Choose how GitPet should connect.";

        _primary.Text = !_githubStatus?.CliAvailable ?? true
            ? "Install GitHub CLI"
            : connected
                ? "Use GitHub"
                : "Connect GitHub";

        _changeAccount.Visible = connected;
        _disconnect.Visible = connected;
        _changeAccount.Text = "Change account";
        _disconnect.Text = "Disconnect";
        _local.Text = "Local Git only";

        var githubMode = _config.ConnectionMode == GitPetConnectionModes.GitHub && connected;
        _primary.BorderColor = githubMode ? GuardianTheme.HotPinkSoft : GuardianTheme.HotPink;
        _local.BorderColor = _config.ConnectionMode == GitPetConnectionModes.LocalGitOnly
            ? Color.FromArgb(255, 178, 132)
            : GitOrange;
        _primary.Invalidate();
        _local.Invalidate();
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        UseWaitCursor = busy;
        _primary.Enabled = !busy;
        _changeAccount.Enabled = !busy;
        _disconnect.Enabled = !busy;
        _local.Enabled = !busy;
        _refresh.Enabled = !busy;
        _close.Enabled = !busy;
    }

    private static void ConfigureButton(
        OnboardingButton button,
        Color fill,
        Color border,
        int width = 240)
    {
        button.Width = width;
        button.Height = 42;
        button.Margin = new Padding(8, 8, 8, 8);
        button.Dock = DockStyle.Fill;
        button.FillColor = fill;
        button.BorderColor = border;
        button.HoverColor = ControlPaint.Light(fill, 0.08f);
        button.CornerRadius = 8;
        button.ForeColor = Color.White;
        button.Font = new Font("Segoe UI", 9.2f, FontStyle.Bold);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _lifetime.Cancel();
        if (disposing) _lifetime.Dispose();
        base.Dispose(disposing);
    }
}
