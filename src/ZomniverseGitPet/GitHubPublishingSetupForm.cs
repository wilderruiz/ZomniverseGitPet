using System.Diagnostics;

namespace ZomniverseGitPet;

internal sealed class GitHubPublishingSetupForm : Form
{
    private readonly GitHubReleasePublisher _publisher;
    private readonly Label _status = new();
    private readonly Button _install = new();
    private readonly Button _signIn = new();
    private readonly Button _refresh = new();
    private bool _busy;

    public GitHubPublishingSetupForm(GitHubReleasePublisher publisher)
    {
        _publisher = publisher;

        Text = "Set up GitHub publishing";
        Icon = AppIconProvider.Icon;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(720, 430);
        BackColor = GuardianTheme.Window;
        ForeColor = GuardianTheme.Ink;
        Font = new Font("Segoe UI", 9);
        WindowChrome.ApplyGuardianChrome(this);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = GuardianTheme.Window
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildStatusCard(), 0, 1);
        root.Controls.Add(BuildExplanation(), 0, 2);
        root.Controls.Add(BuildFooter(), 0, 3);
        Controls.Add(root);

        Shown += async (_, _) => await RefreshStatusAsync();
    }

    private Control BuildHeader()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(26, 12, 26, 10),
            BackColor = GuardianTheme.SurfaceRaised
        };
        panel.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "One-time developer setup. GitPet uses GitHub CLI authentication and never stores a personal-access token.",
            ForeColor = GuardianTheme.MutedInk,
            Font = new Font("Segoe UI", 9.5f),
            TextAlign = ContentAlignment.BottomLeft
        });
        panel.Controls.Add(new Label
        {
            Dock = DockStyle.Top,
            Height = 42,
            Text = "◇  SET UP GITHUB PUBLISHING",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 15, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        });
        return panel;
    }

    private Control BuildStatusCard()
    {
        var outer = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(22, 12, 22, 8),
            BackColor = GuardianTheme.Window
        };
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20, 16, 20, 16),
            BackColor = GuardianTheme.Surface
        };
        _status.Dock = DockStyle.Fill;
        _status.Text = "Checking GitHub CLI…";
        _status.ForeColor = GuardianTheme.MutedInk;
        _status.Font = new Font("Segoe UI", 10);
        _status.TextAlign = ContentAlignment.MiddleLeft;
        card.Controls.Add(_status);
        outer.Controls.Add(card);
        return outer;
    }

    private Control BuildExplanation()
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(28, 14, 28, 8),
            Text =
                "GitHub CLI is required only for developers who publish ZomniverseGitPet releases.\r\n\r\n" +
                "Install GitHub CLI opens a visible PowerShell window and runs Winget. Sign in to GitHub opens GitHub CLI's browser-based login. Nothing is installed or authenticated silently.\r\n\r\n" +
                "After either step completes, return here and choose Refresh.",
            ForeColor = GuardianTheme.Ink,
            Font = new Font("Segoe UI", 9.5f),
            TextAlign = ContentAlignment.TopLeft
        };
    }

    private Control BuildFooter()
    {
        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(14, 15, 20, 10),
            BackColor = GuardianTheme.SurfaceRaised
        };

        var close = MakeButton("Close", 92, GuardianTheme.SurfaceSoft, GuardianTheme.Border);
        close.Click += (_, _) => Close();

        _refresh.Text = "Refresh";
        StyleButton(_refresh, 105, GuardianTheme.SurfaceSoft, GuardianTheme.Border);
        _refresh.Click += async (_, _) => await RefreshStatusAsync();

        _signIn.Text = "Sign in to GitHub";
        StyleButton(_signIn, 150, GuardianTheme.Violet, GuardianTheme.HotPink);
        _signIn.Click += (_, _) => LaunchGitHubLogin();

        _install.Text = "Install GitHub CLI";
        StyleButton(_install, 150, GuardianTheme.Violet, GuardianTheme.HotPink);
        _install.Click += (_, _) => LaunchGitHubCliInstall();

        footer.Controls.Add(close);
        footer.Controls.Add(_refresh);
        footer.Controls.Add(_signIn);
        footer.Controls.Add(_install);
        CancelButton = close;
        return footer;
    }

    private async Task RefreshStatusAsync()
    {
        if (_busy) return;
        SetBusy(true);
        try
        {
            var cli = await _publisher.GetGitHubCliStatusAsync();
            _status.Text = cli.Message;
            _status.ForeColor = cli.Authenticated
                ? GuardianTheme.Healthy
                : GuardianTheme.Warning;
            _install.Enabled = !cli.Available;
            _signIn.Enabled = cli.Available && !cli.Authenticated;
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            _status.ForeColor = GuardianTheme.Warning;
            _install.Enabled = true;
            _signIn.Enabled = false;
        }
        finally
        {
            SetBusy(false, preserveActionState: true);
        }
    }

    private void LaunchGitHubCliInstall()
    {
        const string command =
            "winget install --id GitHub.cli -e; " +
            "Write-Host ''; Write-Host 'When installation finishes, close this window and click Refresh in GitPet.'";
        if (!LaunchPowerShell(command)) return;

        _status.Text = "GitHub CLI installation opened in PowerShell. Complete it there, then click Refresh.";
        _status.ForeColor = GuardianTheme.Changes;
    }

    /* ==========================================================================
       PATCH: RESOLVED GITHUB CLI LOGIN
       DATE.TIME: 2026-09-10 19:24 +03:00
       Use installed gh.exe even before PATH refreshes.
       ========================================================================== */
    private void LaunchGitHubLogin()
    {
        var executable = FindGitHubCliExecutable();
        if (string.IsNullOrWhiteSpace(executable))
        {
            _status.Text = "GitHub CLI could not be located. Install it, then click Refresh.";
            _status.ForeColor = GuardianTheme.Warning;
            return;
        }

        var quotedExecutable = executable.Replace("'", "''");
        var command =
            "& '" + quotedExecutable + "' auth login --hostname github.com --web --git-protocol https; " +
            "Write-Host ''; Write-Host 'When sign-in finishes, close this window and click Refresh in GitPet.'";
        if (!LaunchPowerShell(command)) return;

        _status.Text = "GitHub sign-in opened in PowerShell. Complete the browser flow, then click Refresh.";
        _status.ForeColor = GuardianTheme.Changes;
    }

    private static string? FindGitHubCliExecutable()
    {
        var candidates = new List<string>();

        var configured = Environment.GetEnvironmentVariable("GH_EXE");
        if (!string.IsNullOrWhiteSpace(configured))
            candidates.Add(configured);

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

    private bool LaunchPowerShell(string command)
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
            MessageBox.Show(
                this,
                "GitPet could not open PowerShell.\r\n\r\n" + ex.Message,
                "GitHub publishing setup",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }
    }

    private void SetBusy(bool busy, bool preserveActionState = false)
    {
        _busy = busy;
        UseWaitCursor = busy;
        _refresh.Enabled = !busy;
        if (!preserveActionState)
        {
            _install.Enabled = !busy;
            _signIn.Enabled = !busy;
        }
    }

    private static Button MakeButton(string text, int width, Color fill, Color border)
    {
        var button = new Button { Text = text };
        StyleButton(button, width, fill, border);
        return button;
    }

    private static void StyleButton(Button button, int width, Color fill, Color border)
    {
        button.Width = width;
        button.Height = 38;
        button.Margin = new Padding(7, 0, 0, 0);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = border;
        button.BackColor = fill;
        button.ForeColor = Color.White;
        button.Font = new Font("Segoe UI", 9, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
        button.UseVisualStyleBackColor = false;
    }
}
