using System.Runtime.InteropServices;

namespace LynxLab;

/// <summary>
/// First real Direct2D render target in Lynx Lab.
///
/// This control intentionally renders a compact diagnostic scene rather than
/// the full Guardian. It proves HWND render-target creation, resize/recreate
/// handling, native antialiasing and animation before the V9 geometry is
/// migrated layer-by-layer.
/// </summary>
internal sealed class Direct2DTestControl : Control
{
    private static readonly Guid FactoryIid =
        new("06152247-6F50-465A-9245-118BFD3B6007");

    private IntPtr _factory;
    private IntPtr _target;
    private string _status = "not initialized";
    private long _frameCount;

    private double _seconds;
    private LynxPalette _palette = LynxPalette.Default;
    private LynxVisualState _state = LynxVisualState.Idle;
    private LynxActivityState _activity = LynxActivityState.None;

    public Direct2DTestControl()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.Opaque |
            ControlStyles.ResizeRedraw,
            true);

        BackColor = Color.FromArgb(0x0B, 0x10, 0x16);
        TabStop = false;
    }

    public string BackendStatus => _status;
    public long FrameCount => _frameCount;

    public event EventHandler? BackendStatusChanged;

    public void SetFrame(
        double seconds,
        LynxPalette palette,
        LynxVisualState state,
        LynxActivityState activity)
    {
        _seconds = seconds;
        _palette = palette;
        _state = state;
        _activity = activity;
        Invalidate();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        TryInitialize();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        ReleaseDeviceResources();
        ReleaseFactory();
        base.OnHandleDestroyed(e);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);

        if (_target == IntPtr.Zero || ClientSize.Width <= 0 || ClientSize.Height <= 0)
            return;

        try
        {
            var size = new SizeU(
                (uint)Math.Max(1, ClientSize.Width),
                (uint)Math.Max(1, ClientSize.Height));

            var resize = GetComDelegate<ResizeDelegate>(_target, 58);
            var hr = resize(_target, ref size);

            if (hr < 0)
            {
                SetStatus($"resize failed · {HResultText(hr)}");
                ReleaseTarget();
            }
        }
        catch (Exception ex)
        {
            SetStatus("resize exception · " + ex.Message);
            ReleaseTarget();
        }
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        // Direct2D clears the complete HWND render target.
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (ClientSize.Width <= 2 || ClientSize.Height <= 2)
            return;

        if (_target == IntPtr.Zero && !TryInitialize())
        {
            DrawFallback(e.Graphics);
            return;
        }

        try
        {
            RenderDirect2D();
        }
        catch (Exception ex)
        {
            SetStatus("render exception · " + ex.Message);
            ReleaseTarget();
            DrawFallback(e.Graphics);
        }
    }

    protected override void Dispose(bool disposing)
    {
        ReleaseDeviceResources();
        ReleaseFactory();
        base.Dispose(disposing);
    }

    private bool TryInitialize()
    {
        if (!IsHandleCreated || ClientSize.Width <= 0 || ClientSize.Height <= 0)
            return false;

        try
        {
            if (_factory == IntPtr.Zero)
            {
                var iid = FactoryIid;
                var hr = D2D1CreateFactory(
                    0,
                    ref iid,
                    IntPtr.Zero,
                    out _factory);

                if (hr < 0 || _factory == IntPtr.Zero)
                {
                    SetStatus("factory failed · " + HResultText(hr));
                    ReleaseFactory();
                    return false;
                }
            }

            if (_target == IntPtr.Zero)
            {
                var renderTargetProperties = new RenderTargetProperties
                {
                    Type = 0,
                    PixelFormat = new PixelFormat
                    {
                        Format = 0,
                        AlphaMode = 0
                    },
                    DpiX = 0f,
                    DpiY = 0f,
                    Usage = 0,
                    MinLevel = 0
                };

                var hwndProperties = new HwndRenderTargetProperties
                {
                    Hwnd = Handle,
                    PixelSize = new SizeU(
                        (uint)Math.Max(1, ClientSize.Width),
                        (uint)Math.Max(1, ClientSize.Height)),
                    PresentOptions = 0
                };

                var createTarget =
                    GetComDelegate<CreateHwndRenderTargetDelegate>(_factory, 14);

                var hr = createTarget(
                    _factory,
                    ref renderTargetProperties,
                    ref hwndProperties,
                    out _target);

                if (hr < 0 || _target == IntPtr.Zero)
                {
                    SetStatus("HWND target failed · " + HResultText(hr));
                    ReleaseTarget();
                    return false;
                }
            }

            SetStatus("DIRECT2D HWND TARGET ✓");
            return true;
        }
        catch (Exception ex)
        {
            SetStatus("initialization exception · " + ex.Message);
            ReleaseTarget();
            return false;
        }
    }

    private void RenderDirect2D()
    {
        if (_target == IntPtr.Zero)
            return;

        var beginDraw = GetComDelegate<BeginDrawDelegate>(_target, 48);
        var clear = GetComDelegate<ClearDelegate>(_target, 47);
        var endDraw = GetComDelegate<EndDrawDelegate>(_target, 49);
        var fillEllipse = GetComDelegate<FillEllipseDelegate>(_target, 21);
        var drawEllipse = GetComDelegate<DrawEllipseDelegate>(_target, 20);
        var fillRectangle = GetComDelegate<FillRectangleDelegate>(_target, 17);
        var drawRectangle = GetComDelegate<DrawRectangleDelegate>(_target, 16);
        var drawLine = GetComDelegate<DrawLineDelegate>(_target, 15);

        var background = ToColorF(Color.FromArgb(0x0B, 0x10, 0x16));
        var accent = ResolveAccent();
        var partner = ResolvePartnerAccent();
        var fur = ResolveFur();

        IntPtr accentBrush = IntPtr.Zero;
        IntPtr partnerBrush = IntPtr.Zero;
        IntPtr furBrush = IntPtr.Zero;
        IntPtr softBrush = IntPtr.Zero;

        try
        {
            accentBrush = CreateBrush(accent);
            partnerBrush = CreateBrush(partner);
            furBrush = CreateBrush(fur);
            softBrush = CreateBrush(Color.FromArgb(0x45, 0x55, 0x64));

            beginDraw(_target);
            clear(_target, ref background);

            var width = Math.Max(1f, ClientSize.Width);
            var height = Math.Max(1f, ClientSize.Height);
            var cx = width / 2f;
            var cy = height / 2f;
            var side = Math.Min(width, height);

            // Thin hardware-rendered frame.
            var frame = new RectF(6f, 6f, width - 6f, height - 6f);
            drawRectangle(
                _target,
                ref frame,
                softBrush,
                1f,
                IntPtr.Zero);

            // A simplified Guardian mass used only as a Direct2D proof target.
            var body = new Ellipse(
                new Point2F(cx, cy + side * 0.10f),
                side * 0.19f,
                side * 0.26f);
            fillEllipse(_target, ref body, furBrush);

            var head = new Ellipse(
                new Point2F(cx, cy - side * 0.15f),
                side * 0.17f,
                side * 0.15f);
            fillEllipse(_target, ref head, furBrush);

            // Armor plate.
            var armor = new RectF(
                cx - side * 0.13f,
                cy - side * 0.005f,
                cx + side * 0.13f,
                cy + side * 0.17f);
            fillRectangle(_target, ref armor, softBrush);
            drawRectangle(
                _target,
                ref armor,
                accentBrush,
                Math.Max(1.25f, side * 0.005f),
                IntPtr.Zero);

            // Eyes.
            var eyeOffset = side * 0.065f;
            var eyeRadius = Math.Max(2f, side * 0.012f);
            var leftEye = new Ellipse(
                new Point2F(cx - eyeOffset, cy - side * 0.16f),
                eyeRadius,
                eyeRadius * 0.72f);
            var rightEye = new Ellipse(
                new Point2F(cx + eyeOffset, cy - side * 0.16f),
                eyeRadius,
                eyeRadius * 0.72f);
            fillEllipse(_target, ref leftEye, accentBrush);
            fillEllipse(_target, ref rightEye, accentBrush);

            DrawActivityRings(
                drawEllipse,
                drawLine,
                accentBrush,
                partnerBrush,
                cx,
                cy,
                side);

            var hr = endDraw(_target, out _, out _);

            if (hr == D2DERR_RECREATE_TARGET)
            {
                SetStatus("DIRECT2D · recreating target");
                ReleaseTarget();
                Invalidate();
                return;
            }

            if (hr < 0)
            {
                SetStatus("EndDraw failed · " + HResultText(hr));
                ReleaseTarget();
                return;
            }

            _frameCount++;
            if (_frameCount == 1 || _frameCount % 120 == 0)
                SetStatus($"DIRECT2D HWND TARGET ✓ · {_frameCount} frames");
        }
        finally
        {
            ReleaseCom(ref softBrush);
            ReleaseCom(ref furBrush);
            ReleaseCom(ref partnerBrush);
            ReleaseCom(ref accentBrush);
        }
    }

    private void DrawActivityRings(
        DrawEllipseDelegate drawEllipse,
        DrawLineDelegate drawLine,
        IntPtr accentBrush,
        IntPtr partnerBrush,
        float cx,
        float cy,
        float side)
    {
        var pulse =
            0.5f +
            0.5f * (float)Math.Sin(_seconds * Math.PI * 2d / 2.4d);

        for (var i = 0; i < 3; i++)
        {
            var phase =
                (float)((_seconds * (0.38d + i * 0.17d) + i * 0.29d) % 1d);

            if (_activity == LynxActivityState.Outgoing)
                phase = 1f - phase;

            var radius =
                side * (0.24f + phase * 0.20f);
            var ring = new Ellipse(
                new Point2F(cx, cy),
                radius,
                radius * 0.92f);

            drawEllipse(
                _target,
                ref ring,
                i % 2 == 0 ? accentBrush : partnerBrush,
                Math.Max(1f, side * (0.0035f + pulse * 0.0015f)),
                IntPtr.Zero);
        }

        if (_activity is LynxActivityState.Incoming or LynxActivityState.Outgoing)
        {
            DrawTrafficLane(
                drawLine,
                accentBrush,
                cx,
                cy - side * 0.18f,
                side,
                1.45d,
                0.05d);

            DrawTrafficLane(
                drawLine,
                partnerBrush,
                cx,
                cy + side * 0.02f,
                side,
                0.82d,
                0.43d);

            DrawTrafficLane(
                drawLine,
                accentBrush,
                cx,
                cy + side * 0.20f,
                side,
                1.08d,
                0.72d);
        }
    }

    private void DrawTrafficLane(
        DrawLineDelegate drawLine,
        IntPtr brush,
        float cx,
        float y,
        float side,
        double speed,
        double offset)
    {
        var phase =
            (float)((_seconds * speed + offset) % 1d);

        var incoming = _activity == LynxActivityState.Incoming;
        var fromX = incoming
            ? cx - side * 0.42f
            : cx - side * 0.18f;
        var toX = incoming
            ? cx - side * 0.18f
            : cx - side * 0.42f;

        var x = fromX + (toX - fromX) * phase;
        var length = Math.Max(7f, side * 0.035f);
        var p0 = new Point2F(
            incoming ? x - length : x + length,
            y);
        var p1 = new Point2F(x, y);

        drawLine(
            _target,
            p0,
            p1,
            brush,
            Math.Max(1.4f, side * 0.006f),
            IntPtr.Zero);

        // Mirror lane from the other side.
        var mirrorX = cx * 2f - x;
        var mp0 = new Point2F(
            incoming ? mirrorX + length : mirrorX - length,
            y);
        var mp1 = new Point2F(mirrorX, y);

        drawLine(
            _target,
            mp0,
            mp1,
            brush,
            Math.Max(1.4f, side * 0.006f),
            IntPtr.Zero);
    }

    private Color ResolveAccent()
    {
        var stateColor = _state switch
        {
            LynxVisualState.Clean => Color.FromArgb(0x57, 0xD7, 0xA0),
            LynxVisualState.Attention => Color.FromArgb(0xF2, 0x75, 0x86),
            LynxVisualState.Get => Color.FromArgb(0x78, 0xA9, 0xFF),
            LynxVisualState.Send => Color.FromArgb(0xA7, 0x8B, 0xFA),
            LynxVisualState.Conflict => Color.FromArgb(0xF0, 0xBD, 0x61),
            _ => _palette.Accent
        };

        return Mix(stateColor, _palette.Accent, 0.24f);
    }

    private Color ResolvePartnerAccent()
    {
        if (_activity == LynxActivityState.None)
            return _palette.Eye;

        var partner = LynxPalette.ActivityPartner(_activity);
        var amount = LynxPalette.ActivityMix(_activity, _seconds);
        return Mix(_palette.Accent, partner.Accent, amount);
    }

    private Color ResolveFur()
    {
        var partner = _activity == LynxActivityState.None
            ? _palette
            : LynxPalette.ActivityPartner(_activity);

        var amount = _activity == LynxActivityState.None
            ? 0f
            : LynxPalette.ActivityMix(_activity, _seconds) * 0.38f;

        return Mix(
            Color.FromArgb(0x37, 0x25, 0x55),
            Mix(_palette.Fur, partner.Fur, amount),
            0.45f);
    }

    private IntPtr CreateBrush(Color color)
    {
        var createBrush =
            GetComDelegate<CreateSolidColorBrushDelegate>(_target, 8);

        var d2dColor = ToColorF(color);
        var hr = createBrush(
            _target,
            ref d2dColor,
            IntPtr.Zero,
            out var brush);

        if (hr < 0 || brush == IntPtr.Zero)
            throw new InvalidOperationException(
                "CreateSolidColorBrush failed · " + HResultText(hr));

        return brush;
    }

    private void DrawFallback(Graphics graphics)
    {
        graphics.Clear(BackColor);

        using var border = new Pen(Color.FromArgb(0xF2, 0x75, 0x86), 1f);
        using var text = new SolidBrush(Color.FromArgb(0xD4, 0xDE, 0xE8));
        using var font = new Font("Segoe UI", 9f, FontStyle.Bold);

        graphics.DrawRectangle(
            border,
            6,
            6,
            Math.Max(1, ClientSize.Width - 13),
            Math.Max(1, ClientSize.Height - 13));

        graphics.DrawString(
            "Direct2D target unavailable\n" + _status,
            font,
            text,
            new PointF(16, 16));
    }

    private void ReleaseDeviceResources()
    {
        ReleaseTarget();
    }

    private void ReleaseTarget()
    {
        if (_target != IntPtr.Zero)
        {
            Marshal.Release(_target);
            _target = IntPtr.Zero;
        }
    }

    private void ReleaseFactory()
    {
        if (_factory != IntPtr.Zero)
        {
            Marshal.Release(_factory);
            _factory = IntPtr.Zero;
        }
    }

    private static void ReleaseCom(ref IntPtr value)
    {
        if (value == IntPtr.Zero)
            return;

        Marshal.Release(value);
        value = IntPtr.Zero;
    }

    private void SetStatus(string value)
    {
        if (string.Equals(_status, value, StringComparison.Ordinal))
            return;

        _status = value;
        BackendStatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private static T GetComDelegate<T>(
        IntPtr instance,
        int slot)
        where T : Delegate
    {
        var vtable = Marshal.ReadIntPtr(instance);
        var method = Marshal.ReadIntPtr(
            vtable,
            slot * IntPtr.Size);

        return Marshal.GetDelegateForFunctionPointer<T>(method);
    }

    private static ColorF ToColorF(Color color) =>
        new(
            color.R / 255f,
            color.G / 255f,
            color.B / 255f,
            color.A / 255f);

    private static Color Mix(Color a, Color b, float bWeight)
    {
        bWeight = Math.Clamp(bWeight, 0f, 1f);
        var aWeight = 1f - bWeight;

        return Color.FromArgb(
            (int)(a.A * aWeight + b.A * bWeight),
            (int)(a.R * aWeight + b.R * bWeight),
            (int)(a.G * aWeight + b.G * bWeight),
            (int)(a.B * aWeight + b.B * bWeight));
    }

    private static string HResultText(int hr) =>
        $"HRESULT 0x{unchecked((uint)hr):X8}";

    private const int D2DERR_RECREATE_TARGET =
        unchecked((int)0x8899000C);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct Point2F
    {
        public Point2F(float x, float y)
        {
            X = x;
            Y = y;
        }

        public readonly float X;
        public readonly float Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SizeU
    {
        public SizeU(uint width, uint height)
        {
            Width = width;
            Height = height;
        }

        public uint Width;
        public uint Height;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RectF
    {
        public RectF(float left, float top, float right, float bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public float Left;
        public float Top;
        public float Right;
        public float Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Ellipse
    {
        public Ellipse(
            Point2F center,
            float radiusX,
            float radiusY)
        {
            Center = center;
            RadiusX = radiusX;
            RadiusY = radiusY;
        }

        public Point2F Center;
        public float RadiusX;
        public float RadiusY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ColorF
    {
        public ColorF(float r, float g, float b, float a)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public float R;
        public float G;
        public float B;
        public float A;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PixelFormat
    {
        public uint Format;
        public uint AlphaMode;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RenderTargetProperties
    {
        public uint Type;
        public PixelFormat PixelFormat;
        public float DpiX;
        public float DpiY;
        public uint Usage;
        public uint MinLevel;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HwndRenderTargetProperties
    {
        public IntPtr Hwnd;
        public SizeU PixelSize;
        public uint PresentOptions;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CreateHwndRenderTargetDelegate(
        IntPtr self,
        ref RenderTargetProperties renderTargetProperties,
        ref HwndRenderTargetProperties hwndRenderTargetProperties,
        out IntPtr hwndRenderTarget);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CreateSolidColorBrushDelegate(
        IntPtr self,
        ref ColorF color,
        IntPtr brushProperties,
        out IntPtr brush);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void BeginDrawDelegate(IntPtr self);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int EndDrawDelegate(
        IntPtr self,
        out ulong tag1,
        out ulong tag2);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void ClearDelegate(
        IntPtr self,
        ref ColorF clearColor);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void FillEllipseDelegate(
        IntPtr self,
        ref Ellipse ellipse,
        IntPtr brush);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void DrawEllipseDelegate(
        IntPtr self,
        ref Ellipse ellipse,
        IntPtr brush,
        float strokeWidth,
        IntPtr strokeStyle);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void FillRectangleDelegate(
        IntPtr self,
        ref RectF rectangle,
        IntPtr brush);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void DrawRectangleDelegate(
        IntPtr self,
        ref RectF rectangle,
        IntPtr brush,
        float strokeWidth,
        IntPtr strokeStyle);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void DrawLineDelegate(
        IntPtr self,
        Point2F point0,
        Point2F point1,
        IntPtr brush,
        float strokeWidth,
        IntPtr strokeStyle);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int ResizeDelegate(
        IntPtr self,
        ref SizeU pixelSize);

    [DllImport(
        "d2d1.dll",
        CallingConvention = CallingConvention.StdCall,
        ExactSpelling = true)]
    private static extern int D2D1CreateFactory(
        uint factoryType,
        ref Guid riid,
        IntPtr factoryOptions,
        out IntPtr factory);
}
