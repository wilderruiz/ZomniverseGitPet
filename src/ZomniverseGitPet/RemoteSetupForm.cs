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
        Size = new Size(820, 610);
        MinimumSize = new Size(700, 540);
        MaximumSize = new Size(1100, 820);
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
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 106));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 128));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));

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
            Padding = new Padding(30, 17, 30, 14)
        };

        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 44,
            Text = "◇  CONNECT REMOTE",
            Font = new Font("Segoe UI", 15.5f, FontStyle.Bold),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var subtitle = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Tell GitPet where this project should be pushed.",
            Font = new Font("Segoe UI", 10.5f),
            ForeColor = Color.FromArgb(199, 186, 220),
            TextAlign = ContentAlignment.MiddleLeft
        };

        panel.Controls.Add(subtitle);
        panel.Controls.Add(title);
        return panel;
    }

    private Control BuildIntro(string projectName)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(32, 20, 32, 10),
            Text = $"PROJECT  ·  {projectName}\n\n" +
                   "Your restore point is already saved locally. To push it online, Git needs the address of an existing remote repository. " +
                   "Create the repository on your Git hosting service first, then paste its clone URL below.",
            ForeColor = Color.FromArgb(224, 216, 237),
            Font = new Font("Segoe UI", 10.5f),
            TextAlign = ContentAlignment.TopLeft,
            AutoEllipsis = true
        };
    }

    private Control BuildUrlCard()
    {
        var outer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Padding = new Padding(32, 0, 32, 12)
        };

        var card = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(18, 12, 18, 10),
            BackColor = CardSurface
        };
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        card.Controls.Add(new Label
        {
            Text = "REMOTE URL  ·  saved as origin",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(194, 171, 229),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        _remoteUrl.Dock = DockStyle.Fill;
        _remoteUrl.Margin = new Padding(0, 3, 0, 3);
        _remoteUrl.BackColor = InputSurface;
        _remoteUrl.ForeColor = Color.White;
        _remoteUrl.BorderStyle = BorderStyle.FixedSingle;
        _remoteUrl.Font = new Font("Cascadia Mono", 9.5f);
        card.Controls.Add(_remoteUrl, 0, 1);

        _validation.Dock = DockStyle.Fill;
        _validation.ForeColor = SoftPink;
        _validation.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        _validation.TextAlign = ContentAlignment.MiddleLeft;
        _validation.Text = "Examples: https://github.com/user/project.git   or   git@github.com:user/project.git";
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
            Padding = new Padding(32, 6, 32, 16)
        };

        var text = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            BackColor = Color.FromArgb(38, 29, 54),
            ForeColor = MutedInk,
            Font = new Font("Segoe UI", 9.5f),
            WordWrap = true,
            DetectUrls = false,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            TabStop = false,
            Cursor = Cursors.Arrow,
            Text = "WHAT WILL HAPPEN\n\n" +
                   "GitPet will add exactly one local Git remote named 'origin' using the address you provide. It will not create an online repository, change an existing remote, stage files, make another commit, or push automatically. " +
                   "After the connection succeeds, GitPet will show the normal Push confirmation before anything is sent."
        };

        outer.Controls.Add(text);
        return outer;
    }

    private Control BuildButtons()
    {
        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(18, 18, 24, 14),
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
