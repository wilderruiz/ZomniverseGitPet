using System.Drawing.Drawing2D;

namespace ZomniverseGitPet;

internal enum GuardianActionKind
{
    Standard,
    Primary,
    Pull,
    Push,
    Danger
}

internal sealed class GuardianActionButton : Button
{
    private bool _hovered;
    private bool _pressed;
    private GuardianActionKind _kind;

    public GuardianActionKind Kind
    {
        get => _kind;
        set { _kind = value; Invalidate(); }
    }

    public GuardianActionButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.UserPaint, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        ForeColor = GuardianTheme.Ink;
        Font = new Font("Segoe UI", 9, FontStyle.Bold);
        Cursor = Cursors.Hand;
        TabStop = true;
        Height = 38;
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
        e.Graphics.Clear(Parent?.BackColor ?? GuardianTheme.Surface);

        var bounds = new RectangleF(1.5f, 1.5f, Math.Max(1, Width - 3), Math.Max(1, Height - 3));
        using var path = GuardianTheme.RoundedRectangle(bounds, 11f);

        var (fill, border, text) = Palette();
        using var fillBrush = new SolidBrush(fill);
        using var borderPen = new Pen(border,
            Kind is GuardianActionKind.Pull or GuardianActionKind.Push or GuardianActionKind.Primary ? 1.8f : 1.2f);

        e.Graphics.FillPath(fillBrush, path);
        e.Graphics.DrawPath(borderPen, path);

        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            Rectangle.Round(bounds),
            text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        if (Focused && ShowFocusCues)
        {
            var focus = Rectangle.Inflate(Rectangle.Round(bounds), -5, -5);
            ControlPaint.DrawFocusRectangle(e.Graphics, focus, GuardianTheme.Ink, fill);
        }
    }

    private (Color Fill, Color Border, Color Text) Palette()
    {
        if (!Enabled)
            return (Color.FromArgb(39, 34, 47), Color.FromArgb(64, 56, 75), Color.FromArgb(121, 112, 133));

        return Kind switch
        {
            GuardianActionKind.Primary => (
                _pressed ? GuardianTheme.VioletPressed : _hovered ? GuardianTheme.VioletHover : GuardianTheme.Violet,
                GuardianTheme.HotPinkSoft,
                Color.White),

            GuardianActionKind.Pull => (
                _pressed ? Color.FromArgb(32, 54, 91) : _hovered ? Color.FromArgb(40, 68, 111) : Color.FromArgb(28, 48, 80),
                GuardianTheme.Info,
                Color.White),

            GuardianActionKind.Push => (
                _pressed ? Color.FromArgb(78, 38, 112) : _hovered ? Color.FromArgb(106, 51, 146) : Color.FromArgb(79, 39, 111),
                GuardianTheme.HotPink,
                Color.White),

            GuardianActionKind.Danger => (
                _pressed ? Color.FromArgb(95, 34, 47) : _hovered ? Color.FromArgb(118, 43, 60) : Color.FromArgb(78, 31, 42),
                GuardianTheme.Warning,
                Color.White),

            _ => (
                _pressed ? Color.FromArgb(44, 35, 58) : _hovered ? Color.FromArgb(52, 42, 68) : GuardianTheme.SurfaceRaised,
                _hovered ? Color.FromArgb(101, 78, 132) : GuardianTheme.Border,
                GuardianTheme.Ink)
        };
    }
}
