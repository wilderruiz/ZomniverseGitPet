namespace ZomniverseGitPet;

/* ========================================================================== 
   PATCH: STARTUP SPLASH
   FUNCTION:
   Show embedded happy GitPet immediately while application services initialize.

   DATE.TIME ADDED: 2026-09-11 07:39 +03:00

   REASON (20 words max):
   Replace silent startup delay with visible GitPet feedback without delaying application readiness.
   ========================================================================== */
internal sealed class StartupSplashForm : Form
{
    private static readonly int[] BounceOffsets = [0, -2, -5, -8, -5, -2, 0, 2, 0];

    private readonly PetAssets _assets = new();
    private readonly PictureBox _fox = new();
    private readonly Label _stage = new();
    private readonly System.Windows.Forms.Timer _animationTimer = new() { Interval = 115 };
    private readonly Rectangle _foxHome = new(170, 42, 180, 180);
    private int _frame;
    private int _dotPhase;
    private string _stageText = "Waking up GitPet";

    public StartupSplashForm()
    {
        Text = "Starting ZomniverseGitPet";
        Icon = AppIconProvider.Icon;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(520, 350);
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = GuardianTheme.Window;
        ForeColor = GuardianTheme.Ink;
        Font = new Font("Segoe UI", 9);
        DoubleBuffered = true;

        _fox.Image = _assets.Happy;
        _fox.SizeMode = PictureBoxSizeMode.Zoom;
        _fox.BackColor = Color.Transparent;
        _fox.Bounds = _foxHome;
        _fox.TabStop = false;

        var title = new Label
        {
            AutoSize = false,
            Bounds = new Rectangle(28, 225, 464, 38),
            Text = "ZOMNIVERSE GITPET",
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 20, FontStyle.Bold)
        };

        var subtitle = new Label
        {
            AutoSize = false,
            Bounds = new Rectangle(28, 262, 464, 24),
            Text = "Your purple desktop Git guardian",
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = GuardianTheme.MutedInk,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 10)
        };

        _stage.AutoSize = false;
        _stage.Bounds = new Rectangle(28, 303, 464, 24);
        _stage.Text = _stageText + "...";
        _stage.TextAlign = ContentAlignment.MiddleCenter;
        _stage.ForeColor = GuardianTheme.HotPinkSoft;
        _stage.BackColor = Color.Transparent;
        _stage.Font = new Font("Segoe UI", 9, FontStyle.Bold);

        var progress = new Panel
        {
            Bounds = new Rectangle(118, 334, 284, 3),
            BackColor = GuardianTheme.SurfaceSoft
        };
        var progressGlow = new Panel
        {
            Dock = DockStyle.Left,
            Width = 96,
            BackColor = GuardianTheme.HotPink
        };
        progress.Controls.Add(progressGlow);

        Controls.Add(_fox);
        Controls.Add(title);
        Controls.Add(subtitle);
        Controls.Add(_stage);
        Controls.Add(progress);

        Paint += (_, e) =>
        {
            using var border = new Pen(GuardianTheme.HotPink, 2f);
            e.Graphics.DrawRectangle(border, 1, 1, ClientSize.Width - 3, ClientSize.Height - 3);
        };

        _animationTimer.Tick += (_, _) => AdvanceAnimation(progressGlow, progress.ClientSize.Width);
    }

    public void ShowImmediately()
    {
        Show();
        BringToFront();
        Update();
        Application.DoEvents();
        _animationTimer.Start();
    }

    public void SetStage(string stage)
    {
        if (string.IsNullOrWhiteSpace(stage) || IsDisposed) return;
        _stageText = stage.Trim();
        _dotPhase = 0;
        _stage.Text = _stageText;
        AdvanceFox();
        Update();
        Application.DoEvents();
    }

    public void CloseForLaunch()
    {
        if (IsDisposed) return;
        _animationTimer.Stop();
        Close();
    }

    private void AdvanceAnimation(Panel glow, int trackWidth)
    {
        AdvanceFox();

        _dotPhase = (_dotPhase + 1) % 4;
        _stage.Text = _stageText + new string('.', _dotPhase);

        if (trackWidth > 0)
        {
            var travel = Math.Max(1, trackWidth - glow.Width);
            var phase = _frame % 18;
            var reflected = phase <= 9 ? phase : 18 - phase;
            glow.Left = (int)Math.Round(travel * (reflected / 9d));
        }
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
