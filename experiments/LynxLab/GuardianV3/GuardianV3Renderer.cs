using System.Drawing.Drawing2D;

namespace LynxLab;

internal sealed class GuardianV3Renderer :
    ILynxRenderer,
    IAnimatedLynxRenderer,
    IActivityLynxRenderer
{
    private const float DesignSize = 160f;
    private LynxAnimationFrame _animation = LynxAnimationFrame.Static;
    private LynxActivityState _activity = LynxActivityState.None;
    private double _activityElapsed;

    public string Name => "Guardian V9 armored · traffic flow";

    public void SetAnimationFrame(LynxAnimationFrame frame) => _animation = frame;

    public void SetActivityFrame(
        LynxActivityState activity,
        double secondsSinceActivityChange)
    {
        _activity = activity;
        _activityElapsed = Math.Max(0d, secondsSinceActivityChange);
    }

    public void Draw(
        Graphics graphics,
        Rectangle bounds,
        LynxPalette palette,
        LynxVisualState state,
        bool debugOverlay)
    {
        var saved = graphics.Save();

        try
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.TranslateTransform(bounds.Left, bounds.Top);
            graphics.ScaleTransform(bounds.Width / DesignSize, bounds.Height / DesignSize);

            var selectedPalette = palette;
            var activePalette = LynxPalette.Blend(
                selectedPalette,
                _activity,
                _activityElapsed);
            var armorPalette = ArmorTransitionPalette(
                activePalette,
                _activityElapsed);
            var colors = SilhouetteColors.FromPalette(activePalette);
            var miniature = Math.Min(bounds.Width, bounds.Height) <= 180;

            DrawActivityField(
                graphics,
                selectedPalette,
                activePalette,
                state,
                _activity,
                _activityElapsed,
                miniature);

            var expression = ApplyActivityExpression(
                ExpressionProfile.For(state, _animation.TransitionAmount),
                _activity,
                _activityElapsed);

            var tailSaved = graphics.Save();
            try
            {
                ApplyTailSway(
                    graphics,
                    _animation.TailSwayDegrees +
                    expression.TailPoseDegrees +
                    ActivityTailOffset(_activity, _activityElapsed));
                DrawTail(graphics, colors);
            }
            finally
            {
                graphics.Restore(tailSaved);
            }

            var bodySaved = graphics.Save();
            try
            {
                ApplyTransitionOffset(graphics, _animation.BodyOffsetY);
                ApplyBreathing(graphics, _animation.Breath);

                DrawTorso(graphics, colors);
                DrawHaunches(graphics, colors);
                DrawForelegs(graphics, colors);
                DrawBodyArmor(graphics, armorPalette, state, _activity, miniature);
                DrawChestFur(graphics);
                DrawCollarArmor(graphics, armorPalette, state, _activity, miniature);
                var headSaved = graphics.Save();
                try
                {
                    ApplyHeadPose(graphics, expression);
                    DrawEars(graphics, colors, expression);
                    DrawHead(graphics, colors);
                    DrawFace(
                        graphics,
                        activePalette,
                        _activity,
                        miniature,
                        _animation.Blink,
                        expression);
                }
                finally
                {
                    graphics.Restore(headSaved);
                }

                DrawShield(
                    graphics,
                    armorPalette,
                    state,
                    _activity,
                    miniature,
                    _animation.ShieldPulse);
                DrawRimLighting(
                    graphics,
                    colors,
                    activePalette,
                    state,
                    _activity,
                    miniature);
                DrawActivityEffect(
                    graphics,
                    selectedPalette,
                    activePalette,
                    state,
                    _activity,
                    _activityElapsed,
                    miniature);
            }
            finally
            {
                graphics.Restore(bodySaved);
            }

            if (debugOverlay)
                DrawDebugGeometry(graphics);
        }
        finally
        {
            graphics.Restore(saved);
        }
    }

    private static void ApplyTailSway(Graphics g, float degrees)
    {
        // Pivot low on the tail so the root stays planted while the plume moves.
        g.TranslateTransform(-48f, -140f, MatrixOrder.Append);
        g.RotateTransform(degrees, MatrixOrder.Append);
        g.TranslateTransform(48f, 140f, MatrixOrder.Append);
    }

    private static void ApplyTransitionOffset(Graphics g, float offsetY)
    {
        // Phase 3 transition reactions are intentionally sub-pixel at desktop
        // scale. The ground and tail root stay fixed while the body briefly
        // settles/lifts, then returns exactly to its state-loop baseline.
        if (Math.Abs(offsetY) < 0.001f)
            return;

        g.TranslateTransform(0f, offsetY, MatrixOrder.Append);
    }

    private static void ApplyBreathing(Graphics g, float breath)
    {
        // Barely perceptible body expansion plus a tiny upward weight shift.
        var scaleY = 1f + breath * 0.0045f;
        var bob = -breath * 0.38f;

        g.TranslateTransform(0f, bob, MatrixOrder.Append);
        g.TranslateTransform(-80f, -145f, MatrixOrder.Append);
        g.ScaleTransform(1f, scaleY, MatrixOrder.Append);
        g.TranslateTransform(80f, 145f, MatrixOrder.Append);
    }

    private static void ApplyHeadPose(Graphics g, ExpressionProfile expression)
    {
        if (Math.Abs(expression.HeadOffsetY) > 0.001f)
            g.TranslateTransform(0f, expression.HeadOffsetY, MatrixOrder.Append);

        if (Math.Abs(expression.HeadTiltDegrees) < 0.001f)
            return;

        g.TranslateTransform(-80f, -79f, MatrixOrder.Append);
        g.RotateTransform(expression.HeadTiltDegrees, MatrixOrder.Append);
        g.TranslateTransform(80f, 79f, MatrixOrder.Append);
    }

    private static void DrawGroundReference(Graphics g, SilhouetteColors c)
    {
        using var brush = new SolidBrush(Color.FromArgb(26, c.Edge));
        g.FillEllipse(brush, 24, 146, 112, 7);
    }

    private static void DrawTail(Graphics g, SilhouetteColors c)
    {
        // Fuller plume with an S-curve. The tail should read as heavy fur,
        // not as a flat crescent or leaf.
        using var tail = Path(
            M(49, 143),
            C(31, 147, 15, 138, 10, 124),
            C(3, 106, 7, 88, 18, 74),
            C(27, 63, 38, 58, 48, 60),
            L(43, 53),
            C(55, 54, 66, 64, 68, 78),
            C(71, 94, 64, 109, 57, 121),
            C(51, 132, 48, 139, 49, 143),
            Z());

        using var fill = new LinearGradientBrush(new RectangleF(6, 53, 64, 94),
            Mix(c.Tail, c.TailAccent, 0.42f), c.Tail, 35f);
        using var edge = Outline(c);

        g.FillPath(fill, tail);
        g.DrawPath(edge, tail);

        using var upperTuft = Path(
            M(17, 79),
            L(10, 75),
            L(20, 70),
            L(17, 64),
            C(27, 59, 38, 58, 48, 60),
            C(37, 62, 28, 68, 22, 77),
            Z());

        using var middleSweep = Path(
            M(13, 108),
            C(18, 91, 30, 77, 49, 70),
            C(38, 70, 28, 78, 22, 90),
            C(18, 98, 15, 104, 13, 108),
            Z());

        using var lowerSweep = Path(
            M(15, 121),
            C(23, 132, 35, 138, 49, 136),
            L(44, 142),
            C(31, 143, 21, 136, 15, 121),
            Z());

        using var accent = new LinearGradientBrush(new RectangleF(10, 55, 44, 89),
            Mix(c.TailAccent, c.EarInner, 0.25f), Mix(c.TailAccent, c.Tail, 0.6f), 80f);
        g.FillPath(accent, upperTuft);
        g.FillPath(accent, middleSweep);
        g.FillPath(accent, lowerSweep);
        using var fold = Path(M(49, 70), C(36, 86, 19, 106, 24, 123),
            C(17, 112, 22, 91, 36, 79), C(41, 75, 45, 72, 49, 70), Z());
        using var shade = new SolidBrush(Color.FromArgb(65, 12, 7, 25));
        g.FillPath(shade, fold);
    }

    private static void DrawTorso(Graphics g, SilhouetteColors c)
    {
        // Broader seated torso with shoulders that taper into the chest.
        using var torso = Path(
            M(53, 84),
            C(47, 92, 43, 103, 42, 117),
            C(41, 132, 45, 144, 56, 151),
            C(63, 156, 71, 158, 80, 158),
            C(89, 158, 97, 156, 104, 151),
            C(115, 144, 119, 132, 118, 117),
            C(117, 103, 113, 92, 107, 84),
            C(100, 79, 91, 76, 80, 76),
            C(69, 76, 60, 79, 53, 84),
            Z());

        using var fill = new LinearGradientBrush(new RectangleF(42, 76, 76, 82),
            Mix(c.Body, c.BodyAccent, 0.45f), c.Body, 65f);
        using var edge = Outline(c);

        g.FillPath(fill, torso);
        g.DrawPath(edge, torso);

        using var leftShoulder = Path(
            M(51, 92),
            L(60, 96),
            L(57, 101),
            L(65, 105),
            L(61, 110),
            C(56, 108, 52, 102, 51, 92),
            Z());

        using var rightShoulder = Mirror(leftShoulder);
        using var shoulderBrush = new SolidBrush(c.BodyAccent);

        g.FillPath(shoulderBrush, leftShoulder);
        g.FillPath(shoulderBrush, rightShoulder);
    }

    private static void DrawHaunches(Graphics g, SilhouetteColors c)
    {
        using var left = Path(
            M(45, 113),
            C(38, 122, 38, 137, 45, 147),
            C(50, 154, 59, 156, 66, 151),
            C(69, 145, 68, 135, 65, 125),
            C(61, 116, 53, 111, 45, 113),
            Z());

        using var right = Mirror(left);
        using var fill = new SolidBrush(c.Body);
        using var edge = Outline(c);

        g.FillPath(fill, left);
        g.FillPath(fill, right);
        g.DrawPath(edge, left);
        g.DrawPath(edge, right);
    }

    private static void DrawForelegs(Graphics g, SilhouetteColors c)
    {
        DrawForeleg(g, left: true, c);
        DrawForeleg(g, left: false, c);
    }

    private static void DrawForeleg(Graphics g, bool left, SilhouetteColors c)
    {
        using var leg = Path(
            M(60, 99),
            C(57, 110, 57, 125, 59, 139),
            C(60, 146, 64, 150, 69, 151),
            C(72, 151, 74, 148, 74, 144),
            C(73, 131, 73, 116, 76, 103),
            C(71, 99, 65, 98, 60, 99),
            Z());

        using var actual = left ? leg : Mirror(leg);
        using var fill = new LinearGradientBrush(new RectangleF(56, 99, 48, 55),
            Mix(c.Limb, c.BodyAccent, 0.25f), c.Limb, 90f);
        using var edge = Outline(c);

        g.FillPath(fill, actual);
        g.DrawPath(edge, actual);

        var pawRect = left
            ? new RectangleF(56, 143, 20, 11)
            : new RectangleF(84, 143, 20, 11);

        using var paw = new GraphicsPath();
        paw.AddEllipse(pawRect);
        using var pawFill = new SolidBrush(c.Paw);

        g.FillPath(pawFill, paw);
        g.DrawPath(edge, paw);
    }

    private static void DrawBodyArmor(
        Graphics g,
        LynxPalette palette,
        LynxVisualState state,
        LynxActivityState activity,
        bool miniature)
    {
        var stateAccent = CombinedAccent(state, activity, palette);
        var armorDark = Mix(
            Color.FromArgb(14, 19, 25),
            palette.Fur,
            0.18f);
        var armorMid = Mix(
            Color.FromArgb(34, 44, 57),
            palette.Fur,
            0.48f);
        var armorEdge = Mix(
            palette.Accent,
            stateAccent,
            0.30f);

        using var leftChest = Path(
            M(49, 92), L(63, 96), L(73, 105),
            L(69, 127), L(60, 139), L(53, 131),
            L(48, 110), Z());
        using var rightChest = Mirror(leftChest);

        using var chestFill = new LinearGradientBrush(
            new RectangleF(47, 92, 66, 49),
            armorMid,
            armorDark,
            90f);
        using var armorPen = new Pen(
            Color.FromArgb(miniature ? 220 : 190, armorEdge),
            miniature ? 1.55f : 0.95f)
        {
            LineJoin = LineJoin.Bevel
        };

        g.FillPath(chestFill, leftChest);
        g.FillPath(chestFill, rightChest);
        g.DrawPath(armorPen, leftChest);
        g.DrawPath(armorPen, rightChest);

        using var leftShoulder = Path(
            M(48, 91), L(58, 87), L(70, 94),
            L(63, 101), L(53, 98), Z());
        using var rightShoulder = Mirror(leftShoulder);
        using var shoulderFill = new LinearGradientBrush(
            new RectangleF(48, 87, 64, 15),
            Mix(Color.FromArgb(41, 50, 62), palette.Accent, 0.30f),
            Mix(Color.FromArgb(14, 18, 24), palette.Fur, 0.20f),
            25f);

        g.FillPath(shoulderFill, leftShoulder);
        g.FillPath(shoulderFill, rightShoulder);
        g.DrawPath(armorPen, leftShoulder);
        g.DrawPath(armorPen, rightShoulder);

        using var leftBracer = Path(
            M(58.5f, 118), L(72.5f, 118),
            L(72.3f, 139), L(60f, 139), Z());
        using var rightBracer = Mirror(leftBracer);
        using var bracerFill = new LinearGradientBrush(
            new RectangleF(58, 118, 44, 22),
            Mix(Color.FromArgb(36, 44, 54), palette.Accent, 0.26f),
            Mix(Color.FromArgb(12, 16, 21), palette.Fur, 0.18f),
            90f);

        g.FillPath(bracerFill, leftBracer);
        g.FillPath(bracerFill, rightBracer);
        g.DrawPath(armorPen, leftBracer);
        g.DrawPath(armorPen, rightBracer);

        using var channel = new Pen(
            Color.FromArgb(miniature ? 235 : 205, armorEdge),
            miniature ? 1.8f : 0.9f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        g.DrawLine(channel, 54f, 97f, 65f, 101f);
        g.DrawLine(channel, 106f, 97f, 95f, 101f);
        g.DrawLine(channel, 61f, 122f, 61f, 135f);
        g.DrawLine(channel, 99f, 122f, 99f, 135f);
    }

    private static void DrawHead(Graphics g, SilhouetteColors c)
    {
        // Smaller and more tapered than the first pass. The cheek fur provides
        // width without turning the whole skull into a circle.
        using var head = Path(
            M(46, 39),
            C(52, 30, 62, 24, 72, 21),
            L(70, 17),
            L(78, 20),
            L(86, 16),
            L(84, 21),
            C(98, 23, 108, 30, 114, 39),
            C(120, 48, 121, 58, 118, 67),

            L(123, 72),
            L(115, 73),
            L(120, 79),
            L(111, 78),
            L(114, 84),
            L(106, 83),

            C(99, 91, 90, 96, 80, 98),
            C(70, 96, 61, 91, 54, 83),

            L(46, 84),
            L(49, 78),
            L(40, 79),
            L(45, 73),
            L(37, 72),
            L(42, 67),

            C(39, 58, 40, 48, 46, 39),
            Z());

        using var fill = new LinearGradientBrush(new RectangleF(39, 20, 82, 78),
            Mix(c.Head, c.EarInner, 0.22f), Mix(c.Head, c.Body, 0.25f), 55f);
        using var edge = Outline(c);

        g.FillPath(fill, head);
        g.DrawPath(edge, head);
    }

    private static void DrawEars(
        Graphics g,
        SilhouetteColors c,
        ExpressionProfile expression)
    {
        using var leftEar = Path(
            M(46, 45),
            C(40, 35, 40, 20, 48, 6),
            C(58, 14, 64, 24, 66, 36),
            C(59, 40, 52, 43, 46, 45),
            Z());

        using var rightEar = Mirror(leftEar);

        using var leftInner = Path(
            M(48, 37),
            C(46, 28, 48, 18, 51, 12),
            C(57, 19, 60, 27, 61, 34),
            L(57, 31),
            L(58, 37),
            L(52, 33),
            L(50, 39),
            Z());

        using var rightInner = Mirror(leftInner);

        RotatePathAround(
            leftEar,
            new PointF(54f, 42f),
            -expression.EarOutwardDegrees);
        RotatePathAround(
            leftInner,
            new PointF(54f, 42f),
            -expression.EarOutwardDegrees);
        RotatePathAround(
            rightEar,
            new PointF(106f, 42f),
            expression.EarOutwardDegrees);
        RotatePathAround(
            rightInner,
            new PointF(106f, 42f),
            expression.EarOutwardDegrees);

        using var fill = new LinearGradientBrush(
            new RectangleF(40, 6, 80, 40),
            Mix(c.Ear, c.Head, 0.3f),
            c.Ear,
            90f);
        using var edge = Outline(c);
        using var innerBrush = new SolidBrush(c.EarInner);

        g.FillPath(fill, leftEar);
        g.FillPath(fill, rightEar);
        g.DrawPath(edge, leftEar);
        g.DrawPath(edge, rightEar);
        g.FillPath(innerBrush, leftInner);
        g.FillPath(innerBrush, rightInner);
    }


    private static void DrawChestFur(Graphics g)
    {
        // Broad shoulders become a curved ruff, then narrow between the forelegs.
        // The head overlaps its root so the chest grows naturally from the neck.
        using var chest = Path(
            M(60, 89), C(69, 86, 91, 86, 100, 89),
            C(100, 97, 98, 103, 96, 107), L(94, 102),
            C(93, 110, 90, 115, 87, 119), L(88, 114),
            C(83, 122, 84, 133, 80, 140),
            C(76, 133, 77, 122, 72, 114), L(73, 119),
            C(70, 115, 67, 110, 66, 102), L(64, 107),
            C(62, 103, 60, 97, 60, 89), Z());
        using var white = new LinearGradientBrush(new RectangleF(60, 89, 40, 51),
            Color.FromArgb(249, 245, 255), Color.FromArgb(184, 169, 209), 90f);
        g.FillPath(white, chest);
        using var leftLock = Path(M(65, 103), C(68, 108, 74, 110, 78, 115),
            L(75, 113), C(78, 119, 78, 124, 78, 129),
            C(74, 123, 70, 118, 69, 111), L(68, 115),
            C(66, 111, 65, 107, 65, 103), Z());
        using var rightLock = Mirror(leftLock);
        using var lockFill = new SolidBrush(Color.FromArgb(234, 226, 245));
        g.FillPath(lockFill, leftLock);
        g.FillPath(lockFill, rightLock);
        using var centerLock = Path(M(72, 107), C(77, 110, 83, 110, 88, 107),
            C(85, 113, 83, 119, 84, 123), L(81, 120),
            C(82, 126, 81, 131, 80, 135),
            C(78, 129, 77, 123, 76, 120), L(75, 123),
            C(75, 116, 74, 111, 72, 107), Z());
        using var centerFill = new SolidBrush(Color.FromArgb(251, 248, 255));
        g.FillPath(centerFill, centerLock);
    }

    private static void DrawFace(
        Graphics g,
        LynxPalette palette,
        LynxActivityState activity,
        bool miniature,
        float blink,
        ExpressionProfile expression)
    {
        using var mask = Path(
            M(47, 66), C(49, 59, 56, 56, 63, 58),
            C(70, 60, 76, 65, 80, 70),
            C(84, 65, 90, 60, 97, 58),
            C(104, 56, 111, 59, 113, 66),
            C(117, 74, 111, 81, 104, 85),
            C(96, 91, 88, 94, 80, 95),
            C(72, 94, 64, 91, 56, 85),
            C(49, 81, 43, 74, 47, 66), Z());
        using var white = new LinearGradientBrush(
            new RectangleF(45, 57, 70, 39),
            Color.FromArgb(255, 253, 255),
            Color.FromArgb(222, 211, 237),
            90f);
        g.FillPath(white, mask);

        DrawFaceEye(
            g,
            palette,
            activity,
            miniature,
            false,
            blink,
            expression);
        DrawFaceEye(
            g,
            palette,
            activity,
            miniature,
            true,
            blink,
            expression);

        using var nose = Path(
            M(75, 72), C(77, 70.5f, 83, 70.5f, 85, 72),
            C(85, 74, 82, 77, 80, 77.5f),
            C(78, 77, 75, 74, 75, 72), Z());
        using var ink = new SolidBrush(Color.FromArgb(27, 14, 43));
        g.FillPath(ink, nose);

        var mouthEdgeY = 84.3f;
        var mouthCenterY = mouthEdgeY + expression.MouthCurve;
        using var mouth = Path(
            M(73, mouthEdgeY),
            C(75.5f, mouthEdgeY, 78f, mouthCenterY, 80f, mouthCenterY),
            C(82f, mouthCenterY, 84.5f, mouthEdgeY, 87f, mouthEdgeY));

        using var mouthPen = new Pen(ink, miniature ? 1.6f : 1.15f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        g.DrawLine(mouthPen, 80, 76.5f, 80, 81.5f);
        g.DrawPath(mouthPen, mouth);
    }

    private static void DrawFaceEye(
        Graphics g,
        LynxPalette palette,
        LynxActivityState activity,
        bool miniature,
        bool right,
        float blink,
        ExpressionProfile expression)
    {
        using var leftEye = Path(
            M(54, 51), C(58, 51, 64, 53.5f, 68, 56.5f),
            C(67, 63, 64, 66, 60, 65),
            C(56, 64, 53, 59, 54, 51), Z());
        using var eye = right ? Mirror(leftEye) : (GraphicsPath)leftEye.Clone();

        var eyeOpen = Math.Clamp(
            expression.EyeOpenness - blink * 0.90f,
            0.10f,
            1.12f);

        const float blinkPivotY = 58.5f;
        using (var blinkMatrix = new Matrix())
        {
            blinkMatrix.Translate(0f, -blinkPivotY, MatrixOrder.Append);
            blinkMatrix.Scale(1f, eyeOpen, MatrixOrder.Append);
            blinkMatrix.Translate(0f, blinkPivotY, MatrixOrder.Append);
            eye.Transform(blinkMatrix);
        }

        using var dark = new SolidBrush(Color.FromArgb(23, 12, 37));
        g.FillPath(dark, eye);

        var saved = g.Save();
        try
        {
            g.SetClip(eye, CombineMode.Intersect);

            var center = right ? 99f : 61f;
            var irisWidth = 9f * expression.PupilScale;
            var irisHeight = 11f * eyeOpen * expression.PupilScale;
            var irisY = 60.5f - irisHeight / 2f;
            var pupilWidth = 4.2f * expression.PupilScale;
            var pupilHeight = 9f * eyeOpen * expression.PupilScale;
            var pupilY = 58.5f - pupilHeight / 2f;

            var irisRect = new RectangleF(
                center - irisWidth / 2f,
                irisY,
                irisWidth,
                irisHeight);

            if (activity == LynxActivityState.Failure)
            {
                using var failureIris = new LinearGradientBrush(
                    irisRect,
                    Color.FromArgb(238, 58, 86),
                    Color.FromArgb(142, 67, 224),
                    right ? 180f : 0f);

                g.FillEllipse(
                    failureIris,
                    irisRect);

                using var failureCore = new SolidBrush(
                    Color.FromArgb(175, 99, 52, 194));
                g.FillEllipse(
                    failureCore,
                    center - pupilWidth * 0.72f,
                    pupilY + pupilHeight * 0.08f,
                    pupilWidth * 1.44f,
                    pupilHeight * 0.84f);
            }
            else
            {
                using var iris = new SolidBrush(
                    Mix(
                        Color.FromArgb(139, 70, 221),
                        palette.Eye,
                        0.12f));

                g.FillEllipse(
                    iris,
                    irisRect);
            }

            g.FillEllipse(
                dark,
                center - pupilWidth / 2f,
                pupilY,
                pupilWidth,
                pupilHeight);

            if (blink < 0.72f)
            {
                var highlightSize = miniature ? 2.5f : 2f;
                g.FillEllipse(
                    Brushes.White,
                    center - 3f,
                    54f + blink * 2.2f + expression.EyeHighlightOffsetY,
                    highlightSize,
                    highlightSize * eyeOpen);
            }
        }
        finally
        {
            g.Restore(saved);
        }

        var browOuterY = 50.8f + expression.BrowLift;
        var browInnerY =
            56.5f +
            expression.BrowInnerDrop +
            expression.BrowLift;

        using var leftLid = Path(
            M(53.5f, browOuterY),
            C(
                58f, browOuterY + 0.2f,
                64f, browInnerY - 2f,
                68.5f, browInnerY));
        using var lid =
            right ? Mirror(leftLid) : (GraphicsPath)leftLid.Clone();

        using var lidPen = new Pen(
            Color.FromArgb(42, 21, 67),
            miniature ? 1.5f : 1.0f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        g.DrawPath(lidPen, lid);

        if (blink > 0.62f)
        {
            var center = right ? 99f : 61f;
            using var closedPen = new Pen(
                Color.FromArgb(55, 28, 82),
                miniature ? 1.7f : 1.05f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            g.DrawArc(
                closedPen,
                center - 7f,
                56.2f,
                14f,
                4.5f,
                8f,
                164f);
        }
    }


    private static void DrawCollarArmor(
        Graphics g,
        LynxPalette palette,
        LynxVisualState state,
        LynxActivityState activity,
        bool miniature)
    {
        // The upper edge follows the jaw; the lower edge forms rigid plates.
        using var leftPanel = Path(
            M(57, 90), L(66, 95), L(80, 99), L(80, 108),
            L(65, 106), L(53, 100), L(55, 94), Z());
        using var rightPanel = Mirror(leftPanel);
        var stateAccent = CombinedAccent(state, activity, palette);
        using var armor = new LinearGradientBrush(
            new RectangleF(53, 90, 54, 18),
            Mix(Color.FromArgb(39, 49, 59), palette.Accent, 0.34f),
            Mix(Color.FromArgb(10, 14, 19), palette.Fur, 0.18f),
            90f);
        using var edge = new Pen(
            Color.FromArgb(220, Mix(palette.Accent, stateAccent, 0.50f)),
            miniature ? 1.65f : 0.95f)
        {
            LineJoin = LineJoin.Bevel
        };
        g.FillPath(armor, leftPanel);
        g.FillPath(armor, rightPanel);
        g.DrawPath(edge, leftPanel);
        g.DrawPath(edge, rightPanel);

        using var leftFacet = Path(
            M(56, 96), L(64, 100), L(69, 104), L(61, 102), Z());
        using var rightFacet = Mirror(leftFacet);
        using var purple = new SolidBrush(Mix(palette.Accent, stateAccent, 0.30f));
        g.FillPath(purple, leftFacet);
        g.FillPath(purple, rightFacet);

        if (!miniature)
        {
            using var leftSeam = Path(M(61, 93), L(63, 98), L(73, 102));
            using var rightSeam = Mirror(leftSeam);
            using var seam = new Pen(Color.FromArgb(72, 58, 92), 0.7f);
            g.DrawPath(seam, leftSeam);
            g.DrawPath(seam, rightSeam);
        }
    }

    private static void DrawShield(
        Graphics g,
        LynxPalette palette,
        LynxVisualState state,
        LynxActivityState activity,
        bool miniature,
        float pulse)
    {
        var saved = g.Save();
        try
        {
            // Seat the badge against the collar while retaining the exposed chest ruff.
            g.TranslateTransform(0, -1.5f);
            DrawShieldBadge(g, palette, state, activity, miniature, pulse);
        }
        finally
        {
            g.Restore(saved);
        }
    }

    private static void DrawShieldBadge(
        Graphics g,
        LynxPalette palette,
        LynxVisualState state,
        LynxActivityState activity,
        bool miniature,
        float pulse)
    {
        using var shield = Path(
            M(80, 96), L(91, 101), L(89, 113),
            L(80, 121), L(71, 113), L(69, 101), Z());
        var violet = Mix(Color.FromArgb(161, 102, 235), palette.Accent, 0.10f);
        var stateAccent = CombinedAccent(state, activity, palette);
        pulse = Math.Clamp(pulse, 0f, 1f);
        var haloAlpha = 24 + (int)Math.Round(36f * pulse);
        var haloWidth = (miniature ? 3.2f : 2.8f) + 0.45f * pulse;
        using var halo = new Pen(Color.FromArgb(haloAlpha, stateAccent), haloWidth)
        {
            LineJoin = LineJoin.Round
        };
        // A single low-opacity edge accent keeps the emblem legible on white fur.
        g.DrawPath(halo, shield);
        using var fill = new LinearGradientBrush(
            new RectangleF(69, 96, 22, 25),
            Mix(Color.FromArgb(48, 75, 96), palette.Accent, 0.58f),
            Mix(Color.FromArgb(18, 31, 40), palette.Fur, 0.34f),
            65f);
        using var border = new Pen(Mix(Color.FromArgb(201, 166, 249), palette.Eye, 0.08f),
            miniature ? 1.8f : 1.25f)
        {
            LineJoin = LineJoin.Miter
        };
        g.FillPath(fill, shield);
        g.DrawPath(border, shield);

        using var inset = Path(
            M(80, 99), L(88, 103), L(86.5f, 111.5f),
            L(80, 117.5f), L(73.5f, 111.5f), L(72, 103), Z());
        using var innerFill = new LinearGradientBrush(
            new RectangleF(72, 99, 16, 19),
            Mix(Color.FromArgb(66, 104, 126), palette.Accent, 0.60f),
            Mix(Color.FromArgb(22, 50, 57), palette.Fur, 0.35f),
            90f);
        g.FillPath(innerFill, inset);
        if (!miniature)
        {
            using var innerEdge = new Pen(violet, 0.65f);
            g.DrawPath(innerEdge, inset);
        }

        using var check = new Pen(Color.White, miniature ? 2.7f : 2.2f)
        {
            StartCap = LineCap.Square,
            EndCap = LineCap.Square,
            LineJoin = LineJoin.Miter
        };
        g.DrawLines(check,
        [
            new PointF(75.5f, 107.5f),
            new PointF(79, 111),
            new PointF(85, 104.5f)
        ]);
    }

    private static void DrawRimLighting(
        Graphics g,
        SilhouetteColors c,
        LynxPalette palette,
        LynxVisualState state,
        LynxActivityState activity,
        bool miniature)
    {
        var stateAccent = CombinedAccent(state, activity, palette);
        var rimColor = Mix(c.EarInner, stateAccent, state is LynxVisualState.Idle ? 0.0f : 0.18f);
        using var rim = new Pen(Color.FromArgb(miniature ? 115 : 145, rimColor),
            miniature ? 1.15f : 0.8f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        using var ear = Path(M(42, 29), C(42, 21, 44, 13, 48, 6),
            C(52, 9, 56, 13, 59, 17));
        using var temple = Path(M(47, 38), C(53, 30, 63, 25, 71, 22));
        using var tail = Path(M(8, 108), C(8, 119, 12, 130, 22, 136));
        g.DrawPath(rim, ear);
        g.DrawPath(rim, temple);
        g.DrawPath(rim, tail);
        if (!miniature)
        {
            using var cheek = Path(M(42, 57), C(41, 61, 42, 64, 43, 67));
            using var leg = Path(M(59, 117), C(59, 123, 59, 131, 60, 137));
            g.DrawPath(rim, cheek);
            g.DrawPath(rim, leg);
        }
    }

    private static ExpressionProfile ApplyActivityExpression(
        ExpressionProfile expression,
        LynxActivityState activity,
        double elapsed)
    {
        var breathe = (float)Math.Sin(elapsed * Math.PI * 1.35);

        return activity switch
        {
            LynxActivityState.Thinking => expression with
            {
                EyeOpenness = 0.78f,
                PupilScale = 0.88f,
                BrowLift = 0.8f,
                BrowInnerDrop = 2.6f,
                MouthCurve = -0.7f,
                EarOutwardDegrees = -2.8f,
                HeadTiltDegrees = -2.2f + breathe * 0.7f,
                HeadOffsetY = -0.8f
            },

            LynxActivityState.Preparing => expression with
            {
                EyeOpenness = 0.84f,
                PupilScale = 0.92f,
                BrowLift = 0.1f,
                BrowInnerDrop = 1.8f,
                MouthCurve = -0.4f,
                EarOutwardDegrees = -3.6f,
                HeadTiltDegrees = 1.8f,
                HeadOffsetY = -0.8f
            },

            LynxActivityState.Sorting => expression with
            {
                EyeOpenness = 0.94f,
                PupilScale = 1.02f,
                BrowLift = -1.0f,
                BrowInnerDrop = -1.4f,
                MouthCurve = 0.4f,
                EarOutwardDegrees = -2.2f,
                HeadTiltDegrees = breathe * 1.1f,
                HeadOffsetY = -0.4f
            },

            LynxActivityState.Packing => expression with
            {
                EyeOpenness = 0.80f,
                PupilScale = 0.94f,
                BrowLift = 0.2f,
                BrowInnerDrop = 1.2f,
                MouthCurve = 1.2f,
                EarOutwardDegrees = -1.0f,
                HeadTiltDegrees = 0f,
                HeadOffsetY = -0.5f
            },

            LynxActivityState.Incoming => expression with
            {
                EyeOpenness = 1.14f,
                PupilScale = 1.22f,
                BrowLift = -4.0f,
                BrowInnerDrop = -5.0f,
                MouthCurve = 0.2f,
                EarOutwardDegrees = -5.8f,
                HeadTiltDegrees = -4.4f,
                HeadOffsetY = -1.3f
            },

            LynxActivityState.Outgoing => expression with
            {
                EyeOpenness = 0.82f,
                PupilScale = 0.96f,
                BrowLift = -1.4f,
                BrowInnerDrop = -2.0f,
                MouthCurve = 2.8f,
                EarOutwardDegrees = 0.4f,
                HeadTiltDegrees = 3.0f,
                HeadOffsetY = -0.3f
            },

            LynxActivityState.Reconciling => expression with
            {
                EyeOpenness = 0.62f,
                PupilScale = 0.76f,
                BrowLift = 2.0f,
                BrowInnerDrop = 5.2f,
                MouthCurve = -3.6f,
                EarOutwardDegrees = -5.4f,
                HeadTiltDegrees = 0f,
                HeadOffsetY = -1.4f
            },

            LynxActivityState.Success => expression with
            {
                EyeOpenness = 0.76f,
                PupilScale = 1.02f,
                BrowLift = -2.8f,
                BrowInnerDrop = -4.4f,
                MouthCurve = 4.8f,
                EarOutwardDegrees = 3.2f,
                HeadTiltDegrees = 2.4f,
                HeadOffsetY = 0.2f
            },

            LynxActivityState.Warning => expression with
            {
                EyeOpenness = 0.88f,
                PupilScale = 0.94f,
                BrowLift = 0.8f,
                BrowInnerDrop = 2.8f,
                MouthCurve = -3.0f,
                EarOutwardDegrees = 5.8f,
                HeadTiltDegrees = -2.0f,
                HeadOffsetY = 0.8f
            },

            LynxActivityState.Failure => expression with
            {
                EyeOpenness = 0.56f,
                PupilScale = 0.72f,
                BrowLift = 3.0f,
                BrowInnerDrop = 6.2f,
                MouthCurve = -5.2f,
                EarOutwardDegrees = 7.2f,
                HeadTiltDegrees = 0f,
                HeadOffsetY = 1.6f
            },

            LynxActivityState.Resting => expression with
            {
                EyeOpenness = 0.22f,
                PupilScale = 0.80f,
                BrowLift = -2.0f,
                BrowInnerDrop = -4.0f,
                MouthCurve = 1.8f,
                EarOutwardDegrees = 7.5f,
                HeadTiltDegrees = -5.0f,
                HeadOffsetY = 2.2f
            },

            _ => expression
        };
    }

    private static float ActivityTailOffset(
        LynxActivityState activity,
        double elapsed)
    {
        var wave =
            (float)Math.Sin(elapsed * Math.PI * 2d / 1.8d);
        var fastWave =
            (float)Math.Sin(elapsed * Math.PI * 2d / 0.92d);
        var slowWave =
            (float)Math.Sin(elapsed * Math.PI * 2d / 3.8d);

        return activity switch
        {
            LynxActivityState.Thinking => wave * 1.6f,
            LynxActivityState.Preparing => wave * 2.1f,
            LynxActivityState.Sorting => wave * 5.2f,
            LynxActivityState.Packing => wave * 3.0f,
            LynxActivityState.Incoming => 7.0f + wave * 4.2f,
            LynxActivityState.Outgoing => 5.0f + wave * 3.4f,
            LynxActivityState.Reconciling => fastWave * 2.6f,
            LynxActivityState.Success => 6.0f + wave * 3.7f,
            LynxActivityState.Warning => -3.0f + fastWave * 3.2f,
            LynxActivityState.Failure => -5.0f + fastWave * 4.6f,
            LynxActivityState.Resting => -7.0f + slowWave * 2.2f,
            _ => slowWave * 1.4f
        };
    }

    private static LynxPalette ArmorTransitionPalette(
        LynxPalette identity,
        double elapsed)
    {
        var blue = LynxPalette.All.First(palette =>
            string.Equals(
                palette.Name,
                "Midnight Blue",
                StringComparison.Ordinal));
        var emerald = LynxPalette.All.First(palette =>
            string.Equals(
                palette.Name,
                "Forest Emerald",
                StringComparison.Ordinal));

        var mix =
            0.5f +
            0.5f * (float)Math.Sin(
                elapsed * Math.PI * 2d / 4.8d);

        Color BlendArmor(Color blueColor, Color greenColor, Color identityColor)
        {
            var mood = Mix(blueColor, greenColor, mix);
            return Mix(mood, identityColor, 0.18f);
        }

        return new LynxPalette(
            "Armor Blue ↔ Forest Emerald",
            BlendArmor(blue.Fur, emerald.Fur, identity.Fur),
            BlendArmor(blue.Ear, emerald.Ear, identity.Ear),
            BlendArmor(blue.Edge, emerald.Edge, identity.Edge),
            BlendArmor(blue.Eye, emerald.Eye, identity.Eye),
            BlendArmor(blue.Accent, emerald.Accent, identity.Accent),
            BlendArmor(blue.Muzzle, emerald.Muzzle, identity.Muzzle),
            BlendArmor(blue.Detail, emerald.Detail, identity.Detail));
    }

    private static void DrawActivityField(
        Graphics g,
        LynxPalette selectedPalette,
        LynxPalette activePalette,
        LynxVisualState state,
        LynxActivityState activity,
        double elapsed,
        bool miniature)
    {
        if (activity == LynxActivityState.None)
            return;

        var partner = LynxPalette.ActivityPartner(activity);
        var mix = LynxPalette.ActivityMix(activity, elapsed);
        var semantic = CombinedAccent(state, activity, activePalette);

        var selectedAccent = Mix(
            selectedPalette.Accent,
            semantic,
            0.22f);
        var partnerAccent = Mix(
            partner.Accent,
            semantic,
            0.22f);

        var rotation = (float)((elapsed * 42d) % 360d);
        var pulse = 0.5f +
            0.5f * (float)Math.Sin(elapsed * Math.PI * 2d / 1.6d);

        var outerRect = new RectangleF(8f, 4f, 144f, 151f);
        var innerRect = new RectangleF(15f, 10f, 130f, 141f);

        var outerAlpha = miniature ? 165 : 120;
        var innerAlpha = miniature ? 135 : 95;

        using var outer = new Pen(
            Color.FromArgb(outerAlpha, Mix(selectedAccent, partnerAccent, mix)),
            miniature ? 2.1f : 1.15f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        using var inner = new Pen(
            Color.FromArgb(innerAlpha, Mix(partnerAccent, selectedAccent, mix)),
            miniature ? 1.45f : 0.85f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        switch (activity)
        {
            case LynxActivityState.Thinking:
            case LynxActivityState.Preparing:
                g.DrawArc(outer, outerRect, rotation, 118f);
                g.DrawArc(inner, innerRect, rotation + 178f, 82f);
                break;

            case LynxActivityState.Sorting:
                g.DrawArc(outer, outerRect, rotation, 72f);
                g.DrawArc(outer, outerRect, rotation + 120f, 72f);
                g.DrawArc(outer, outerRect, rotation + 240f, 72f);
                g.DrawArc(inner, innerRect, -rotation * 0.72f, 120f);
                break;

            case LynxActivityState.Packing:
            {
                var inset = 5f + pulse * 5f;
                using var bracket = new Pen(
                    Color.FromArgb(
                        miniature ? 195 : 145,
                        Mix(selectedAccent, partnerAccent, mix)),
                    miniature ? 1.9f : 1.0f)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                };

                g.DrawLine(bracket, 10f + inset, 44f, 10f + inset, 118f);
                g.DrawLine(bracket, 150f - inset, 44f, 150f - inset, 118f);
                g.DrawLine(bracket, 10f + inset, 44f, 25f + inset, 44f);
                g.DrawLine(bracket, 150f - inset, 44f, 135f - inset, 44f);
                g.DrawLine(bracket, 10f + inset, 118f, 25f + inset, 118f);
                g.DrawLine(bracket, 150f - inset, 118f, 135f - inset, 118f);
                break;
            }

            case LynxActivityState.Incoming:
            case LynxActivityState.Outgoing:
            {
                // Three independent perimeter waves travel across the whole
                // mascot. Incoming contracts outside -> pet; Outgoing is the
                // exact inverse. Each wave fades in and out during travel.
                var farRect = new RectangleF(
                    -3f,
                    -6f,
                    166f,
                    172f);
                var nearRect = new RectangleF(
                    18f,
                    13f,
                    124f,
                    135f);

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
                        (float)((elapsed * speed + offset) % 1d);
                    var travel =
                        activity == LynxActivityState.Incoming
                            ? phase
                            : 1f - phase;
                    var fade =
                        (float)Math.Sin(phase * Math.PI);

                    var ring = LerpRect(
                        farRect,
                        nearRect,
                        travel);

                    var ringColor =
                        i % 2 == 0
                            ? Mix(
                                selectedAccent,
                                partnerAccent,
                                travel)
                            : Mix(
                                partnerAccent,
                                selectedAccent,
                                travel);

                    using var trafficRing = new Pen(
                        Color.FromArgb(
                            Math.Max(
                                0,
                                (int)((miniature ? 188f : 132f) * fade)),
                            ringColor),
                        miniature ? 1.8f : 0.95f)
                    {
                        StartCap = LineCap.Round,
                        EndCap = LineCap.Round
                    };

                    var spin =
                        (float)((elapsed * (24d + i * 8d)) % 360d);

                    g.DrawArc(
                        trafficRing,
                        ring,
                        spin + i * 28f,
                        145f);
                    g.DrawArc(
                        trafficRing,
                        ring,
                        spin + 185f + i * 28f,
                        145f);
                }

                break;
            }

            case LynxActivityState.Reconciling:
                g.DrawArc(outer, outerRect, rotation, 148f);
                g.DrawArc(inner, innerRect, -rotation, 148f);
                break;

            case LynxActivityState.Success:
                using (var success = new Pen(
                    Color.FromArgb(
                        miniature ? 145 + (int)(75f * pulse) : 105 + (int)(55f * pulse),
                        Mix(selectedAccent, partnerAccent, mix)),
                    miniature ? 2.5f : 1.4f))
                {
                    g.DrawEllipse(success, outerRect);
                }
                break;

            case LynxActivityState.Warning:
            {
                // Two perimeter traces rotate continuously in opposite
                // directions so the warning reads even at desktop size.
                g.DrawArc(outer, outerRect, rotation, 132f);
                g.DrawArc(
                    inner,
                    innerRect,
                    180f - rotation * 1.18f,
                    132f);
                break;
            }

            case LynxActivityState.Failure:
            {
                // Two segments converge on the same collision point, touch,
                // then recoil. A triangular wave makes the motion repeat
                // without teleporting.
                var phase =
                    (float)((elapsed % 1.7d) / 1.7d);
                var collision =
                    phase <= 0.5f
                        ? phase * 2f
                        : (1f - phase) * 2f;

                var leftCenter = 198f + 72f * collision;
                var rightCenter = 342f - 72f * collision;
                var failColor = Mix(
                    Color.FromArgb(226, 58, 86),
                    partnerAccent,
                    0.36f);

                using var failA = new Pen(
                    Color.FromArgb(
                        miniature ? 235 : 185,
                        failColor),
                    miniature ? 2.6f : 1.35f)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                };
                using var failB = new Pen(
                    Color.FromArgb(
                        miniature ? 225 : 175,
                        Mix(
                            Color.FromArgb(141, 74, 222),
                            selectedAccent,
                            0.30f)),
                    miniature ? 2.3f : 1.25f)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                };

                g.DrawArc(
                    failA,
                    outerRect,
                    leftCenter - 24f,
                    48f);
                g.DrawArc(
                    failB,
                    outerRect,
                    rightCenter - 24f,
                    48f);

                if (collision > 0.86f)
                {
                    var impact =
                        (collision - 0.86f) / 0.14f;
                    using var impactPen = new Pen(
                        Color.FromArgb(
                            miniature
                                ? (int)(210f * impact)
                                : (int)(155f * impact),
                            Mix(failColor, partnerAccent, 0.45f)),
                        miniature ? 2.0f : 1.0f)
                    {
                        StartCap = LineCap.Round,
                        EndCap = LineCap.Round
                    };

                    g.DrawLine(
                        impactPen,
                        80f,
                        2f,
                        80f,
                        13f);
                    g.DrawLine(
                        impactPen,
                        73f,
                        6f,
                        77f,
                        15f);
                    g.DrawLine(
                        impactPen,
                        87f,
                        6f,
                        83f,
                        15f);
                }
                break;
            }

            case LynxActivityState.Resting:
            {
                // Quiet rings drift away from the mascot and disappear.
                for (var i = 0; i < 3; i++)
                {
                    var phase =
                        (float)((elapsed * 0.28d + i / 3d) % 1d);
                    var expand = 4f + phase * 14f;
                    var alpha =
                        (int)((1f - phase) *
                            (miniature ? 118f : 82f));

                    using var rest = new Pen(
                        Color.FromArgb(
                            Math.Max(0, alpha),
                            Mix(
                                selectedAccent,
                                partnerAccent,
                                phase)),
                        miniature ? 1.5f : 0.82f)
                    {
                        StartCap = LineCap.Round,
                        EndCap = LineCap.Round
                    };

                    var ring = new RectangleF(
                        innerRect.X - expand,
                        innerRect.Y - expand,
                        innerRect.Width + expand * 2f,
                        innerRect.Height + expand * 2f);

                    g.DrawArc(rest, ring, 205f, 130f);
                    g.DrawArc(rest, ring, 25f, 130f);
                }
                break;
            }
        }
    }

    private static void DrawActivityEffect(
        Graphics g,
        LynxPalette selectedPalette,
        LynxPalette activePalette,
        LynxVisualState state,
        LynxActivityState activity,
        double elapsed,
        bool miniature)
    {
        if (activity == LynxActivityState.None)
            return;

        var partner = LynxPalette.ActivityPartner(activity);
        var mix = LynxPalette.ActivityMix(activity, elapsed);
        var accentA = Mix(
            selectedPalette.Accent,
            CombinedAccent(state, activity, activePalette),
            0.25f);
        var accentB = Mix(
            partner.Accent,
            CombinedAccent(state, activity, activePalette),
            0.25f);

        var primary = Mix(accentA, accentB, mix);
        var secondary = Mix(accentB, accentA, mix);
        var pulse = 0.5f +
            0.5f * (float)Math.Sin(elapsed * Math.PI * 2d / 1.2d);

        using var primaryPen = new Pen(
            Color.FromArgb(miniature ? 235 : 200, primary),
            miniature ? 2.0f : 1.05f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        using var secondaryPen = new Pen(
            Color.FromArgb(miniature ? 215 : 175, secondary),
            miniature ? 1.7f : 0.9f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        using var primaryBrush = new SolidBrush(
            Color.FromArgb(miniature ? 235 : 205, primary));
        using var secondaryBrush = new SolidBrush(
            Color.FromArgb(miniature ? 220 : 185, secondary));

        switch (activity)
        {
            case LynxActivityState.Thinking:
            case LynxActivityState.Preparing:
            {
                for (var i = 0; i < 4; i++)
                {
                    var angle =
                        elapsed * 1.45 +
                        i * Math.PI * 2d / 4d;
                    var x = 80f + (float)Math.Cos(angle) * 67f;
                    var y = 80f + (float)Math.Sin(angle) * 70f;
                    var size = miniature ? 3.2f : 2.1f;

                    g.FillEllipse(
                        i % 2 == 0 ? primaryBrush : secondaryBrush,
                        x - size / 2f,
                        y - size / 2f,
                        size,
                        size);
                }

                var scanY = 22f + (float)((elapsed * 26d) % 116d);
                using var scan = new Pen(
                    Color.FromArgb(
                        miniature ? 135 : 90,
                        Mix(primary, secondary, 0.5f)),
                    miniature ? 1.25f : 0.7f);
                g.DrawLine(scan, 25f, scanY, 135f, scanY);
                break;
            }

            case LynxActivityState.Sorting:
            {
                var phase = (float)((elapsed * 34d) % 118d);
                DrawDataChip(
                    g,
                    7f,
                    22f + phase,
                    primary,
                    miniature);
                DrawDataChip(
                    g,
                    146f,
                    140f - phase,
                    secondary,
                    miniature);
                DrawDataChip(
                    g,
                    22f + phase * 0.82f,
                    7f,
                    primary,
                    miniature);
                DrawDataChip(
                    g,
                    132f - phase * 0.75f,
                    149f,
                    secondary,
                    miniature);
                break;
            }

            case LynxActivityState.Packing:
            {
                var inward = 2f + pulse * 8f;

                DrawArrow(g, primaryPen, 5f + inward, 62f, 29f + inward, 62f);
                DrawArrow(g, secondaryPen, 155f - inward, 62f, 131f - inward, 62f);
                DrawArrow(g, primaryPen, 10f + inward, 122f, 34f + inward, 122f);
                DrawArrow(g, secondaryPen, 150f - inward, 122f, 126f - inward, 122f);
                break;
            }

            case LynxActivityState.Incoming:
            {
                DrawTrafficArrow(
                    g, primary, true, false, 43f,
                    elapsed, 1.52d, 0.02d, miniature);
                DrawTrafficArrow(
                    g, secondary, true, true, 43f,
                    elapsed, 0.68d, 0.44d, miniature);

                DrawTrafficArrow(
                    g, secondary, true, false, 91f,
                    elapsed, 0.96d, 0.21d, miniature);
                DrawTrafficArrow(
                    g, primary, true, true, 91f,
                    elapsed, 1.31d, 0.67d, miniature);

                DrawTrafficArrow(
                    g, primary, true, false, 132f,
                    elapsed, 0.74d, 0.56d, miniature);
                DrawTrafficArrow(
                    g, secondary, true, true, 132f,
                    elapsed, 1.15d, 0.11d, miniature);
                break;
            }

            case LynxActivityState.Outgoing:
            {
                DrawTrafficArrow(
                    g, primary, false, false, 43f,
                    elapsed, 1.52d, 0.02d, miniature);
                DrawTrafficArrow(
                    g, secondary, false, true, 43f,
                    elapsed, 0.68d, 0.44d, miniature);

                DrawTrafficArrow(
                    g, secondary, false, false, 91f,
                    elapsed, 0.96d, 0.21d, miniature);
                DrawTrafficArrow(
                    g, primary, false, true, 91f,
                    elapsed, 1.31d, 0.67d, miniature);

                DrawTrafficArrow(
                    g, primary, false, false, 132f,
                    elapsed, 0.74d, 0.56d, miniature);
                DrawTrafficArrow(
                    g, secondary, false, true, 132f,
                    elapsed, 1.15d, 0.11d, miniature);
                break;
            }

            case LynxActivityState.Reconciling:
            {
                var angle = elapsed * 1.65d;

                for (var i = 0; i < 6; i++)
                {
                    var orbit =
                        angle +
                        i * Math.PI * 2d / 6d;
                    var x = 80f + (float)Math.Cos(orbit) * 69f;
                    var y = 81f + (float)Math.Sin(orbit) * 71f;
                    var size = miniature ? 2.8f : 1.9f;

                    g.FillEllipse(
                        i % 2 == 0 ? primaryBrush : secondaryBrush,
                        x - size / 2f,
                        y - size / 2f,
                        size,
                        size);
                }
                break;
            }

            case LynxActivityState.Success:
            {
                for (var i = 0; i < 5; i++)
                {
                    var angle =
                        -Math.PI / 2d +
                        i * Math.PI * 2d / 5d;
                    var radius = 67f + pulse * 4f;
                    var x = 80f + (float)Math.Cos(angle) * radius;
                    var y = 80f + (float)Math.Sin(angle) * radius;
                    var size = miniature ? 3.3f : 2.2f;
                    g.FillEllipse(
                        i % 2 == 0 ? primaryBrush : secondaryBrush,
                        x - size / 2f,
                        y - size / 2f,
                        size,
                        size);
                }
                break;
            }

            case LynxActivityState.Warning:
            {
                // Roughly three times the old marker size. It rocks around
                // its center so the warning is obvious on the tiny mascot.
                var tilt =
                    (float)Math.Sin(elapsed * Math.PI * 2d / 1.15d) *
                    11f;
                var warningSaved = g.Save();

                try
                {
                    g.TranslateTransform(
                        -80f,
                        -27f,
                        MatrixOrder.Append);
                    g.RotateTransform(
                        tilt,
                        MatrixOrder.Append);
                    g.TranslateTransform(
                        80f,
                        27f,
                        MatrixOrder.Append);

                    var warningColor = Mix(
                        Color.FromArgb(255, 201, 72),
                        primary,
                        0.18f);

                    using var warningPen = new Pen(
                        Color.FromArgb(
                            miniature ? 250 : 225,
                            warningColor),
                        miniature ? 2.8f : 1.55f)
                    {
                        LineJoin = LineJoin.Round
                    };
                    using var warningBrush = new SolidBrush(
                        Color.FromArgb(
                            miniature ? 245 : 220,
                            warningColor));

                    using var triangle = new GraphicsPath();
                    triangle.AddPolygon(
                    [
                        new PointF(80f, 1f),
                        new PointF(107f, 48f),
                        new PointF(53f, 48f)
                    ]);

                    g.DrawPath(warningPen, triangle);
                    g.DrawLine(
                        warningPen,
                        80f,
                        14f,
                        80f,
                        32f);
                    g.FillEllipse(
                        warningBrush,
                        77.5f,
                        37f,
                        5f,
                        5f);
                }
                finally
                {
                    g.Restore(warningSaved);
                }

                break;
            }

            case LynxActivityState.Failure:
            {
                // Side failure marks pulse while the outer perimeter segments
                // perform the actual crash-and-repel cycle.
                var failurePulse =
                    0.45f +
                    0.55f * (float)Math.Abs(
                        Math.Sin(elapsed * Math.PI * 2d / 0.86d));

                using var failPen = new Pen(
                    Color.FromArgb(
                        (int)((miniature ? 245f : 205f) * failurePulse),
                        Mix(
                            Color.FromArgb(232, 57, 87),
                            primary,
                            0.30f)),
                    miniature ? 2.35f : 1.25f)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                };

                g.DrawLine(failPen, 5f, 38f, 20f, 53f);
                g.DrawLine(failPen, 20f, 38f, 5f, 53f);
                g.DrawLine(failPen, 140f, 38f, 155f, 53f);
                g.DrawLine(failPen, 155f, 38f, 140f, 53f);
                break;
            }

            case LynxActivityState.Resting:
            {
                // Each z drifts away from the head and fades completely
                // before respawning near the pet.
                for (var i = 0; i < 3; i++)
                {
                    var phase =
                        (float)((elapsed * 0.26d + i * 0.31d) % 1d);
                    var alpha =
                        (int)((1f - phase) *
                            (miniature ? 235f : 195f));
                    var x = 118f + phase * 30f;
                    var y = 56f - phase * 44f;
                    var fontSize =
                        (miniature ? 7.2f : 5.2f) +
                        phase * (miniature ? 2.4f : 1.5f);

                    using var restBrush = new SolidBrush(
                        Color.FromArgb(
                            Math.Max(0, alpha),
                            Mix(primary, secondary, phase)));
                    using var font = new Font(
                        "Segoe UI",
                        fontSize,
                        FontStyle.Bold);

                    g.DrawString(
                        "z",
                        font,
                        restBrush,
                        x,
                        y);
                }

                break;
            }
        }
    }

    private static void DrawDataChip(
        Graphics g,
        float x,
        float y,
        Color accent,
        bool miniature)
    {
        var width = miniature ? 7f : 6f;
        var height = miniature ? 3.4f : 2.8f;
        using var fill = new SolidBrush(Color.FromArgb(175, accent));
        using var edge = new Pen(
            Color.FromArgb(220, accent),
            miniature ? 0.9f : 0.6f);
        g.FillRectangle(fill, x, y, width, height);
        g.DrawRectangle(edge, x, y, width, height);
    }

    private static RectangleF LerpRect(
        RectangleF from,
        RectangleF to,
        float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);

        return new RectangleF(
            from.X + (to.X - from.X) * amount,
            from.Y + (to.Y - from.Y) * amount,
            from.Width + (to.Width - from.Width) * amount,
            from.Height + (to.Height - from.Height) * amount);
    }

    private static void DrawTrafficArrow(
        Graphics g,
        Color accent,
        bool incoming,
        bool fromRight,
        float y,
        double elapsed,
        double speed,
        double offset,
        bool miniature)
    {
        var phase =
            (float)((elapsed * speed + offset) % 1d);

        // Full opacity around mid-flight; invisible at both endpoints.
        var fade =
            (float)Math.Sin(phase * Math.PI);
        var alpha =
            Math.Max(
                0,
                (int)((miniature ? 248f : 215f) * fade));

        // The travel corridor intentionally stops before the body so the
        // packet fades away at the moment it visually reaches the pet.
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
                    (innerRight - outerRight) * phase;
                tailX = headX + 10f;
            }
            else
            {
                headX =
                    outerLeft +
                    (innerLeft - outerLeft) * phase;
                tailX = headX - 10f;
            }
        }
        else
        {
            if (fromRight)
            {
                headX =
                    innerRight +
                    (outerRight - innerRight) * phase;
                tailX = headX - 10f;
            }
            else
            {
                headX =
                    innerLeft +
                    (outerLeft - innerLeft) * phase;
                tailX = headX + 10f;
            }
        }

        using var pen = new Pen(
            Color.FromArgb(alpha, accent),
            miniature ? 2.15f : 1.10f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        DrawArrow(
            g,
            pen,
            tailX,
            y,
            headX,
            y);
    }

    private static void DrawArrow(
        Graphics g,
        Pen pen,
        float x1,
        float y1,
        float x2,
        float y2)
    {
        g.DrawLine(pen, x1, y1, x2, y2);

        var direction = Math.Sign(x2 - x1);
        if (direction == 0)
            return;

        g.DrawLine(pen, x2, y2, x2 - direction * 4f, y2 - 3f);
        g.DrawLine(pen, x2, y2, x2 - direction * 4f, y2 + 3f);
    }

    private static Color ActivityAccent(
        LynxActivityState activity,
        LynxPalette palette)
    {
        var semantic = activity switch
        {
            LynxActivityState.Thinking or
            LynxActivityState.Preparing => Color.FromArgb(114, 200, 255),

            LynxActivityState.Sorting or
            LynxActivityState.Packing => Color.FromArgb(170, 150, 255),

            LynxActivityState.Incoming => Color.FromArgb(120, 216, 223),
            LynxActivityState.Outgoing => Color.FromArgb(170, 150, 255),
            LynxActivityState.Reconciling => Color.FromArgb(240, 189, 97),
            LynxActivityState.Success => Color.FromArgb(87, 215, 160),
            LynxActivityState.Warning => Color.FromArgb(240, 189, 97),
            LynxActivityState.Failure => Color.FromArgb(242, 117, 134),
            LynxActivityState.Resting => Color.FromArgb(140, 115, 232),
            _ => palette.Accent
        };

        return Mix(semantic, palette.Accent, 0.34f);
    }

    private static Color CombinedAccent(
        LynxVisualState state,
        LynxActivityState activity,
        LynxPalette palette) =>
        activity == LynxActivityState.None
            ? StateAccent(state, palette)
            : ActivityAccent(activity, palette);

    private static void RotatePathAround(
        GraphicsPath path,
        PointF pivot,
        float degrees)
    {
        if (Math.Abs(degrees) < 0.001f)
            return;

        using var matrix = new Matrix();
        matrix.Translate(-pivot.X, -pivot.Y, MatrixOrder.Append);
        matrix.Rotate(degrees, MatrixOrder.Append);
        matrix.Translate(pivot.X, pivot.Y, MatrixOrder.Append);
        path.Transform(matrix);
    }

    private readonly record struct ExpressionProfile(
        float EyeOpenness,
        float PupilScale,
        float BrowLift,
        float BrowInnerDrop,
        float MouthCurve,
        float EarOutwardDegrees,
        float TailPoseDegrees,
        float EyeHighlightOffsetY,
        float HeadTiltDegrees,
        float HeadOffsetY)
    {
        public static ExpressionProfile For(
            LynxVisualState state,
            float transitionAmount)
        {
            var expression = state switch
            {
                LynxVisualState.Clean => new ExpressionProfile(
                    0.84f, 1.02f,
                    -2.0f, -4.0f,
                    2.6f,
                    5.0f, -3.0f,
                    0.4f,
                    0.0f, 0.8f),

                LynxVisualState.Changes => new ExpressionProfile(
                    1.12f, 1.18f,
                    -3.8f, -5.0f,
                    -2.0f,
                    -4.0f, 5.5f,
                    -0.7f,
                    -2.5f, -0.8f),

                LynxVisualState.Attention => new ExpressionProfile(
                    0.70f, 0.82f,
                    1.4f, 4.2f,
                    -3.4f,
                    -5.5f, 1.0f,
                    0f,
                    0.0f, -1.2f),

                LynxVisualState.Save => new ExpressionProfile(
                    0.80f, 1.04f,
                    -2.2f, -3.7f,
                    3.8f,
                    3.6f, 2.8f,
                    0.5f,
                    2.2f, 0.8f),

                LynxVisualState.Get => new ExpressionProfile(
                    1.16f, 1.26f,
                    -4.4f, -5.8f,
                    0.7f,
                    -5.5f, 7.0f,
                    -0.8f,
                    -4.0f, -1.0f),

                LynxVisualState.Send => new ExpressionProfile(
                    0.82f, 0.96f,
                    -1.0f, -2.0f,
                    3.2f,
                    1.2f, 4.5f,
                    0.2f,
                    2.8f, -0.4f),

                LynxVisualState.Conflict => new ExpressionProfile(
                    0.58f, 0.70f,
                    2.8f, 6.0f,
                    -4.8f,
                    -7.0f, -4.5f,
                    0f,
                    0.0f, -1.8f),

                _ => new ExpressionProfile(
                    0.90f, 1.00f,
                    0f, 0f,
                    -1.4f,
                    0f, 0f,
                    0f,
                    0.0f, 0.0f)
            };

            var boost = Math.Clamp(transitionAmount, 0f, 1f);

            return expression with
            {
                BrowInnerDrop =
                    expression.BrowInnerDrop +
                    (state is LynxVisualState.Attention or LynxVisualState.Conflict
                        ? 1.4f * boost
                        : 0f),

                EarOutwardDegrees =
                    expression.EarOutwardDegrees +
                    (state == LynxVisualState.Get
                        ? -1.6f * boost
                        : state == LynxVisualState.Changes
                            ? -1.0f * boost
                            : 0f),

                MouthCurve =
                    expression.MouthCurve +
                    (state == LynxVisualState.Save
                        ? 0.8f * boost
                        : state == LynxVisualState.Conflict
                            ? -0.7f * boost
                            : 0f)
            };
        }
    }

    private static Color StateAccent(LynxVisualState state, LynxPalette palette)
    {
        var semantic = state switch
        {
            LynxVisualState.Clean => Color.FromArgb(117, 226, 189),
            LynxVisualState.Changes => Color.FromArgb(239, 137, 158),
            LynxVisualState.Attention => Color.FromArgb(255, 110, 127),
            LynxVisualState.Save => Color.FromArgb(117, 226, 189),
            LynxVisualState.Get => Color.FromArgb(114, 200, 255),
            LynxVisualState.Send => Color.FromArgb(170, 150, 255),
            LynxVisualState.Conflict => Color.FromArgb(228, 164, 108),
            _ => palette.Accent
        };

        return Mix(
            semantic,
            palette.Accent,
            state is LynxVisualState.Idle ? 0.0f : 0.34f);
    }

    private static Pen Outline(SilhouetteColors c) =>
        new(
            Color.FromArgb(
                205,
                Mix(Color.FromArgb(19, 15, 27), c.Edge, 0.16f)),
            1.0f)
        {
            LineJoin = LineJoin.Round,
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

    private static GraphicsPath Mirror(GraphicsPath source)
    {
        var clone = (GraphicsPath)source.Clone();
        using var matrix = new Matrix(-1, 0, 0, 1, 160, 0);
        clone.Transform(matrix);
        return clone;
    }

    private static void DrawDebugGeometry(Graphics graphics)
    {
        using var debug = new Pen(Color.FromArgb(165, 240, 189, 97), 1f)
        {
            DashStyle = DashStyle.Dash
        };

        graphics.DrawRectangle(debug, 1, 1, 158, 158);
        graphics.DrawLine(debug, 80, 0, 80, 160);
        graphics.DrawLine(debug, 0, 80, 160, 80);

        using var anchor = new SolidBrush(Color.FromArgb(230, 240, 189, 97));
        graphics.FillEllipse(anchor, 77, 75, 6, 6);
        graphics.FillEllipse(anchor, 77, 100, 6, 6);
        graphics.FillEllipse(anchor, 77, 145, 6, 6);
    }

    private static GraphicsPath Path(params Part[] parts)
    {
        var path = new GraphicsPath();
        PointF? current = null;

        foreach (var part in parts)
        {
            switch (part.Kind)
            {
                case PartKind.Move:
                    current = part.P1;
                    break;

                case PartKind.Line:
                    if (current is null)
                    {
                        current = part.P1;
                        break;
                    }

                    path.AddLine(current.Value, part.P1);
                    current = part.P1;
                    break;

                case PartKind.Cubic:
                    if (current is null)
                    {
                        current = part.P3;
                        break;
                    }

                    path.AddBezier(current.Value, part.P1, part.P2, part.P3);
                    current = part.P3;
                    break;

                case PartKind.Close:
                    path.CloseFigure();
                    current = null;
                    break;
            }
        }

        return path;
    }

    private static Part M(float x, float y) =>
        new(PartKind.Move, new PointF(x, y), default, default);

    private static Part L(float x, float y) =>
        new(PartKind.Line, new PointF(x, y), default, default);

    private static Part C(
        float c1x,
        float c1y,
        float c2x,
        float c2y,
        float x,
        float y) =>
        new(
            PartKind.Cubic,
            new PointF(c1x, c1y),
            new PointF(c2x, c2y),
            new PointF(x, y));

    private static Part Z() =>
        new(PartKind.Close, default, default, default);

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

    private readonly record struct SilhouetteColors(
        Color Tail,
        Color TailAccent,
        Color Body,
        Color BodyAccent,
        Color Limb,
        Color Paw,
        Color Head,
        Color Ear,
        Color EarInner,
        Color Edge)
    {
        public static SilhouetteColors FromPalette(LynxPalette palette)
        {
            // Silhouette pass stays purple regardless of selected mood palette.
            // Palette only adds a restrained undertone so we can judge anatomy
            // without color becoming the design decision.
            return new SilhouetteColors(
                Tail: Mix(Color.FromArgb(27, 16, 48), palette.Fur, 0.12f),
                TailAccent: Mix(Color.FromArgb(92, 48, 171), palette.Accent, 0.24f),
                Body: Mix(Color.FromArgb(48, 27, 80), palette.Fur, 0.12f),
                BodyAccent: Mix(Color.FromArgb(91, 51, 160), palette.Accent, 0.20f),
                Limb: Mix(Color.FromArgb(35, 20, 59), palette.Fur, 0.10f),
                Paw: Mix(Color.FromArgb(25, 14, 43), palette.Fur, 0.08f),
                Head: Mix(Color.FromArgb(111, 58, 203), palette.Accent, 0.18f),
                Ear: Mix(Color.FromArgb(57, 31, 96), palette.Fur, 0.12f),
                EarInner: Mix(Color.FromArgb(177, 125, 248), palette.Eye, 0.22f),
                Edge: Mix(Color.FromArgb(31, 25, 42), palette.Edge, 0.08f));
        }
    }

    private enum PartKind
    {
        Move,
        Line,
        Cubic,
        Close
    }

    private readonly record struct Part(
        PartKind Kind,
        PointF P1,
        PointF P2,
        PointF P3);
}
