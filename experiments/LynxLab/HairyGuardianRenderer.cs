using System.Drawing.Drawing2D;

namespace LynxLab;

/*
 * Live vector interpretation of the approved "hairy guardian" direction.
 * No source bitmap is used at runtime. Every visible part is drawn as vector
 * geometry so later animation can move eyes, ears, tail, collar and body
 * independently.
 */
internal sealed class HairyGuardianRenderer : ILynxRenderer
{
    public string Name => "Hairy Guardian · live GDI+ vector";

    public void Draw(
        Graphics graphics,
        Rectangle bounds,
        LynxPalette palette,
        LynxVisualState state,
        bool debugOverlay)
    {
        var saved = graphics.Save();
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;

        graphics.TranslateTransform(bounds.Left, bounds.Top);
        graphics.ScaleTransform(bounds.Width / 160f, bounds.Height / 160f);

        var accent = ResolveStateColor(palette, state);
        var colors = GuardianColors.FromPalette(palette, accent);

        DrawGroundGlow(graphics, colors);
        DrawTail(graphics, colors);
        DrawBody(graphics, colors);
        DrawHead(graphics, colors);
        DrawFace(graphics, colors, state);
        DrawCollar(graphics, colors);
        DrawStateSignal(graphics, colors, state);

        if (debugOverlay)
            DrawDebug(graphics);

        graphics.Restore(saved);
    }

    private static void DrawGroundGlow(Graphics g, GuardianColors c)
    {
        using var path = new GraphicsPath();
        path.AddEllipse(23, 143, 114, 10);

        using var brush = new PathGradientBrush(path)
        {
            CenterColor = Color.FromArgb(80, c.Accent),
            SurroundColors = [Color.FromArgb(0, c.Accent)]
        };

        g.FillPath(brush, path);
    }

    private static void DrawTail(Graphics g, GuardianColors c)
    {
        using var tail = Path(
            P(42, 126),
            C(22, 125, 10, 111, 13, 88),
            C(16, 66, 34, 56, 48, 69),
            C(58, 78, 60, 92, 55, 103),
            C(52, 111, 48, 119, 42, 126));

        using var tailBrush = VerticalGradient(
            new RectangleF(10, 55, 55, 75),
            c.Darkest,
            c.BodyDark);

        using var edge = new Pen(c.Edge, 1.8f)
        {
            LineJoin = LineJoin.Round
        };

        g.FillPath(tailBrush, tail);
        g.DrawPath(edge, tail);

        using var tailSweep = Path(
            P(18, 92),
            C(26, 74, 42, 69, 52, 79),
            C(45, 71, 33, 72, 25, 83),
            C(21, 88, 19, 91, 18, 92));

        using var sweepBrush = new SolidBrush(Color.FromArgb(175, c.PurpleMid));
        g.FillPath(sweepBrush, tailSweep);

        using var tailTip = Path(
            P(17, 79),
            C(24, 65, 37, 59, 48, 67),
            C(41, 61, 31, 61, 23, 68),
            C(19, 72, 18, 76, 17, 79));

        using var tipBrush = new SolidBrush(Color.FromArgb(220, c.PurpleLight));
        g.FillPath(tipBrush, tailTip);
    }

    private static void DrawBody(Graphics g, GuardianColors c)
    {
        using var body = Path(
            P(46, 88),
            C(39, 102, 38, 122, 44, 137),
            C(49, 149, 63, 154, 80, 154),
            C(97, 154, 111, 149, 116, 137),
            C(122, 122, 121, 102, 114, 88),
            C(105, 80, 94, 77, 80, 77),
            C(66, 77, 55, 80, 46, 88));

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
            P(47, 92),
            C(55, 89, 63, 89, 70, 93),
            C(63, 100, 58, 111, 58, 124),
            C(51, 119, 46, 108, 47, 92));

        using var rightShoulder = Mirror(leftShoulder, 80);
        using var shoulderBrush = new SolidBrush(Color.FromArgb(150, c.PurpleMid));

        g.FillPath(shoulderBrush, leftShoulder);
        g.FillPath(shoulderBrush, rightShoulder);

        DrawPaw(g, new RectangleF(46, 129, 31, 24), c);
        DrawPaw(g, new RectangleF(83, 129, 31, 24), c);

        using var chest = Path(
            P(64, 91),
            C(68, 100, 73, 109, 80, 121),
            C(87, 109, 92, 100, 96, 91),
            C(91, 88, 86, 86, 80, 86),
            C(74, 86, 69, 88, 64, 91));

        using var chestBrush = VerticalGradient(
            new RectangleF(62, 85, 36, 38),
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
            P(43, 46),
            C(38, 34, 38, 18, 44, 7),
            C(53, 14, 59, 23, 62, 34),
            C(56, 37, 50, 41, 43, 46));

        using var rightEar = Mirror(leftEar, 80);

        using var earBrush = VerticalGradient(
            new RectangleF(37, 6, 47, 42),
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
            P(46, 35),
            C(44, 27, 45, 19, 48, 14),
            C(53, 20, 56, 26, 57, 32),
            C(53, 32, 50, 33, 46, 35));

        using var rightInner = Mirror(leftInner, 80);
        using var innerBrush = new SolidBrush(Color.FromArgb(225, c.PurpleLight));

        g.FillPath(innerBrush, leftInner);
        g.FillPath(innerBrush, rightInner);

        using var head = Path(
            P(42, 38),
            C(47, 25, 60, 18, 80, 18),
            C(100, 18, 113, 25, 118, 38),
            C(125, 46, 127, 59, 124, 72),
            C(122, 81, 116, 90, 106, 96),
            C(98, 101, 89, 104, 80, 104),
            C(71, 104, 62, 101, 54, 96),
            C(44, 90, 38, 81, 36, 72),
            C(33, 59, 35, 46, 42, 38));

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
            P(38, 65),
            C(36, 74, 39, 84, 48, 92),
            L(44, 82),
            L(51, 84),
            L(48, 75),
            L(55, 77),
            C(52, 69, 47, 65, 38, 65));

        using var right = Mirror(left, 80);
        using var brush = new SolidBrush(Color.FromArgb(115, c.PurpleLight));

        g.FillPath(brush, left);
        g.FillPath(brush, right);
    }

    private static void DrawFace(
        Graphics g,
        GuardianColors c,
        LynxVisualState state)
    {
        using var muzzle = Path(
            P(45, 61),
            C(51, 53, 61, 50, 70, 56),
            C(74, 59, 77, 63, 80, 68),
            C(83, 63, 86, 59, 90, 56),
            C(99, 50, 109, 53, 115, 61),
            C(120, 68, 117, 80, 108, 86),
            C(99, 92, 90, 94, 80, 94),
            C(70, 94, 61, 92, 52, 86),
            C(43, 80, 40, 68, 45, 61));

        using var muzzleBrush = VerticalGradient(
            new RectangleF(40, 50, 80, 46),
            Color.FromArgb(255, 255, 255),
            Color.FromArgb(222, 223, 238));

        g.FillPath(muzzleBrush, muzzle);

        var alert = state is LynxVisualState.Attention or LynxVisualState.Conflict;
        DrawEye(g, 60, 58, c, alert, left: true);
        DrawEye(g, 100, 58, c, alert, left: false);

        using var browPen = new Pen(
            Color.FromArgb(alert ? 185 : 95, c.Darkest),
            alert ? 2.2f : 1.5f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        if (alert)
        {
            g.DrawLine(browPen, 52, 49, 66, 53);
            g.DrawLine(browPen, 108, 49, 94, 53);
        }
        else
        {
            g.DrawArc(browPen, 51, 46, 16, 8, 200, 100);
            g.DrawArc(browPen, 93, 46, 16, 8, 240, 100);
        }

        using var nose = new GraphicsPath();
        nose.AddPolygon([Pt(74, 71), Pt(86, 71), Pt(80, 78)]);
        using var noseBrush = new SolidBrush(Color.FromArgb(26, 17, 43));
        g.FillPath(noseBrush, nose);

        using var mouthPen = new Pen(Color.FromArgb(44, 27, 70), 2f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        g.DrawLine(mouthPen, 80, 77, 80, 82);
        g.DrawBezier(mouthPen, Pt(80, 82), Pt(76, 82), Pt(74, 85), Pt(72, 87));
        g.DrawBezier(mouthPen, Pt(80, 82), Pt(84, 82), Pt(86, 85), Pt(88, 87));
    }

    private static void DrawEye(
        Graphics g,
        float x,
        float y,
        GuardianColors c,
        bool alert,
        bool left)
    {
        var width = alert ? 17f : 16f;
        var height = alert ? 18f : 19f;
        var rect = new RectangleF(x - width / 2f, y - height / 2f, width, height);

        using var eye = new GraphicsPath();
        eye.AddEllipse(rect);

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
            P(57, 94),
            C(65, 99, 72, 102, 80, 103),
            C(88, 102, 95, 99, 103, 94),
            L(108, 100),
            C(99, 107, 90, 111, 80, 112),
            C(70, 111, 61, 107, 52, 100),
            Z());

        using var collarBrush = VerticalGradient(
            new RectangleF(52, 94, 56, 18),
            Color.FromArgb(45, 42, 58),
            Color.FromArgb(11, 12, 18));

        using var collarEdge = new Pen(Color.FromArgb(150, c.PurpleLight), 1.2f);

        g.FillPath(collarBrush, collar);
        g.DrawPath(collarEdge, collar);

        using var shield = Path(
            P(80, 99),
            L(91, 105),
            L(89, 119),
            L(80, 127),
            L(71, 119),
            L(69, 105),
            Z());

        using var shieldBrush = VerticalGradient(
            new RectangleF(68, 99, 24, 29),
            c.PurpleBright,
            c.Darkest);

        using var shieldEdge = new Pen(c.Accent, 1.7f)
        {
            LineJoin = LineJoin.Round
        };

        g.FillPath(shieldBrush, shield);
        g.DrawPath(shieldEdge, shield);

        using var glow = new Pen(Color.FromArgb(105, c.Accent), 4f)
        {
            LineJoin = LineJoin.Round
        };
        g.DrawPath(glow, shield);
        g.DrawPath(shieldEdge, shield);

        using var checkPen = new Pen(Color.White, 2.8f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        g.DrawLines(checkPen, [Pt(74.5f, 113), Pt(78.7f, 117), Pt(86, 108.5f)]);
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
