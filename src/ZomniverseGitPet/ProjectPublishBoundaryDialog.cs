namespace ZomniverseGitPet;

/* ==========================================================================
   PATCH: PUBLISH BOUNDARY REVIEW DIALOG
   DATE.TIME: 2026-09-11 21:14 +03:00
   Show allow-list versus Send results and read-only commands.
   ========================================================================== */
internal sealed class ProjectPublishBoundaryDialog : Form
{
    private readonly ProjectPublishBoundaryResult _result;
    private readonly TextBox _body = new();
    private readonly Button _toggleButton;
    private bool _showingCommands;

    public ProjectPublishBoundaryDialog(ProjectPublishBoundaryResult result, bool publishingGate = false)
    {
        _result = result;
        Text = publishingGate ? "Publishing boundary check" : "Compare project with Send";
        Icon = AppIconProvider.Icon;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        MaximizeBox = true;
        ShowInTaskbar = false;
        MinimumSize = new Size(820, 620);
        ClientSize = new Size(980, 720);
        BackColor = GuardianTheme.Window;
        ForeColor = GuardianTheme.Ink;
        Font = new Font("Segoe UI", 9);
        WindowChrome.ApplyGuardianChrome(this);

        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 72,
            Padding = new Padding(24, 14, 24, 8),
            Text = result.ExactMatch
                ? "◇ PROJECT / SEND COMPARISON  ✓"
                : "◇ PROJECT / SEND COMPARISON",
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = result.ExactMatch ? GuardianTheme.Healthy : GuardianTheme.Changes,
            BackColor = GuardianTheme.SurfaceRaised
        };

        var summary = new Label
        {
            Dock = DockStyle.Top,
            Height = 118,
            Padding = new Padding(24, 13, 24, 10),
            BackColor = result.ExactMatch ? GuardianTheme.HealthyFill : GuardianTheme.ChangesFill,
            ForeColor = GuardianTheme.Ink,
            Font = new Font("Segoe UI", 9.5f),
            Text = BuildSummary(result, publishingGate)
        };

        _body.Dock = DockStyle.Fill;
        _body.Multiline = true;
        _body.ReadOnly = true;
        _body.WordWrap = false;
        _body.ScrollBars = ScrollBars.Both;
        _body.BorderStyle = BorderStyle.FixedSingle;
        _body.BackColor = GuardianTheme.Console;
        _body.ForeColor = GuardianTheme.Ink;
        _body.Font = new Font("Cascadia Mono", 9.5f);
        _body.Text = result.BuildReport();

        var bodyHost = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18, 12, 18, 12),
            BackColor = GuardianTheme.Window
        };
        bodyHost.Controls.Add(_body);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 64,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(12, 12, 18, 10),
            BackColor = GuardianTheme.SurfaceRaised
        };

        var close = MakeButton("Close", true, 92);
        var copy = MakeButton("Copy report", false, 118);
        _toggleButton = MakeButton("Command log", false, 118);
        var shell = MakeButton("Open PowerShell", false, 138);

        close.Click += (_, _) => Close();
        copy.Click += (_, _) =>
        {
            Clipboard.SetText(_result.BuildReport());
            copy.Text = "Copied ✓";
        };
        _toggleButton.Click += (_, _) => ToggleBody();
        shell.Click += (_, _) => OpenPowerShell();

        footer.Controls.Add(close);
        footer.Controls.Add(copy);
        footer.Controls.Add(_toggleButton);
        footer.Controls.Add(shell);

        Controls.Add(bodyHost);
        Controls.Add(footer);
        Controls.Add(summary);
        Controls.Add(title);
        CancelButton = close;
    }

    private static string BuildSummary(ProjectPublishBoundaryResult result, bool publishingGate)
    {
        if (result.ExactMatch)
        {
            return $"Project: {result.ProjectName}\r\n" +
                   $"Expected: {result.Matched.Count}  ·  Prepared to send: {result.Matched.Count}\r\n" +
                   "Exact match. Everything prepared for Send belongs to the saved project allow list.";
        }

        var prefix = publishingGate
            ? "SEND BLOCKED — the saved project boundary does not match the prepared snapshot.\r\n"
            : "The saved project boundary does not match the prepared snapshot.\r\n";
        return prefix +
               $"Matched: {result.Matched.Count}  ·  Allow-list only: {result.AllowListOnly.Count}  ·  Send only: {result.SendOnly.Count}" +
               (result.Issues.Count > 0 ? $"  ·  Issues: {result.Issues.Count}" : string.Empty);
    }

    private void ToggleBody()
    {
        _showingCommands = !_showingCommands;
        _body.Text = _showingCommands ? _result.CommandLog : _result.BuildReport();
        _toggleButton.Text = _showingCommands ? "Comparison" : "Command log";
    }

    private void OpenPowerShell()
    {
        try
        {
            Clipboard.SetText(_result.CommandLog);
            var escaped = _result.RepositoryRoot.Replace("'", "''", StringComparison.Ordinal);
            var command =
                $"Set-Location -LiteralPath '{escaped}'; " +
                "Write-Host 'GitPet read-only publish-boundary diagnostics' -ForegroundColor Cyan; " +
                "Write-Host 'The exact commands were copied to your clipboard and remain visible in GitPet.'";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoExit -Command \"" + command.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"",
                UseShellExecute = true,
                WorkingDirectory = _result.RepositoryRoot
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "PowerShell could not be opened.\r\n\r\n" + ex.Message,
                "Open PowerShell",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private static Button MakeButton(string text, bool primary, int width)
    {
        var button = new Button
        {
            Text = text,
            Width = width,
            Height = 38,
            Margin = new Padding(6, 0, 0, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? GuardianTheme.Violet : GuardianTheme.SurfaceSoft,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderSize = primary ? 2 : 1;
        button.FlatAppearance.BorderColor = primary ? GuardianTheme.HotPink : GuardianTheme.Border;
        button.FlatAppearance.MouseOverBackColor = primary ? GuardianTheme.VioletHover : GuardianTheme.SurfaceRaised;
        return button;
    }
}
