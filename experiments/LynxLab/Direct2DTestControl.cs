using System.Numerics;
using System.Runtime.InteropServices;

namespace LynxLab;

/// <summary>
/// First real Direct2D render target in Lynx Lab.
///
/// Migration 5 validates the native Guardian at the production 160×160 size.
/// The same Direct2D renderer can run with lab chrome/padding or in exact-size
/// mode for desktop-pet readability checks before DirectComposition hosting.
/// </summary>
internal sealed class Direct2DTestControl : Control
{
    private static readonly Guid FactoryIid =
        new("06152247-6F50-465A-9245-118BFD3B6007");

    private IntPtr _factory;
    private IntPtr _target;

    private IntPtr _tailGeometry;
    private IntPtr _tailUpperTuftGeometry;
    private IntPtr _tailMiddleSweepGeometry;
    private IntPtr _tailLowerSweepGeometry;
    private IntPtr _tailFoldGeometry;
    private IntPtr _leftPawGeometry;
    private IntPtr _rightPawGeometry;
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

    private IntPtr _chestGeometry;
    private IntPtr _faceMaskGeometry;
    private IntPtr _leftEyeGeometry;
    private IntPtr _rightEyeGeometry;
    private IntPtr _noseGeometry;

    private IntPtr _leftArmorGeometry;
    private IntPtr _rightArmorGeometry;
    private IntPtr _leftShoulderArmorGeometry;
    private IntPtr _rightShoulderArmorGeometry;
    private IntPtr _leftBracerGeometry;
    private IntPtr _rightBracerGeometry;

    private IntPtr _leftCollarGeometry;
    private IntPtr _rightCollarGeometry;
    private IntPtr _leftCollarFacetGeometry;
    private IntPtr _rightCollarFacetGeometry;

    private IntPtr _shieldGeometry;
    private IntPtr _shieldInsetGeometry;

    private string _status = "not initialized";
    private long _frameCount;

    private double _seconds;
    private LynxPalette _palette = LynxPalette.Default;
    private LynxVisualState _state = LynxVisualState.Idle;
    private LynxActivityState _activity = LynxActivityState.None;
    private bool _showDiagnosticFrame = true;
    private bool _productionSizeMode;

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

    public bool ShowDiagnosticFrame
    {
        get => _showDiagnosticFrame;
        set
        {
            if (_showDiagnosticFrame == value)
                return;

            _showDiagnosticFrame = value;
            Invalidate();
        }
    }

    public bool ProductionSizeMode
    {
        get => _productionSizeMode;
        set
        {
            if (_productionSizeMode == value)
                return;

            _productionSizeMode = value;
            Invalidate();
        }
    }

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
                    // Direct2D coordinates are DIPs. Keep the render target
                    // aligned with the WinForms control's current monitor DPI
                    // so ClientSize pixels are converted exactly once.
                    DpiX = Math.Max(96f, DeviceDpi),
                    DpiY = Math.Max(96f, DeviceDpi),
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

            SetStatus("DIRECT2D GUARDIAN MIGRATION 5 ✓");
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

            if (_showDiagnosticFrame)
            {
                frameBrush =
                    CreateBrush(Color.FromArgb(0x45, 0x55, 0x64));
            }

            beginDraw(_target);
            clear(_target, ref background);

            var dpi = Math.Max(96f, DeviceDpi);
            var dipScale = 96f / dpi;
            var width = Math.Max(
                1f,
                ClientSize.Width * dipScale);
            var height = Math.Max(
                1f,
                ClientSize.Height * dipScale);
            var cx = width / 2f;
            var cy = height / 2f;
            var side = Math.Min(width, height);

            if (_showDiagnosticFrame)
            {
                var frame =
                    new RectF(
                        6f,
                        6f,
                        width - 6f,
                        height - 6f);

                drawRectangle(
                    _target,
                    ref frame,
                    frameBrush,
                    1f,
                    IntPtr.Zero);
            }

            // Activity motion is split around the mascot:
            // perimeter fields behind, readable cues above.
            DrawActivityBackground(
                width,
                height);

            DrawGuardianCore(
                width,
                height,
                core);

            DrawActivityForeground(
                width,
                height);

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
                    $"DIRECT2D GUARDIAN MIGRATION 5 ✓ · {_frameCount} frames");
        }
        finally
        {
            ReleaseCom(ref frameBrush);
            ReleaseCom(ref partnerBrush);
            ReleaseCom(ref accentBrush);
        }
    }


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
        var drawLine =
            GetComDelegate<DrawLineDelegate>(_target, 15);
        var setTransform =
            GetComDelegate<SetTransformDelegate>(_target, 30);

        var expression =
            ResolveExpression(_state, _activity, _seconds);
        var activePalette =
            LynxPalette.Blend(
                _palette,
                _activity,
                _seconds);
        var armorPalette =
            ResolveArmorPalette(activePalette, _seconds);
        var accent =
            ResolveStateAccent(
                _state,
                _activity,
                activePalette);

        IntPtr tailBrush = IntPtr.Zero;
        IntPtr tailAccentBrush = IntPtr.Zero;
        IntPtr tailShadeBrush = IntPtr.Zero;
        IntPtr bodyBrush = IntPtr.Zero;
        IntPtr limbBrush = IntPtr.Zero;
        IntPtr pawBrush = IntPtr.Zero;
        IntPtr headBrush = IntPtr.Zero;
        IntPtr earBrush = IntPtr.Zero;
        IntPtr innerEarBrush = IntPtr.Zero;
        IntPtr edgeBrush = IntPtr.Zero;

        IntPtr whiteBrush = IntPtr.Zero;
        IntPtr chestShadeBrush = IntPtr.Zero;
        IntPtr inkBrush = IntPtr.Zero;
        IntPtr irisBrush = IntPtr.Zero;
        IntPtr failureBrush = IntPtr.Zero;
        IntPtr highlightBrush = IntPtr.Zero;

        IntPtr armorDarkBrush = IntPtr.Zero;
        IntPtr armorMidBrush = IntPtr.Zero;
        IntPtr armorEdgeBrush = IntPtr.Zero;
        IntPtr collarBrush = IntPtr.Zero;
        IntPtr facetBrush = IntPtr.Zero;
        IntPtr shieldBrush = IntPtr.Zero;
        IntPtr shieldInsetBrush = IntPtr.Zero;
        IntPtr shieldBorderBrush = IntPtr.Zero;

        try
        {
            tailBrush = CreateBrush(colors.Tail);
            tailAccentBrush =
                CreateBrush(
                    Mix(
                        colors.TailAccent,
                        colors.EarInner,
                        0.22f));
            tailShadeBrush =
                CreateBrush(
                    Color.FromArgb(92, 12, 7, 25));
            bodyBrush = CreateBrush(colors.Body);
            limbBrush = CreateBrush(colors.Limb);
            pawBrush = CreateBrush(colors.Paw);
            headBrush = CreateBrush(colors.Head);
            earBrush = CreateBrush(colors.Ear);
            innerEarBrush = CreateBrush(colors.EarInner);
            edgeBrush = CreateBrush(colors.Edge);

            whiteBrush =
                CreateBrush(Color.FromArgb(248, 244, 253));
            chestShadeBrush =
                CreateBrush(Color.FromArgb(222, 211, 237));
            inkBrush =
                CreateBrush(Color.FromArgb(24, 13, 38));
            irisBrush =
                CreateBrush(
                    Mix(
                        Color.FromArgb(139, 70, 221),
                        activePalette.Eye,
                        0.18f));
            failureBrush =
                CreateBrush(
                    Mix(
                        Color.FromArgb(238, 58, 86),
                        Color.FromArgb(142, 67, 224),
                        0.46f));
            highlightBrush =
                CreateBrush(Color.White);

            var armorDark =
                Mix(
                    Color.FromArgb(13, 18, 24),
                    armorPalette.Fur,
                    0.22f);
            var armorMid =
                Mix(
                    Color.FromArgb(34, 46, 58),
                    armorPalette.Accent,
                    0.36f);
            var armorEdge =
                Mix(
                    armorPalette.Accent,
                    accent,
                    0.42f);

            armorDarkBrush = CreateBrush(armorDark);
            armorMidBrush = CreateBrush(armorMid);
            armorEdgeBrush = CreateBrush(armorEdge);
            collarBrush =
                CreateBrush(
                    Mix(
                        Color.FromArgb(24, 31, 39),
                        armorPalette.Accent,
                        0.24f));
            facetBrush =
                CreateBrush(
                    Mix(
                        armorPalette.Accent,
                        accent,
                        0.32f));
            shieldBrush =
                CreateBrush(
                    Mix(
                        Color.FromArgb(48, 75, 96),
                        armorPalette.Accent,
                        0.58f));
            shieldInsetBrush =
                CreateBrush(
                    Mix(
                        Color.FromArgb(35, 70, 79),
                        armorPalette.Accent,
                        0.62f));
            shieldBorderBrush =
                CreateBrush(
                    Mix(
                        Color.FromArgb(201, 166, 249),
                        armorPalette.Eye,
                        0.14f));

            var viewport =
                ViewportTransform(width, height);
            const float edgeWidth = 1.0f;

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
            FillAndStroke(
                fillGeometry,
                drawGeometry,
                _tailGeometry,
                tailBrush,
                edgeBrush,
                edgeWidth);

            // V9 tail detail: these layered sweeps are what make the tail
            // read as a plume instead of disappearing into the torso.
            fillGeometry(
                _target,
                _tailUpperTuftGeometry,
                tailAccentBrush,
                IntPtr.Zero);
            fillGeometry(
                _target,
                _tailMiddleSweepGeometry,
                tailAccentBrush,
                IntPtr.Zero);
            fillGeometry(
                _target,
                _tailLowerSweepGeometry,
                tailAccentBrush,
                IntPtr.Zero);
            fillGeometry(
                _target,
                _tailFoldGeometry,
                tailShadeBrush,
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

            // Dark torso armor before the white chest ruff.
            FillAndStroke(
                fillGeometry,
                drawGeometry,
                _leftArmorGeometry,
                armorMidBrush,
                armorEdgeBrush,
                1.1f);
            FillAndStroke(
                fillGeometry,
                drawGeometry,
                _rightArmorGeometry,
                armorMidBrush,
                armorEdgeBrush,
                1.1f);
            FillAndStroke(
                fillGeometry,
                drawGeometry,
                _leftShoulderArmorGeometry,
                armorDarkBrush,
                armorEdgeBrush,
                1.0f);
            FillAndStroke(
                fillGeometry,
                drawGeometry,
                _rightShoulderArmorGeometry,
                armorDarkBrush,
                armorEdgeBrush,
                1.0f);
            FillAndStroke(
                fillGeometry,
                drawGeometry,
                _leftBracerGeometry,
                armorDarkBrush,
                armorEdgeBrush,
                1.0f);
            FillAndStroke(
                fillGeometry,
                drawGeometry,
                _rightBracerGeometry,
                armorDarkBrush,
                armorEdgeBrush,
                1.0f);

            FillAndStroke(
                fillGeometry,
                drawGeometry,
                _leftPawGeometry,
                pawBrush,
                edgeBrush,
                edgeWidth);
            FillAndStroke(
                fillGeometry,
                drawGeometry,
                _rightPawGeometry,
                pawBrush,
                edgeBrush,
                edgeWidth);

            // Two restrained toe creases per paw so the silhouette reads as
            // paws at 160 × 160 rather than as flat shoes.
            DrawLine(
                drawLine,
                edgeBrush,
                63.3f, 148.0f,
                63.7f, 151.6f,
                0.82f);
            DrawLine(
                drawLine,
                edgeBrush,
                68.3f, 147.3f,
                68.0f, 151.8f,
                0.82f);
            DrawLine(
                drawLine,
                edgeBrush,
                96.7f, 148.0f,
                96.3f, 151.6f,
                0.82f);
            DrawLine(
                drawLine,
                edgeBrush,
                91.7f, 147.3f,
                92.0f, 151.8f,
                0.82f);

            fillGeometry(
                _target,
                _chestGeometry,
                whiteBrush,
                IntPtr.Zero);

            // Collar armor locks the white ruff into the torso.
            FillAndStroke(
                fillGeometry,
                drawGeometry,
                _leftCollarGeometry,
                collarBrush,
                armorEdgeBrush,
                1.1f);
            FillAndStroke(
                fillGeometry,
                drawGeometry,
                _rightCollarGeometry,
                collarBrush,
                armorEdgeBrush,
                1.1f);
            fillGeometry(
                _target,
                _leftCollarFacetGeometry,
                facetBrush,
                IntPtr.Zero);
            fillGeometry(
                _target,
                _rightCollarFacetGeometry,
                facetBrush,
                IntPtr.Zero);

            // Head group uses the same state/activity tilt semantics as V9.
            var headLocal =
                Matrix3x2.CreateTranslation(
                    0f,
                    expression.HeadOffsetY) *
                Matrix3x2.CreateRotation(
                    expression.HeadTiltDegrees *
                    MathF.PI / 180f,
                    new Vector2(80f, 79f));
            var headTransform =
                ToD2D(headLocal * viewport);
            setTransform(_target, ref headTransform);

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
            fillGeometry(
                _target,
                _faceMaskGeometry,
                whiteBrush,
                IntPtr.Zero);

            DrawDirectFace(
                fillGeometry,
                drawGeometry,
                fillEllipse,
                drawEllipse,
                drawLine,
                setTransform,
                viewport,
                headLocal,
                expression,
                inkBrush,
                irisBrush,
                failureBrush,
                highlightBrush);

            // Shield remains in body space, exactly as in V9.
            setTransform(_target, ref bodyTransform);
            FillAndStroke(
                fillGeometry,
                drawGeometry,
                _shieldGeometry,
                shieldBrush,
                shieldBorderBrush,
                1.35f);
            fillGeometry(
                _target,
                _shieldInsetGeometry,
                shieldInsetBrush,
                IntPtr.Zero);

            drawLine(
                _target,
                new Point2F(75.5f, 106f),
                new Point2F(79f, 109.5f),
                highlightBrush,
                2.2f,
                IntPtr.Zero);
            drawLine(
                _target,
                new Point2F(79f, 109.5f),
                new Point2F(85f, 103f),
                highlightBrush,
                2.2f,
                IntPtr.Zero);

            var identity = Matrix3x2F.Identity;
            setTransform(_target, ref identity);
        }
        finally
        {
            ReleaseCom(ref shieldBorderBrush);
            ReleaseCom(ref shieldInsetBrush);
            ReleaseCom(ref shieldBrush);
            ReleaseCom(ref facetBrush);
            ReleaseCom(ref collarBrush);
            ReleaseCom(ref armorEdgeBrush);
            ReleaseCom(ref armorMidBrush);
            ReleaseCom(ref armorDarkBrush);

            ReleaseCom(ref highlightBrush);
            ReleaseCom(ref failureBrush);
            ReleaseCom(ref irisBrush);
            ReleaseCom(ref inkBrush);
            ReleaseCom(ref chestShadeBrush);
            ReleaseCom(ref whiteBrush);

            ReleaseCom(ref edgeBrush);
            ReleaseCom(ref innerEarBrush);
            ReleaseCom(ref earBrush);
            ReleaseCom(ref headBrush);
            ReleaseCom(ref pawBrush);
            ReleaseCom(ref limbBrush);
            ReleaseCom(ref bodyBrush);
            ReleaseCom(ref tailShadeBrush);
            ReleaseCom(ref tailAccentBrush);
            ReleaseCom(ref tailBrush);
        }
    }

    private void DrawDirectFace(
        FillGeometryDelegate fillGeometry,
        DrawGeometryDelegate drawGeometry,
        FillEllipseDelegate fillEllipse,
        DrawEllipseDelegate drawEllipse,
        DrawLineDelegate drawLine,
        SetTransformDelegate setTransform,
        Matrix3x2 viewport,
        Matrix3x2 headLocal,
        DirectExpression expression,
        IntPtr inkBrush,
        IntPtr irisBrush,
        IntPtr failureBrush,
        IntPtr highlightBrush)
    {
        var blink = BlinkAmount(_seconds);
        var eyeOpen =
            Math.Clamp(
                expression.EyeOpenness -
                blink * 0.90f,
                0.10f,
                1.12f);

        DrawEye(
            false,
            61f,
            _leftEyeGeometry);
        DrawEye(
            true,
            99f,
            _rightEyeGeometry);

        var baseTransform =
            ToD2D(headLocal * viewport);
        setTransform(_target, ref baseTransform);

        fillGeometry(
            _target,
            _noseGeometry,
            inkBrush,
            IntPtr.Zero);

        drawLine(
            _target,
            new Point2F(80f, 76.5f),
            new Point2F(80f, 81.5f),
            inkBrush,
            1.2f,
            IntPtr.Zero);

        var mouthEdgeY = 84.3f;
        var mouthCenterY =
            mouthEdgeY + expression.MouthCurve;

        drawLine(
            _target,
            new Point2F(73f, mouthEdgeY),
            new Point2F(80f, mouthCenterY),
            inkBrush,
            1.25f,
            IntPtr.Zero);
        drawLine(
            _target,
            new Point2F(80f, mouthCenterY),
            new Point2F(87f, mouthEdgeY),
            inkBrush,
            1.25f,
            IntPtr.Zero);

        var leftOuterY =
            50.8f + expression.BrowLift;
        var leftInnerY =
            56.5f +
            expression.BrowInnerDrop +
            expression.BrowLift;

        drawLine(
            _target,
            new Point2F(53.5f, leftOuterY),
            new Point2F(68.5f, leftInnerY),
            inkBrush,
            1.25f,
            IntPtr.Zero);
        drawLine(
            _target,
            new Point2F(106.5f, leftOuterY),
            new Point2F(91.5f, leftInnerY),
            inkBrush,
            1.25f,
            IntPtr.Zero);

        void DrawEye(
            bool right,
            float center,
            IntPtr eyeGeometry)
        {
            var eyeScale =
                Matrix3x2.CreateScale(
                    1f,
                    eyeOpen,
                    new Vector2(center, 58.5f));
            var eyeTransform =
                ToD2D(
                    eyeScale *
                    headLocal *
                    viewport);

            setTransform(_target, ref eyeTransform);
            fillGeometry(
                _target,
                eyeGeometry,
                inkBrush,
                IntPtr.Zero);

            var irisHeight =
                11f *
                eyeOpen *
                expression.PupilScale;
            var pupilHeight =
                8.8f *
                eyeOpen *
                expression.PupilScale;

            var iris = new Ellipse(
                new Point2F(center, 58.8f),
                4.5f * expression.PupilScale,
                Math.Max(1.2f, irisHeight / 2f));
            var pupil = new Ellipse(
                new Point2F(center, 58.5f),
                2.1f * expression.PupilScale,
                Math.Max(1f, pupilHeight / 2f));

            var headOnly =
                ToD2D(headLocal * viewport);
            setTransform(_target, ref headOnly);

            fillEllipse(
                _target,
                ref iris,
                _activity == LynxActivityState.Failure
                    ? failureBrush
                    : irisBrush);
            fillEllipse(
                _target,
                ref pupil,
                inkBrush);

            if (blink < 0.72f)
            {
                var highlight = new Ellipse(
                    new Point2F(
                        center - 2.2f,
                        55.2f),
                    1.2f,
                    Math.Max(0.7f, 1.2f * eyeOpen));

                fillEllipse(
                    _target,
                    ref highlight,
                    highlightBrush);
            }
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
        var margin =
            _productionSizeMode
                ? 0f
                : 20f;

        var side =
            Math.Max(
                1f,
                Math.Min(width, height) - margin);
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
            LynxActivityState.Thinking => slow * 1.7f,
            LynxActivityState.Preparing => slow * 2.2f,
            LynxActivityState.Sorting => slow * 5.0f,
            LynxActivityState.Packing => slow * 3.0f,
            LynxActivityState.Incoming => 5.5f + slow * 4.0f,
            LynxActivityState.Outgoing => 4.0f + slow * 3.2f,
            LynxActivityState.Reconciling => fast * 2.7f,
            LynxActivityState.Success => 5.0f + slow * 3.4f,
            LynxActivityState.Warning => -2.5f + fast * 3.0f,
            LynxActivityState.Failure => -4.5f + fast * 4.2f,
            LynxActivityState.Resting => -6.0f + slow * 1.8f,
            _ => slow * 2.2f
        };
    }

    private static float BlinkAmount(double seconds)
    {
        var phase = seconds % 4.8d;

        if (phase < 4.42d)
            return 0f;

        var t =
            (float)((phase - 4.42d) / 0.38d);

        return MathF.Sin(
            Math.Clamp(t, 0f, 1f) *
            MathF.PI);
    }

    private DirectExpression ResolveExpression(
        LynxVisualState state,
        LynxActivityState activity,
        double seconds)
    {
        var expression = state switch
        {
            LynxVisualState.Clean =>
                new DirectExpression(
                    0.84f, 1.02f,
                    -2.0f, -4.0f,
                    2.6f,
                    0.0f, 0.8f),

            LynxVisualState.Changes =>
                new DirectExpression(
                    1.12f, 1.18f,
                    -3.8f, -5.0f,
                    -2.0f,
                    -2.5f, -0.8f),

            LynxVisualState.Attention =>
                new DirectExpression(
                    0.70f, 0.82f,
                    1.4f, 4.2f,
                    -3.4f,
                    0.0f, -1.2f),

            LynxVisualState.Save =>
                new DirectExpression(
                    0.80f, 1.04f,
                    -2.2f, -3.7f,
                    3.8f,
                    2.2f, 0.8f),

            LynxVisualState.Get =>
                new DirectExpression(
                    1.16f, 1.26f,
                    -4.4f, -5.8f,
                    0.7f,
                    -4.0f, -1.0f),

            LynxVisualState.Send =>
                new DirectExpression(
                    0.82f, 0.96f,
                    -1.0f, -2.0f,
                    3.2f,
                    2.8f, -0.4f),

            LynxVisualState.Conflict =>
                new DirectExpression(
                    0.58f, 0.70f,
                    2.8f, 6.0f,
                    -4.8f,
                    0.0f, -1.8f),

            _ =>
                new DirectExpression(
                    0.90f, 1.00f,
                    0f, 0f,
                    -1.4f,
                    0f, 0f)
        };

        var breathe =
            (float)Math.Sin(
                seconds * Math.PI * 1.35d);

        return activity switch
        {
            LynxActivityState.Thinking =>
                expression with
                {
                    EyeOpenness = 0.78f,
                    PupilScale = 0.88f,
                    BrowLift = 0.8f,
                    BrowInnerDrop = 2.6f,
                    MouthCurve = -0.7f,
                    HeadTiltDegrees =
                        -2.2f + breathe * 0.7f,
                    HeadOffsetY = -0.8f
                },

            LynxActivityState.Preparing =>
                expression with
                {
                    EyeOpenness = 0.84f,
                    PupilScale = 0.92f,
                    BrowLift = 0.1f,
                    BrowInnerDrop = 1.8f,
                    MouthCurve = -0.4f,
                    HeadTiltDegrees = 1.8f,
                    HeadOffsetY = -0.8f
                },

            LynxActivityState.Sorting =>
                expression with
                {
                    EyeOpenness = 0.94f,
                    PupilScale = 1.02f,
                    BrowLift = -1.0f,
                    BrowInnerDrop = -1.4f,
                    MouthCurve = 0.4f,
                    HeadTiltDegrees = breathe * 1.1f,
                    HeadOffsetY = -0.4f
                },

            LynxActivityState.Packing =>
                expression with
                {
                    EyeOpenness = 0.80f,
                    PupilScale = 0.94f,
                    BrowLift = 0.2f,
                    BrowInnerDrop = 1.2f,
                    MouthCurve = 1.2f,
                    HeadTiltDegrees = 0f,
                    HeadOffsetY = -0.5f
                },

            LynxActivityState.Incoming =>
                expression with
                {
                    EyeOpenness = 1.14f,
                    PupilScale = 1.22f,
                    BrowLift = -4.0f,
                    BrowInnerDrop = -5.0f,
                    MouthCurve = 0.2f,
                    HeadTiltDegrees = -4.4f,
                    HeadOffsetY = -1.3f
                },

            LynxActivityState.Outgoing =>
                expression with
                {
                    EyeOpenness = 0.82f,
                    PupilScale = 0.96f,
                    BrowLift = -1.4f,
                    BrowInnerDrop = -2.0f,
                    MouthCurve = 2.8f,
                    HeadTiltDegrees = 3.0f,
                    HeadOffsetY = -0.3f
                },

            LynxActivityState.Reconciling =>
                expression with
                {
                    EyeOpenness = 0.62f,
                    PupilScale = 0.76f,
                    BrowLift = 2.0f,
                    BrowInnerDrop = 5.2f,
                    MouthCurve = -3.6f,
                    HeadTiltDegrees = 0f,
                    HeadOffsetY = -1.4f
                },

            LynxActivityState.Success =>
                expression with
                {
                    EyeOpenness = 0.76f,
                    PupilScale = 1.02f,
                    BrowLift = -2.8f,
                    BrowInnerDrop = -4.4f,
                    MouthCurve = 4.8f,
                    HeadTiltDegrees = 2.4f,
                    HeadOffsetY = 0.2f
                },

            LynxActivityState.Warning =>
                expression with
                {
                    EyeOpenness = 0.88f,
                    PupilScale = 0.94f,
                    BrowLift = 0.8f,
                    BrowInnerDrop = 2.8f,
                    MouthCurve = -3.0f,
                    HeadTiltDegrees = -2.0f,
                    HeadOffsetY = 0.8f
                },

            LynxActivityState.Failure =>
                expression with
                {
                    EyeOpenness = 0.56f,
                    PupilScale = 0.72f,
                    BrowLift = 3.0f,
                    BrowInnerDrop = 6.2f,
                    MouthCurve = -5.2f,
                    HeadTiltDegrees = 0f,
                    HeadOffsetY = 1.6f
                },

            LynxActivityState.Resting =>
                expression with
                {
                    EyeOpenness = 0.22f,
                    PupilScale = 0.80f,
                    BrowLift = -2.0f,
                    BrowInnerDrop = -4.0f,
                    MouthCurve = 1.8f,
                    HeadTiltDegrees = -5.0f,
                    HeadOffsetY = 2.2f
                },

            _ => expression
        };
    }

    private LynxPalette ResolveArmorPalette(
        LynxPalette identity,
        double seconds)
    {
        var blue =
            LynxPalette.All.First(palette =>
                string.Equals(
                    palette.Name,
                    "Midnight Blue",
                    StringComparison.Ordinal));
        var emerald =
            LynxPalette.All.First(palette =>
                string.Equals(
                    palette.Name,
                    "Forest Emerald",
                    StringComparison.Ordinal));

        var amount =
            0.5f +
            0.5f *
            (float)Math.Sin(
                seconds * Math.PI * 2d / 4.8d);

        Color BlendArmor(
            Color blueColor,
            Color greenColor,
            Color identityColor)
        {
            var mood =
                Mix(
                    blueColor,
                    greenColor,
                    amount);

            return Mix(
                mood,
                identityColor,
                0.18f);
        }

        return new LynxPalette(
            "Armor Blue ↔ Forest Emerald",
            BlendArmor(
                blue.Fur,
                emerald.Fur,
                identity.Fur),
            BlendArmor(
                blue.Ear,
                emerald.Ear,
                identity.Ear),
            BlendArmor(
                blue.Edge,
                emerald.Edge,
                identity.Edge),
            BlendArmor(
                blue.Eye,
                emerald.Eye,
                identity.Eye),
            BlendArmor(
                blue.Accent,
                emerald.Accent,
                identity.Accent),
            BlendArmor(
                blue.Muzzle,
                emerald.Muzzle,
                identity.Muzzle),
            BlendArmor(
                blue.Detail,
                emerald.Detail,
                identity.Detail));
    }

    private static Color ResolveStateAccent(
        LynxVisualState state,
        LynxActivityState activity,
        LynxPalette palette)
    {
        if (activity != LynxActivityState.None)
        {
            var activityColor = activity switch
            {
                LynxActivityState.Thinking or
                LynxActivityState.Preparing =>
                    Color.FromArgb(114, 200, 255),

                LynxActivityState.Sorting or
                LynxActivityState.Packing =>
                    Color.FromArgb(170, 150, 255),

                LynxActivityState.Incoming =>
                    Color.FromArgb(120, 216, 223),

                LynxActivityState.Outgoing =>
                    Color.FromArgb(170, 150, 255),

                LynxActivityState.Reconciling =>
                    Color.FromArgb(240, 189, 97),

                LynxActivityState.Success =>
                    Color.FromArgb(87, 215, 160),

                LynxActivityState.Warning =>
                    Color.FromArgb(240, 189, 97),

                LynxActivityState.Failure =>
                    Color.FromArgb(242, 117, 134),

                LynxActivityState.Resting =>
                    Color.FromArgb(140, 115, 232),

                _ => palette.Accent
            };

            return Mix(
                activityColor,
                palette.Accent,
                0.34f);
        }

        var semantic = state switch
        {
            LynxVisualState.Clean =>
                Color.FromArgb(117, 226, 189),
            LynxVisualState.Changes =>
                Color.FromArgb(239, 137, 158),
            LynxVisualState.Attention =>
                Color.FromArgb(255, 110, 127),
            LynxVisualState.Save =>
                Color.FromArgb(117, 226, 189),
            LynxVisualState.Get =>
                Color.FromArgb(114, 200, 255),
            LynxVisualState.Send =>
                Color.FromArgb(170, 150, 255),
            LynxVisualState.Conflict =>
                Color.FromArgb(228, 164, 108),
            _ => palette.Accent
        };

        return state == LynxVisualState.Idle
            ? semantic
            : Mix(
                semantic,
                palette.Accent,
                0.34f);
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
            TailAccent: Mix(
                Color.FromArgb(92, 48, 171),
                active.Accent,
                0.30f),
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
                0.22f),
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
            MoveTo(49, 143),
            Bezier(31, 147, 15, 138, 10, 124),
            Bezier(3, 106, 7, 88, 18, 74),
            Bezier(27, 63, 38, 58, 48, 60),
            Line(43, 53),
            Bezier(55, 54, 66, 64, 68, 78),
            Bezier(71, 94, 64, 109, 57, 121),
            Bezier(51, 132, 48, 139, 49, 143),
            Close()
        ]);

        _tailUpperTuftGeometry = CreatePathGeometry(
        [
            MoveTo(17, 79),
            Line(10, 75),
            Line(20, 70),
            Line(17, 64),
            Bezier(27, 59, 38, 58, 48, 60),
            Bezier(37, 62, 28, 68, 22, 77),
            Close()
        ]);

        _tailMiddleSweepGeometry = CreatePathGeometry(
        [
            MoveTo(13, 108),
            Bezier(18, 91, 30, 77, 49, 70),
            Bezier(38, 70, 28, 78, 22, 90),
            Bezier(18, 98, 15, 104, 13, 108),
            Close()
        ]);

        _tailLowerSweepGeometry = CreatePathGeometry(
        [
            MoveTo(15, 121),
            Bezier(23, 132, 35, 138, 49, 136),
            Line(44, 142),
            Bezier(31, 143, 21, 136, 15, 121),
            Close()
        ]);

        _tailFoldGeometry = CreatePathGeometry(
        [
            MoveTo(49, 70),
            Bezier(36, 86, 19, 106, 24, 123),
            Bezier(17, 112, 22, 91, 36, 79),
            Bezier(41, 75, 45, 72, 49, 70),
            Close()
        ]);

        var leftPaw =
            new[]
            {
                MoveTo(58, 147),
                Bezier(58, 144, 60.5f, 142, 63.5f, 141.5f),
                Bezier(65, 140.5f, 67, 140.3f, 68.5f, 141.2f),
                Bezier(72, 141.5f, 74.5f, 143.5f, 75.5f, 146),
                Bezier(76.2f, 148.2f, 75.2f, 150.4f, 73.2f, 151.6f),
                Bezier(71.6f, 153.2f, 69.1f, 153.8f, 67, 152.8f),
                Bezier(65.1f, 154f, 62.5f, 153.7f, 60.5f, 152.5f),
                Bezier(58.6f, 151.3f, 57.4f, 149.3f, 58, 147),
                Close()
            };
        _leftPawGeometry =
            CreatePathGeometry(leftPaw);
        _rightPawGeometry =
            CreatePathGeometry(Mirror(leftPaw));

        _torsoGeometry = CreatePathGeometry(
        [
            MoveTo(53, 84),
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
                MoveTo(45, 113),
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
                MoveTo(60, 99),
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
            MoveTo(46, 39),
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
                MoveTo(46, 45),
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
                MoveTo(48, 37),
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

        _chestGeometry = CreatePathGeometry(
        [
            MoveTo(60, 89),
            Bezier(69, 86, 91, 86, 100, 89),
            Bezier(100, 97, 98, 103, 96, 107),
            Line(94, 102),
            Bezier(93, 110, 90, 115, 87, 119),
            Line(88, 114),
            Bezier(83, 122, 84, 133, 80, 140),
            Bezier(76, 133, 77, 122, 72, 114),
            Line(73, 119),
            Bezier(70, 115, 67, 110, 66, 102),
            Line(64, 107),
            Bezier(62, 103, 60, 97, 60, 89),
            Close()
        ]);

        _faceMaskGeometry = CreatePathGeometry(
        [
            MoveTo(47, 66),
            Bezier(49, 59, 56, 56, 63, 58),
            Bezier(70, 60, 76, 65, 80, 70),
            Bezier(84, 65, 90, 60, 97, 58),
            Bezier(104, 56, 111, 59, 113, 66),
            Bezier(117, 74, 111, 81, 104, 85),
            Bezier(96, 91, 88, 94, 80, 95),
            Bezier(72, 94, 64, 91, 56, 85),
            Bezier(49, 81, 43, 74, 47, 66),
            Close()
        ]);

        var leftEye =
            new[]
            {
                MoveTo(54, 51),
                Bezier(58, 51, 64, 53.5f, 68, 56.5f),
                Bezier(67, 63, 64, 66, 60, 65),
                Bezier(56, 64, 53, 59, 54, 51),
                Close()
            };
        _leftEyeGeometry =
            CreatePathGeometry(leftEye);
        _rightEyeGeometry =
            CreatePathGeometry(Mirror(leftEye));

        _noseGeometry = CreatePathGeometry(
        [
            MoveTo(75, 72),
            Bezier(77, 70.5f, 83, 70.5f, 85, 72),
            Bezier(85, 74, 82, 77, 80, 77.5f),
            Bezier(78, 77, 75, 74, 75, 72),
            Close()
        ]);

        var leftArmor =
            new[]
            {
                MoveTo(49, 92),
                Line(63, 96),
                Line(73, 105),
                Line(69, 127),
                Line(60, 139),
                Line(53, 131),
                Line(48, 110),
                Close()
            };
        _leftArmorGeometry =
            CreatePathGeometry(leftArmor);
        _rightArmorGeometry =
            CreatePathGeometry(Mirror(leftArmor));

        var leftShoulderArmor =
            new[]
            {
                MoveTo(48, 91),
                Line(58, 87),
                Line(70, 94),
                Line(63, 101),
                Line(53, 98),
                Close()
            };
        _leftShoulderArmorGeometry =
            CreatePathGeometry(leftShoulderArmor);
        _rightShoulderArmorGeometry =
            CreatePathGeometry(Mirror(leftShoulderArmor));

        var leftBracer =
            new[]
            {
                MoveTo(58.5f, 118),
                Line(72.5f, 118),
                Line(72.3f, 139),
                Line(60, 139),
                Close()
            };
        _leftBracerGeometry =
            CreatePathGeometry(leftBracer);
        _rightBracerGeometry =
            CreatePathGeometry(Mirror(leftBracer));

        var leftCollar =
            new[]
            {
                MoveTo(57, 90),
                Line(66, 95),
                Line(80, 99),
                Line(80, 108),
                Line(65, 106),
                Line(53, 100),
                Line(55, 94),
                Close()
            };
        _leftCollarGeometry =
            CreatePathGeometry(leftCollar);
        _rightCollarGeometry =
            CreatePathGeometry(Mirror(leftCollar));

        var leftFacet =
            new[]
            {
                MoveTo(56, 96),
                Line(64, 100),
                Line(69, 104),
                Line(61, 102),
                Close()
            };
        _leftCollarFacetGeometry =
            CreatePathGeometry(leftFacet);
        _rightCollarFacetGeometry =
            CreatePathGeometry(Mirror(leftFacet));

        _shieldGeometry = CreatePathGeometry(
        [
            MoveTo(80, 94.5f),
            Line(91, 99.5f),
            Line(89, 111.5f),
            Line(80, 119.5f),
            Line(71, 111.5f),
            Line(69, 99.5f),
            Close()
        ]);

        _shieldInsetGeometry = CreatePathGeometry(
        [
            MoveTo(80, 97.5f),
            Line(88, 101.5f),
            Line(86.5f, 110f),
            Line(80, 116f),
            Line(73.5f, 110f),
            Line(72, 101.5f),
            Close()
        ]);
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

    private static PathCommand MoveTo(float x, float y) =>
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

    private void DrawActivityBackground(
        float width,
        float height)
    {
        if (_activity == LynxActivityState.None)
            return;

        var drawLine =
            GetComDelegate<DrawLineDelegate>(_target, 15);
        var drawEllipse =
            GetComDelegate<DrawEllipseDelegate>(_target, 20);
        var setTransform =
            GetComDelegate<SetTransformDelegate>(_target, 30);

        var colors = ResolveActivityColors();
        var mix =
            LynxPalette.ActivityMix(
                _activity,
                _seconds);
        var rotation =
            (float)((_seconds * 42d) % 360d);
        var pulse =
            0.5f +
            0.5f *
            (float)Math.Sin(
                _seconds * Math.PI * 2d / 1.6d);

        var viewport =
            ToD2D(ViewportTransform(width, height));
        setTransform(_target, ref viewport);

        try
        {
            switch (_activity)
            {
                case LynxActivityState.Thinking:
                case LynxActivityState.Preparing:
                {
                    var deliberate =
                        _activity == LynxActivityState.Preparing;
                    var scanRotation =
                        rotation * (deliberate ? 0.62f : 1f);

                    using var brushes =
                        new ActivityBrushPair(
                            this,
                            colors.Primary,
                            150,
                            colors.Secondary,
                            120);

                    DrawArcPolyline(
                        drawLine,
                        brushes.Primary,
                        8f, 4f, 144f, 151f,
                        scanRotation,
                        118f,
                        1.35f);

                    DrawArcPolyline(
                        drawLine,
                        brushes.Secondary,
                        15f, 10f, 130f, 141f,
                        scanRotation + 178f,
                        82f,
                        1.0f);
                    break;
                }

                case LynxActivityState.Sorting:
                {
                    using var brushes =
                        new ActivityBrushPair(
                            this,
                            colors.Primary,
                            165,
                            colors.Secondary,
                            135);

                    DrawArcPolyline(
                        drawLine,
                        brushes.Primary,
                        8f, 4f, 144f, 151f,
                        rotation,
                        72f,
                        1.45f);
                    DrawArcPolyline(
                        drawLine,
                        brushes.Primary,
                        8f, 4f, 144f, 151f,
                        rotation + 120f,
                        72f,
                        1.45f);
                    DrawArcPolyline(
                        drawLine,
                        brushes.Primary,
                        8f, 4f, 144f, 151f,
                        rotation + 240f,
                        72f,
                        1.45f);
                    DrawArcPolyline(
                        drawLine,
                        brushes.Secondary,
                        15f, 10f, 130f, 141f,
                        -rotation * 0.72f,
                        120f,
                        1.0f);
                    break;
                }

                case LynxActivityState.Packing:
                {
                    using var brush =
                        new ActivityBrush(
                            this,
                            Mix(
                                colors.Primary,
                                colors.Secondary,
                                mix),
                            185);

                    var inset =
                        5f + pulse * 5f;

                    DrawLine(
                        drawLine,
                        brush.Value,
                        10f + inset, 44f,
                        10f + inset, 118f,
                        1.45f);
                    DrawLine(
                        drawLine,
                        brush.Value,
                        150f - inset, 44f,
                        150f - inset, 118f,
                        1.45f);
                    DrawLine(
                        drawLine,
                        brush.Value,
                        10f + inset, 44f,
                        25f + inset, 44f,
                        1.45f);
                    DrawLine(
                        drawLine,
                        brush.Value,
                        150f - inset, 44f,
                        135f - inset, 44f,
                        1.45f);
                    DrawLine(
                        drawLine,
                        brush.Value,
                        10f + inset, 118f,
                        25f + inset, 118f,
                        1.45f);
                    DrawLine(
                        drawLine,
                        brush.Value,
                        150f - inset, 118f,
                        135f - inset, 118f,
                        1.45f);
                    break;
                }

                case LynxActivityState.Incoming:
                case LynxActivityState.Outgoing:
                {
                    for (var i = 0; i < 3; i++)
                    {
                        var speed = i switch
                        {
                            0 => 0.62d,
                            1 => 0.91d,
                            _ => 1.24d
                        };
                        var offset = i switch
                        {
                            0 => 0.00d,
                            1 => 0.37d,
                            _ => 0.71d
                        };

                        var phase =
                            (float)((
                                _seconds * speed +
                                offset) % 1d);
                        var travel =
                            _activity ==
                            LynxActivityState.Incoming
                                ? phase
                                : 1f - phase;
                        var fade =
                            (float)Math.Sin(
                                phase * Math.PI);

                        var left =
                            -3f +
                            (18f - -3f) * travel;
                        var top =
                            -6f +
                            (13f - -6f) * travel;
                        var ringWidth =
                            166f +
                            (124f - 166f) * travel;
                        var ringHeight =
                            172f +
                            (135f - 172f) * travel;

                        var color =
                            i % 2 == 0
                                ? Mix(
                                    colors.Primary,
                                    colors.Secondary,
                                    travel)
                                : Mix(
                                    colors.Secondary,
                                    colors.Primary,
                                    travel);

                        using var brush =
                            new ActivityBrush(
                                this,
                                color,
                                (int)(190f * fade));

                        var spin =
                            (float)((
                                _seconds *
                                (24d + i * 8d)) %
                                360d);

                        DrawArcPolyline(
                            drawLine,
                            brush.Value,
                            left,
                            top,
                            ringWidth,
                            ringHeight,
                            spin + i * 28f,
                            145f,
                            1.45f);
                        DrawArcPolyline(
                            drawLine,
                            brush.Value,
                            left,
                            top,
                            ringWidth,
                            ringHeight,
                            spin + 185f + i * 28f,
                            145f,
                            1.45f);
                    }

                    break;
                }

                case LynxActivityState.Reconciling:
                {
                    using var brushes =
                        new ActivityBrushPair(
                            this,
                            colors.Primary,
                            175,
                            colors.Secondary,
                            150);

                    DrawArcPolyline(
                        drawLine,
                        brushes.Primary,
                        8f, 4f, 144f, 151f,
                        rotation,
                        148f,
                        1.55f);
                    DrawArcPolyline(
                        drawLine,
                        brushes.Secondary,
                        15f, 10f, 130f, 141f,
                        -rotation,
                        148f,
                        1.2f);
                    break;
                }

                case LynxActivityState.Success:
                {
                    using var brush =
                        new ActivityBrush(
                            this,
                            Mix(
                                colors.Primary,
                                colors.Secondary,
                                mix),
                            145 +
                            (int)(75f * pulse));

                    var halo = new Ellipse(
                        new Point2F(80f, 79.5f),
                        72f,
                        75.5f);

                    drawEllipse(
                        _target,
                        ref halo,
                        brush.Value,
                        2.1f + pulse * 0.4f,
                        IntPtr.Zero);
                    break;
                }

                case LynxActivityState.Warning:
                {
                    using var brushes =
                        new ActivityBrushPair(
                            this,
                            colors.Primary,
                            205,
                            colors.Secondary,
                            175);

                    DrawArcPolyline(
                        drawLine,
                        brushes.Primary,
                        8f, 4f, 144f, 151f,
                        rotation,
                        132f,
                        1.8f);
                    DrawArcPolyline(
                        drawLine,
                        brushes.Secondary,
                        15f, 10f, 130f, 141f,
                        180f -
                        rotation * 1.18f,
                        132f,
                        1.4f);
                    break;
                }

                case LynxActivityState.Failure:
                {
                    var phase =
                        (float)((
                            _seconds % 1.7d) /
                            1.7d);
                    var collision =
                        phase <= 0.5f
                            ? phase * 2f
                            : (1f - phase) * 2f;

                    var leftCenter =
                        198f +
                        72f * collision;
                    var rightCenter =
                        342f -
                        72f * collision;

                    using var failA =
                        new ActivityBrush(
                            this,
                            Mix(
                                Color.FromArgb(
                                    226, 58, 86),
                                colors.Primary,
                                0.30f),
                            225);
                    using var failB =
                        new ActivityBrush(
                            this,
                            Mix(
                                Color.FromArgb(
                                    141, 74, 222),
                                colors.Secondary,
                                0.30f),
                            215);

                    DrawArcPolyline(
                        drawLine,
                        failA.Value,
                        8f, 4f, 144f, 151f,
                        leftCenter - 24f,
                        48f,
                        2.0f);
                    DrawArcPolyline(
                        drawLine,
                        failB.Value,
                        8f, 4f, 144f, 151f,
                        rightCenter - 24f,
                        48f,
                        1.8f);

                    if (collision > 0.86f)
                    {
                        var impact =
                            (collision - 0.86f) /
                            0.14f;

                        using var impactBrush =
                            new ActivityBrush(
                                this,
                                Mix(
                                    colors.Primary,
                                    colors.Secondary,
                                    0.5f),
                                (int)(220f * impact));

                        DrawLine(
                            drawLine,
                            impactBrush.Value,
                            80f, 2f,
                            80f, 13f,
                            1.8f);
                        DrawLine(
                            drawLine,
                            impactBrush.Value,
                            73f, 6f,
                            77f, 15f,
                            1.6f);
                        DrawLine(
                            drawLine,
                            impactBrush.Value,
                            87f, 6f,
                            83f, 15f,
                            1.6f);
                    }

                    break;
                }

                case LynxActivityState.Resting:
                {
                    for (var i = 0; i < 3; i++)
                    {
                        var phase =
                            (float)((
                                _seconds * 0.28d +
                                i / 3d) % 1d);
                        var expand =
                            4f + phase * 14f;
                        var alpha =
                            (int)(120f *
                                (1f - phase));

                        using var brush =
                            new ActivityBrush(
                                this,
                                Mix(
                                    colors.Primary,
                                    colors.Secondary,
                                    phase),
                                alpha);

                        DrawArcPolyline(
                            drawLine,
                            brush.Value,
                            15f - expand,
                            10f - expand,
                            130f + expand * 2f,
                            141f + expand * 2f,
                            205f,
                            130f,
                            1.15f);
                        DrawArcPolyline(
                            drawLine,
                            brush.Value,
                            15f - expand,
                            10f - expand,
                            130f + expand * 2f,
                            141f + expand * 2f,
                            25f,
                            130f,
                            1.15f);
                    }

                    break;
                }
            }
        }
        finally
        {
            var identity = Matrix3x2F.Identity;
            setTransform(_target, ref identity);
        }
    }

    private void DrawActivityForeground(
        float width,
        float height)
    {
        if (_activity == LynxActivityState.None)
            return;

        var drawLine =
            GetComDelegate<DrawLineDelegate>(_target, 15);
        var fillEllipse =
            GetComDelegate<FillEllipseDelegate>(_target, 21);
        var fillRectangle =
            GetComDelegate<FillRectangleDelegate>(_target, 17);
        var drawRectangle =
            GetComDelegate<DrawRectangleDelegate>(_target, 16);
        var setTransform =
            GetComDelegate<SetTransformDelegate>(_target, 30);

        var colors = ResolveActivityColors();
        var pulse =
            0.5f +
            0.5f *
            (float)Math.Sin(
                _seconds * Math.PI * 2d / 1.2d);

        var viewport =
            ToD2D(ViewportTransform(width, height));
        setTransform(_target, ref viewport);

        try
        {
            switch (_activity)
            {
                case LynxActivityState.Thinking:
                case LynxActivityState.Preparing:
                {
                    var deliberate =
                        _activity == LynxActivityState.Preparing;
                    var orbitSpeed = deliberate ? 0.92d : 1.45d;
                    var scanSpeed = deliberate ? 17d : 26d;

                    for (var i = 0; i < 4; i++)
                    {
                        var angle =
                            _seconds * orbitSpeed +
                            i *
                            Math.PI * 2d / 4d;
                        var x =
                            80f +
                            (float)Math.Cos(angle) *
                            67f;
                        var y =
                            80f +
                            (float)Math.Sin(angle) *
                            70f;

                        using var brush =
                            new ActivityBrush(
                                this,
                                i % 2 == 0
                                    ? colors.Primary
                                    : colors.Secondary,
                                235);

                        var dot = new Ellipse(
                            new Point2F(x, y),
                            1.7f,
                            1.7f);

                        fillEllipse(
                            _target,
                            ref dot,
                            brush.Value);
                    }

                    var scanY =
                        22f +
                        (float)((
                            _seconds * scanSpeed) %
                            116d);

                    using var scan =
                        new ActivityBrush(
                            this,
                            Mix(
                                colors.Primary,
                                colors.Secondary,
                                0.5f),
                            140);

                    DrawLine(
                        drawLine,
                        scan.Value,
                        25f, scanY,
                        135f, scanY,
                        1.0f);
                    break;
                }

                case LynxActivityState.Sorting:
                {
                    var phaseA = ActivityTravel(_seconds, 0.31d, 0.03d);
                    var phaseB = ActivityTravel(_seconds, 0.22d, 0.41d);
                    var phaseC = ActivityTravel(_seconds, 0.37d, 0.68d);
                    var phaseD = ActivityTravel(_seconds, 0.26d, 0.19d);

                    DrawDataChipD2D(
                        fillRectangle,
                        drawRectangle,
                        7f,
                        18f + phaseA * 120f,
                        colors.Primary);
                    DrawDataChipD2D(
                        fillRectangle,
                        drawRectangle,
                        146f,
                        142f - phaseB * 120f,
                        colors.Secondary);
                    DrawDataChipD2D(
                        fillRectangle,
                        drawRectangle,
                        18f + phaseC * 118f,
                        7f,
                        colors.Primary);
                    DrawDataChipD2D(
                        fillRectangle,
                        drawRectangle,
                        136f - phaseD * 118f,
                        149f,
                        colors.Secondary);
                    break;
                }

                case LynxActivityState.Packing:
                {
                    var inward =
                        2f + pulse * 8f;

                    using var brushes =
                        new ActivityBrushPair(
                            this,
                            colors.Primary,
                            235,
                            colors.Secondary,
                            220);

                    DrawArrowD2D(
                        drawLine,
                        brushes.Primary,
                        5f + inward, 62f,
                        29f + inward, 62f,
                        1.7f);
                    DrawArrowD2D(
                        drawLine,
                        brushes.Secondary,
                        155f - inward, 62f,
                        131f - inward, 62f,
                        1.7f);
                    DrawArrowD2D(
                        drawLine,
                        brushes.Primary,
                        10f + inward, 122f,
                        34f + inward, 122f,
                        1.7f);
                    DrawArrowD2D(
                        drawLine,
                        brushes.Secondary,
                        150f - inward, 122f,
                        126f - inward, 122f,
                        1.7f);
                    break;
                }

                case LynxActivityState.Incoming:
                case LynxActivityState.Outgoing:
                {
                    var incoming =
                        _activity ==
                        LynxActivityState.Incoming;

                    DrawTrafficArrowD2D(
                        drawLine,
                        colors.Primary,
                        incoming,
                        false,
                        43f,
                        1.52d,
                        0.02d);
                    DrawTrafficArrowD2D(
                        drawLine,
                        colors.Secondary,
                        incoming,
                        true,
                        43f,
                        0.68d,
                        0.44d);

                    DrawTrafficArrowD2D(
                        drawLine,
                        colors.Secondary,
                        incoming,
                        false,
                        91f,
                        0.96d,
                        0.21d);
                    DrawTrafficArrowD2D(
                        drawLine,
                        colors.Primary,
                        incoming,
                        true,
                        91f,
                        1.31d,
                        0.67d);

                    DrawTrafficArrowD2D(
                        drawLine,
                        colors.Primary,
                        incoming,
                        false,
                        132f,
                        0.74d,
                        0.56d);
                    DrawTrafficArrowD2D(
                        drawLine,
                        colors.Secondary,
                        incoming,
                        true,
                        132f,
                        1.15d,
                        0.11d);
                    break;
                }

                case LynxActivityState.Reconciling:
                {
                    for (var i = 0; i < 6; i++)
                    {
                        var direction =
                            i % 2 == 0 ? 1d : -1d;
                        var angle =
                            _seconds * 1.65d * direction +
                            i *
                            Math.PI * 2d / 6d;
                        var x =
                            80f +
                            (float)Math.Cos(angle) *
                            69f;
                        var y =
                            81f +
                            (float)Math.Sin(angle) *
                            71f;

                        using var brush =
                            new ActivityBrush(
                                this,
                                i % 2 == 0
                                    ? colors.Primary
                                    : colors.Secondary,
                                230);

                        var dot = new Ellipse(
                            new Point2F(x, y),
                            1.55f,
                            1.55f);

                        fillEllipse(
                            _target,
                            ref dot,
                            brush.Value);
                    }

                    break;
                }

                case LynxActivityState.Success:
                {
                    for (var i = 0; i < 5; i++)
                    {
                        var angle =
                            -Math.PI / 2d +
                            _seconds * 0.72d +
                            i *
                            Math.PI * 2d / 5d;
                        var radius =
                            67f +
                            pulse * 4f;
                        var x =
                            80f +
                            (float)Math.Cos(angle) *
                            radius;
                        var y =
                            80f +
                            (float)Math.Sin(angle) *
                            radius;

                        using var brush =
                            new ActivityBrush(
                                this,
                                i % 2 == 0
                                    ? colors.Primary
                                    : colors.Secondary,
                                235);

                        var dot = new Ellipse(
                            new Point2F(x, y),
                            1.8f,
                            1.8f);

                        fillEllipse(
                            _target,
                            ref dot,
                            brush.Value);
                    }

                    break;
                }

                case LynxActivityState.Warning:
                {
                    var tilt =
                        (float)Math.Sin(
                            _seconds *
                            Math.PI * 2d /
                            1.15d) *
                        11f;

                    var p1 =
                        RotatePoint(
                            new Point2F(80f, 1f),
                            new Point2F(80f, 27f),
                            tilt);
                    var p2 =
                        RotatePoint(
                            new Point2F(107f, 48f),
                            new Point2F(80f, 27f),
                            tilt);
                    var p3 =
                        RotatePoint(
                            new Point2F(53f, 48f),
                            new Point2F(80f, 27f),
                            tilt);

                    using var warning =
                        new ActivityBrush(
                            this,
                            Mix(
                                Color.FromArgb(
                                    255, 201, 72),
                                colors.Primary,
                                0.18f),
                            250);

                    DrawLine(
                        drawLine,
                        warning.Value,
                        p1.X, p1.Y,
                        p2.X, p2.Y,
                        2.35f);
                    DrawLine(
                        drawLine,
                        warning.Value,
                        p2.X, p2.Y,
                        p3.X, p3.Y,
                        2.35f);
                    DrawLine(
                        drawLine,
                        warning.Value,
                        p3.X, p3.Y,
                        p1.X, p1.Y,
                        2.35f);

                    var top =
                        RotatePoint(
                            new Point2F(80f, 14f),
                            new Point2F(80f, 27f),
                            tilt);
                    var bottom =
                        RotatePoint(
                            new Point2F(80f, 32f),
                            new Point2F(80f, 27f),
                            tilt);

                    DrawLine(
                        drawLine,
                        warning.Value,
                        top.X, top.Y,
                        bottom.X, bottom.Y,
                        2.5f);

                    var dotCenter =
                        RotatePoint(
                            new Point2F(80f, 39.5f),
                            new Point2F(80f, 27f),
                            tilt);
                    var dot =
                        new Ellipse(
                            dotCenter,
                            2.5f,
                            2.5f);

                    fillEllipse(
                        _target,
                        ref dot,
                        warning.Value);
                    break;
                }

                case LynxActivityState.Failure:
                {
                    var failurePulse =
                        0.45f +
                        0.55f *
                        (float)Math.Abs(
                            Math.Sin(
                                _seconds *
                                Math.PI * 2d /
                                0.86d));

                    using var fail =
                        new ActivityBrush(
                            this,
                            Mix(
                                Color.FromArgb(
                                    232, 57, 87),
                                colors.Primary,
                                0.30f),
                            (int)(245f *
                                failurePulse));

                    DrawLine(
                        drawLine,
                        fail.Value,
                        5f, 38f,
                        20f, 53f,
                        2.0f);
                    DrawLine(
                        drawLine,
                        fail.Value,
                        20f, 38f,
                        5f, 53f,
                        2.0f);
                    DrawLine(
                        drawLine,
                        fail.Value,
                        140f, 38f,
                        155f, 53f,
                        2.0f);
                    DrawLine(
                        drawLine,
                        fail.Value,
                        155f, 38f,
                        140f, 53f,
                        2.0f);
                    break;
                }

                case LynxActivityState.Resting:
                {
                    for (var i = 0; i < 3; i++)
                    {
                        var phase =
                            (float)((
                                _seconds * 0.26d +
                                i * 0.31d) % 1d);
                        var alpha =
                            (int)(
                                (1f - phase) *
                                235f);
                        var x =
                            118f +
                            phase * 30f;
                        var y =
                            56f -
                            phase * 44f;
                        var size =
                            5.5f +
                            phase * 3.2f;

                        using var brush =
                            new ActivityBrush(
                                this,
                                Mix(
                                    colors.Primary,
                                    colors.Secondary,
                                    phase),
                                alpha);

                        DrawZGlyph(
                            drawLine,
                            brush.Value,
                            x,
                            y,
                            size,
                            1.55f);
                    }

                    break;
                }
            }
        }
        finally
        {
            var identity = Matrix3x2F.Identity;
            setTransform(_target, ref identity);
        }
    }

    private ActivityColors ResolveActivityColors()
    {
        var active =
            LynxPalette.Blend(
                _palette,
                _activity,
                _seconds);
        var partner =
            LynxPalette.ActivityPartner(
                _activity);
        var semantic =
            ResolveStateAccent(
                _state,
                _activity,
                active);

        var selected =
            Mix(
                _palette.Accent,
                semantic,
                0.22f);
        var partnerAccent =
            Mix(
                partner.Accent,
                semantic,
                0.22f);
        var amount =
            LynxPalette.ActivityMix(
                _activity,
                _seconds);

        return new ActivityColors(
            Primary: Mix(
                selected,
                partnerAccent,
                amount),
            Secondary: Mix(
                partnerAccent,
                selected,
                amount));
    }

    private void DrawDataChipD2D(
        FillRectangleDelegate fillRectangle,
        DrawRectangleDelegate drawRectangle,
        float x,
        float y,
        Color color)
    {
        using var fill =
            new ActivityBrush(
                this,
                color,
                185);
        using var edge =
            new ActivityBrush(
                this,
                color,
                235);

        var rect =
            new RectF(
                x,
                y,
                x + 6.5f,
                y + 3.2f);

        fillRectangle(
            _target,
            ref rect,
            fill.Value);
        drawRectangle(
            _target,
            ref rect,
            edge.Value,
            0.8f,
            IntPtr.Zero);
    }

    private static float ActivityTravel(
        double seconds,
        double speed,
        double offset) =>
        (float)((seconds * speed + offset) % 1d);

    private void DrawTrafficArrowD2D(
        DrawLineDelegate drawLine,
        Color color,
        bool incoming,
        bool fromRight,
        float y,
        double speed,
        double offset)
    {
        var phase =
            (float)((
                _seconds * speed +
                offset) % 1d);
        var fade =
            (float)Math.Sin(
                phase * Math.PI);
        var alpha =
            Math.Max(
                0,
                (int)(248f * fade));

        const float outerLeft = -7f;
        const float innerLeft = 31f;
        const float innerRight = 129f;
        const float outerRight = 167f;

        float tailX;
        float headX;

        if (incoming)
        {
            if (fromRight)
            {
                headX =
                    outerRight +
                    (innerRight -
                     outerRight) * phase;
                tailX = headX + 10f;
            }
            else
            {
                headX =
                    outerLeft +
                    (innerLeft -
                     outerLeft) * phase;
                tailX = headX - 10f;
            }
        }
        else
        {
            if (fromRight)
            {
                headX =
                    innerRight +
                    (outerRight -
                     innerRight) * phase;
                tailX = headX - 10f;
            }
            else
            {
                headX =
                    innerLeft +
                    (outerLeft -
                     innerLeft) * phase;
                tailX = headX + 10f;
            }
        }

        using var brush =
            new ActivityBrush(
                this,
                color,
                alpha);

        DrawArrowD2D(
            drawLine,
            brush.Value,
            tailX,
            y,
            headX,
            y,
            1.75f);
    }

    private void DrawArrowD2D(
        DrawLineDelegate drawLine,
        IntPtr brush,
        float x1,
        float y1,
        float x2,
        float y2,
        float strokeWidth)
    {
        DrawLine(
            drawLine,
            brush,
            x1, y1,
            x2, y2,
            strokeWidth);

        var direction =
            Math.Sign(x2 - x1);

        if (direction == 0)
            return;

        DrawLine(
            drawLine,
            brush,
            x2, y2,
            x2 - direction * 4f,
            y2 - 3f,
            strokeWidth);
        DrawLine(
            drawLine,
            brush,
            x2, y2,
            x2 - direction * 4f,
            y2 + 3f,
            strokeWidth);
    }

    private void DrawArcPolyline(
        DrawLineDelegate drawLine,
        IntPtr brush,
        float left,
        float top,
        float width,
        float height,
        float startDegrees,
        float sweepDegrees,
        float strokeWidth)
    {
        const int segments = 28;
        var previous =
            PointOnEllipse(
                left,
                top,
                width,
                height,
                startDegrees);

        for (var i = 1; i <= segments; i++)
        {
            var amount =
                i / (float)segments;
            var current =
                PointOnEllipse(
                    left,
                    top,
                    width,
                    height,
                    startDegrees +
                    sweepDegrees * amount);

            DrawLine(
                drawLine,
                brush,
                previous.X,
                previous.Y,
                current.X,
                current.Y,
                strokeWidth);

            previous = current;
        }
    }

    private static Point2F PointOnEllipse(
        float left,
        float top,
        float width,
        float height,
        float degrees)
    {
        var radians =
            degrees *
            MathF.PI / 180f;
        var cx =
            left + width / 2f;
        var cy =
            top + height / 2f;

        return new Point2F(
            cx +
            MathF.Cos(radians) *
            width / 2f,
            cy +
            MathF.Sin(radians) *
            height / 2f);
    }

    private static Point2F RotatePoint(
        Point2F point,
        Point2F pivot,
        float degrees)
    {
        var radians =
            degrees *
            MathF.PI / 180f;
        var cos =
            MathF.Cos(radians);
        var sin =
            MathF.Sin(radians);
        var x =
            point.X - pivot.X;
        var y =
            point.Y - pivot.Y;

        return new Point2F(
            pivot.X +
            x * cos -
            y * sin,
            pivot.Y +
            x * sin +
            y * cos);
    }

    private void DrawZGlyph(
        DrawLineDelegate drawLine,
        IntPtr brush,
        float x,
        float y,
        float size,
        float strokeWidth)
    {
        DrawLine(
            drawLine,
            brush,
            x, y,
            x + size, y,
            strokeWidth);
        DrawLine(
            drawLine,
            brush,
            x + size, y,
            x, y + size,
            strokeWidth);
        DrawLine(
            drawLine,
            brush,
            x, y + size,
            x + size, y + size,
            strokeWidth);
    }

    private void DrawLine(
        DrawLineDelegate drawLine,
        IntPtr brush,
        float x1,
        float y1,
        float x2,
        float y2,
        float strokeWidth)
    {
        drawLine(
            _target,
            new Point2F(x1, y1),
            new Point2F(x2, y2),
            brush,
            strokeWidth,
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
        ReleaseCom(ref _shieldInsetGeometry);
        ReleaseCom(ref _shieldGeometry);
        ReleaseCom(ref _rightCollarFacetGeometry);
        ReleaseCom(ref _leftCollarFacetGeometry);
        ReleaseCom(ref _rightCollarGeometry);
        ReleaseCom(ref _leftCollarGeometry);
        ReleaseCom(ref _rightBracerGeometry);
        ReleaseCom(ref _leftBracerGeometry);
        ReleaseCom(ref _rightShoulderArmorGeometry);
        ReleaseCom(ref _leftShoulderArmorGeometry);
        ReleaseCom(ref _rightArmorGeometry);
        ReleaseCom(ref _leftArmorGeometry);
        ReleaseCom(ref _noseGeometry);
        ReleaseCom(ref _rightEyeGeometry);
        ReleaseCom(ref _leftEyeGeometry);
        ReleaseCom(ref _faceMaskGeometry);
        ReleaseCom(ref _chestGeometry);

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
        ReleaseCom(ref _rightPawGeometry);
        ReleaseCom(ref _leftPawGeometry);
        ReleaseCom(ref _tailFoldGeometry);
        ReleaseCom(ref _tailLowerSweepGeometry);
        ReleaseCom(ref _tailMiddleSweepGeometry);
        ReleaseCom(ref _tailUpperTuftGeometry);
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

    private sealed class ActivityBrush : IDisposable
    {
        private IntPtr _value;

        public ActivityBrush(
            Direct2DTestControl owner,
            Color color,
            int alpha)
        {
            alpha = Math.Clamp(alpha, 0, 255);
            _value =
                owner.CreateBrush(
                    Color.FromArgb(
                        alpha,
                        color));
        }

        public IntPtr Value => _value;

        public void Dispose()
        {
            ReleaseCom(ref _value);
        }
    }

    private sealed class ActivityBrushPair : IDisposable
    {
        private readonly ActivityBrush _primary;
        private readonly ActivityBrush _secondary;

        public ActivityBrushPair(
            Direct2DTestControl owner,
            Color primary,
            int primaryAlpha,
            Color secondary,
            int secondaryAlpha)
        {
            _primary =
                new ActivityBrush(
                    owner,
                    primary,
                    primaryAlpha);
            _secondary =
                new ActivityBrush(
                    owner,
                    secondary,
                    secondaryAlpha);
        }

        public IntPtr Primary =>
            _primary.Value;

        public IntPtr Secondary =>
            _secondary.Value;

        public void Dispose()
        {
            _secondary.Dispose();
            _primary.Dispose();
        }
    }

    private readonly record struct ActivityColors(
        Color Primary,
        Color Secondary);

    private readonly record struct DirectExpression(
        float EyeOpenness,
        float PupilScale,
        float BrowLift,
        float BrowInnerDrop,
        float MouthCurve,
        float HeadTiltDegrees,
        float HeadOffsetY);

    private readonly record struct CoreColors(
        Color Tail,
        Color TailAccent,
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
