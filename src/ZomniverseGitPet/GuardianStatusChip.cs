using System.Drawing.Drawing2D;

namespace ZomniverseGitPet;

internal enum GuardianChipTone
{
    Neutral,
    Healthy,
    Changes,
    Warning
}

internal sealed class GuardianStatusChip : Control
{
    private GuardianChipTone _tone;

    public GuardianChipTone Tone
    {
        get => _tone;
        set { _tone = value; Invalidate(); }
    }

    public GuardianStatusChip()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.UserPaint, true);
        Size = new Size(132, 30);
        Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        ForeColor = GuardianTheme.Ink;
        TabStop = false;
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? GuardianTheme.Surface);

        var bounds = new RectangleF(1, 1, Width - 2, Height - 2);
        using var path = GuardianTheme.RoundedRectangle(bounds, bounds.Height / 2f);
        var (accent, fill) = Tone switch
        {
            GuardianChipTone.Healthy => (GuardianTheme.Healthy, GuardianTheme.HealthyFill),
            GuardianChipTone.Changes => (GuardianTheme.Changes, GuardianTheme.ChangesFill),
            GuardianChipTone.Warning => (GuardianTheme.Warning, GuardianTheme.WarningFill),
            _ => (GuardianTheme.Info, GuardianTheme.InfoFill)
        };

        using var fillBrush = new SolidBrush(fill);
        using var border = new Pen(Color.FromArgb(150, accent), 1.2f);
        e.Graphics.FillPath(fillBrush, path);
        e.Graphics.DrawPath(border, path);

        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            Rectangle.Round(bounds),
            accent,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}
