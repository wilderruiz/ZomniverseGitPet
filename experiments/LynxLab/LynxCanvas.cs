namespace LynxLab;

internal sealed class LynxCanvas : Control
{
    private ILynxRenderer _renderer;
    private LynxPalette _palette;
    private LynxVisualState _state;
    private bool _debugOverlay;

    public LynxCanvas(ILynxRenderer renderer)
    {
        _renderer = renderer;
        _palette = LynxPalette.Default;

        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.FromArgb(0x0B, 0x10, 0x16);
        MinimumSize = Size.Empty;
    }

    public ILynxRenderer Renderer
    {
        get => _renderer;
        set
        {
            _renderer = value;
            Invalidate();
        }
    }

    public LynxPalette Palette
    {
        get => _palette;
        set
        {
            _palette = value;
            Invalidate();
        }
    }

    public LynxVisualState State
    {
        get => _state;
        set
        {
            _state = value;
            Invalidate();
        }
    }

    public bool DebugOverlay
    {
        get => _debugOverlay;
        set
        {
            _debugOverlay = value;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        if (ClientSize.Width <= 4 || ClientSize.Height <= 4)
            return;

        var side = Math.Min(ClientSize.Width - 20, ClientSize.Height - 20);
        if (side <= 0)
            return;

        var bounds = new Rectangle(
            (ClientSize.Width - side) / 2,
            (ClientSize.Height - side) / 2,
            side,
            side);

        try
        {
            _renderer.Draw(e.Graphics, bounds, _palette, _state, _debugOverlay);
        }
        catch (Exception ex)
        {
            LabCrashLog.Write("Main viewport renderer", ex);

            using var brush = new SolidBrush(Color.FromArgb(0xF2, 0x75, 0x86));
            using var font = new Font("Segoe UI", 9f, FontStyle.Bold);
            e.Graphics.DrawString(
                "Renderer error — see LynxLab crash log",
                font,
                brush,
                new PointF(14, 14));
        }
    }
}
