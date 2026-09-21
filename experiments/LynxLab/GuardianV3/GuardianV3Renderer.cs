using System.Drawing.Drawing2D;

namespace LynxLab;

internal sealed class GuardianV3Renderer : ILynxRenderer, IAnimatedLynxRenderer
{
    private const float DesignSize = 160f;
    private LynxAnimationFrame _animation = LynxAnimationFrame.Static;

    public string Name => "Guardian V3 layered · animation phase 1";

    public void SetAnimationFrame(LynxAnimationFrame frame) => _animation = frame;

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

            var colors = SilhouetteColors.FromPalette(palette);
            var miniature = Math.Min(bounds.Width, bounds.Height) <= 180;

            DrawGroundReference(graphics, colors);

            var tailSaved = graphics.Save();
            try
            {
                ApplyTailSway(graphics, _animation.TailSwayDegrees);
                DrawTail(graphics, colors);
            }
            finally
            {
                graphics.Restore(tailSaved);
            }

            var bodySaved = graphics.Save();
            try
            {
                ApplyBreathing(graphics, _animation.Breath);

                DrawTorso(graphics, colors);
                DrawHaunches(graphics, colors);
                DrawForelegs(graphics, colors);
                DrawChestFur(graphics);
                DrawCollarArmor(graphics, palette, miniature);
                DrawEars(graphics, colors);
                DrawHead(graphics, colors);
                DrawFace(graphics, palette, miniature, _animation.Blink);
                DrawShield(graphics, palette, miniature, _animation.ShieldPulse);
                DrawRimLighting(graphics, colors, miniature);
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
        g.TranslateTransform(48f, 140f, MatrixOrder.Append);
        g.RotateTransform(degrees, MatrixOrder.Append);
        g.TranslateTransform(-48f, -140f, MatrixOrder.Append);
    }

    private static void ApplyBreathing(Graphics g, float breath)
    {
        // Barely perceptible body expansion plus a tiny upward weight shift.
        var scaleY = 1f + breath * 0.0045f;
        var bob = -breath * 0.38f;

        g.TranslateTransform(0f, bob, MatrixOrder.Append);
        g.TranslateTransform(80f, 145f, MatrixOrder.Append);
        g.ScaleTransform(1f, scaleY, MatrixOrder.Append);
        g.TranslateTransform(-80f, -145f, MatrixOrder.Append);
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

    private static void DrawEars(Graphics g, SilhouetteColors c)
    {
        using var leftEar = Path(
            M(46, 45),
            C(40, 35, 40, 20, 48, 6),
            C(58, 14, 64, 24, 66, 36),
            C(59, 40, 52, 43, 46, 45),
            Z());

        using var rightEar = Mirror(leftEar);
        using var fill = new LinearGradientBrush(new RectangleF(40, 6, 80, 40),
            Mix(c.Ear, c.Head, 0.3f), c.Ear, 90f);
        using var edge = Outline(c);

        g.FillPath(fill, leftEar);
        g.FillPath(fill, rightEar);
        g.DrawPath(edge, leftEar);
        g.DrawPath(edge, rightEar);

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
        using var innerBrush = new SolidBrush(c.EarInner);

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

    private static void DrawFace(Graphics g, LynxPalette palette, bool miniature, float blink)
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
        using var white = new LinearGradientBrush(new RectangleF(45, 57, 70, 39),
            Color.FromArgb(255, 253, 255), Color.FromArgb(222, 211, 237), 90f);
        g.FillPath(white, mask);

        DrawFaceEye(g, palette, miniature, false, blink);
        DrawFaceEye(g, palette, miniature, true, blink);

        using var nose = Path(
            M(75, 72), C(77, 70.5f, 83, 70.5f, 85, 72),
            C(85, 74, 82, 77, 80, 77.5f),
            C(78, 77, 75, 74, 75, 72), Z());
        using var ink = new SolidBrush(Color.FromArgb(27, 14, 43));
        g.FillPath(ink, nose);

        using var mouth = Path(
            M(73, 85), C(75, 83, 78, 81.5f, 80, 81.5f),
            C(82, 81.5f, 85, 83, 87, 85));
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
        bool miniature,
        bool right,
        float blink)
    {
        // Keep the approved vigilant shape, then compress it vertically for a blink.
        using var leftEye = Path(
            M(54, 51), C(58, 51, 64, 53.5f, 68, 56.5f),
            C(67, 63, 64, 66, 60, 65),
            C(56, 64, 53, 59, 54, 51), Z());
        using var eye = right ? Mirror(leftEye) : (GraphicsPath)leftEye.Clone();

        var eyeOpen = Math.Clamp(1f - blink * 0.90f, 0.10f, 1f);

        // Blink around the eye's fixed vertical centre.
        //
        // With MatrixOrder.Append, points are transformed in the order the
        // operations are appended. The previous +pivot / scale / -pivot order
        // moved the eye geometry vertically as it closed, which made the eyes
        // jump outside their sockets and flicker badly in the 160x160 preview.
        //
        // Move the eye centre to the origin first, squash it there, then move
        // it back. The socket therefore stays spatially locked throughout the
        // blink; only its vertical aperture changes.
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
            var irisHeight = 11f * eyeOpen;
            var irisY = 60.5f - irisHeight / 2f;
            var pupilHeight = 9f * eyeOpen;
            var pupilY = 58.5f - pupilHeight / 2f;

            using var iris = new SolidBrush(
                Mix(Color.FromArgb(139, 70, 221), palette.Eye, 0.12f));
            g.FillEllipse(iris, center - 4.5f, irisY, 9f, irisHeight);
            g.FillEllipse(dark, center - 2.1f, pupilY, 4.2f, pupilHeight);

            if (blink < 0.72f)
            {
                var highlightSize = miniature ? 2.5f : 2f;
                g.FillEllipse(
                    Brushes.White,
                    center - 3f,
                    54f + blink * 2.2f,
                    highlightSize,
                    highlightSize * eyeOpen);
            }
        }
        finally
        {
            g.Restore(saved);
        }

        using var leftLid = Path(
            M(53.5f, 50.8f), C(58, 51, 64, 53.5f, 68.5f, 56.5f));
        using var lid = right ? Mirror(leftLid) : (GraphicsPath)leftLid.Clone();
        using var lidPen = new Pen(
            Color.FromArgb(42, 21, 67),
            miniature ? 1.4f : 0.9f)
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
            g.DrawArc(closedPen, center - 7f, 56.2f, 14f, 4.5f, 8f, 164f);
        }
    }

    private static void DrawCollarArmor(Graphics g, LynxPalette palette, bool miniature)
    {
        // The upper edge follows the jaw; the lower edge forms rigid plates.
        using var leftPanel = Path(
            M(57, 90), L(66, 95), L(80, 99), L(80, 108),
            L(65, 106), L(53, 100), L(55, 94), Z());
        using var rightPanel = Mirror(leftPanel);
        using var armor = new LinearGradientBrush(new RectangleF(53, 90, 54, 18),
            Mix(Color.FromArgb(62, 51, 79), palette.Fur, 0.08f), Color.FromArgb(20, 15, 30), 90f);
        using var edge = new Pen(Color.FromArgb(108, 87, 140), miniature ? 1.4f : 0.85f)
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
        using var purple = new SolidBrush(Mix(Color.FromArgb(129, 73, 204), palette.Accent, 0.10f));
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

    private static void DrawShield(Graphics g, LynxPalette palette, bool miniature, float pulse)
    {
        var saved = g.Save();
        try
        {
            // Seat the badge against the collar while retaining the exposed chest ruff.
            g.TranslateTransform(0, -1.5f);
            DrawShieldBadge(g, palette, miniature, pulse);
        }
        finally
        {
            g.Restore(saved);
        }
    }

    private static void DrawShieldBadge(Graphics g, LynxPalette palette, bool miniature, float pulse)
    {
        using var shield = Path(
            M(80, 96), L(91, 101), L(89, 113),
            L(80, 121), L(71, 113), L(69, 101), Z());
        var violet = Mix(Color.FromArgb(161, 102, 235), palette.Accent, 0.10f);
        pulse = Math.Clamp(pulse, 0f, 1f);
        var haloAlpha = 24 + (int)Math.Round(36f * pulse);
        var haloWidth = (miniature ? 3.2f : 2.8f) + 0.45f * pulse;
        using var halo = new Pen(Color.FromArgb(haloAlpha, violet), haloWidth)
        {
            LineJoin = LineJoin.Round
        };
        // A single low-opacity edge accent keeps the emblem legible on white fur.
        g.DrawPath(halo, shield);
        using var fill = new LinearGradientBrush(new RectangleF(69, 96, 22, 25),
            Color.FromArgb(112, 66, 167), Color.FromArgb(48, 23, 83), 65f);
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
        using var innerFill = new LinearGradientBrush(new RectangleF(72, 99, 16, 19),
            Mix(Color.FromArgb(140, 76, 212), palette.Accent, 0.08f), Color.FromArgb(77, 33, 138), 90f);
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

    private static void DrawRimLighting(Graphics g, SilhouetteColors c, bool miniature)
    {
        using var rim = new Pen(Color.FromArgb(miniature ? 115 : 145, c.EarInner),
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

    private static Pen Outline(SilhouetteColors c) =>
        new(c.Edge, 1.8f)
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
                Tail: Mix(Color.FromArgb(27, 16, 48), palette.Fur, 0.08f),
                TailAccent: Mix(Color.FromArgb(92, 48, 171), palette.Accent, 0.10f),
                Body: Mix(Color.FromArgb(48, 27, 80), palette.Fur, 0.08f),
                BodyAccent: Mix(Color.FromArgb(91, 51, 160), palette.Accent, 0.08f),
                Limb: Mix(Color.FromArgb(35, 20, 59), palette.Fur, 0.07f),
                Paw: Mix(Color.FromArgb(25, 14, 43), palette.Fur, 0.05f),
                Head: Mix(Color.FromArgb(111, 58, 203), palette.Accent, 0.08f),
                Ear: Mix(Color.FromArgb(57, 31, 96), palette.Fur, 0.08f),
                EarInner: Mix(Color.FromArgb(177, 125, 248), palette.Eye, 0.08f),
                Edge: Mix(Color.FromArgb(137, 88, 209), palette.Edge, 0.10f));
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
