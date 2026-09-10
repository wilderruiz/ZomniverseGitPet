using System.Drawing.Drawing2D;

namespace ZomniverseGitPet;

internal sealed class OnboardingSurfacePanel : Panel
{
    public Color FillColor { get; set; } = GuardianTheme.Surface;
    public Color BorderColor { get; set; } = GuardianTheme.Border;
    public float CornerRadius { get; set; } = 12f;

    public OnboardingSurfacePanel()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint,
            true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? GuardianTheme.Window);

        var bounds = new RectangleF(1, 1, Math.Max(1, Width - 3), Math.Max(1, Height - 3));
        using var path = GuardianTheme.RoundedRectangle(bounds, CornerRadius);
        using var fill = new SolidBrush(FillColor);
        using var border = new Pen(BorderColor, 1.2f);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(border, path);
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);

        if (Width <= 0 || Height <= 0) return;

        using var path = GuardianTheme.RoundedRectangle(
            new RectangleF(0, 0, Width, Height),
            CornerRadius);
        var previousRegion = Region;
        Region = new Region(path);
        previousRegion?.Dispose();
    }
}
