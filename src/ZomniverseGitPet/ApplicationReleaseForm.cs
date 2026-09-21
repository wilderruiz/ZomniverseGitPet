using System.Diagnostics;

namespace ZomniverseGitPet;

internal sealed class ApplicationReleaseForm : Form
{
    private readonly MajorUpdateAssessment _assessment;
    private readonly GitHubReleasePublisher _publisher;
    private readonly ApplicationReleasePackage _package;
    private readonly Label _auth = new();
    private readonly RichTextBox _status = new();
    private readonly TextBox _title = new();
    private readonly RichTextBox _notes = new();
    private readonly Button _publish = new();
    private readonly Button _refresh = new();
    private GitHubCliStatus? _cli;
    private bool _busy;

    public ApplicationReleaseForm(
        MajorUpdateAssessment assessment,
        GitHubReleasePublisher publisher)
    {
        _assessment = assessment;
        _publisher = publisher;
        _package = publisher.InspectPreparedPackage(assessment.RepositoryPath, assessment.OriginUrl);

        Text = "Publish application release";
        Icon = AppIconProvider.Icon;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(860, 660);
        Size = new Size(1020, 760);
        BackColor = GuardianTheme.Window;
        ForeColor = GuardianTheme.Ink;
        Font = new Font("Segoe UI", 9);
        WindowChrome.ApplyGuardianChrome(this);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            BackColor = GuardianTheme.Window
        };
        /* ==========================================================================
           PATCH: INCREASE RELEASE PACKAGE SUMMARY HEIGHT
           FUNCTION:
           Gives the package summary card enough vertical space to display every
           version, repository, source, asset, and authentication detail.

           DATE.TIME ADDED: 2026-09-11 23:15 +03:00

           REASON:
           The fixed package-summary row clips its final detail lines.
           ========================================================================== */
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 94));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 230));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 94));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildPackageCard(), 0, 1);
        root.Controls.Add(BuildReleaseEditor(), 0, 2);
        root.Controls.Add(BuildStatus(), 0, 3);
        root.Controls.Add(BuildFooter(), 0, 4);
        Controls.Add(root);

        var versionLabel = string.IsNullOrWhiteSpace(_package.Version) ? "release" : _package.Version;
        _title.Text = $"ZomniverseGitPet {versionLabel}";
        _notes.Text = BuildDefaultNotes();
        _status.Text = BuildInitialStatus();
        _status.ForeColor = _package.Ready ? GuardianTheme.Ink : GuardianTheme.Warning;

        Shown += async (_, _) => await RefreshGitHubCliAsync();
        RefreshActionState();
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
            Text = "Publish a verified Windows package to GitHub using your existing GitHub CLI login. GitPet never stores a personal-access token.",
            ForeColor = GuardianTheme.MutedInk,
            Font = new Font("Segoe UI", 9.5f),
            TextAlign = ContentAlignment.BottomLeft
        });
        panel.Controls.Add(new Label
        {
            Dock = DockStyle.Top,
            Height = 42,
            Text = "◇  PUBLISH APPLICATION RELEASE",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 15, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        });
        return panel;
    }

    private Control BuildPackageCard()
    {
        var outer = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(22, 12, 22, 8),
            BackColor = GuardianTheme.Window
        };
        var card = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(18, 12, 18, 12),
            BackColor = GuardianTheme.Surface
        };
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

        var packageText = _package.Ready
            ? $"VERSION     {_package.Version}\r\n" +
              $"TAG         {_package.ReleaseTag}\r\n" +
              $"REPOSITORY  {_package.RepositorySlug}\r\n" +
              $"TARGET      {_assessment.Branch}\r\n" +
              $"SOURCE      {_package.SourceBranch} @ {ShortCommit(_package.SourceCommit)}\r\n" +
              "ASSETS      installer · portable · manifest · checksums ✓"
            : _package.StatusMessage;

        card.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = packageText,
            ForeColor = _package.Ready ? GuardianTheme.Ink : GuardianTheme.Warning,
            Font = new Font("Cascadia Mono", 9),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        }, 0, 0);

        _auth.Dock = DockStyle.Fill;
        _auth.Text = "GITHUB AUTH\r\nChecking GitHub CLI…";
        _auth.ForeColor = GuardianTheme.MutedInk;
        _auth.Font = new Font("Segoe UI", 9.5f);
        _auth.TextAlign = ContentAlignment.MiddleLeft;
        card.Controls.Add(_auth, 1, 0);

        outer.Controls.Add(card);
        return outer;
    }

    private Control BuildReleaseEditor()
    {
        var outer = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(22, 4, 22, 8),
            BackColor = GuardianTheme.Window
        };
        var card = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(18, 12, 18, 14),
            BackColor = GuardianTheme.SurfaceSoft
        };
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        card.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "RELEASE TITLE",
            ForeColor = GuardianTheme.MutedInk,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        _title.Dock = DockStyle.Fill;
        _title.BackColor = GuardianTheme.Console;
        _title.ForeColor = GuardianTheme.Ink;
        _title.BorderStyle = BorderStyle.FixedSingle;
        card.Controls.Add(_title, 0, 1);

        card.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "RELEASE NOTES",
            ForeColor = GuardianTheme.MutedInk,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.BottomLeft
        }, 0, 2);

        _notes.Dock = DockStyle.Fill;
        _notes.BackColor = GuardianTheme.Console;
        _notes.ForeColor = GuardianTheme.Ink;
        _notes.BorderStyle = BorderStyle.FixedSingle;
        _notes.Font = new Font("Segoe UI", 9.5f);
        _notes.ScrollBars = RichTextBoxScrollBars.Vertical;
        card.Controls.Add(_notes, 0, 3);

        outer.Controls.Add(card);
        return outer;
    }

    private Control BuildStatus()
    {
        var outer = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(22, 2, 22, 8),
            BackColor = GuardianTheme.Window
        };
        _status.Dock = DockStyle.Fill;
        _status.ReadOnly = true;
        _status.BorderStyle = BorderStyle.None;
        _status.BackColor = GuardianTheme.Console;
        _status.ForeColor = GuardianTheme.Ink;
        _status.Font = new Font("Cascadia Mono", 9);
        _status.ScrollBars = RichTextBoxScrollBars.Vertical;
        outer.Controls.Add(_status);
        return outer;
    }

    private Control BuildFooter()
    {
        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(16, 15, 22, 10),
            BackColor = GuardianTheme.SurfaceRaised
        };

        var close = MakeButton("Close", 92, GuardianTheme.SurfaceSoft, GuardianTheme.Border);
        close.Click += (_, _) => Close();

        _publish.Text = "Publish GitHub Release ↑";
        StyleButton(_publish, 188, GuardianTheme.Violet, GuardianTheme.HotPink);
        _publish.Click += async (_, _) => await PublishAsync();

        var releases = MakeButton("Releases ↗", 112, GuardianTheme.SurfaceSoft, GuardianTheme.Border);
        releases.Click += (_, _) => OpenReleases();

        _refresh.Text = "Refresh auth";
        StyleButton(_refresh, 118, GuardianTheme.SurfaceSoft, GuardianTheme.Border);
        _refresh.Click += async (_, _) => await RefreshGitHubCliAsync();

        footer.Controls.Add(close);
        footer.Controls.Add(_publish);
        footer.Controls.Add(releases);
        footer.Controls.Add(_refresh);
        CancelButton = close;
        return footer;
    }

    private async Task RefreshGitHubCliAsync()
    {
        if (_busy) return;
        SetBusy(true, "Checking GitHub CLI authentication…");
        try
        {
            _cli = await _publisher.GetGitHubCliStatusAsync();
            _auth.Text = "GITHUB AUTH\r\n" + _cli.Message;
            _auth.ForeColor = _cli.Authenticated ? GuardianTheme.Healthy : GuardianTheme.Warning;
            _status.Text = BuildInitialStatus();
            _status.ForeColor = CanPublishIgnoringBusy() ? GuardianTheme.Healthy : GuardianTheme.Warning;
        }
        catch (Exception ex)
        {
            _cli = new GitHubCliStatus(false, false, ex.Message);
            _auth.Text = "GITHUB AUTH\r\n" + ex.Message;
            _auth.ForeColor = GuardianTheme.Warning;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task PublishAsync()
    {
        if (!CanPublishIgnoringBusy()) return;

        using var confirm = new ApplicationReleaseConfirmForm(
            _package,
            _assessment.Branch,
            _title.Text.Trim());
        if (confirm.ShowDialog(this) != DialogResult.OK) return;

        SetBusy(true, $"Publishing {_package.ReleaseTag} to GitHub…");
        try
        {
            var result = await _publisher.PublishAsync(
                _package,
                _assessment.Branch,
                _title.Text,
                _notes.Text);

            _status.Text = result.Message;
            _status.ForeColor = result.Success ? GuardianTheme.Healthy : GuardianTheme.Warning;

            if (result.Success && !string.IsNullOrWhiteSpace(result.ReleaseUrl))
            {
                var open = MessageBox.Show(
                    this,
                    "Release published successfully. Open it on GitHub now?",
                    "Application release",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);
                if (open == DialogResult.Yes) OpenUrl(result.ReleaseUrl);
            }
        }
        finally
        {
            SetBusy(false);
        }
    }

    private string BuildInitialStatus()
    {
        if (!_package.Ready) return _package.StatusMessage;
        if (!GitHubReleasePublisher.PackageMatchesSource(_package, _assessment.Branch, _assessment.HeadCommit))
            return
                "Publication blocked: this package was built from a different branch or commit.\r\n\r\n" +
                $"Package: {_package.SourceBranch} @ {ShortCommit(_package.SourceCommit)}\r\n" +
                $"Current: {_assessment.Branch} @ {ShortCommit(_assessment.HeadCommit)}\r\n\r\n" +
                "Run scripts\\build-release.ps1 again after your final Save/Send, then reopen this window.";
        if (_assessment.WorkingTreeDirty)
            return "Publication blocked: this project still has unsaved changes. Save them first so the package and source history have a stable baseline.";
        if (_assessment.RemoteRelation != RemoteHistoryRelation.Equal)
            return "Publication blocked: local and origin are not aligned. Use Save/Get/Send until the current branch is aligned, then reopen this window.";
        if (_cli is { Authenticated: false }) return _cli.Message;

        return _cli is { Authenticated: true }
            ? $"READY TO PUBLISH {_package.ReleaseTag} ✓\r\nGitPet verified the package, exact source commit, remote alignment and GitHub CLI login. Publication remains manual and requires confirmation."
            : "Package and source provenance verified. GitPet is checking GitHub CLI authentication before enabling publication.";
    }

    private string BuildDefaultNotes()
    {
        if (!_package.Ready) return string.Empty;
        return
            $"ZomniverseGitPet {_package.Version}\r\n\r\n" +
            "Windows release package containing the recommended installer, a portable build, the automatic-update manifest, and SHA-256 checksums.\r\n\r\n" +
            "The Setup EXE is the recommended download for normal Windows use.";
    }

    private bool CanPublishIgnoringBusy() =>
        _package.Ready &&
        GitHubReleasePublisher.PackageMatchesSource(_package, _assessment.Branch, _assessment.HeadCommit) &&
        !_assessment.WorkingTreeDirty &&
        _assessment.RemoteRelation == RemoteHistoryRelation.Equal &&
        _cli is { Authenticated: true } &&
        !string.IsNullOrWhiteSpace(_title.Text);

    private void SetBusy(bool busy, string? message = null)
    {
        _busy = busy;
        UseWaitCursor = busy;
        _title.Enabled = !busy;
        _notes.Enabled = !busy;
        _refresh.Enabled = !busy;
        _publish.Enabled = !busy && CanPublishIgnoringBusy();

        if (!string.IsNullOrWhiteSpace(message))
        {
            _status.Text = message;
            _status.ForeColor = GuardianTheme.Changes;
        }
    }

    private void RefreshActionState() => SetBusy(_busy);

    private void OpenReleases()
    {
        if (string.IsNullOrWhiteSpace(_package.RepositorySlug)) return;
        OpenUrl("https://github.com/" + _package.RepositorySlug + "/releases");
    }

    private static string ShortCommit(string value) =>
        string.IsNullOrWhiteSpace(value) ? "unknown" : value[..Math.Min(8, value.Length)];

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            MessageBox.Show("GitPet could not open the browser.", "ZomniverseGitPet", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

internal sealed class ApplicationReleaseConfirmForm : Form
{
    public ApplicationReleaseConfirmForm(
        ApplicationReleasePackage package,
        string branch,
        string title)
    {
        Text = "Confirm application release";
        Icon = AppIconProvider.Icon;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(650, 350);
        BackColor = GuardianTheme.Window;
        ForeColor = GuardianTheme.Ink;
        Font = new Font("Segoe UI", 9);
        WindowChrome.ApplyGuardianChrome(this);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = GuardianTheme.Window
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));

        root.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(22, 10, 22, 8),
            Text = "◇  PUBLISH RELEASE",
            BackColor = GuardianTheme.SurfaceRaised,
            ForeColor = GuardianTheme.HotPink,
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        root.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(28, 22, 28, 12),
            Text =
                $"Publish {package.ReleaseTag} to {package.RepositorySlug}?\r\n\r\n" +
                $"Target branch: {branch}\r\n" +
                $"Source commit: {ShortCommit(package.SourceCommit)}\r\n" +
                $"Title: {title}\r\n\r\n" +
                "GitPet will upload exactly four verified release assets. Existing releases are never replaced, and no branch is force-pushed.",
            ForeColor = GuardianTheme.Ink,
            Font = new Font("Segoe UI", 10),
            TextAlign = ContentAlignment.TopLeft
        }, 0, 1);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(14, 14, 20, 10),
            BackColor = GuardianTheme.SurfaceRaised
        };
        var publish = MakeButton("Publish", 120, GuardianTheme.Violet, GuardianTheme.HotPink);
        publish.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
        var cancel = MakeButton("Cancel", 110, GuardianTheme.SurfaceSoft, GuardianTheme.Border);
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        footer.Controls.Add(publish);
        footer.Controls.Add(cancel);
        root.Controls.Add(footer, 0, 2);
        Controls.Add(root);
        AcceptButton = publish;
        CancelButton = cancel;
    }

    private static string ShortCommit(string value) =>
        string.IsNullOrWhiteSpace(value) ? "unknown" : value[..Math.Min(8, value.Length)];

    private static Button MakeButton(string text, int width, Color fill, Color border)
    {
        var button = new Button
        {
            Text = text,
            Width = width,
            Height = 38,
            Margin = new Padding(7, 0, 0, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = fill,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = border;
        return button;
    }
}
