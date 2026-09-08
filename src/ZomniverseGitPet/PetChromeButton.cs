using System.Drawing.Drawing2D;

namespace ZomniverseGitPet;

internal enum PetChromeKind
{
    Minimize,
    CloseRibbon
}

internal sealed class PetChromeButton : Control
{
    private bool _hovered;
    private bool _pressed;
    private PetChromeKind _kind;

    public PetChromeKind Kind
    {
        get => _kind;
        set
        {
            _kind = value;
            Size = value == PetChromeKind.CloseRibbon ? new Size(34, 24) : new Size(25, 22);
            Invalidate();
        }
    }

    public PetChromeButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.UserPaint |
                 ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9, FontStyle.Bold);
        Cursor = Cursors.Hand;
        Size = new Size(25, 22);
        TabStop = false;
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
        if (e.Button == MouseButtons.Left)
        {
            _pressed = true;
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _pressed = false;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        if (Kind == PetChromeKind.CloseRibbon)
            PaintCloseRibbon(e.Graphics);
        else
            PaintMinimize(e.Graphics);
    }

    private void PaintMinimize(Graphics graphics)
    {
        var bounds = new RectangleF(1.5f, 1.5f, Width - 3, Height - 3);
        using var path = GuardianTheme.RoundedRectangle(bounds, 7f);
        var fillColor = _pressed
            ? Color.FromArgb(70, 45, 95)
            : _hovered
                ? Color.FromArgb(65, 47, 84)
                : Color.FromArgb(39, 30, 52);
        using var fill = new SolidBrush(fillColor);
        using var border = new Pen(_hovered ? GuardianTheme.HotPinkSoft : Color.FromArgb(121, 87, 173), 1.2f);
        graphics.FillPath(fill, path);
        graphics.DrawPath(border, path);

        using var pen = new Pen(Color.White, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        var y = Height / 2f + 2f;
        graphics.DrawLine(pen, 7f, y, Width - 7f, y);
    }

    private void PaintCloseRibbon(Graphics graphics)
    {
        var bounds = new RectangleF(1, 1, Width - 2, Height - 4);
        using var path = GuardianTheme.RoundedRectangle(bounds, 7f);
        var fillColor = _pressed
            ? Color.FromArgb(193, 31, 111)
            : _hovered
                ? Color.FromArgb(255, 76, 173)
                : GuardianTheme.HotPink;

        using var fill = new SolidBrush(fillColor);
        using var border = new Pen(Color.FromArgb(255, 183, 222), 1f);
        graphics.FillPath(fill, path);
        graphics.DrawPath(border, path);

        var notch = new[]
        {
            new PointF(Width - 11, Height - 4),
            new PointF(Width - 5, Height - 4),
            new PointF(Width - 8, Height - 1)
        };
        graphics.FillPolygon(fill, notch);

        TextRenderer.DrawText(
            graphics,
            "×",
            Font,
            Rectangle.Round(bounds),
            Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
}
