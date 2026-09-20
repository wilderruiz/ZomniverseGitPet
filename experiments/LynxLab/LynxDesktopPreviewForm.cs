using System.Drawing.Drawing2D;

namespace LynxLab;

internal sealed class LynxDesktopPreviewForm : Form
{
    private ILynxRenderer _renderer;
    private LynxPalette _palette = LynxPalette.Default;
    private LynxVisualState _state = LynxVisualState.Idle;
    private bool _debugOverlay;
    private Point _dragOrigin;
    private Point _windowOrigin;
    private bool _dragging;

    public LynxDesktopPreviewForm(ILynxRenderer renderer)
    {
        _renderer = renderer;

        Text = "Lynx Lab Desktop Preview";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        ClientSize = new Size(240, 246);
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;
        DoubleBuffered = true;

        MouseDown += BeginDrag;
        MouseMove += ContinueDrag;
        MouseUp += EndDrag;

        PositionAtBottomRight();
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

    public void PositionAtBottomRight()
    {
        var work = Screen.PrimaryScreen?.WorkingArea ?? Screen.GetWorkingArea(Cursor.Position);
        Location = new Point(
            work.Right - Width - 24,
            work.Bottom - Height - 24);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        DrawBubble(e.Graphics);

        try
        {
            _renderer.Draw(
                e.Graphics,
                new Rectangle(40, 78, 160, 160),
                _palette,
                _state,
                _debugOverlay);
        }
        catch (Exception ex)
        {
            LabCrashLog.Write("Desktop preview renderer", ex);

            using var brush = new SolidBrush(Color.FromArgb(0xF2, 0x75, 0x86));
            using var font = new Font("Segoe UI", 7.5f, FontStyle.Bold);
            e.Graphics.DrawString(
                "renderer error",
                font,
                brush,
                new PointF(58, 122));
        }
    }

    private void DrawBubble(Graphics g)
    {
        var rect = new Rectangle(6, 0, 228, 70);
        using var path = RoundedRectangle(rect, 8);
        using var fill = new SolidBrush(Color.FromArgb(0x11, 0x18, 0x20));
        using var border = new Pen(StateColor(), 1f);
        g.FillPath(fill, path);
        g.DrawPath(border, path);

        var (title, sub) = StateText();
        using var titleFont = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        using var subFont = new Font("Segoe UI", 8f, FontStyle.Regular);
        using var titleBrush = new SolidBrush(Color.FromArgb(0xD9, 0xE1, 0xEA));
        using var subBrush = new SolidBrush(Color.FromArgb(0x76, 0x87, 0x9A));

        g.DrawString(title, titleFont, titleBrush, new PointF(13, 12));
        g.DrawString(sub, subFont, subBrush, new PointF(13, 34));
    }

    private (string Title, string Sub) StateText() =>
        _state switch
        {
            LynxVisualState.Clean => ("● CLEAN", "repository healthy"),
            LynxVisualState.Changes => ("● CHANGES DETECTED", "ready to review"),
            LynxVisualState.Attention => ("● GIT NEEDS ATTENTION", "open Guardian"),
            LynxVisualState.Save => ("● SAVE", "checkpoint preview"),
            LynxVisualState.Get => ("● GET", "incoming update"),
            LynxVisualState.Send => ("● SEND", "outgoing update"),
            LynxVisualState.Conflict => ("● RECONCILE", "conflict simulation"),
            _ => ("● LYNX LAB", "idle baseline")
        };

    private Color StateColor() =>
        _state switch
        {
            LynxVisualState.Clean => Color.FromArgb(0x57, 0xD7, 0xA0),
            LynxVisualState.Changes => _palette.Eye,
            LynxVisualState.Attention => Color.FromArgb(0xF2, 0x75, 0x86),
            LynxVisualState.Save => Color.FromArgb(0x57, 0xD7, 0xA0),
            LynxVisualState.Get => Color.FromArgb(0x78, 0xA9, 0xFF),
            LynxVisualState.Send => Color.FromArgb(0xA7, 0x8B, 0xFA),
            LynxVisualState.Conflict => Color.FromArgb(0xF0, 0xBD, 0x61),
            _ => _palette.Accent
        };

    private static GraphicsPath RoundedRectangle(Rectangle rect, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();

        path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();

        return path;
    }

    private void BeginDrag(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;

        _dragging = true;
        _dragOrigin = Cursor.Position;
        _windowOrigin = Location;
    }

    private void ContinueDrag(object? sender, MouseEventArgs e)
    {
        if (!_dragging) return;

        var now = Cursor.Position;
        Location = new Point(
            _windowOrigin.X + now.X - _dragOrigin.X,
            _windowOrigin.Y + now.Y - _dragOrigin.Y);
    }

    private void EndDrag(object? sender, MouseEventArgs e)
    {
        _dragging = false;
    }
}
