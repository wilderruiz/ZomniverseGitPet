using System.Drawing.Drawing2D;

namespace ZomniverseGitPet;

internal sealed class PetChromeButton : Control
{
    private bool _hovered;
    private bool _pressed;

    public PetChromeButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        ForeColor = Color.FromArgb(64, 38, 103);
        Font = new Font("Segoe UI", 10, FontStyle.Bold);
        Cursor = Cursors.Hand;
        Size = new Size(28, 28);
        TabStop = false;
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hovered = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hovered = false; _pressed = false; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) { _pressed = true; Invalidate(); } }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); _pressed = false; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        var bounds = new RectangleF(2, 2, Width - 4, Height - 4);
        var fillColor = _pressed ? Color.FromArgb(202, 185, 234) : _hovered ? Color.FromArgb(233, 223, 249) : Color.FromArgb(247, 241, 255);
        using var fill = new SolidBrush(fillColor);
        using var border = new Pen(Color.FromArgb(113, 78, 170), 1.5f);
        e.Graphics.FillEllipse(fill, bounds);
        e.Graphics.DrawEllipse(border, bounds);
        TextRenderer.DrawText(e.Graphics, Text, Font, Rectangle.Round(bounds), ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
}
