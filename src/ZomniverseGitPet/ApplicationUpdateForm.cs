namespace ZomniverseGitPet;

internal sealed class ApplicationUpdateForm : Form
{
    public ApplicationUpdateForm(ApplicationUpdateInfo update)
    {
        Text = $"Update ZomniverseGitPet {update.AvailableVersion}";
        Icon = AppIconProvider.Icon;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        ClientSize = new Size(840, 650);
        BackColor = GuardianTheme.Window;
        ForeColor = GuardianTheme.Ink;
        Font = new Font("Segoe UI", 9.5f);
        WindowChrome.ApplyGuardianChrome(this);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 5,
            ColumnCount = 1,
            Padding = new Padding(26),
            BackColor = GuardianTheme.Window
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));

        var heading = new Label
        {
            Dock = DockStyle.Fill,
            Text = "🐾  GITPET IS BACK FROM THE VET",
            ForeColor = GuardianTheme.HotPinkSoft,
            BackColor = GuardianTheme.Window,
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            AccessibleName = "GitPet update available"
        };

        var summary = new Label
        {
            Dock = DockStyle.Fill,
            Text =
                $"ZomniverseGitPet {update.AvailableVersion} completed its latest release clinic visit and is ready.\r\n" +
                $"You currently have {update.CurrentVersion} installed.",
            ForeColor = GuardianTheme.Ink,
            BackColor = GuardianTheme.Window,
            Font = new Font("Segoe UI", 10.5f),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(4, 4, 4, 4),
            AccessibleName = "Update version summary"
        };

        var notesHeading = new Label
        {
            Dock = DockStyle.Fill,
            Text = "VET VISIT — WHAT CHANGED",
            ForeColor = GuardianTheme.MutedInk,
            BackColor = GuardianTheme.SurfaceRaised,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0)
        };

        var notes = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            BackColor = GuardianTheme.Console,
            ForeColor = GuardianTheme.Ink,
            Font = new Font("Segoe UI", 9.5f),
            DetectUrls = true,
            Text = update.VetVisitSummary,
            Margin = new Padding(0, 8, 0, 8),
            AccessibleName = "Vet visit update summary",
            AccessibleDescription =
                "Summary of new features, fixes and safety treatments in this GitPet release."
        };
        notes.LinkClicked += (_, e) => OpenLink(e.LinkText);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 12, 0, 0),
            BackColor = GuardianTheme.Window
        };

        var install = new GuardianActionButton
        {
            Text = "Install update",
            Width = 148,
            Height = 38,
            Kind = GuardianActionKind.Primary,
            SyncStateAware = false,
            DialogResult = DialogResult.OK,
            Margin = Padding.Empty
        };

        var later = new GuardianActionButton
        {
            Text = "Later",
            Width = 90,
            Height = 38,
            Kind = GuardianActionKind.Standard,
            SyncStateAware = false,
            DialogResult = DialogResult.Cancel,
            Margin = new Padding(8, 0, 0, 0)
        };

        var releasePage = MakeLinkButton("Release page", 116, update.ReleasePage);
        var vetRecord = MakeLinkButton("Vet record", 108, update.DewormingReportUrl);
        var passport = MakeLinkButton("Vet passport", 112, update.VetPassportUrl);

        buttons.Controls.Add(install);
        buttons.Controls.Add(later);
        buttons.Controls.Add(releasePage);
        if (!string.IsNullOrWhiteSpace(update.DewormingReportUrl))
            buttons.Controls.Add(vetRecord);
        if (!string.IsNullOrWhiteSpace(update.VetPassportUrl))
            buttons.Controls.Add(passport);

        root.Controls.Add(heading, 0, 0);
        root.Controls.Add(summary, 0, 1);
        root.Controls.Add(notesHeading, 0, 2);
        root.Controls.Add(notes, 0, 3);
        root.Controls.Add(buttons, 0, 4);
        Controls.Add(root);

        AcceptButton = install;
        CancelButton = later;
    }

    private static GuardianActionButton MakeLinkButton(
        string text,
        int width,
        string url)
    {
        var button = new GuardianActionButton
        {
            Text = text,
            Width = width,
            Height = 38,
            Kind = GuardianActionKind.Standard,
            SyncStateAware = false,
            Margin = new Padding(8, 0, 0, 0),
            Enabled = !string.IsNullOrWhiteSpace(url)
        };
        button.Click += (_, _) => OpenLink(url);
        return button;
    }

    private static void OpenLink(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch { }
    }
}

internal sealed class ApplicationUpdateProgressForm : Form
{
    private readonly ProgressBar _progress;
    private readonly Label _status;

    public ApplicationUpdateProgressForm(string version)
    {
        Text = "Downloading ZomniverseGitPet update";
        Icon = AppIconProvider.Icon;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        ClientSize = new Size(560, 230);
        BackColor = GuardianTheme.Window;
        ForeColor = GuardianTheme.Ink;
        Font = new Font("Segoe UI", 9.5f);
        WindowChrome.ApplyGuardianChrome(this);

        var heading = new Label
        {
            Dock = DockStyle.Top,
            Height = 72,
            Padding = new Padding(26, 16, 26, 8),
            Text = "DOWNLOADING UPDATE",
            ForeColor = GuardianTheme.HotPinkSoft,
            BackColor = GuardianTheme.Window,
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _status = new Label
        {
            Dock = DockStyle.Top,
            Height = 64,
            Padding = new Padding(30, 8, 30, 4),
            Text = $"Getting ZomniverseGitPet {version}…",
            ForeColor = GuardianTheme.Ink,
            BackColor = GuardianTheme.Window,
            Font = new Font("Segoe UI", 10f),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var progressHost = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(30, 14, 30, 30),
            BackColor = GuardianTheme.Window
        };

        _progress = new ProgressBar
        {
            Dock = DockStyle.Top,
            Height = 24,
            Minimum = 0,
            Maximum = 100,
            Style = ProgressBarStyle.Continuous
        };
        progressHost.Controls.Add(_progress);

        Controls.Add(progressHost);
        Controls.Add(_status);
        Controls.Add(heading);
    }

    public void SetProgress(int percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        _progress.Value = percent;
        _status.Text = percent >= 100
            ? "Download complete. Verifying package…"
            : $"Getting the verified installer… {percent}%";
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            return;
        }
        base.OnFormClosing(e);
    }
}
