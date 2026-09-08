using System.Net.Mail;

namespace ZomniverseGitPet;

internal sealed class GitIdentityForm : Form
{
    private static readonly Color Surface = Color.FromArgb(30, 23, 45);
    private static readonly Color PanelSurface = Color.FromArgb(45, 31, 66);
    private static readonly Color Ink = Color.FromArgb(236, 231, 246);
    private static readonly Color MutedInk = Color.FromArgb(190, 179, 208);
    private static readonly Color Purple = Color.FromArgb(112, 70, 180);
    private static readonly Color HotPink = Color.FromArgb(236, 70, 170);

    private readonly TextBox _name = new();
    private readonly TextBox _email = new();
    private readonly RadioButton _projectOnly = new();
    private readonly RadioButton _global = new();
    private readonly Label _validation = new();

    public GitIdentityForm(string projectName, string currentName, string currentEmail)
    {
        Text = "Git identity required";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(620, 500);
        BackColor = Surface;
        ForeColor = Ink;
        Font = new Font("Segoe UI", 9);

        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 64,
            Padding = new Padding(20, 14, 20, 6),
            Text = "◇ TELL GIT WHO IS MAKING THIS COMMIT",
            Font = new Font("Segoe UI", 13, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = PanelSurface
        };

        var intro = new Label
        {
            Dock = DockStyle.Top,
            Height = 110,
            Padding = new Padding(20, 12, 20, 8),
            ForeColor = Color.FromArgb(220, 212, 235),
            Text = $"Git needs an author name and email before it can create a restore-point commit for {projectName}.\r\n\r\n" +
                   "This information becomes part of the commit history. If you later push the commit online, other people may be able to see it. " +
                   "You can use a Git hosting noreply email if you prefer not to publish your personal address."
        };

        var form = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 6,
            Padding = new Padding(22, 12, 22, 8),
            BackColor = Surface
        };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        form.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _name.Text = currentName;
        _email.Text = currentEmail;
        ConfigureTextBox(_name);
        ConfigureTextBox(_email);

        form.Controls.Add(MakeLabel("Name"), 0, 0);
        form.Controls.Add(_name, 1, 0);
        form.Controls.Add(MakeLabel("Email"), 0, 1);
        form.Controls.Add(_email, 1, 1);

        _projectOnly.Text = "Use for this project only  — safest default";
        _projectOnly.Checked = true;
        _projectOnly.AutoSize = true;
        _projectOnly.ForeColor = Ink;
        _projectOnly.Dock = DockStyle.Fill;
        _projectOnly.Padding = new Padding(0, 6, 0, 0);

        _global.Text = "Use for all Git projects on this PC";
        _global.AutoSize = true;
        _global.ForeColor = Ink;
        _global.Dock = DockStyle.Fill;
        _global.Padding = new Padding(0, 6, 0, 0);

        form.Controls.Add(MakeLabel("Save identity"), 0, 2);
        form.Controls.Add(_projectOnly, 1, 2);
        form.Controls.Add(new Label(), 0, 3);
        form.Controls.Add(_global, 1, 3);

        _validation.Dock = DockStyle.Fill;
        _validation.ForeColor = Color.FromArgb(246, 159, 195);
        _validation.TextAlign = ContentAlignment.MiddleLeft;
        form.SetColumnSpan(_validation, 2);
        form.Controls.Add(_validation, 0, 4);

        var privacy = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = MutedInk,
            Padding = new Padding(0, 6, 0, 0),
            Text = "GitPet stores this identity using Git's own configuration. It is not sent anywhere by this dialog. " +
                   "A future manual push may publish it as commit metadata because that is how Git works."
        };
        form.SetColumnSpan(privacy, 2);
        form.Controls.Add(privacy, 0, 5);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 62,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12),
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

        Controls.Add(form);
        Controls.Add(buttons);
        Controls.Add(intro);
        Controls.Add(title);
    }

    public string IdentityName => _name.Text.Trim();
    public string IdentityEmail => _email.Text.Trim();
    public bool UseGlobal => _global.Checked;

    private void TryAccept()
    {
        var name = IdentityName;
        var email = IdentityEmail;
        if (name.Length == 0)
        {
            _validation.Text = "Please enter the name you want stored in Git commit history.";
            _name.Focus();
            return;
        }
        if (!LooksLikeEmail(email))
        {
            _validation.Text = "Please enter a valid email address, or a Git hosting noreply email.";
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
        box.Margin = new Padding(0, 7, 0, 7);
        box.BackColor = Color.FromArgb(24, 18, 36);
        box.ForeColor = Color.White;
        box.BorderStyle = BorderStyle.FixedSingle;
        box.Font = new Font("Segoe UI", 10);
    }

    private static Label MakeLabel(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        ForeColor = Color.FromArgb(215, 204, 232),
        TextAlign = ContentAlignment.MiddleLeft,
        Font = new Font("Segoe UI", 9, FontStyle.Bold)
    };

    private static Button MakeButton(string text, bool primary)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new Size(primary ? 190 : 90, 36),
            Height = 36,
            Margin = new Padding(6, 2, 0, 2),
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Purple : Color.FromArgb(65, 53, 83),
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };
        button.FlatAppearance.BorderSize = primary ? 2 : 1;
        button.FlatAppearance.BorderColor = primary ? HotPink : Color.FromArgb(110, 94, 132);
        return button;
    }
}
