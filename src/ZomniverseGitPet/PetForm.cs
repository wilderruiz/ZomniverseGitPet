namespace ZomniverseGitPet;

public sealed class PetForm : Form
{
    private static readonly Color NeutralBackground = Color.FromArgb(247, 243, 252);
    private static readonly Color HealthyBackground = Color.FromArgb(239, 249, 243);
    private static readonly Color ReviewBackground = Color.FromArgb(255, 248, 233);
    private static readonly Color WarningBackground = Color.FromArgb(255, 244, 235);

    private readonly Label _status;
    private readonly PictureBox _fox;
    private readonly NotifyIcon _tray;
    private readonly PetAssets _assets;
    private Point _dragOffset;
    private bool _dragging;

    public bool AllowClose { get; set; }
    public bool RefreshInProgress { get; set; }

    public PetForm(Action showGuardian, Func<Task> chooseRepository, Action exit)
    {
        Text = "ZomniverseGitPet";
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(230, 224);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1200, 800);
        Location = new Point(area.Right - Width - 20, area.Bottom - Height - 20);
        TopMost = true;
        ShowInTaskbar = true;
        BackColor = NeutralBackground;

        _assets = new PetAssets();
        _fox = new PictureBox
        {
            Image = _assets.Idle,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent,
            Bounds = new Rectangle(35, 8, 160, 160),
            TabStop = false
        };
        _status = new Label
        {
            Text = "Checking repository...", Font = new Font("Segoe UI", 9, FontStyle.Bold), TextAlign = ContentAlignment.TopCenter,
            ForeColor = Color.FromArgb(55, 35, 86), Bounds = new Rectangle(8, 172, 214, 44)
        };
        Controls.AddRange([_fox, _status]);

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open Guardian", null, (_, _) => showGuardian());
        menu.Items.Add("Choose repository", null, async (_, _) => await chooseRepository());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit ZomniverseGitPet", null, (_, _) => exit());
        ContextMenuStrip = menu;
        foreach (Control control in new Control[] { _fox, _status }) control.ContextMenuStrip = menu;

        foreach (Control control in new Control[] { this, _fox, _status })
        {
            control.DoubleClick += (_, _) => showGuardian();
            control.MouseDown += (_, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    _dragging = true;
                    _dragOffset = PointToClient(control.PointToScreen(e.Location));
                }
            };
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
        _fox.Image = _assets.Idle;
        BackColor = NeutralBackground;
        _status.Text = "Choose a repository";
    }

    public void SetStatus(RepositoryStatus status)
    {
        if (!status.Healthy) { SetError(status.Error); return; }
        if (status.Files.Count == 0)
        {
            _fox.Image = _assets.Happy;
            BackColor = HealthyBackground;
            _status.Text = $"Clean ✓\r\n{status.Branch}";
        }
        else
        {
            _fox.Image = _assets.ReviewReady;
            BackColor = ReviewBackground;
            _status.Text = $"{status.Files.Count} changed item(s)\r\nReady to review";
        }
    }

    public void SetError(string error)
    {
        _fox.Image = _assets.Warning;
        BackColor = WarningBackground;
        _status.Text = "Git problem\r\nOpen Guardian";
        _tray.Text = error.Length > 60 ? error[..60] : error;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _tray.Visible = false; _tray.Dispose(); _assets.Dispose(); }
        base.Dispose(disposing);
    }
}

