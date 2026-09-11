namespace ZomniverseGitPet;

public sealed class PetForm : Form
{
    private static readonly Color TransparencyColor = Color.FromArgb(32, 20, 48);
    private static readonly int[] IncomingBounceOffsets = [0, -5, -11, -15, -9, -3, 0, -6, 0];

    private readonly PetMessageBubble _bubble;
    private readonly PictureBox _fox;
    private readonly NotifyIcon _tray;
    private readonly PetAssets _assets;
    private readonly ToolTip _toolTip;
    private readonly PetChromeButton _minimize;
    private readonly PetChromeButton _close;
    private readonly System.Windows.Forms.Timer _incomingAnimationTimer = new() { Interval = 130 };
    private readonly System.Windows.Forms.Timer _incomingHoldTimer = new() { Interval = 6500 };
    private Rectangle _normalFoxBounds = new(40, 0, 160, 160);
    private Point _dragOffset;
    private bool _dragging;
    private Image _stateImage;
    private string _stateMessage = "● CHECKING\nRepository status";
    private bool _incomingAlertActive;

    /* ==========================================================================
       PATCH: PERSISTENT PET GUIDANCE STATE
       FUNCTION:
       Prevents routine repository refreshes from replacing guidance required by an open dialog.

       DATE.TIME ADDED: 2026-09-11 17:34 +03:00

       REASON:
       Keep contextual instructions visible until the related dialog closes.
       ========================================================================== */
    private bool _guidanceHoldActive;
    private int _incomingAnimationFrame;
    private int _lastBehind;
    private string _lastSyncBranch = "";
    private string _lastSyncRepository = "";

    public bool AllowClose { get; set; }
    public bool RefreshInProgress { get; set; }

    public PetForm(Action showGuardian, Func<Task> chooseRepository, Action exit)
    {
        Text = "ZomniverseGitPet";
        Icon = AppIconProvider.Icon;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(240, 246);
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
            Location = new Point(6, 0)
        };
        _bubble.SetMessage(_stateMessage);

        _minimize = CreatePetButton(PetChromeKind.Minimize, (_, _) => WindowState = FormWindowState.Minimized);
        _close = CreatePetButton(PetChromeKind.CloseRibbon, (_, _) => exit());

        Controls.AddRange([_bubble, _fox, _minimize, _close]);
        LayoutPet();

        _toolTip = new ToolTip { AutomaticDelay = 350, AutoPopDelay = 7000, ReshowDelay = 100 };
        _toolTip.SetToolTip(_fox, "Double-click to open Guardian • Drag to move • Right-click for options");
        _toolTip.SetToolTip(_bubble, "Repository status • Scroll for longer messages • Double-click to open Guardian");
        _toolTip.SetToolTip(_minimize, "Minimize the pet to the Windows taskbar");
        _toolTip.SetToolTip(_close, "Exit ZomniverseGitPet");

        var menu = new ContextMenuStrip();
        menu.Items.Add("Show pet", null, (_, _) => ShowPet());
        menu.Items.Add("Open Guardian", null, (_, _) => showGuardian());
        menu.Items.Add("Projects…", null, async (_, _) => await chooseRepository());
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
            control.MouseMove += (_, _) =>
            {
                if (_dragging)
                    Location = new Point(Cursor.Position.X - _dragOffset.X, Cursor.Position.Y - _dragOffset.Y);
            };
            control.MouseUp += (_, _) => _dragging = false;
        }

        _bubble.BubbleDoubleClick += (_, _) => showGuardian();
        _fox.MouseEnter += (_, _) =>
        {
            if (_incomingAlertActive) return;
            _fox.Image = _assets.Happy;
            _fox.Bounds = new Rectangle(_normalFoxBounds.X - 2, _normalFoxBounds.Y - 2, 164, 164);
        };
        _fox.MouseLeave += (_, _) =>
        {
            if (_incomingAlertActive) return;
            _fox.Image = _stateImage;
            _fox.Bounds = _normalFoxBounds;
        };

        _tray = new NotifyIcon
        {
            Icon = AppIconProvider.Icon,
            Text = "ZomniverseGitPet",
            Visible = true,
            ContextMenuStrip = menu
        };
        _tray.DoubleClick += (_, _) => showGuardian();

        /*
        PATCH: INCOMING UPDATE WAKE-UP
        DATE: 2026-09-09
        Animate pet when new online commits arrive.
        */
        var initialSync = GuardianSyncState.Current;
        _lastBehind = initialSync.Behind;
        _lastSyncBranch = initialSync.Branch;
        _lastSyncRepository = GuardianSyncState.Config?.RepositoryPath ?? "";
        GuardianSyncState.Changed += OnGuardianSyncStateChanged;

        _incomingAnimationTimer.Tick += (_, _) => AdvanceIncomingAnimation();
        _incomingHoldTimer.Tick += (_, _) => EndIncomingUpdateAlert();

        FormClosing += (_, e) =>
        {
            if (!AllowClose)
            {
                e.Cancel = true;
                Hide();
            }
        };
    }

    public void SetNeedsRepository()
    {
        SetPetState(_assets.Idle, "● NO PROJECT\nOpen Projects to begin");
    }

    public void SetStatus(RepositoryStatus status)
    {
        if (!status.Healthy)
        {
            SetError(status.Error);
            return;
        }

        if (status.Files.Count == 0)
        {
            SetPetState(_assets.Happy, $"● CLEAN\n{status.Branch}");
        }
        else
        {
            SetPetState(_assets.ReviewReady, $"● CHANGES DETECTED\n{status.Files.Count} ready to review");
        }
    }

    public void SetError(string error)
    {
        SetPetState(_assets.Warning, "● GIT NEEDS ATTENTION\nOpen Guardian");
        _tray.Text = error.Length > 60 ? error[..60] : error;
    }

    /* ========================================================================== 
       PATCH: PET SETUP GUIDANCE
       DATE.TIME: 2026-09-10 21:16 +03:00
       Let GitPet coach users through onboarding steps.
       ========================================================================== */
    public void ShowGuidance(string message)
    {
        if (string.IsNullOrWhiteSpace(message) || _incomingAlertActive) return;
        if (!Visible) Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        _fox.Image = _assets.Happy;
        _bubble.SetMessage(message.Trim());
        LayoutPet();
    }

    /* ==========================================================================
       HELPER: BeginGuidanceHold
       FUNCTION:
       Displays contextual warning guidance and protects it from routine status refreshes.

       DATE.TIME ADDED: 2026-09-11 17:34 +03:00

       REASON:
       Keep ignored-file instructions visible throughout the user review.
       ========================================================================== */
    public void BeginGuidanceHold(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        _guidanceHoldActive = true;
        if (!Visible) Show();
        if (WindowState == FormWindowState.Minimized)
            WindowState = FormWindowState.Normal;

        _fox.Image = _assets.Warning;
        _bubble.SetMessage(message.Trim());
        LayoutPet();
    }

    /* ==========================================================================
       HELPER: EndGuidanceHold
       FUNCTION:
       Ends contextual guidance and restores the most recently recorded repository state.

       DATE.TIME ADDED: 2026-09-11 17:34 +03:00

       REASON:
       Return the pet to normal repository reporting after review.
       ========================================================================== */
    public void EndGuidanceHold()
    {
        _guidanceHoldActive = false;
        if (_incomingAlertActive) return;

        _fox.Image = _stateImage;
        _bubble.SetMessage(_stateMessage);
        LayoutPet();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            GuardianSyncState.Changed -= OnGuardianSyncStateChanged;
            _incomingAnimationTimer.Stop();
            _incomingHoldTimer.Stop();
            _incomingAnimationTimer.Dispose();
            _incomingHoldTimer.Dispose();
            _tray.Visible = false;
            _tray.Dispose();
            _toolTip.Dispose();
            _assets.Dispose();
        }
        base.Dispose(disposing);
    }

    private static PetChromeButton CreatePetButton(PetChromeKind kind, EventHandler onClick)
    {
        var button = new PetChromeButton
        {
            Kind = kind,
            AccessibleName = kind == PetChromeKind.CloseRibbon
                ? "Exit ZomniverseGitPet"
                : "Minimize ZomniverseGitPet"
        };
        button.Click += onClick;
        return button;
    }

    private void SetPetState(Image image, string message)
    {
        _stateImage = image;
        _stateMessage = message;

        /* ==========================================================================
           PATCH: PRESERVE ACTIVE PET GUIDANCE
           FUNCTION:
           Records repository state without replacing guidance displayed for an open dialog.

           DATE.TIME ADDED: 2026-09-11 17:34 +03:00

           REASON:
           Stop status refreshes from immediately replacing ignored-file instructions.
           ========================================================================== */
        if (_incomingAlertActive || _guidanceHoldActive) return;

        _fox.Image = image;
        _bubble.SetMessage(message);
        LayoutPet();
    }

    private void OnGuardianSyncStateChanged(object? sender, EventArgs e)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            if (!IsHandleCreated) return;
            try { BeginInvoke((Action)(() => OnGuardianSyncStateChanged(sender, e))); }
            catch (InvalidOperationException) { }
            return;
        }

        var snapshot = GuardianSyncState.Current;
        var repository = GuardianSyncState.Config?.RepositoryPath ?? "";
        var sameRepository = string.Equals(repository, _lastSyncRepository, StringComparison.OrdinalIgnoreCase);
        var sameBranch = string.Equals(snapshot.Branch, _lastSyncBranch, StringComparison.OrdinalIgnoreCase);
        var previousBehind = _lastBehind;

        _lastSyncRepository = repository;
        _lastSyncBranch = snapshot.Branch;
        _lastBehind = snapshot.Behind;

        // Switching project/branch can reveal already-existing incoming history.
        // That is state discovery, not a new-arrival event, so do not celebrate it.
        if (!sameRepository || !sameBranch) return;

        if (snapshot.HasRepository &&
            snapshot.HasRemote &&
            snapshot.OnlineReachable &&
            !snapshot.ReconciliationPending &&
            snapshot.Behind > previousBehind)
        {
            BeginIncomingUpdateAlert(snapshot.Behind);
        }
    }

    private void BeginIncomingUpdateAlert(int readyToGet)
    {
        _incomingAlertActive = true;
        _incomingAnimationFrame = 0;
        _incomingHoldTimer.Stop();
        _incomingAnimationTimer.Stop();

        if (!Visible) Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;

        var updateText = readyToGet == 1 ? "1 update ready to Get ↓" : $"{readyToGet} updates ready to Get ↓";
        _bubble.SetMessage($"📬 YOU GOT SOMETHING!\n{updateText}");
        LayoutPet();

        _fox.Image = _assets.ReviewReady;
        _fox.Bounds = _normalFoxBounds;
        _incomingAnimationTimer.Start();
    }

    private void AdvanceIncomingAnimation()
    {
        if (!_incomingAlertActive)
        {
            _incomingAnimationTimer.Stop();
            return;
        }

        if (_incomingAnimationFrame >= IncomingBounceOffsets.Length)
        {
            _incomingAnimationTimer.Stop();
            _fox.Image = _assets.ReviewReady;
            _fox.Bounds = _normalFoxBounds;
            _incomingHoldTimer.Start();
            return;
        }

        var offset = IncomingBounceOffsets[_incomingAnimationFrame];
        _fox.Image = _incomingAnimationFrame % 2 == 0 ? _assets.ReviewReady : _assets.Happy;
        _fox.Bounds = new Rectangle(
            _normalFoxBounds.X,
            _normalFoxBounds.Y + offset,
            _normalFoxBounds.Width,
            _normalFoxBounds.Height);
        _incomingAnimationFrame++;
    }

    private void EndIncomingUpdateAlert()
    {
        _incomingAnimationTimer.Stop();
        _incomingHoldTimer.Stop();
        if (!_incomingAlertActive) return;

        _incomingAlertActive = false;
        _fox.Image = _stateImage;
        _bubble.SetMessage(_stateMessage);
        LayoutPet();
    }

    private void LayoutPet()
    {
        var bottom = Bottom;
        _normalFoxBounds = new Rectangle(40, _bubble.Bottom - 3, 160, 160);
        if (!_incomingAlertActive || !_incomingAnimationTimer.Enabled)
            _fox.Bounds = _normalFoxBounds;

        // Minimize lives in the speech-bubble chrome; close is a hot-pink ribbon on the fox's left ear.
        _minimize.Location = new Point(_bubble.Right - _minimize.Width - 10, _bubble.Top + 8);
        _close.Location = new Point(_normalFoxBounds.Left + 38, _normalFoxBounds.Top + 8);

        _minimize.BringToFront();
        _close.BringToFront();

        ClientSize = new Size(240, _fox.Bottom + 5);
        if (Visible) Top = bottom - Height;
    }

    private void ShowPet()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }
}
