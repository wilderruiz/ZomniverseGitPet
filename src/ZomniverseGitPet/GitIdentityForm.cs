using System.Net.Mail;

namespace ZomniverseGitPet;

internal sealed class GitIdentityForm : Form
{
    private static readonly Color Surface = Color.FromArgb(27, 20, 40);
    private static readonly Color PanelSurface = Color.FromArgb(45, 31, 66);
    private static readonly Color CardSurface = Color.FromArgb(35, 27, 51);
    private static readonly Color InputSurface = Color.FromArgb(22, 17, 33);
    private static readonly Color Ink = Color.FromArgb(242, 237, 249);
    private static readonly Color MutedInk = Color.FromArgb(187, 176, 205);
    private static readonly Color Purple = Color.FromArgb(112, 70, 180);
    private static readonly Color HotPink = Color.FromArgb(236, 70, 170);
    private static readonly Color SoftPink = Color.FromArgb(246, 159, 195);

    private readonly TextBox _name = new();
    private readonly TextBox _email = new();
    private readonly RadioButton _projectOnly = new();
    private readonly RadioButton _global = new();
    private readonly Label _validation = new();

    public GitIdentityForm(string projectName, string currentName, string currentEmail)
    {
        Text = "Git identity required";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        Size = new Size(900, 760);
        MinimumSize = new Size(760, 640);
        MaximumSize = new Size(1200, 960);
        BackColor = Surface;
        ForeColor = Ink;
        Font = new Font("Segoe UI", 9);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Surface
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 224));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildIntro(projectName), 0, 1);
        root.Controls.Add(BuildIdentityFields(currentName, currentEmail), 0, 2);
        root.Controls.Add(BuildPrivacyCard(), 0, 3);
        root.Controls.Add(BuildButtons(), 0, 4);

        Controls.Add(root);
    }

    public string IdentityName => _name.Text.Trim();
    public string IdentityEmail => _email.Text.Trim();
    public bool UseGlobal => _global.Checked;

    private Control BuildHeader()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = PanelSurface,
            Padding = new Padding(30, 18, 30, 16)
        };

        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 44,
            Text = "◇  GIT IDENTITY",
            Font = new Font("Segoe UI", 15.5f, FontStyle.Bold),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var subtitle = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Who should sign this restore point?",
            Font = new Font("Segoe UI", 10.5f, FontStyle.Regular),
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
            Padding = new Padding(32, 20, 32, 12)
        };

        var project = new Label
        {
            Dock = DockStyle.Top,
            Height = 30,
            Text = $"RESTORE POINT  ·  {projectName}",
            ForeColor = Color.FromArgb(212, 188, 245),
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };

        var explanation = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Git adds an author name and email to every commit. Enter the identity you want attached to this restore point. " +
                   "Nothing is sent anywhere by this screen.",
            ForeColor = Color.FromArgb(224, 216, 237),
            Font = new Font("Segoe UI", 10.5f),
            TextAlign = ContentAlignment.TopLeft,
            Padding = new Padding(0, 12, 0, 0)
        };

        panel.Controls.Add(explanation);
        panel.Controls.Add(project);
        return panel;
    }

    private Control BuildIdentityFields(string currentName, string currentEmail)
    {
        var outer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Padding = new Padding(32, 0, 32, 14)
        };

        var card = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5,
            Padding = new Padding(22, 14, 22, 12),
            BackColor = CardSurface
        };
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _name.Text = currentName;
        _email.Text = currentEmail;
        ConfigureTextBox(_name);
        ConfigureTextBox(_email);

        card.Controls.Add(MakeFieldLabel("DISPLAY NAME"), 0, 0);
        card.Controls.Add(_name, 1, 0);
        card.Controls.Add(MakeFieldLabel("EMAIL ADDRESS"), 0, 1);
        card.Controls.Add(_email, 1, 1);

        _projectOnly.Text = "This project only   ·   recommended";
        _projectOnly.Checked = true;
        ConfigureScopeChoice(_projectOnly, true);

        _global.Text = "All Git projects on this PC";
        ConfigureScopeChoice(_global, false);

        card.Controls.Add(MakeFieldLabel("SAVE FOR"), 0, 2);
        card.Controls.Add(_projectOnly, 1, 2);
        card.Controls.Add(new Label(), 0, 3);
        card.Controls.Add(_global, 1, 3);

        _validation.Dock = DockStyle.Fill;
        _validation.ForeColor = SoftPink;
        _validation.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        _validation.TextAlign = ContentAlignment.MiddleLeft;
        _validation.Padding = new Padding(0, 2, 0, 0);
        card.SetColumnSpan(_validation, 2);
        card.Controls.Add(_validation, 0, 4);

        outer.Controls.Add(card);
        return outer;
    }

    private Control BuildPrivacyCard()
    {
        var outer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Padding = new Padding(32, 8, 32, 16)
        };

        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(39, 29, 57),
            Padding = new Padding(20, 14, 16, 12)
        };

        var heading = new Label
        {
            Dock = DockStyle.Top,
            Height = 30,
            Text = "◉  PRIVACY NOTE",
            ForeColor = Color.FromArgb(213, 188, 245),
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var privacy = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            BackColor = Color.FromArgb(39, 29, 57),
            ForeColor = MutedInk,
            Font = new Font("Segoe UI", 9.5f),
            WordWrap = true,
            DetectUrls = false,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            TabStop = false,
            Cursor = Cursors.Arrow,
            Text = "Git stores this identity in its own configuration. If you later push the commit online, the name and email may become visible as commit metadata. " +
                   "You can use a Git hosting noreply email if you prefer not to publish your personal address."
        };

        card.Controls.Add(privacy);
        card.Controls.Add(heading);
        outer.Controls.Add(card);
        return outer;
    }

    private Control BuildButtons()
    {
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Height = 78,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(18, 19, 24, 16),
            BackColor = PanelSurface
        };

        var save = MakeButton("Save & retry restore point", true);
        var cancel = MakeButton("Cancel", false);
        save.Click += (_, _) => TryAccept();
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        AcceptButton = save;
        CancelButton = cancel;
        return buttons;
    }

    private void TryAccept()
    {
        var name = IdentityName;
        var email = IdentityEmail;
        if (name.Length == 0)
        {
            _validation.Text = "Enter the name you want shown in Git commit history.";
            _name.Focus();
            return;
        }
        if (!LooksLikeEmail(email))
        {
            _validation.Text = "Enter a valid email address or a Git hosting noreply email.";
            _email.Focus();
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private static bool LooksLikeEmail(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        try
        {
            var address = new MailAddress(value);
            return address.Address.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase) && value.Contains('@');
        }
        catch { return false; }
    }

    private static void ConfigureTextBox(TextBox box)
    {
        box.Dock = DockStyle.Fill;
        box.Margin = new Padding(0, 6, 0, 6);
        box.BackColor = InputSurface;
        box.ForeColor = Color.White;
        box.BorderStyle = BorderStyle.FixedSingle;
        box.Font = new Font("Segoe UI", 10.5f);
    }

    private static void ConfigureScopeChoice(RadioButton button, bool primary)
    {
        button.AutoSize = true;
        button.Dock = DockStyle.Fill;
        button.Padding = new Padding(0, 4, 0, 0);
        button.ForeColor = primary ? Ink : Color.FromArgb(207, 197, 223);
        button.Font = new Font("Segoe UI", 9.5f, primary ? FontStyle.Bold : FontStyle.Regular);
        button.Cursor = Cursors.Hand;
    }

    private static Label MakeFieldLabel(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        ForeColor = Color.FromArgb(180, 164, 204),
        TextAlign = ContentAlignment.MiddleLeft,
        Font = new Font("Segoe UI", 8, FontStyle.Bold)
    };

    private static Button MakeButton(string text, bool primary)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new Size(primary ? 218 : 96, 40),
            Height = 40,
            Margin = new Padding(8, 0, 0, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Purple : Color.FromArgb(65, 53, 83),
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderSize = primary ? 2 : 1;
        button.FlatAppearance.BorderColor = primary ? HotPink : Color.FromArgb(110, 94, 132);
        button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(132, 79, 198) : Color.FromArgb(78, 64, 98);
        button.FlatAppearance.MouseDownBackColor = primary ? Color.FromArgb(91, 54, 146) : Color.FromArgb(54, 43, 70);
        return button;
    }
}
