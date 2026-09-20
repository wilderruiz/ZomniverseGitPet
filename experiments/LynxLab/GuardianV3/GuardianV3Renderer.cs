using System.Drawing.Drawing2D;

namespace LynxLab;

internal sealed class GuardianV3Renderer : ILynxRenderer
{
    private const float DesignSize = 160f;

    public string Name => "Guardian V3 layered · silhouette pass";

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
            DrawForelegs(graphics, colors);
            DrawHead(graphics, colors);
            DrawEars(graphics, colors);

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
        // The approved mascot's tail is a major identity feature: tall, heavy,
        // fluffy and clearly visible behind the left side of the body.
        using var tail = Path(
            M(50, 142),
            C(32, 148, 16, 140, 10, 126),
            C(2, 108, 5, 89, 16, 74),
            C(25, 62, 37, 56, 47, 59),
            L(42, 51),
            C(55, 54, 65, 66, 65, 81),
            C(66, 98, 59, 116, 50, 142),
            Z());

        using var fill = new SolidBrush(c.Tail);
        using var edge = Outline(c);

        g.FillPath(fill, tail);
        g.DrawPath(edge, tail);

        // Outer fur breaks keep the silhouette from reading as a smooth crescent.
        using var upperTuft = Path(
            M(16, 78),
            L(10, 74),
            L(20, 70),
            L(16, 64),
            C(26, 58, 37, 56, 47, 59),
            C(35, 60, 26, 67, 21, 76),
            Z());

        using var midTuft = Path(
            M(12, 105),
            L(18, 96),
            L(15, 91),
            L(25, 87),
            L(21, 81),
            C(32, 72, 43, 68, 52, 72),
            C(35, 76, 22, 88, 12, 105),
            Z());

        using var tuftBrush = new SolidBrush(c.TailAccent);
        g.FillPath(tuftBrush, upperTuft);
        g.FillPath(tuftBrush, midTuft);
    }

    private static void DrawTorso(Graphics g, SilhouetteColors c)
    {
        // Narrower shoulders and a longer torso than V2: the reference feels
        // like a sitting fox/guardian rather than a round plush toy.
        using var torso = Path(
            M(55, 86),
            C(49, 93, 45, 104, 44, 118),
            C(43, 133, 47, 144, 57, 151),
            C(64, 156, 73, 158, 80, 158),
            C(87, 158, 96, 156, 103, 151),
            C(113, 144, 117, 133, 116, 118),
            C(115, 104, 111, 93, 105, 86),
            C(98, 80, 90, 78, 80, 78),
            C(70, 78, 62, 80, 55, 86),
            Z());

        using var fill = new SolidBrush(c.Body);
        using var edge = Outline(c);

        g.FillPath(fill, torso);
        g.DrawPath(edge, torso);

        // Shoulder fur gives a visible break between head and forelegs.
        using var leftShoulder = Path(
            M(53, 91),
            L(61, 96),
            L(58, 101),
            L(66, 105),
            L(61, 110),
            L(67, 115),
            C(59, 115, 53, 108, 51, 100),
            Z());

        using var rightShoulder = Mirror(leftShoulder);
        using var shoulderBrush = new SolidBrush(c.BodyAccent);

        g.FillPath(shoulderBrush, leftShoulder);
        g.FillPath(shoulderBrush, rightShoulder);
    }

    private static void DrawForelegs(Graphics g, SilhouetteColors c)
    {
        // Separate long forelegs are important. V2 visually merged the whole
        // lower half into one oval body.
        DrawForeleg(g, left: true, c);
        DrawForeleg(g, left: false, c);
    }

    private static void DrawForeleg(Graphics g, bool left, SilhouetteColors c)
    {
        using var leg = Path(
            M(58, 101),
            C(54, 112, 53, 126, 55, 139),
            C(57, 148, 62, 153, 68, 154),
            C(72, 154, 74, 151, 74, 147),
            C(72, 133, 72, 118, 75, 105),
            C(70, 100, 64, 99, 58, 101),
            Z());

        using var actual = left ? leg : Mirror(leg);
        using var fill = new SolidBrush(c.Limb);
        using var edge = Outline(c);

        g.FillPath(fill, actual);
        g.DrawPath(edge, actual);

        // Compact paws replace V2's huge horizontal ovals.
        var pawRect = left
            ? new RectangleF(52, 143, 24, 13)
            : new RectangleF(84, 143, 24, 13);

        using var paw = new GraphicsPath();
        paw.AddEllipse(pawRect);
        using var pawFill = new SolidBrush(c.Paw);

        g.FillPath(pawFill, paw);
        g.DrawPath(edge, paw);
    }

    private static void DrawHead(Graphics g, SilhouetteColors c)
    {
        // Broad temples, tapered jaw, explicit cheek fur and a small crown tuft.
        // This should be judged against the approved reference before facial
        // features are added.
        using var head = Path(
            M(43, 38),
            C(50, 28, 60, 22, 70, 19),
            L(68, 15),
            L(77, 18),
            L(86, 14),
            L(84, 19),
            C(101, 21, 112, 28, 118, 39),
            C(124, 48, 125, 59, 122, 69),

            // right cheek tufts
            L(128, 75),
            L(119, 76),
            L(124, 82),
            L(114, 81),
            L(117, 88),
            L(108, 87),

            C(101, 96, 91, 101, 80, 103),
            C(69, 101, 59, 96, 52, 88),

            // left cheek tufts
            L(43, 87),
            L(46, 81),
            L(36, 82),
            L(41, 76),
            L(32, 75),
            L(38, 69),

            C(35, 58, 36, 48, 43, 38),
            Z());

        using var fill = new SolidBrush(c.Head);
        using var edge = Outline(c);

        g.FillPath(fill, head);
        g.DrawPath(edge, head);
    }

    private static void DrawEars(Graphics g, SilhouetteColors c)
    {
        using var leftEar = Path(
            M(44, 48),
            C(37, 38, 37, 20, 46, 4),
            C(57, 13, 64, 24, 67, 38),
            C(59, 42, 51, 46, 44, 48),
            Z());

        using var rightEar = Mirror(leftEar);
        using var fill = new SolidBrush(c.Ear);
        using var edge = Outline(c);

        g.FillPath(fill, leftEar);
        g.FillPath(fill, rightEar);
        g.DrawPath(edge, leftEar);
        g.DrawPath(edge, rightEar);

        using var leftInner = Path(
            M(46, 39),
            C(44, 29, 46, 18, 49, 11),
            C(56, 18, 60, 28, 61, 36),
            L(56, 32),
            L(57, 39),
            L(51, 34),
            L(48, 41),
            Z());

        using var rightInner = Mirror(leftInner);
        using var innerBrush = new SolidBrush(c.EarInner);

        g.FillPath(innerBrush, leftInner);
        g.FillPath(innerBrush, rightInner);
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
