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
    private readonly GuardianProgressPanel _progress = new();

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

        _progress.Dock = DockStyle.Fill;
        _progress.Configure("ZOMNIVERSE GITPET", "Your purple desktop Git guardian");
        Controls.Add(_progress);

        Paint += (_, e) =>
        {
            using var border = new Pen(GuardianTheme.HotPink, 2f);
            e.Graphics.DrawRectangle(border, 1, 1, ClientSize.Width - 3, ClientSize.Height - 3);
        };
    }

    public void ShowImmediately()
    {
        Show();
        BringToFront();
        Update();
        Application.DoEvents();
        _progress.Start("Waking up GitPet");
    }

    public void SetStage(string stage)
    {
        if (string.IsNullOrWhiteSpace(stage) || IsDisposed) return;
        _progress.SetStage(stage);
        Update();
        Application.DoEvents();
    }

    public void CloseForLaunch()
    {
        if (IsDisposed) return;
        _progress.Stop();
        Close();
    }
}
