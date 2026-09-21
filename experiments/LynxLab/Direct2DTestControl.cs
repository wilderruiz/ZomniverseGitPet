using System.Numerics;
using System.Runtime.InteropServices;

namespace LynxLab;

/// <summary>
/// First real Direct2D render target in Lynx Lab.
///
/// Migration 1 keeps the proven HWND render target and ports the Guardian's
/// core silhouette to native Direct2D path geometry: tail, torso, haunches,
/// forelegs, head and ears. The GDI+ V9 viewport remains the visual reference.
/// </summary>
internal sealed class Direct2DTestControl : Control
{
    private static readonly Guid FactoryIid =
        new("06152247-6F50-465A-9245-118BFD3B6007");

    private IntPtr _factory;
    private IntPtr _target;

    private IntPtr _tailGeometry;
    private IntPtr _torsoGeometry;
    private IntPtr _leftHaunchGeometry;
    private IntPtr _rightHaunchGeometry;
    private IntPtr _leftLegGeometry;
    private IntPtr _rightLegGeometry;
    private IntPtr _headGeometry;
    private IntPtr _leftEarGeometry;
    private IntPtr _rightEarGeometry;
    private IntPtr _leftInnerEarGeometry;
    private IntPtr _rightInnerEarGeometry;

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

            EnsureGuardianGeometry();

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

            SetStatus("DIRECT2D GUARDIAN CORE ✓");
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
        var drawEllipse = GetComDelegate<DrawEllipseDelegate>(_target, 20);
        var drawRectangle = GetComDelegate<DrawRectangleDelegate>(_target, 16);
        var drawLine = GetComDelegate<DrawLineDelegate>(_target, 15);

        var background = ToColorF(Color.FromArgb(0x0B, 0x10, 0x16));
        var accent = ResolveAccent();
        var partner = ResolvePartnerAccent();
        var core = ResolveCoreColors();

        IntPtr accentBrush = IntPtr.Zero;
        IntPtr partnerBrush = IntPtr.Zero;
        IntPtr frameBrush = IntPtr.Zero;

        try
        {
            accentBrush = CreateBrush(accent);
            partnerBrush = CreateBrush(partner);
            frameBrush = CreateBrush(Color.FromArgb(0x45, 0x55, 0x64));

            beginDraw(_target);
            clear(_target, ref background);

            var width = Math.Max(1f, ClientSize.Width);
            var height = Math.Max(1f, ClientSize.Height);
            var cx = width / 2f;
            var cy = height / 2f;
            var side = Math.Min(width, height);

            var frame = new RectF(6f, 6f, width - 6f, height - 6f);
            drawRectangle(
                _target,
                ref frame,
                frameBrush,
                1f,
                IntPtr.Zero);

            // Keep traffic/effect motion behind the migrated silhouette.
            DrawActivityRings(
                drawEllipse,
                drawLine,
                accentBrush,
                partnerBrush,
                cx,
                cy,
                side);

            DrawGuardianCore(
                width,
                height,
                core);

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
                SetStatus(
                    $"DIRECT2D GUARDIAN CORE ✓ · {_frameCount} frames");
        }
        finally
        {
            ReleaseCom(ref frameBrush);
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
     private void DrawGuardianCore(
        float width,
        float height,
        CoreColors colors)
    {
        var fillGeometry =
            GetComDelegate<FillGeometryDelegate>(_target, 23);
        var drawGeometry =
            GetComDelegate<DrawGeometryDelegate>(_target, 22);
        var fillEllipse =
            GetComDelegate<FillEllipseDelegate>(_target, 21);
        var drawEllipse =
            GetComDelegate<DrawEllipseDelegate>(_target, 20);
        var setTransform =
            GetComDelegate<SetTransformDelegate>(_target, 30);

        IntPtr tailBrush = IntPtr.Zero;
        IntPtr bodyBrush = IntPtr.Zero;
        IntPtr limbBrush = IntPtr.Zero;
        IntPtr pawBrush = IntPtr.Zero;
        IntPtr headBrush = IntPtr.Zero;
        IntPtr earBrush = IntPtr.Zero;
        IntPtr innerEarBrush = IntPtr.Zero;
        IntPtr edgeBrush = IntPtr.Zero;

        try
        {
            tailBrush = CreateBrush(colors.Tail);
            bodyBrush = CreateBrush(colors.Body);
            limbBrush = CreateBrush(colors.Limb);
            pawBrush = CreateBrush(colors.Paw);
            headBrush = CreateBrush(colors.Head);
            earBrush = CreateBrush(colors.Ear);
            innerEarBrush = CreateBrush(colors.EarInner);
            edgeBrush = CreateBrush(colors.Edge);

            var viewport = ViewportTransform(width, height);
            var edgeWidth = 1.0f;

            // Tail gets an independent transform so the first migrated
            // silhouette already preserves character movement.
            var tailAngle =
                TailSwayDegrees(_activity, _seconds) *
                MathF.PI / 180f;
            var tailMotion =
                Matrix3x2.CreateRotation(
                    tailAngle,
                    new Vector2(48f, 140f)) *
                viewport;

            var tailTransform = ToD2D(tailMotion);
            setTransform(_target, ref tailTransform);
            fillGeometry(
                _target,
                _tailGeometry,
                tailBrush,
                IntPtr.Zero);
            drawGeometry(
                _target,
                _tailGeometry,
                edgeBrush,
                edgeWidth,
                IntPtr.Zero);

            var bodyTransform = ToD2D(viewport);
            setTransform(_target, ref bodyTransform);

            FillAndStroke(
                fillGeometry,
                drawGeometry,
                _torsoGeometry,
                bodyBrush,
                edgeBrush,
                edgeWidth);

            FillAndStroke(
                fillGeometry,
                drawGeometry,
                _leftHaunchGeometry,
                bodyBrush,
                edgeBrush,
                edgeWidth);
            FillAndStroke(
                fillGeometry,
                drawGeometry,
                _rightHaunchGeometry,
                bodyBrush,
                edgeBrush,
                edgeWidth);

            FillAndStroke(
                fillGeometry,
                drawGeometry,
                _leftLegGeometry,
                limbBrush,
                edgeBrush,
                edgeWidth);
            FillAndStroke(
                fillGeometry,
                drawGeometry,
                _rightLegGeometry,
                limbBrush,
                edgeBrush,
                edgeWidth);

            var leftPaw = new Ellipse(
                new Point2F(66f, 148.5f),
                10f,
                5.5f);
            var rightPaw = new Ellipse(
                new Point2F(94f, 148.5f),
                10f,
                5.5f);

            fillEllipse(
                _target,
                ref leftPaw,
                pawBrush);
            drawEllipse(
                _target,
                ref leftPaw,
                edgeBrush,
                edgeWidth,
                IntPtr.Zero);
            fillEllipse(
                _target,
                ref rightPaw,
                pawBrush);
            drawEllipse(
                _target,
                ref rightPaw,
                edgeBrush,
                edgeWidth,
                IntPtr.Zero);

            // Ears sit behind the skull, matching the V9 layer order.
            FillAndStroke(
                fillGeometry,
                drawGeometry,
                _leftEarGeometry,
                earBrush,
                edgeBrush,
                edgeWidth);
            FillAndStroke(
                fillGeometry,
                drawGeometry,
                _rightEarGeometry,
                earBrush,
                edgeBrush,
                edgeWidth);

            fillGeometry(
                _target,
                _leftInnerEarGeometry,
                innerEarBrush,
                IntPtr.Zero);
            fillGeometry(
                _target,
                _rightInnerEarGeometry,
                innerEarBrush,
                IntPtr.Zero);

            FillAndStroke(
                fillGeometry,
                drawGeometry,
                _headGeometry,
                headBrush,
                edgeBrush,
                edgeWidth);

            var identity = Matrix3x2F.Identity;
            setTransform(_target, ref identity);
        }
        finally
        {
            ReleaseCom(ref edgeBrush);
            ReleaseCom(ref innerEarBrush);
            ReleaseCom(ref earBrush);
            ReleaseCom(ref headBrush);
            ReleaseCom(ref pawBrush);
            ReleaseCom(ref limbBrush);
            ReleaseCom(ref bodyBrush);
            ReleaseCom(ref tailBrush);
        }
    }

    private void FillAndStroke(
        FillGeometryDelegate fillGeometry,
        DrawGeometryDelegate drawGeometry,
        IntPtr geometry,
        IntPtr fillBrush,
        IntPtr edgeBrush,
        float edgeWidth)
    {
        fillGeometry(
            _target,
            geometry,
            fillBrush,
            IntPtr.Zero);

        drawGeometry(
            _target,
            geometry,
            edgeBrush,
            edgeWidth,
            IntPtr.Zero);
    }

    private Matrix3x2 ViewportTransform(
        float width,
        float height)
    {
        var side =
            Math.Max(
                1f,
                Math.Min(width, height) - 20f);
        var scale = side / 160f;
        var left = (width - side) / 2f;
        var top = (height - side) / 2f;

        return
            Matrix3x2.CreateScale(scale) *
            Matrix3x2.CreateTranslation(left, top);
    }

    private static float TailSwayDegrees(
        LynxActivityState activity,
        double seconds)
    {
        var slow =
            (float)Math.Sin(
                seconds * Math.PI * 2d / 2.8d);
        var fast =
            (float)Math.Sin(
                seconds * Math.PI * 2d / 1.25d);

        return activity switch
        {
            LynxActivityState.Sorting => slow * 5.0f,
            LynxActivityState.Incoming => 5.5f + slow * 4.0f,
            LynxActivityState.Outgoing => 4.0f + slow * 3.2f,
            LynxActivityState.Warning => -2.5f + fast * 3.0f,
            LynxActivityState.Failure => -4.5f + fast * 4.2f,
            LynxActivityState.Resting => -6.0f + slow * 1.8f,
            _ => slow * 2.2f
        };
    }

    private CoreColors ResolveCoreColors()
    {
        var active =
            LynxPalette.Blend(
                _palette,
                _activity,
                _seconds);

        return new CoreColors(
            Tail: Mix(
                Color.FromArgb(27, 16, 48),
                active.Fur,
                0.12f),
            Body: Mix(
                Color.FromArgb(48, 27, 80),
                active.Fur,
                0.12f),
            Limb: Mix(
                Color.FromArgb(35, 20, 59),
                active.Fur,
                0.10f),
            Paw: Mix(
                Color.FromArgb(25, 14, 43),
                active.Fur,
                0.08f),
            Head: Mix(
                Color.FromArgb(111, 58, 203),
                active.Accent,
                0.18f),
            Ear: Mix(
                Color.FromArgb(57, 31, 96),
                active.Fur,
                0.12f),
            EarInner: Mix(
                Color.FromArgb(177, 125, 248),
                active.Eye,
                0.22f),
            Edge: Mix(
                Color.FromArgb(19, 15, 27),
                active.Edge,
                0.06f));
    }

    private void EnsureGuardianGeometry()
    {
        if (_tailGeometry != IntPtr.Zero)
            return;

        _tailGeometry = CreatePathGeometry(
        [
            Move(49, 143),
            Bezier(31, 147, 15, 138, 10, 124),
            Bezier(3, 106, 7, 88, 18, 74),
            Bezier(27, 63, 38, 58, 48, 60),
            Line(43, 53),
            Bezier(55, 54, 66, 64, 68, 78),
            Bezier(71, 94, 64, 109, 57, 121),
            Bezier(51, 132, 48, 139, 49, 143),
            Close()
        ]);

        _torsoGeometry = CreatePathGeometry(
        [
            Move(53, 84),
            Bezier(47, 92, 43, 103, 42, 117),
            Bezier(41, 132, 45, 144, 56, 151),
            Bezier(63, 156, 71, 158, 80, 158),
            Bezier(89, 158, 97, 156, 104, 151),
            Bezier(115, 144, 119, 132, 118, 117),
            Bezier(117, 103, 113, 92, 107, 84),
            Bezier(100, 79, 91, 76, 80, 76),
            Bezier(69, 76, 60, 79, 53, 84),
            Close()
        ]);

        var leftHaunch =
            new[]
            {
                Move(45, 113),
                Bezier(38, 122, 38, 137, 45, 147),
                Bezier(50, 154, 59, 156, 66, 151),
                Bezier(69, 145, 68, 135, 65, 125),
                Bezier(61, 116, 53, 111, 45, 113),
                Close()
            };
        _leftHaunchGeometry =
            CreatePathGeometry(leftHaunch);
        _rightHaunchGeometry =
            CreatePathGeometry(Mirror(leftHaunch));

        var leftLeg =
            new[]
            {
                Move(60, 99),
                Bezier(57, 110, 57, 125, 59, 139),
                Bezier(60, 146, 64, 150, 69, 151),
                Bezier(72, 151, 74, 148, 74, 144),
                Bezier(73, 131, 73, 116, 76, 103),
                Bezier(71, 99, 65, 98, 60, 99),
                Close()
            };
        _leftLegGeometry =
            CreatePathGeometry(leftLeg);
        _rightLegGeometry =
            CreatePathGeometry(Mirror(leftLeg));

        _headGeometry = CreatePathGeometry(
        [
            Move(46, 39),
            Bezier(52, 30, 62, 24, 72, 21),
            Line(70, 17),
            Line(78, 20),
            Line(86, 16),
            Line(84, 21),
            Bezier(98, 23, 108, 30, 114, 39),
            Bezier(120, 48, 121, 58, 118, 67),
            Line(123, 72),
            Line(115, 73),
            Line(120, 79),
            Line(111, 78),
            Line(114, 84),
            Line(106, 83),
            Bezier(99, 91, 90, 96, 80, 98),
            Bezier(70, 96, 61, 91, 54, 83),
            Line(46, 84),
            Line(49, 78),
            Line(40, 79),
            Line(45, 73),
            Line(37, 72),
            Line(42, 67),
            Bezier(39, 58, 40, 48, 46, 39),
            Close()
        ]);

        var leftEar =
            new[]
            {
                Move(46, 45),
                Bezier(40, 35, 40, 20, 48, 6),
                Bezier(58, 14, 64, 24, 66, 36),
                Bezier(59, 40, 52, 43, 46, 45),
                Close()
            };
        _leftEarGeometry =
            CreatePathGeometry(leftEar);
        _rightEarGeometry =
            CreatePathGeometry(Mirror(leftEar));

        var leftInnerEar =
            new[]
            {
                Move(48, 37),
                Bezier(46, 28, 48, 18, 51, 12),
                Bezier(57, 19, 60, 27, 61, 34),
                Line(57, 31),
                Line(58, 37),
                Line(52, 33),
                Line(50, 39),
                Close()
            };
        _leftInnerEarGeometry =
            CreatePathGeometry(leftInnerEar);
        _rightInnerEarGeometry =
            CreatePathGeometry(Mirror(leftInnerEar));
    }

    private IntPtr CreatePathGeometry(
        IReadOnlyList<PathCommand> commands)
    {
        var create =
            GetComDelegate<CreatePathGeometryDelegate>(
                _factory,
                10);

        var hr = create(
            _factory,
            out var geometry);

        if (hr < 0 || geometry == IntPtr.Zero)
            throw new InvalidOperationException(
                "CreatePathGeometry failed · " +
                HResultText(hr));

        IntPtr sink = IntPtr.Zero;

        try
        {
            var open =
                GetComDelegate<OpenGeometryDelegate>(
                    geometry,
                    17);

            hr = open(
                geometry,
                out sink);

            if (hr < 0 || sink == IntPtr.Zero)
                throw new InvalidOperationException(
                    "PathGeometry.Open failed · " +
                    HResultText(hr));

            var begin =
                GetComDelegate<BeginFigureDelegate>(
                    sink,
                    5);
            var addLine =
                GetComDelegate<AddLineDelegate>(
                    sink,
                    10);
            var addBezier =
                GetComDelegate<AddBezierDelegate>(
                    sink,
                    11);
            var end =
                GetComDelegate<EndFigureDelegate>(
                    sink,
                    8);
            var close =
                GetComDelegate<CloseSinkDelegate>(
                    sink,
                    9);

            var started = false;

            foreach (var command in commands)
            {
                switch (command.Kind)
                {
                    case PathCommandKind.Move:
                        begin(
                            sink,
                            command.P1,
                            0);
                        started = true;
                        break;

                    case PathCommandKind.Line:
                        addLine(
                            sink,
                            command.P1);
                        break;

                    case PathCommandKind.Bezier:
                        var bezier =
                            new BezierSegment(
                                command.P1,
                                command.P2,
                                command.P3);
                        addBezier(
                            sink,
                            ref bezier);
                        break;

                    case PathCommandKind.Close:
                        if (started)
                            end(
                                sink,
                                1);
                        break;
                }
            }

            hr = close(sink);

            if (hr < 0)
                throw new InvalidOperationException(
                    "GeometrySink.Close failed · " +
                    HResultText(hr));

            return geometry;
        }
        catch
        {
            Marshal.Release(geometry);
            throw;
        }
        finally
        {
            if (sink != IntPtr.Zero)
                Marshal.Release(sink);
        }
    }

    private static PathCommand[] Mirror(
        IReadOnlyList<PathCommand> source)
    {
        var result =
            new PathCommand[source.Count];

        for (var i = 0; i < source.Count; i++)
        {
            var command = source[i];
            result[i] = command with
            {
                P1 = Mirror(command.P1),
                P2 = Mirror(command.P2),
                P3 = Mirror(command.P3)
            };
        }

        return result;
    }

    private static Point2F Mirror(Point2F point) =>
        new(160f - point.X, point.Y);

    private static PathCommand Move(float x, float y) =>
        new(
            PathCommandKind.Move,
            new Point2F(x, y),
            default,
            default);

    private static PathCommand Line(float x, float y) =>
        new(
            PathCommandKind.Line,
            new Point2F(x, y),
            default,
            default);

    private static PathCommand Bezier(
        float c1x,
        float c1y,
        float c2x,
        float c2y,
        float x,
        float y) =>
        new(
            PathCommandKind.Bezier,
            new Point2F(c1x, c1y),
            new Point2F(c2x, c2y),
            new Point2F(x, y));

    private static PathCommand Close() =>
        new(
            PathCommandKind.Close,
            default,
            default,
            default);

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
        ReleaseCom(ref _rightInnerEarGeometry);
        ReleaseCom(ref _leftInnerEarGeometry);
        ReleaseCom(ref _rightEarGeometry);
        ReleaseCom(ref _leftEarGeometry);
        ReleaseCom(ref _headGeometry);
        ReleaseCom(ref _rightLegGeometry);
        ReleaseCom(ref _leftLegGeometry);
        ReleaseCom(ref _rightHaunchGeometry);
        ReleaseCom(ref _leftHaunchGeometry);
        ReleaseCom(ref _torsoGeometry);
        ReleaseCom(ref _tailGeometry);

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

    private readonly record struct CoreColors(
        Color Tail,
        Color Body,
        Color Limb,
        Color Paw,
        Color Head,
        Color Ear,
        Color EarInner,
        Color Edge);

    private enum PathCommandKind
    {
        Move,
        Line,
        Bezier,
        Close
    }

    private readonly record struct PathCommand(
        PathCommandKind Kind,
        Point2F P1,
        Point2F P2,
        Point2F P3);

    [StructLayout(LayoutKind.Sequential)]
    private struct BezierSegment
    {
        public BezierSegment(
            Point2F point1,
            Point2F point2,
            Point2F point3)
        {
            Point1 = point1;
            Point2 = point2;
            Point3 = point3;
        }

        public Point2F Point1;
        public Point2F Point2;
        public Point2F Point3;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Matrix3x2F
    {
        public float M11;
        public float M12;
        public float M21;
        public float M22;
        public float Dx;
        public float Dy;

        public static Matrix3x2F Identity =>
            new()
            {
                M11 = 1f,
                M22 = 1f
            };
    }

    private static Matrix3x2F ToD2D(
        Matrix3x2 matrix) =>
        new()
        {
            M11 = matrix.M11,
            M12 = matrix.M12,
            M21 = matrix.M21,
            M22 = matrix.M22,
            Dx = matrix.M31,
            Dy = matrix.M32
        };

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
    private delegate int CreatePathGeometryDelegate(
        IntPtr self,
        out IntPtr pathGeometry);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int OpenGeometryDelegate(
        IntPtr self,
        out IntPtr geometrySink);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void BeginFigureDelegate(
        IntPtr self,
        Point2F startPoint,
        uint figureBegin);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void AddLineDelegate(
        IntPtr self,
        Point2F point);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void AddBezierDelegate(
        IntPtr self,
        ref BezierSegment bezier);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void EndFigureDelegate(
        IntPtr self,
        uint figureEnd);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CloseSinkDelegate(
        IntPtr self);

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
    private delegate void DrawGeometryDelegate(
        IntPtr self,
        IntPtr geometry,
        IntPtr brush,
        float strokeWidth,
        IntPtr strokeStyle);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void FillGeometryDelegate(
        IntPtr self,
        IntPtr geometry,
        IntPtr brush,
        IntPtr opacityBrush);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void SetTransformDelegate(
        IntPtr self,
        ref Matrix3x2F transform);

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
