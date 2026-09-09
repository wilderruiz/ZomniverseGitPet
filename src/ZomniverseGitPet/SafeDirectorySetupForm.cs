namespace ZomniverseGitPet;

internal sealed class SafeDirectorySetupForm : Form
{
    private static readonly Color Ink = Color.FromArgb(236, 231, 246);
    private static readonly Color MutedInk = Color.FromArgb(188, 176, 208);
    private static readonly Color Surface = Color.FromArgb(30, 23, 45);
    private static readonly Color PanelSurface = Color.FromArgb(45, 31, 66);
    private static readonly Color PreviewSurface = Color.FromArgb(22, 17, 34);
    private static readonly Color Purple = Color.FromArgb(112, 70, 180);
    private static readonly Color HotPink = Color.FromArgb(236, 70, 170);

    public SafeDirectorySetupForm(string projectPath, string gitMessage)
    {
        ProjectPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(projectPath));

        Text = "Trust project folder";
        Icon = AppIconProvider.Icon;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        ShowInTaskbar = false;
        MinimumSize = new Size(720, 560);
        ClientSize = new Size(860, 680);
        BackColor = Surface;
        ForeColor = Ink;
        Font = new Font("Segoe UI", 9);
        WindowChrome.ApplyGuardianChrome(this);

        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 78,
            Padding = new Padding(22, 14, 22, 8),
            Text = "◇ TRUST THIS PROJECT FOLDER?\r\nGit blocked the new repository because Windows reports a different folder owner.",
            Font = new Font("Segoe UI", 13.5f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = PanelSurface
        };

        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(22, 18, 22, 16),
            BackColor = Surface
        };
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 106));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 116));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var explanation = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Ink,
            Text = "Git uses an ownership safety check to prevent one Windows account from silently using a repository owned by another account.\r\n\r\n" +
                   "If this is a folder you intentionally selected and you trust its contents, GitPet can mark only this exact folder as safe for your Windows user."
        };

        var pathPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(38, 28, 55), Padding = new Padding(14, 10, 14, 10) };
        var pathLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            Text = "PROJECT FOLDER",
            ForeColor = MutedInk,
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };
        var pathBox = new TextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            Text = ProjectPath,
            BackColor = PreviewSurface,
            ForeColor = Ink,
            BorderStyle = BorderStyle.FixedSingle
        };
        pathPanel.Controls.Add(pathBox);
        pathPanel.Controls.Add(pathLabel);

        var effectPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(38, 28, 55), Padding = new Padding(14, 10, 14, 10) };
        var effect = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(219, 211, 234),
            Text = "WHAT WILL CHANGE\r\n\r\nGitPet will add this one absolute folder path to your personal Git safe.directory list. " +
                   "It will not trust every folder, change Windows ownership or permissions, stage files, commit, configure a remote, pull, or push."
        };
        effectPanel.Controls.Add(effect);

        var detailsPanel = new Panel { Dock = DockStyle.Fill, BackColor = PreviewSurface, Padding = new Padding(12) };
        var detailsHeader = new Label
        {
            Dock = DockStyle.Top,
            Height = 30,
            Text = "GIT DETAILS · read-only",
            ForeColor = MutedInk,
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };
        var details = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            BackColor = PreviewSurface,
            ForeColor = Color.FromArgb(220, 211, 235),
            Font = new Font("Cascadia Mono", 9f),
            WordWrap = true,
            DetectUrls = false,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            Text = gitMessage
        };
        detailsPanel.Controls.Add(details);
        detailsPanel.Controls.Add(detailsHeader);

        body.Controls.Add(explanation, 0, 0);
        body.Controls.Add(pathPanel, 0, 1);
        body.Controls.Add(effectPanel, 0, 2);
        body.Controls.Add(detailsPanel, 0, 3);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 66,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12, 13, 12, 9),
            BackColor = PanelSurface
        };

        var trust = MakeButton("Trust this folder & continue", true);
        var cancel = MakeButton("Cancel", false);
        trust.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        buttons.Controls.Add(trust);
        buttons.Controls.Add(cancel);
        AcceptButton = trust;
        CancelButton = cancel;

        Controls.Add(body);
        Controls.Add(buttons);
        Controls.Add(title);
    }

    public string ProjectPath { get; }

    private static Button MakeButton(string text, bool primary)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new Size(primary ? 220 : 90, 38),
            Height = 38,
            Margin = new Padding(6, 2, 0, 2),
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Purple : Color.FromArgb(65, 53, 83),
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderSize = primary ? 2 : 1;
        button.FlatAppearance.BorderColor = primary ? HotPink : Color.FromArgb(110, 94, 132);
        button.FlatAppearance.MouseOverBackColor = primary ? GuardianTheme.VioletHover : GuardianTheme.SurfaceRaised;
        return button;
    }
}
