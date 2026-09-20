using System.Drawing.Drawing2D;

namespace LynxLab;

internal sealed class GuardianV3Renderer : ILynxRenderer
{
    private const float DesignSize = 160f;

    public string Name => "Guardian V3 layered · face pass";

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

            DrawGroundReference(graphics, colors);
            DrawTail(graphics, colors);
            DrawTorso(graphics, colors);
            DrawHaunches(graphics, colors);
            DrawForelegs(graphics, colors);
            DrawChestFur(graphics);
            DrawEars(graphics, colors);
            DrawHead(graphics, colors);
            DrawFace(graphics, palette, Math.Min(bounds.Width, bounds.Height) <= 180);

            if (debugOverlay)
                DrawDebugGeometry(graphics);
        }
        finally
        {
            graphics.Restore(saved);
        }
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

        using var fill = new SolidBrush(c.Tail);
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

        using var accent = new SolidBrush(c.TailAccent);
        g.FillPath(accent, upperTuft);
        g.FillPath(accent, middleSweep);
        g.FillPath(accent, lowerSweep);
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

        using var fill = new SolidBrush(c.Body);
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
        using var fill = new SolidBrush(c.Limb);
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

        using var fill = new SolidBrush(c.Head);
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
        using var fill = new SolidBrush(c.Ear);
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
        using var white = new SolidBrush(Color.FromArgb(243, 239, 251));
        g.FillPath(white, chest);
    }

    private static void DrawFace(Graphics g, LynxPalette palette, bool miniature)
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
        using var white = new SolidBrush(Color.FromArgb(249, 246, 255));
        g.FillPath(white, mask);

        DrawFaceEye(g, palette, miniature, false);
        DrawFaceEye(g, palette, miniature, true);

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

    private static void DrawFaceEye(Graphics g, LynxPalette palette, bool miniature, bool right)
    {
        // A descending upper lid gives vigilance without a separate angry eyebrow.
        using var leftEye = Path(
            M(54, 51), C(58, 51, 64, 53.5f, 68, 56.5f),
            C(67, 63, 64, 66, 60, 65),
            C(56, 64, 53, 59, 54, 51), Z());
        using var eye = right ? Mirror(leftEye) : (GraphicsPath)leftEye.Clone();
        using var dark = new SolidBrush(Color.FromArgb(23, 12, 37));
        g.FillPath(dark, eye);

        var saved = g.Save();
        try
        {
            g.SetClip(eye, CombineMode.Intersect);
            var center = right ? 99f : 61f;
            using var iris = new SolidBrush(Mix(Color.FromArgb(139, 70, 221), palette.Eye, 0.12f));
            g.FillEllipse(iris, center - 4.5f, 55, 9, 11);
            g.FillEllipse(dark, center - 2.1f, 54, 4.2f, 9);
            var highlightSize = miniature ? 2.5f : 2f;
            g.FillEllipse(Brushes.White, center - 3, 54, highlightSize, highlightSize);
        }
        finally
        {
            g.Restore(saved);
        }

        using var leftLid = Path(M(53.5f, 50.8f), C(58, 51, 64, 53.5f, 68.5f, 56.5f));
        using var lid = right ? Mirror(leftLid) : (GraphicsPath)leftLid.Clone();
        using var lidPen = new Pen(Color.FromArgb(42, 21, 67), miniature ? 1.4f : 0.9f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        g.DrawPath(lidPen, lid);
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
