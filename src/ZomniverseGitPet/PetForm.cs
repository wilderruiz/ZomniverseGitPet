namespace ZomniverseGitPet;

public sealed class PetForm : Form
{
    private readonly Label _status;
    private readonly NotifyIcon _tray;
    private Point _dragOffset;
    private bool _dragging;

    public bool AllowClose { get; set; }
    public bool RefreshInProgress { get; set; }

    public PetForm(Action showGuardian, Func<Task> chooseRepository, Action exit)
    {
        Text = "ZomniverseGitPet";
        Size = new Size(230, 170);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1200, 800);
        Location = new Point(area.Right - Width - 20, area.Bottom - Height - 20);
        TopMost = true;
        ShowInTaskbar = true;
        BackColor = Color.FromArgb(35, 35, 40);

        var face = new Label
        {
            Text = "🐾", Font = new Font("Segoe UI Emoji", 38), TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.White, Bounds = new Rectangle(75, 8, 80, 80)
        };
        var title = new Label
        {
            Text = "ZomniverseGitPet", Font = new Font("Segoe UI", 10, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.White, Bounds = new Rectangle(5, 88, 220, 22)
        };
        _status = new Label
        {
            Text = "Checking repository...", Font = new Font("Segoe UI", 9), TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.Gainsboro, Bounds = new Rectangle(5, 112, 220, 45)
        };
        Controls.AddRange([face, title, _status]);

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open Guardian", null, (_, _) => showGuardian());
        menu.Items.Add("Choose repository", null, async (_, _) => await chooseRepository());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit ZomniverseGitPet", null, (_, _) => exit());
        ContextMenuStrip = menu;
        foreach (Control control in new Control[] { face, title, _status }) control.ContextMenuStrip = menu;

        foreach (Control control in new Control[] { this, face, title, _status })
        {
            control.DoubleClick += (_, _) => showGuardian();
            control.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) { _dragging = true; _dragOffset = e.Location; } };
            control.MouseMove += (_, _) => { if (_dragging) Location = new Point(Cursor.Position.X - _dragOffset.X, Cursor.Position.Y - _dragOffset.Y); };
            control.MouseUp += (_, _) => _dragging = false;
        }

        _tray = new NotifyIcon
        {
            Icon = SystemIcons.Application, Text = "ZomniverseGitPet", Visible = true, ContextMenuStrip = menu
        };
        _tray.DoubleClick += (_, _) => showGuardian();
        FormClosing += (_, e) => { if (!AllowClose) { e.Cancel = true; Hide(); } };
    }

    public void SetNeedsRepository()
    {
        BackColor = Color.FromArgb(45, 60, 90);
        _status.Text = "Choose a repository\r\nto begin";
    }

    public void SetStatus(RepositoryStatus status)
    {
        if (!status.Healthy) { SetError(status.Error); return; }
        if (status.Files.Count == 0)
        {
            BackColor = Color.FromArgb(30, 80, 55);
            _status.Text = $"Clean ✓\r\n{status.Branch}";
        }
        else
        {
            BackColor = Color.FromArgb(105, 80, 25);
            _status.Text = $"{status.Files.Count} changed item(s)\r\nReady to review";
        }
    }

    public void SetError(string error)
    {
        BackColor = Color.FromArgb(90, 35, 35);
        _status.Text = "Git problem\r\nOpen Guardian";
        _tray.Text = error.Length > 60 ? error[..60] : error;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _tray.Visible = false; _tray.Dispose(); }
        base.Dispose(disposing);
    }
}

