namespace ZomniverseGitPet;

internal sealed class GuardianProgressPanel : UserControl
{
    private static readonly int[] BounceOffsets = [0, -2, -5, -8, -5, -2, 0, 2, 0];

    private readonly PetAssets _assets = new();
    private readonly PictureBox _fox = new();
    private readonly Label _title = new();
    private readonly Label _subtitle = new();
    private readonly Label _stage = new();
    private readonly Panel _progressTrack = new();
    private readonly Panel _progressGlow = new();
    private readonly System.Windows.Forms.Timer _animationTimer = new() { Interval = 115 };
    private Rectangle _foxHome = new(170, 26, 180, 180);
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
        _fox.Bounds = _foxHome;
        _fox.TabStop = false;

        _title.AutoSize = false;
        _title.Bounds = new Rectangle(28, 210, 464, 38);
        _title.Text = "ZOMNIVERSE GITPET";
        _title.TextAlign = ContentAlignment.MiddleCenter;
        _title.ForeColor = Color.White;
        _title.BackColor = Color.Transparent;
        _title.Font = new Font("Segoe UI", 20, FontStyle.Bold);

        _subtitle.AutoSize = false;
        _subtitle.Bounds = new Rectangle(28, 248, 464, 26);
        _subtitle.Text = "Your purple desktop Git guardian";
        _subtitle.TextAlign = ContentAlignment.MiddleCenter;
        _subtitle.ForeColor = GuardianTheme.MutedInk;
        _subtitle.BackColor = Color.Transparent;
        _subtitle.Font = new Font("Segoe UI", 10);

        _stage.AutoSize = false;
        _stage.Bounds = new Rectangle(28, 292, 464, 24);
        _stage.Text = _stageText + "...";
        _stage.TextAlign = ContentAlignment.MiddleCenter;
        _stage.ForeColor = GuardianTheme.HotPinkSoft;
        _stage.BackColor = Color.Transparent;
        _stage.Font = new Font("Segoe UI", 9, FontStyle.Bold);

        _progressTrack.Bounds = new Rectangle(118, 327, 284, 3);
        _progressTrack.BackColor = GuardianTheme.SurfaceSoft;

        _progressGlow.Width = 96;
        _progressGlow.Height = 3;
        _progressGlow.Left = 0;
        _progressGlow.Top = 0;
        _progressGlow.BackColor = GuardianTheme.HotPink;
        _progressTrack.Controls.Add(_progressGlow);

        Controls.Add(_fox);
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
        AdvanceFox();
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
        AdvanceFox();
        _dotPhase = (_dotPhase + 1) % 4;
        _stage.Text = _stageText + new string('.', _dotPhase);

        var trackWidth = _progressTrack.ClientSize.Width;
        if (trackWidth <= 0) return;

        var travel = Math.Max(1, trackWidth - _progressGlow.Width);
        var phase = _progressFrame++ % 18;
        var reflected = phase <= 9 ? phase : 18 - phase;
        _progressGlow.Left = (int)Math.Round(travel * (reflected / 9d));
    }

    private void AdvanceFox()
    {
        _frame = (_frame + 1) % BounceOffsets.Length;
        _fox.Bounds = new Rectangle(
            _foxHome.X,
            _foxHome.Y + BounceOffsets[_frame],
            _foxHome.Width,
            _foxHome.Height);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animationTimer.Stop();
            _animationTimer.Dispose();
            _assets.Dispose();
        }
        base.Dispose(disposing);
    }
}
