namespace ZomniverseGitPet;

internal sealed class RemoteSetupForm : Form
{
    private static readonly Color Surface = Color.FromArgb(18, 15, 27);
    private static readonly Color PanelSurface = Color.FromArgb(34, 25, 50);
    private static readonly Color CardSurface = Color.FromArgb(27, 21, 39);
    private static readonly Color InputSurface = Color.FromArgb(13, 11, 20);
    private static readonly Color Ink = Color.FromArgb(242, 237, 249);
    private static readonly Color MutedInk = Color.FromArgb(184, 173, 202);
    private static readonly Color Purple = Color.FromArgb(112, 70, 180);
    private static readonly Color HotPink = Color.FromArgb(236, 70, 170);
    private static readonly Color SoftPink = Color.FromArgb(246, 159, 195);

    private readonly TextBox _remoteUrl = new();
    private readonly Label _validation = new();

    public RemoteSetupForm(string projectName)
    {
        Text = "Connect remote";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        Size = new Size(980, 780);
        MinimumSize = new Size(760, 620);
        MaximumSize = new Size(1300, 1000);
        BackColor = Surface;
        ForeColor = Ink;
        Font = new Font("Segoe UI", 9);
        Icon = AppIconProvider.Icon;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Surface
        };

        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 116));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildIntro(projectName), 0, 1);
        root.Controls.Add(BuildUrlCard(), 0, 2);
        root.Controls.Add(BuildSafetyCard(), 0, 3);
        root.Controls.Add(BuildButtons(), 0, 4);
        Controls.Add(root);
    }

    public string RemoteUrl => _remoteUrl.Text.Trim();

    private Control BuildHeader()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = PanelSurface,
            Padding = new Padding(34, 20, 34, 16)
        };

        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 46,
            Text = "◇  CONNECT REMOTE",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var subtitle = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Tell GitPet where this project should be pushed.",
            Font = new Font("Segoe UI", 10.5f),
            ForeColor = Color.FromArgb(199, 186, 220),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };

        panel.Controls.Add(subtitle);
        panel.Controls.Add(title);
        return panel;
    }

    private Control BuildIntro(string projectName)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Padding = new Padding(38, 22, 38, 12)
        };

        var project = new Label
        {
            Dock = DockStyle.Top,
            Height = 34,
            Text = $"PROJECT  ·  {projectName}",
            ForeColor = Color.FromArgb(212, 188, 245),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };

        var explanation = new Label
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 12, 0, 0),
            Text = "Your checkpoint is already saved safely on this PC. To push it online, Git needs the address of an existing remote repository. " +
                   "Create that repository on your Git hosting service first, copy its clone URL, then paste it below.",
            ForeColor = Color.FromArgb(224, 216, 237),
            Font = new Font("Segoe UI", 10.5f),
            TextAlign = ContentAlignment.TopLeft
        };

        panel.Controls.Add(explanation);
        panel.Controls.Add(project);
        return panel;
    }

    private Control BuildUrlCard()
    {
        var outer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Padding = new Padding(38, 0, 38, 18)
        };

        var card = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(22, 16, 22, 14),
            BackColor = CardSurface
        };

        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        card.Controls.Add(new Label
        {
            Text = "REMOTE URL  ·  SAVED LOCALLY AS  origin",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(194, 171, 229),
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        _remoteUrl.Dock = DockStyle.Fill;
        _remoteUrl.Margin = new Padding(0, 7, 0, 7);
        _remoteUrl.BackColor = InputSurface;
        _remoteUrl.ForeColor = Color.White;
        _remoteUrl.BorderStyle = BorderStyle.FixedSingle;
        _remoteUrl.Font = new Font("Cascadia Mono", 10.5f);
        card.Controls.Add(_remoteUrl, 0, 1);

        _validation.Dock = DockStyle.Fill;
        _validation.Padding = new Padding(0, 8, 0, 0);
        _validation.ForeColor = SoftPink;
        _validation.Font = new Font("Segoe UI", 9);
        _validation.TextAlign = ContentAlignment.TopLeft;
        _validation.Text =
            "Paste the clone URL shown by your Git hosting service.\n" +
            "Examples:  https://github.com/user/project.git    or    git@github.com:user/project.git";
        card.Controls.Add(_validation, 0, 2);

        outer.Controls.Add(card);
        return outer;
    }

    private Control BuildSafetyCard()
    {
        var outer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Padding = new Padding(38, 4, 38, 18)
        };

        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(38, 29, 54),
            Padding = new Padding(22, 16, 18, 14)
        };

        var heading = new Label
        {
            Dock = DockStyle.Top,
            Height = 34,
            Text = "WHAT WILL HAPPEN",
            ForeColor = Color.FromArgb(213, 188, 245),
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var text = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            BackColor = Color.FromArgb(38, 29, 54),
            ForeColor = MutedInk,
            Font = new Font("Segoe UI", 10),
            WordWrap = true,
            DetectUrls = false,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            TabStop = false,
            Cursor = Cursors.Arrow,
            Text = "GitPet will add exactly one local Git remote named 'origin' using the address you provide.\n\n" +
                   "It will NOT create an online repository, replace an existing remote, stage files, create another checkpoint, or push automatically.\n\n" +
                   "After the connection succeeds, GitPet will return you to the normal Push confirmation. Nothing is sent online until you explicitly approve that Push."
        };

        card.Controls.Add(text);
        card.Controls.Add(heading);
        outer.Controls.Add(card);
        return outer;
    }

    private Control BuildButtons()
    {
        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(18, 20, 28, 16),
            BackColor = PanelSurface
        };

        var connect = MakeButton("Connect origin & continue", true);
        var cancel = MakeButton("Cancel", false);
        connect.Click += (_, _) => TryAccept();
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        footer.Controls.Add(connect);
        footer.Controls.Add(cancel);
        AcceptButton = connect;
        CancelButton = cancel;
        return footer;
    }

    private void TryAccept()
    {
        var value = RemoteUrl;
        if (value.Length == 0)
        {
            _validation.Text = "Paste the clone URL of the existing remote repository.";
            _remoteUrl.Focus();
            return;
        }

        if (value.Length > 2048 || value.Contains('\r') || value.Contains('\n'))
        {
            _validation.Text = "That remote address does not look valid. Paste one clone URL only.";
            _remoteUrl.Focus();
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private static Button MakeButton(string text, bool primary)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new Size(primary ? 220 : 96, 40),
            Height = 40,
            Margin = new Padding(8, 0, 0, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Purple : Color.FromArgb(64, 51, 80),
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderSize = primary ? 2 : 1;
        button.FlatAppearance.BorderColor = primary ? HotPink : Color.FromArgb(105, 90, 126);
        button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(132, 79, 198) : Color.FromArgb(78, 64, 98);
        return button;
    }
}
