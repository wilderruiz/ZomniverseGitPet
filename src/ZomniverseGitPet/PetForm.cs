namespace ZomniverseGitPet;

public sealed class PetForm : Form
{
    // A deep brand-violet key keeps anti-aliased PNG edges visually compatible
    // with the fox outline instead of producing a bright magenta fringe.
    private static readonly Color TransparencyColor = Color.FromArgb(32, 20, 48);

    private readonly PetMessageBubble _bubble;
    private readonly PictureBox _fox;
    private readonly NotifyIcon _tray;
    private readonly PetAssets _assets;
    private readonly ToolTip _toolTip;
    private readonly PetChromeButton _minimize;
    private readonly PetChromeButton _close;
    private Rectangle _normalFoxBounds = new(40, 0, 160, 160);
    private Point _dragOffset;
    private bool _dragging;
    private Image _stateImage;

    public bool AllowClose { get; set; }
    public bool RefreshInProgress { get; set; }

    public PetForm(Action showGuardian, Func<Task> chooseRepository, Action exit)
    {
        Text = "ZomniverseGitPet";
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(240, 238);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1200, 800);
        Location = new Point(area.Right - Width - 20, area.Bottom - Height - 20);
        TopMost = true;
        ShowInTaskbar = true;
        BackColor = TransparencyColor;
        TransparencyKey = TransparencyColor;

        _assets = new PetAssets();
        _stateImage = _assets.Idle;
        _fox = new PictureBox
        {
            Image = _stateImage,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent,
            Bounds = _normalFoxBounds,
            TabStop = false
        };
        _bubble = new PetMessageBubble
        {
            Location = new Point(10, 0)
        };
        _bubble.SetMessage("Checking repository...");

        _minimize = CreatePetButton("—", Point.Empty, (_, _) => WindowState = FormWindowState.Minimized);
        _close = CreatePetButton("×", Point.Empty, (_, _) => exit());
        Controls.AddRange([_bubble, _fox, _minimize, _close]);
        LayoutPet();

        _toolTip = new ToolTip { AutomaticDelay = 350, AutoPopDelay = 7000, ReshowDelay = 100 };
        _toolTip.SetToolTip(_fox, "Double-click to open Guardian • Drag to move • Right-click for options");
        _toolTip.SetToolTip(_bubble, "Repository status • Scroll for longer messages • Double-click to open Guardian");
        _toolTip.SetToolTip(_minimize, "Minimize pet to the Windows taskbar");
        _toolTip.SetToolTip(_close, "Close ZomniverseGitPet");

        var menu = new ContextMenuStrip();
        menu.Items.Add("Show pet", null, (_, _) => ShowPet());
        menu.Items.Add("Open Guardian", null, (_, _) => showGuardian());
        menu.Items.Add("Choose repository", null, async (_, _) => await chooseRepository());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit ZomniverseGitPet", null, (_, _) => exit());
        ContextMenuStrip = menu;
        foreach (Control control in new Control[] { _fox, _bubble }) control.ContextMenuStrip = menu;

        foreach (Control control in new Control[] { this, _fox, _bubble })
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

        _bubble.BubbleDoubleClick += (_, _) => showGuardian();
        _fox.MouseEnter += (_, _) =>
        {
            _fox.Image = _assets.Happy;
            _fox.Bounds = new Rectangle(_normalFoxBounds.X - 2, _normalFoxBounds.Y - 2, 164, 164);
        };
        _fox.MouseLeave += (_, _) =>
        {
            _fox.Image = _stateImage;
            _fox.Bounds = _normalFoxBounds;
        };

        _tray = new NotifyIcon
        {
            Icon = SystemIcons.Application, Text = "ZomniverseGitPet", Visible = true, ContextMenuStrip = menu
        };
        _tray.DoubleClick += (_, _) => showGuardian();
        FormClosing += (_, e) => { if (!AllowClose) { e.Cancel = true; Hide(); } };
    }

    public void SetNeedsRepository()
    {
        SetPetState(_assets.Idle, "Choose a repository");
    }

    public void SetStatus(RepositoryStatus status)
    {
        if (!status.Healthy) { SetError(status.Error); return; }
        if (status.Files.Count == 0)
        {
            SetPetState(_assets.Happy, $"Clean ✓\r\n{status.Branch}");
        }
        else
        {
            SetPetState(_assets.ReviewReady, $"{status.Files.Count} changed item(s)\r\nReady to review");
        }
    }

    public void SetError(string error)
    {
        SetPetState(_assets.Warning, "Git problem\r\nOpen Guardian");
        _tray.Text = error.Length > 60 ? error[..60] : error;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _tray.Visible = false; _tray.Dispose(); _toolTip.Dispose(); _assets.Dispose(); }
        base.Dispose(disposing);
    }

    private static PetChromeButton CreatePetButton(string text, Point location, EventHandler onClick)
    {
        var button = new PetChromeButton
        {
            Text = text,
            Location = location,
            AccessibleName = text == "×" ? "Close ZomniverseGitPet" : "Minimize ZomniverseGitPet"
        };
        button.Click += onClick;
        return button;
    }

    private void SetPetState(Image image, string message)
    {
        _stateImage = image;
        _fox.Image = image;
        _bubble.SetMessage(message);
        LayoutPet();
    }

    private void LayoutPet()
    {
        var bottom = Bottom;
        _normalFoxBounds = new Rectangle(40, _bubble.Bottom - 2, 160, 160);
        _fox.Bounds = _normalFoxBounds;
        _minimize.Location = new Point(176, _normalFoxBounds.Top + 4);
        _close.Location = new Point(205, _normalFoxBounds.Top + 4);
        _minimize.BringToFront();
        _close.BringToFront();
        ClientSize = new Size(240, _fox.Bottom + 4);
        if (Visible) Top = bottom - Height;
    }

    private void ShowPet()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }
}

