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
        _palette = LynxPalette.All[0];

        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.FromArgb(0x0B, 0x10, 0x16);
        MinimumSize = new Size(200, 200);
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

        var side = Math.Min(ClientSize.Width - 28, ClientSize.Height - 28);
        side = Math.Max(1, side);

        var bounds = new Rectangle(
            (ClientSize.Width - side) / 2,
            (ClientSize.Height - side) / 2,
            side,
            side);

        _renderer.Draw(e.Graphics, bounds, _palette, _state, _debugOverlay);
    }
}
