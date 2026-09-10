using System.Drawing.Drawing2D;

namespace ZomniverseGitPet;

internal sealed class OnboardingButton : Button
{
    public Color FillColor { get; set; } = GuardianTheme.SurfaceSoft;
    public Color BorderColor { get; set; } = GuardianTheme.Border;
    public Color HoverColor { get; set; } = GuardianTheme.SurfaceRaised;
    public float CornerRadius { get; set; } = 8f;

    private bool _hovered;
    private bool _pressed;

    public OnboardingButton()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint,
            true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hovered = false;
        _pressed = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        _pressed = true;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _pressed = false;
        Invalidate();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Cursor = Enabled ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? GuardianTheme.Surface);

        var bounds = new RectangleF(1.5f, 1.5f, Math.Max(1, Width - 3), Math.Max(1, Height - 3));
        using var path = GuardianTheme.RoundedRectangle(bounds, CornerRadius);
        var fillColor = !Enabled
            ? Color.FromArgb(39, 34, 47)
            : _pressed
                ? ControlPaint.Dark(FillColor, 0.12f)
                : _hovered
                    ? HoverColor
                    : FillColor;
        var textColor = Enabled ? ForeColor : GuardianTheme.FaintInk;
        using var fill = new SolidBrush(fillColor);
        using var border = new Pen(Enabled ? BorderColor : GuardianTheme.BorderSoft, 1.2f);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(border, path);

        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            Rectangle.Round(bounds),
            textColor,
            TextFormatFlags.HorizontalCenter |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis);
    }
}
