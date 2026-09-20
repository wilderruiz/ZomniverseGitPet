using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace LynxLab;

/*
 * Live vector interpretation of the approved "hairy guardian" direction.
 * No source bitmap is used at runtime. Every visible part is drawn as vector
 * geometry so later animation can move eyes, ears, tail, collar and body
 * independently.
 */
internal sealed class HairyGuardianRenderer : ILynxRenderer
{
    private const float DesignSize = 160f;
    private const int MiniatureThreshold = 180;
    private const int MiniatureOversample = 3;

    public string Name => "Hairy Guardian · live GDI+ vector";

    public void Draw(
        Graphics graphics,
        Rectangle bounds,
        LynxPalette palette,
        LynxVisualState state,
        bool debugOverlay)
    {
        if (Math.Min(bounds.Width, bounds.Height) <= MiniatureThreshold)
        {
            DrawOversampledMiniature(graphics, bounds, palette, state, debugOverlay);
            return;
        }

        DrawDetailed(graphics, bounds, palette, state, debugOverlay);
    }

    private static void DrawDetailed(
        Graphics graphics,
        Rectangle bounds,
        LynxPalette palette,
        LynxVisualState state,
        bool debugOverlay)
    {
        var saved = graphics.Save();
        ConfigureQuality(graphics);

        graphics.TranslateTransform(bounds.Left, bounds.Top);
        graphics.ScaleTransform(bounds.Width / DesignSize, bounds.Height / DesignSize);

        var accent = ResolveStateColor(palette, state);
        var colors = GuardianColors.FromPalette(palette, accent);

        try
        {
            DrawGroundGlow(graphics, colors);
            DrawTail(graphics, colors);
            DrawBody(graphics, colors);
            DrawHead(graphics, colors);
            DrawFace(graphics, colors, state);
            DrawCollar(graphics, colors);
            DrawStateSignal(graphics, colors, state);

            if (debugOverlay)
                DrawDebug(graphics);
        }
        finally
        {
            graphics.Restore(saved);
        }
    }

    private static void DrawOversampledMiniature(
        Graphics graphics,
        Rectangle bounds,
        LynxPalette palette,
        LynxVisualState state,
        bool debugOverlay)
    {
        var width = Math.Max(1, bounds.Width * MiniatureOversample);
        var height = Math.Max(1, bounds.Height * MiniatureOversample);

        using var surface = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        using (var miniatureGraphics = Graphics.FromImage(surface))
        {
            miniatureGraphics.Clear(Color.Transparent);
            ConfigureQuality(miniatureGraphics);
            miniatureGraphics.ScaleTransform(width / DesignSize, height / DesignSize);

            var accent = ResolveStateColor(palette, state);
            var colors = GuardianColors.FromPalette(palette, accent);

            DrawGroundGlow(miniatureGraphics, colors);
            DrawMiniatureGuardian(miniatureGraphics, colors, state);
            DrawStateSignal(miniatureGraphics, colors, state);

            if (debugOverlay)
                DrawDebug(miniatureGraphics);
        }

        var saved = graphics.Save();
        try
        {
            ConfigureQuality(graphics);
            graphics.CompositingMode = CompositingMode.SourceOver;
            graphics.DrawImage(surface, bounds);
        }
        finally
        {
            graphics.Restore(saved);
        }
    }

    private static void ConfigureQuality(Graphics graphics)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
    }

    private static void DrawGroundGlow(Graphics g, GuardianColors c)
    {
        // Keep this deliberately simple. PathGradientBrush is surprisingly fragile
        // across GDI+ configurations and previously caused the lab to terminate
        // during the first paint on some Windows machines.
        using var outer = new SolidBrush(Color.FromArgb(18, c.Accent));
        using var middle = new SolidBrush(Color.FromArgb(28, c.Accent));
        using var inner = new SolidBrush(Color.FromArgb(40, c.Accent));

        g.FillEllipse(outer, 23, 143, 114, 10);
        g.FillEllipse(middle, 35, 145, 90, 6);
        g.FillEllipse(inner, 52, 146, 56, 4);
    }

    private static void DrawTail(Graphics g, GuardianColors c)
    {
        using var tail = Path(
            P(48, 144),
            C(27, 146, 8, 132, 7, 108),
            C(5, 89, 13, 73, 25, 66),
            L(16, 64),
            C(25, 55, 39, 53, 50, 61),
            C(64, 72, 66, 92, 58, 107),
            L(61, 116),
            C(56, 130, 53, 139, 48, 144),
            Z());

        using var tailBrush = VerticalGradient(
            new RectangleF(6, 53, 60, 94),
            c.Darkest,
            c.BodyDark);

        using var edge = new Pen(c.Edge, 1.8f)
        {
            LineJoin = LineJoin.Round
        };

        g.FillPath(tailBrush, tail);
        g.DrawPath(edge, tail);

        using var tailSweep = Path(
            P(12, 109),
            C(18, 89, 31, 74, 50, 70),
            C(39, 70, 29, 78, 23, 91),
            C(18, 100, 15, 106, 12, 109),
            Z());

        using var sweepBrush = new SolidBrush(Color.FromArgb(175, c.PurpleMid));
        g.FillPath(sweepBrush, tailSweep);

        using var tailTip = Path(
            P(18, 77),
            C(28, 60, 42, 57, 52, 66),
            C(41, 61, 31, 65, 24, 74),
            L(16, 88),
            L(20, 76),
            Z());

        using var tipBrush = new SolidBrush(Color.FromArgb(220, c.PurpleLight));
        g.FillPath(tipBrush, tailTip);

        using var lowerSweep = Path(
            P(16, 120),
            C(24, 129, 36, 136, 49, 134),
            L(42, 140),
            C(29, 139, 20, 132, 16, 120),
            Z());
        using var lowerBrush = new SolidBrush(Color.FromArgb(115, c.PurpleMid));
        g.FillPath(lowerBrush, lowerSweep);
    }

    private static void DrawBody(Graphics g, GuardianColors c)
    {
        using var body = Path(
            P(49, 87),
            C(39, 101, 35, 123, 42, 141),
            C(49, 154, 63, 157, 80, 157),
            C(97, 157, 111, 154, 118, 141),
            C(125, 123, 121, 101, 111, 87),
            C(101, 79, 93, 77, 80, 77),
            C(67, 77, 59, 79, 49, 87),
            Z());

        using var bodyBrush = VerticalGradient(
            new RectangleF(38, 77, 84, 78),
            c.Body,
            c.Darkest);

        using var bodyEdge = new Pen(c.Edge, 1.8f)
        {
            LineJoin = LineJoin.Round
        };

        g.FillPath(bodyBrush, body);
        g.DrawPath(bodyEdge, body);

        using var leftShoulder = Path(
            P(47, 94),
            C(55, 89, 65, 89, 71, 95),
            C(63, 104, 58, 119, 59, 136),
            L(51, 126),
            L(54, 114),
            L(47, 119),
            C(44, 110, 44, 101, 47, 94),
            Z());

        using var rightShoulder = Mirror(leftShoulder, 80);
        using var shoulderBrush = new SolidBrush(Color.FromArgb(150, c.PurpleMid));

        g.FillPath(shoulderBrush, leftShoulder);
        g.FillPath(shoulderBrush, rightShoulder);

        DrawPaw(g, new RectangleF(43, 132, 35, 23), c);
        DrawPaw(g, new RectangleF(82, 132, 35, 23), c);

        using var chest = Path(
            P(59, 91),
            L(67, 99),
            L(64, 105),
            L(72, 108),
            L(68, 116),
            L(76, 118),
            L(80, 142),
            L(84, 118),
            L(92, 116),
            L(88, 108),
            L(96, 105),
            L(93, 99),
            L(101, 91),
            C(93, 87, 87, 85, 80, 85),
            C(73, 85, 67, 87, 59, 91),
            Z());

        using var chestBrush = VerticalGradient(
            new RectangleF(58, 85, 44, 58),
            Color.FromArgb(252, 252, 255),
            Color.FromArgb(210, 213, 230));

        g.FillPath(chestBrush, chest);
    }

    private static void DrawPaw(Graphics g, RectangleF rect, GuardianColors c)
    {
        using var paw = new GraphicsPath();
        paw.AddEllipse(rect);

        using var pawBrush = VerticalGradient(rect, c.BodyDark, c.Darkest);
        using var edge = new Pen(Color.FromArgb(170, c.Edge), 1.2f);

        g.FillPath(pawBrush, paw);
        g.DrawPath(edge, paw);

        using var toePen = new Pen(Color.FromArgb(100, c.PurpleLight), 1.1f);
        for (var i = 1; i <= 3; i++)
        {
            var x = rect.Left + rect.Width * (i / 4f);
            g.DrawArc(
                toePen,
                x - 3,
                rect.Top + rect.Height * 0.47f,
                6,
                rect.Height * 0.36f,
                200,
                140);
        }
    }

    private static void DrawHead(Graphics g, GuardianColors c)
    {
        using var leftEar = Path(
            P(43, 49),
            C(36, 38, 36, 20, 45, 4),
            C(57, 13, 64, 24, 67, 38),
            C(58, 41, 50, 45, 43, 49),
            Z());

        using var rightEar = Mirror(leftEar, 80);

        using var earBrush = VerticalGradient(
            new RectangleF(35, 3, 52, 47),
            c.PurpleMid,
            c.BodyDark);

        using var earEdge = new Pen(c.Edge, 1.8f)
        {
            LineJoin = LineJoin.Round
        };

        g.FillPath(earBrush, leftEar);
        g.FillPath(earBrush, rightEar);
        g.DrawPath(earEdge, leftEar);
        g.DrawPath(earEdge, rightEar);

        using var leftInner = Path(
            P(44, 39),
            C(42, 28, 45, 17, 48, 11),
            C(55, 18, 59, 27, 61, 36),
            L(55, 31),
            L(57, 39),
            L(50, 34),
            L(48, 41),
            Z());

        using var rightInner = Mirror(leftInner, 80);
        using var innerBrush = new SolidBrush(Color.FromArgb(225, c.PurpleLight));

        g.FillPath(innerBrush, leftInner);
        g.FillPath(innerBrush, rightInner);

        using var head = Path(
            P(42, 39),
            C(49, 27, 59, 22, 70, 19),
            L(68, 15),
            L(79, 18),
            L(88, 14),
            L(86, 19),
            C(102, 21, 112, 27, 118, 39),
            C(124, 48, 126, 59, 123, 70),
            L(128, 76),
            L(119, 77),
            L(123, 84),
            L(113, 83),
            L(114, 91),
            C(104, 100, 92, 104, 80, 104),
            C(68, 104, 56, 100, 46, 91),
            L(47, 83),
            L(37, 84),
            L(41, 77),
            L(32, 76),
            L(37, 70),
            C(34, 59, 36, 48, 42, 39),
            Z());

        using var headBrush = VerticalGradient(
            new RectangleF(34, 18, 92, 88),
            c.PurpleBright,
            c.Body);

        using var headEdge = new Pen(c.Edge, 2f)
        {
            LineJoin = LineJoin.Round
        };

        g.FillPath(headBrush, head);
        g.DrawPath(headEdge, head);

        DrawCheekTufts(g, c);
    }

    private static void DrawCheekTufts(Graphics g, GuardianColors c)
    {
        using var left = Path(
            P(37, 63),
            C(35, 71, 37, 82, 47, 91),
            L(43, 82),
            L(53, 85),
            L(48, 76),
            L(57, 78),
            C(53, 68, 47, 64, 37, 63),
            Z());

        using var right = Mirror(left, 80);
        using var brush = new SolidBrush(Color.FromArgb(155, c.PurpleLight));

        g.FillPath(brush, left);
        g.FillPath(brush, right);
    }

    private static void DrawFace(
        Graphics g,
        GuardianColors c,
        LynxVisualState state)
    {
        using var muzzle = Path(
            P(43, 63),
            C(49, 54, 61, 51, 70, 56),
            C(75, 59, 78, 63, 80, 68),
            C(82, 63, 85, 59, 90, 56),
            C(99, 51, 111, 54, 117, 63),
            C(121, 72, 116, 83, 106, 89),
            C(98, 94, 89, 96, 80, 96),
            C(71, 96, 62, 94, 54, 89),
            C(44, 83, 39, 72, 43, 63),
            Z());

        using var muzzleBrush = VerticalGradient(
            new RectangleF(40, 50, 80, 46),
            Color.FromArgb(255, 255, 255),
            Color.FromArgb(222, 223, 238));

        g.FillPath(muzzleBrush, muzzle);

        var alert = state is LynxVisualState.Attention or LynxVisualState.Conflict;
        DrawEye(g, 59, 57, c, alert, left: true);
        DrawEye(g, 101, 57, c, alert, left: false);

        using var browPen = new Pen(
            Color.FromArgb(alert ? 185 : 95, c.Darkest),
            alert ? 2.2f : 1.5f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        if (alert)
        {
            g.DrawLine(browPen, 48, 47, 67, 53);
            g.DrawLine(browPen, 112, 47, 93, 53);
        }
        else
        {
            g.DrawLine(browPen, 49, 48, 67, 53);
            g.DrawLine(browPen, 111, 48, 93, 53);
        }

        using var nose = new GraphicsPath();
        nose.AddBezier(Pt(73, 72), Pt(76, 69), Pt(84, 69), Pt(87, 72));
        nose.AddBezier(Pt(87, 72), Pt(86, 77), Pt(82, 79), Pt(80, 79));
        nose.AddBezier(Pt(80, 79), Pt(78, 79), Pt(74, 77), Pt(73, 72));
        nose.CloseFigure();
        using var noseBrush = new SolidBrush(Color.FromArgb(26, 17, 43));
        g.FillPath(noseBrush, nose);

        using var mouthPen = new Pen(Color.FromArgb(44, 27, 70), 2f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        g.DrawLine(mouthPen, 80, 78, 80, 84);
        g.DrawBezier(mouthPen, Pt(80, 84), Pt(76, 84), Pt(72, 87), Pt(69, 89));
        g.DrawBezier(mouthPen, Pt(80, 84), Pt(84, 84), Pt(88, 87), Pt(91, 89));
    }

    private static void DrawEye(
        Graphics g,
        float x,
        float y,
        GuardianColors c,
        bool alert,
        bool left)
    {
        var width = alert ? 19f : 18f;
        var height = alert ? 19f : 20f;
        var rect = new RectangleF(x - width / 2f, y - height / 2f, width, height);

        using var eye = new GraphicsPath();
        if (left)
        {
            eye.AddBezier(Pt(rect.Left, y - 5), Pt(x - 3, y - 12), Pt(x + 7, y - 9), Pt(rect.Right, y - 2));
            eye.AddBezier(Pt(rect.Right, y - 2), Pt(x + 8, y + 8), Pt(x + 1, rect.Bottom), Pt(x - 3, rect.Bottom - 1));
            eye.AddBezier(Pt(x - 3, rect.Bottom - 1), Pt(x - 9, y + 5), Pt(x - 10, y), Pt(rect.Left, y - 5));
        }
        else
        {
            eye.AddBezier(Pt(rect.Right, y - 5), Pt(x + 3, y - 12), Pt(x - 7, y - 9), Pt(rect.Left, y - 2));
            eye.AddBezier(Pt(rect.Left, y - 2), Pt(x - 8, y + 8), Pt(x - 1, rect.Bottom), Pt(x + 3, rect.Bottom - 1));
            eye.AddBezier(Pt(x + 3, rect.Bottom - 1), Pt(x + 9, y + 5), Pt(x + 10, y), Pt(rect.Right, y - 5));
        }
        eye.CloseFigure();

        using var eyeBrush = VerticalGradient(
            rect,
            Color.FromArgb(33, 19, 58),
            Color.FromArgb(5, 7, 16));

        using var rim = new Pen(Color.FromArgb(165, c.Accent), 1.2f);
        g.FillPath(eyeBrush, eye);
        g.DrawPath(rim, eye);

        using var irisBrush = new SolidBrush(Color.FromArgb(150, c.Accent));
        g.FillEllipse(
            irisBrush,
            x - 4.2f,
            y + 1.5f,
            8.4f,
            5.7f);

        using var highlight = new SolidBrush(Color.White);
        g.FillEllipse(
            highlight,
            x + (left ? -2.2f : -1.3f),
            y - 5.1f,
            3.2f,
            3.2f);
    }

    private static void DrawCollar(Graphics g, GuardianColors c)
    {
        using var collar = Path(
            P(48, 92),
            L(61, 98),
            L(68, 96),
            L(80, 102),
            L(92, 96),
            L(99, 98),
            L(112, 92),
            L(116, 101),
            L(102, 111),
            L(92, 108),
            L(80, 114),
            L(68, 108),
            L(58, 111),
            L(44, 101),
            Z());

        using var collarBrush = VerticalGradient(
            new RectangleF(44, 92, 72, 23),
            Color.FromArgb(45, 42, 58),
            Color.FromArgb(11, 12, 18));

        using var collarEdge = new Pen(Color.FromArgb(210, c.PurpleLight), 1.45f)
        {
            LineJoin = LineJoin.Bevel
        };

        g.FillPath(collarBrush, collar);
        g.DrawPath(collarEdge, collar);

        using var leftFacet = Path(P(47, 98), L(60, 101), L(67, 99), L(60, 108), Z());
        using var rightFacet = Mirror(leftFacet, 80);
        using var facetBrush = new SolidBrush(Color.FromArgb(195, c.PurpleBright));
        g.FillPath(facetBrush, leftFacet);
        g.FillPath(facetBrush, rightFacet);

        using var shield = Path(
            P(80, 99),
            L(94, 106),
            L(91, 124),
            L(80, 133),
            L(69, 124),
            L(66, 106),
            Z());

        using var shieldBrush = VerticalGradient(
            new RectangleF(65, 99, 30, 35),
            c.PurpleBright,
            c.Darkest);

        using var shieldEdge = new Pen(c.PurpleLight, 2.1f)
        {
            LineJoin = LineJoin.Round
        };

        g.FillPath(shieldBrush, shield);
        g.DrawPath(shieldEdge, shield);

        using var glow = new Pen(Color.FromArgb(105, c.Accent), 5f)
        {
            LineJoin = LineJoin.Round
        };
        g.DrawPath(glow, shield);
        g.DrawPath(shieldEdge, shield);

        using var innerShield = Path(
            P(80, 104),
            L(89, 109),
            L(87, 121),
            L(80, 127),
            L(73, 121),
            L(71, 109),
            Z());
        using var innerShieldPen = new Pen(Color.FromArgb(205, c.Accent), 1.2f)
        {
            LineJoin = LineJoin.Round
        };
        g.DrawPath(innerShieldPen, innerShield);

        using var checkPen = new Pen(Color.White, 3.5f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        g.DrawLines(checkPen, [Pt(73.8f, 115), Pt(79, 120), Pt(87.3f, 110.5f)]);
    }

    private static void DrawMiniatureGuardian(
        Graphics g,
        GuardianColors c,
        LynxVisualState state)
    {
        using var tail = Path(
            P(50, 145),
            C(22, 146, 7, 128, 9, 104),
            C(10, 78, 28, 57, 49, 64),
            C(66, 73, 66, 98, 56, 116),
            C(54, 130, 52, 139, 50, 145),
            Z());
        using var tailBrush = VerticalGradient(new RectangleF(7, 56, 60, 92), c.BodyDark, c.Darkest);
        using var outline = new Pen(c.Edge, 2.3f) { LineJoin = LineJoin.Round };
        g.FillPath(tailBrush, tail);
        g.DrawPath(outline, tail);

        using var tailFlash = Path(P(14, 102), C(22, 76, 39, 65, 53, 73), C(37, 71, 24, 84, 14, 102), Z());
        using var flashBrush = new SolidBrush(Color.FromArgb(210, c.PurpleBright));
        g.FillPath(flashBrush, tailFlash);

        DrawBody(g, c);
        DrawHead(g, c);
        DrawFace(g, c, state);
        DrawCollar(g, c);
    }

    private static void DrawStateSignal(
        Graphics g,
        GuardianColors c,
        LynxVisualState state)
    {
        using var signal = new Pen(Color.FromArgb(220, c.Accent), 1.7f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        if (state == LynxVisualState.Get)
        {
            g.DrawLine(signal, 80, 28, 80, 37);
            g.DrawLine(signal, 80, 37, 76, 33);
            g.DrawLine(signal, 80, 37, 84, 33);
        }
        else if (state == LynxVisualState.Send)
        {
            g.DrawLine(signal, 80, 37, 80, 28);
            g.DrawLine(signal, 80, 28, 76, 32);
            g.DrawLine(signal, 80, 28, 84, 32);
        }
        else if (state == LynxVisualState.Save)
        {
            using var savePen = new Pen(Color.FromArgb(230, c.Accent), 2f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            g.DrawLines(savePen, [Pt(70, 33), Pt(76, 39), Pt(90, 24)]);
        }
        else if (state == LynxVisualState.Conflict)
        {
            using var warnBrush = new SolidBrush(Color.FromArgb(225, c.Accent));
            g.FillPolygon(warnBrush, [Pt(80, 27), Pt(73, 40), Pt(87, 40)]);
            using var markPen = new Pen(Color.FromArgb(30, 20, 15), 1.4f);
            g.DrawLine(markPen, 80, 31, 80, 36);
            g.FillEllipse(Brushes.Black, 79.2f, 37.2f, 1.6f, 1.6f);
        }
    }

    private static void DrawDebug(Graphics g)
    {
        using var debugPen = new Pen(Color.FromArgb(150, 240, 189, 97), 1f)
        {
            DashStyle = DashStyle.Dash
        };

        g.DrawRectangle(debugPen, 1, 1, 158, 158);
        g.DrawLine(debugPen, 80, 0, 80, 160);
        g.DrawLine(debugPen, 0, 80, 160, 80);

        using var anchorBrush = new SolidBrush(Color.FromArgb(230, 240, 189, 97));
        g.FillEllipse(anchorBrush, 77, 100, 6, 6);
    }

    private static LinearGradientBrush VerticalGradient(
        RectangleF bounds,
        Color top,
        Color bottom) =>
        new(bounds, top, bottom, LinearGradientMode.Vertical);

    private static GraphicsPath Mirror(GraphicsPath source, float axisX)
    {
        var clone = (GraphicsPath)source.Clone();
        using var matrix = new Matrix(-1, 0, 0, 1, axisX * 2f, 0);
        clone.Transform(matrix);
        return clone;
    }

    private static GraphicsPath Path(params PathPart[] parts)
    {
        var path = new GraphicsPath();
        PointF? current = null;

        foreach (var part in parts)
        {
            switch (part.Kind)
            {
                case PathPartKind.Move:
                    current = part.P1;
                    break;

                case PathPartKind.Line:
                    if (current is null)
                    {
                        current = part.P1;
                        break;
                    }

                    path.AddLine(current.Value, part.P1);
                    current = part.P1;
                    break;

                case PathPartKind.Cubic:
                    if (current is null)
                    {
                        current = part.P1;
                        break;
                    }

                    path.AddBezier(current.Value, part.P1, part.P2, part.P3);
                    current = part.P3;
                    break;

                case PathPartKind.Close:
                    path.CloseFigure();
                    current = null;
                    break;
            }
        }

        return path;
    }

    private static PointF Pt(float x, float y) => new(x, y);

    private static PathPart P(float x, float y) =>
        new(PathPartKind.Move, new PointF(x, y), default, default);

    private static PathPart L(float x, float y) =>
        new(PathPartKind.Line, new PointF(x, y), default, default);

    private static PathPart C(
        float c1x,
        float c1y,
        float c2x,
        float c2y,
        float x,
        float y) =>
        new(
            PathPartKind.Cubic,
            new PointF(c1x, c1y),
            new PointF(c2x, c2y),
            new PointF(x, y));

    private static PathPart Z() =>
        new(PathPartKind.Close, default, default, default);

    private static Color ResolveStateColor(
        LynxPalette palette,
        LynxVisualState state) =>
        state switch
        {
            LynxVisualState.Clean => Color.FromArgb(0x57, 0xD7, 0xA0),
            LynxVisualState.Changes => palette.Eye,
            LynxVisualState.Attention => Color.FromArgb(0xF2, 0x75, 0x86),
            LynxVisualState.Save => Color.FromArgb(0x57, 0xD7, 0xA0),
            LynxVisualState.Get => Color.FromArgb(0x78, 0xA9, 0xFF),
            LynxVisualState.Send => Color.FromArgb(0xA7, 0x8B, 0xFA),
            LynxVisualState.Conflict => Color.FromArgb(0xF0, 0xBD, 0x61),
            _ => palette.Accent
        };

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

    private readonly record struct GuardianColors(
        Color Darkest,
        Color BodyDark,
        Color Body,
        Color PurpleMid,
        Color PurpleBright,
        Color PurpleLight,
        Color Edge,
        Color Accent)
    {
        public static GuardianColors FromPalette(
            LynxPalette palette,
            Color accent)
        {
            // Preserve the approved dark-purple identity. Palette selection changes
            // undertones and state accents rather than turning the guardian into a
            // completely different-colored animal.
            return new GuardianColors(
                Darkest: Mix(Color.FromArgb(18, 11, 31), palette.Fur, 0.18f),
                BodyDark: Mix(Color.FromArgb(38, 24, 61), palette.Fur, 0.22f),
                Body: Mix(Color.FromArgb(66, 39, 103), palette.Fur, 0.18f),
                PurpleMid: Mix(Color.FromArgb(101, 57, 177), palette.Accent, 0.16f),
                PurpleBright: Mix(Color.FromArgb(124, 70, 220), palette.Accent, 0.14f),
                PurpleLight: Mix(Color.FromArgb(181, 134, 255), palette.Eye, 0.14f),
                Edge: Mix(Color.FromArgb(126, 82, 191), palette.Edge, 0.22f),
                Accent: accent);
        }
    }

    private enum PathPartKind
    {
        Move,
        Line,
        Cubic,
        Close
    }

    private readonly record struct PathPart(
        PathPartKind Kind,
        PointF P1,
        PointF P2,
        PointF P3);
}
