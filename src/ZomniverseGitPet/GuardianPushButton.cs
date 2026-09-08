using System.Drawing.Drawing2D;

namespace ZomniverseGitPet;

internal sealed class GuardianPushButton : Button
{
    private static readonly Color Purple = Color.FromArgb(111, 63, 178);
    private static readonly Color PurpleHover = Color.FromArgb(128, 75, 198);
    private static readonly Color PurplePressed = Color.FromArgb(86, 45, 145);
    private static readonly Color HotPink = Color.FromArgb(255, 47, 156);
    private static readonly Color DisabledPurple = Color.FromArgb(184, 169, 203);
    private static readonly Color DisabledPink = Color.FromArgb(225, 142, 190);

    private bool _hovered;
    private bool _pressed;

    public GuardianPushButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.UserPaint, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9, FontStyle.Bold);
        Cursor = Cursors.Hand;
        TabStop = true;
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

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        base.OnMouseDown(mevent);
        if (mevent.Button == MouseButtons.Left)
        {
            _pressed = true;
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        base.OnMouseUp(mevent);
        _pressed = false;
        Invalidate();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? SystemColors.Control);

        var bounds = new RectangleF(1.5f, 1.5f, Math.Max(1, Width - 3), Math.Max(1, Height - 3));
        var radius = Math.Min(15f, bounds.Height / 2f);
        using var path = RoundedRectangle(bounds, radius);

        var fillColor = !Enabled
            ? DisabledPurple
            : _pressed
                ? PurplePressed
                : _hovered
                    ? PurpleHover
                    : Purple;
        var borderColor = Enabled ? HotPink : DisabledPink;
        using var fill = new SolidBrush(fillColor);
        using var border = new Pen(borderColor, 2.2f);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(border, path);

        var textColor = Enabled ? Color.White : Color.FromArgb(244, 239, 249);
        TextRenderer.DrawText(e.Graphics, Text, Font, Rectangle.Round(bounds), textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        if (Focused && ShowFocusCues)
        {
            var focus = Rectangle.Inflate(Rectangle.Round(bounds), -5, -5);
            ControlPaint.DrawFocusRectangle(e.Graphics, focus, Color.White, fillColor);
        }
    }

    private static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
    {
        var diameter = radius * 2f;
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
