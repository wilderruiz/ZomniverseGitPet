namespace ZomniverseGitPet;

internal sealed class GuardianProgressPanel : UserControl
{
    private static readonly int[] BounceOffsets = [0, -2, -5, -8, -5, -2, 0, 2, 0];

    private readonly PetAssets _assets = new();
    private readonly PictureBox _fox = new();
    private readonly PetDirect2DControl _guardianPet = new();
    private readonly Label _title = new();
    private readonly Label _subtitle = new();
    private readonly Label _stage = new();
    private readonly Panel _progressTrack = new();
    private readonly Panel _progressGlow = new();
    private readonly System.Windows.Forms.Timer _animationTimer = new() { Interval = 115 };
    private readonly System.Diagnostics.Stopwatch _guardianClock =
        System.Diagnostics.Stopwatch.StartNew();
    private Rectangle _petHome = new(170, 18, 180, 180);
    private int _frame;
    private int _progressFrame;
    private int _dotPhase;
    private string _stageText = "Working";

    public GuardianProgressPanel()
    {
        Size = new Size(520, 350);
        BackColor = GuardianTheme.Window;
        ForeColor = GuardianTheme.Ink;
        Font = new Font("Segoe UI", 9);
        DoubleBuffered = true;

        _fox.Image = _assets.Happy;
        _fox.SizeMode = PictureBoxSizeMode.Zoom;
        _fox.BackColor = Color.Transparent;
        _fox.Bounds = _petHome;
        _fox.TabStop = false;
        _fox.Visible = false;

        _guardianPet.Bounds = _petHome;
        _guardianPet.ShowDiagnosticFrame = false;
        _guardianPet.ProductionSizeMode = true;
        _guardianPet.CanvasBackgroundColor = GuardianTheme.Window;
        _guardianPet.TabStop = false;
        _guardianPet.BackendStatusChanged += (_, _) => HandleGuardianBackendStatus();

        _title.AutoSize = false;
        _title.Bounds = new Rectangle(28, 194, 464, 48);
        _title.Text = "ZOMNIVERSE GITPET";
        _title.TextAlign = ContentAlignment.MiddleCenter;
        _title.ForeColor = Color.White;
        _title.BackColor = Color.Transparent;
        _title.Font = new Font("Segoe UI", 20, FontStyle.Bold);

        _subtitle.AutoSize = false;
        _subtitle.Bounds = new Rectangle(28, 238, 464, 26);
        _subtitle.Text = "Your purple desktop Git guardian";
        _subtitle.TextAlign = ContentAlignment.MiddleCenter;
        _subtitle.ForeColor = GuardianTheme.MutedInk;
        _subtitle.BackColor = Color.Transparent;
        _subtitle.Font = new Font("Segoe UI", 10);

        _stage.AutoSize = false;
        _stage.Bounds = new Rectangle(28, 278, 464, 28);
        _stage.Text = _stageText + "...";
        _stage.TextAlign = ContentAlignment.MiddleCenter;
        _stage.ForeColor = GuardianTheme.HotPinkSoft;
        _stage.BackColor = Color.Transparent;
        _stage.Font = new Font("Segoe UI", 9, FontStyle.Bold);

        _progressTrack.Bounds = new Rectangle(118, 322, 284, 3);
        _progressTrack.BackColor = GuardianTheme.SurfaceSoft;

        _progressGlow.Width = 96;
        _progressGlow.Height = 3;
        _progressGlow.Left = 0;
        _progressGlow.Top = 0;
        _progressGlow.BackColor = GuardianTheme.HotPink;
        _progressTrack.Controls.Add(_progressGlow);

        Controls.Add(_fox);
        Controls.Add(_guardianPet);
        Controls.Add(_title);
        Controls.Add(_subtitle);
        Controls.Add(_stage);
        Controls.Add(_progressTrack);

        _animationTimer.Tick += (_, _) => AdvanceAnimation();
    }

    public void Configure(string title, string subtitle)
    {
        if (!string.IsNullOrWhiteSpace(title)) _title.Text = title.Trim();
        _subtitle.Text = subtitle?.Trim() ?? "";
    }

    public void Start(string stage)
    {
        SetStage(stage);
        _animationTimer.Start();
    }

    public void SetStage(string stage)
    {
        if (string.IsNullOrWhiteSpace(stage) || IsDisposed) return;
        _stageText = stage.Trim();
        _dotPhase = 0;
        _stage.Text = _stageText;
        AdvancePet();
        UpdateGuardianFrame();
        Invalidate();
    }

    public void SetSubtitle(string subtitle)
    {
        if (IsDisposed) return;
        _subtitle.Text = subtitle?.Trim() ?? "";
    }

    public void Stop()
    {
        _animationTimer.Stop();
    }

    private void AdvanceAnimation()
    {
        AdvancePet();
        UpdateGuardianFrame();
        _dotPhase = (_dotPhase + 1) % 4;
        _stage.Text = _stageText + new string('.', _dotPhase);

        var trackWidth = _progressTrack.ClientSize.Width;
        if (trackWidth <= 0) return;

        var travel = Math.Max(1, trackWidth - _progressGlow.Width);
        var phase = _progressFrame++ % 18;
        var reflected = phase <= 9 ? phase : 18 - phase;
        _progressGlow.Left = (int)Math.Round(travel * (reflected / 9d));
    }

    private void AdvancePet()
    {
        _frame = (_frame + 1) % BounceOffsets.Length;
        var bounds = new Rectangle(
            _petHome.X,
            _petHome.Y + BounceOffsets[_frame],
            _petHome.Width,
            _petHome.Height);
        _guardianPet.Bounds = bounds;
        _fox.Bounds = bounds;
    }

    private void UpdateGuardianFrame()
    {
        if (_guardianPet.IsDisposed || !_guardianPet.Visible) return;

        _guardianPet.SetFrame(
            _guardianClock.Elapsed.TotalSeconds,
            LynxPalette.Default,
            LynxVisualState.Idle,
            LynxActivityState.Preparing);
    }

    private void HandleGuardianBackendStatus()
    {
        if (_guardianPet.IsDisposed || !_guardianPet.Visible) return;

        var status = _guardianPet.BackendStatus;
        if (!status.Contains("failed", StringComparison.OrdinalIgnoreCase) &&
            !status.Contains("exception", StringComparison.OrdinalIgnoreCase))
            return;

        _guardianPet.Visible = false;
        _fox.Visible = true;
        _fox.Bounds = _petHome;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animationTimer.Stop();
            _animationTimer.Dispose();
            _guardianPet.Dispose();
            _assets.Dispose();
        }
        base.Dispose(disposing);
    }
}
