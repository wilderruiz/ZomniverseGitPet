using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace LynxLab;

/*
 * Live vector interpretation of the approved furry purple guardian.
 * The mascot remains fully drawable geometry: head, ears, eyes, cheek fur,
 * tail, body, collar and badge can all be animated independently later.
 */
internal sealed class HairyGuardianRenderer : ILynxRenderer
{
    private const float DesignSize = 160f;
    private const int MiniatureThreshold = 180;
    private const int MiniatureOversample = 4;

    public string Name => "Hairy Guardian v2 · live GDI+ vector";

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
            DrawTail(graphics, colors, miniature: false);
            DrawBody(graphics, colors, miniature: false);
            DrawHead(graphics, colors, miniature: false);
            DrawFace(graphics, colors, state, miniature: false);
            DrawCollar(graphics, colors, miniature: false);
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
        using (var mini = Graphics.FromImage(surface))
        {
            mini.Clear(Color.Transparent);
            ConfigureQuality(mini);
            mini.ScaleTransform(width / DesignSize, height / DesignSize);

            var accent = ResolveStateColor(palette, state);
            var colors = GuardianColors.FromPalette(palette, accent);

            DrawGroundGlow(mini, colors);
            DrawTail(mini, colors, miniature: true);
            DrawBody(mini, colors, miniature: true);
            DrawHead(mini, colors, miniature: true);
            DrawFace(mini, colors, state, miniature: true);
            DrawCollar(mini, colors, miniature: true);
            DrawStateSignal(mini, colors, state);

            if (debugOverlay)
                DrawDebug(mini);
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
        using var outer = new SolidBrush(Color.FromArgb(18, c.Accent));
        using var middle = new SolidBrush(Color.FromArgb(28, c.Accent));
        using var inner = new SolidBrush(Color.FromArgb(42, c.Accent));

        g.FillEllipse(outer, 21, 145, 118, 9);
        g.FillEllipse(middle, 35, 147, 90, 5);
        g.FillEllipse(inner, 54, 148, 52, 3);
    }

    private static void DrawTail(Graphics g, GuardianColors c, bool miniature)
    {
        // Large, layered, dark tail is a key part of the approved silhouette.
        using var tail = Path(
            P(52, 145),
            C(35, 148, 16, 139, 10, 122),
            C(4, 105, 8, 86, 20, 72),
            L(13, 70),
            C(22, 60, 34, 55, 45, 59),
            L(40, 52),
            C(55, 55, 64, 69, 64, 84),
            C(64, 101, 58, 118, 52, 145),
            Z());

        using var tailBrush = VerticalGradient(
            new RectangleF(6, 51, 61, 98),
            c.BodyDark,
            c.Darkest);
        using var edge = new Pen(c.Edge, miniature ? 2.5f : 1.8f)
        {
            LineJoin = LineJoin.Round
        };

        g.FillPath(tailBrush, tail);
        g.DrawPath(edge, tail);

        using var upperFlash = Path(
            P(12, 107),
            C(18, 84, 31, 67, 50, 63),
            C(41, 61, 32, 66, 26, 75),
            L(18, 88),
            L(23, 83),
            C(18, 94, 14, 102, 12, 107),
            Z());

        using var flashBrush = new SolidBrush(Color.FromArgb(miniature ? 205 : 175, c.PurpleBright));
        g.FillPath(flashBrush, upperFlash);

        using var tailTip = Path(
            P(18, 78),
            C(25, 63, 37, 57, 48, 62),
            C(39, 57, 30, 59, 23, 67),
            L(15, 78),
            L(20, 75),
            Z());

        using var tipBrush = new SolidBrush(Color.FromArgb(225, c.PurpleLight));
        g.FillPath(tipBrush, tailTip);

        if (!miniature)
        {
            using var lowerFlash = Path(
                P(15, 122),
                C(24, 133, 35, 138, 49, 136),
                L(44, 142),
                C(31, 143, 20, 136, 15, 122),
                Z());
            using var lowerBrush = new SolidBrush(Color.FromArgb(120, c.PurpleMid));
            g.FillPath(lowerBrush, lowerFlash);

            using var furCut = Path(
                P(22, 96),
                L(29, 88),
                L(27, 99),
                L(36, 91),
                L(31, 105),
                Z());
            using var furCutBrush = new SolidBrush(Color.FromArgb(135, c.PurpleLight));
            g.FillPath(furCutBrush, furCut);
        }
    }

    private static void DrawBody(Graphics g, GuardianColors c, bool miniature)
    {
        // Less spherical than v1: narrower shoulders, heavier dark lower body.
        using var body = Path(
            P(49, 88),
            C(42, 99, 39, 113, 40, 129),
            C(41, 143, 49, 152, 62, 156),
            C(70, 159, 90, 159, 98, 156),
            C(111, 152, 119, 143, 120, 129),
            C(121, 113, 118, 99, 111, 88),
            C(102, 80, 92, 78, 80, 78),
            C(68, 78, 58, 80, 49, 88),
            Z());

        using var bodyBrush = VerticalGradient(
            new RectangleF(39, 77, 82, 82),
            c.Body,
            c.Darkest);
        using var bodyEdge = new Pen(c.Edge, miniature ? 2.3f : 1.7f)
        {
            LineJoin = LineJoin.Round
        };

        g.FillPath(bodyBrush, body);
        g.DrawPath(bodyEdge, body);

        DrawLeg(g, left: true, c, miniature);
        DrawLeg(g, left: false, c, miniature);
        DrawChest(g, c, miniature);
        DrawPaw(g, new RectangleF(45, 135, 31, 20), c, miniature);
        DrawPaw(g, new RectangleF(84, 135, 31, 20), c, miniature);
    }

    private static void DrawLeg(Graphics g, bool left, GuardianColors c, bool miniature)
    {
        using var leg = Path(
            P(48, 101),
            C(54, 94, 63, 91, 70, 96),
            C(63, 107, 60, 121, 61, 137),
            L(53, 128),
            L(55, 117),
            L(49, 121),
            C(46, 114, 45, 107, 48, 101),
            Z());

        using var path = left ? leg : Mirror(leg, 80);
        using var brush = new SolidBrush(Color.FromArgb(miniature ? 190 : 150, c.PurpleMid));
        g.FillPath(brush, path);

        if (!miniature)
        {
            using var slash = Path(
                P(50, 112),
                L(57, 106),
                L(55, 116),
                L(64, 111),
                L(58, 124),
                Z());
            using var slashPath = left ? slash : Mirror(slash, 80);
            using var slashBrush = new SolidBrush(Color.FromArgb(170, c.PurpleLight));
            g.FillPath(slashBrush, slashPath);
        }
    }

    private static void DrawChest(Graphics g, GuardianColors c, bool miniature)
    {
        using var chest = Path(
            P(59, 91),
            L(67, 99),
            L(64, 104),
            L(72, 108),
            L(68, 114),
            L(75, 118),
            L(72, 124),
            L(79, 128),
            L(80, 143),
            L(81, 128),
            L(88, 124),
            L(85, 118),
            L(92, 114),
            L(88, 108),
            L(96, 104),
            L(93, 99),
            L(101, 91),
            C(93, 87, 87, 85, 80, 85),
            C(73, 85, 67, 87, 59, 91),
            Z());

        using var chestBrush = VerticalGradient(
            new RectangleF(58, 85, 44, 59),
            Color.FromArgb(255, 255, 255),
            Color.FromArgb(205, 208, 226));

        g.FillPath(chestBrush, chest);

        if (!miniature)
        {
            using var shadow = new SolidBrush(Color.FromArgb(35, c.PurpleLight));
            g.FillPolygon(shadow,
            [
                Pt(72, 108), Pt(80, 112), Pt(88, 108),
                Pt(85, 118), Pt(80, 122), Pt(75, 118)
            ]);
        }
    }

    private static void DrawPaw(Graphics g, RectangleF rect, GuardianColors c, bool miniature)
    {
        using var paw = new GraphicsPath();
        paw.AddEllipse(rect);

        using var pawBrush = VerticalGradient(rect, c.BodyDark, c.Darkest);
        using var edge = new Pen(Color.FromArgb(175, c.Edge), miniature ? 1.8f : 1.15f);

        g.FillPath(pawBrush, paw);
        g.DrawPath(edge, paw);

        using var toePen = new Pen(Color.FromArgb(120, c.PurpleLight), miniature ? 1.5f : 1.05f);
        for (var i = 1; i <= 3; i++)
        {
            var x = rect.Left + rect.Width * (i / 4f);
            g.DrawArc(
                toePen,
                x - 2.5f,
                rect.Top + rect.Height * 0.48f,
                5f,
                rect.Height * 0.34f,
                200,
                140);
        }
    }

    private static void DrawHead(Graphics g, GuardianColors c, bool miniature)
    {
        DrawEars(g, c, miniature);

        // Wider across the temples, but noticeably narrower through the jaw.
        using var head = Path(
            P(43, 39),
            C(50, 28, 61, 22, 71, 19),
            L(69, 15),
            L(78, 18),
            L(87, 14),
            L(85, 19),
            C(101, 21, 112, 28, 118, 39),
            C(124, 48, 125, 58, 122, 68),
            L(127, 74),
            L(118, 75),
            L(122, 81),
            L(113, 80),
            L(115, 87),
            C(108, 96, 96, 102, 80, 104),
            C(64, 102, 52, 96, 45, 87),
            L(47, 80),
            L(38, 81),
            L(42, 75),
            L(33, 74),
            L(38, 68),
            C(35, 58, 36, 48, 43, 39),
            Z());

        using var headBrush = VerticalGradient(
            new RectangleF(33, 18, 94, 87),
            c.PurpleBright,
            c.Body);
        using var headEdge = new Pen(c.Edge, miniature ? 2.4f : 1.85f)
        {
            LineJoin = LineJoin.Round
        };

        g.FillPath(headBrush, head);
        g.DrawPath(headEdge, head);

        DrawCheekFur(g, c, miniature);

        if (!miniature)
        {
            using var templeShade = new SolidBrush(Color.FromArgb(42, c.Darkest));
            using var leftShade = Path(
                P(38, 54),
                C(39, 45, 45, 36, 52, 31),
                C(45, 43, 43, 55, 45, 67),
                L(38, 68),
                C(36, 63, 36, 58, 38, 54),
                Z());
            using var rightShade = Mirror(leftShade, 80);
            g.FillPath(templeShade, leftShade);
            g.FillPath(templeShade, rightShade);
        }
    }

    private static void DrawEars(Graphics g, GuardianColors c, bool miniature)
    {
        using var leftEar = Path(
            P(44, 48),
            C(37, 38, 37, 20, 46, 4),
            C(57, 13, 64, 24, 67, 38),
            C(59, 42, 51, 45, 44, 48),
            Z());
        using var rightEar = Mirror(leftEar, 80);

        using var earBrush = VerticalGradient(
            new RectangleF(36, 3, 52, 47),
            c.PurpleMid,
            c.BodyDark);
        using var edge = new Pen(c.Edge, miniature ? 2.4f : 1.8f)
        {
            LineJoin = LineJoin.Round
        };

        g.FillPath(earBrush, leftEar);
        g.FillPath(earBrush, rightEar);
        g.DrawPath(edge, leftEar);
        g.DrawPath(edge, rightEar);

        using var leftInner = Path(
            P(45, 39),
            C(43, 29, 45, 18, 49, 11),
            C(55, 18, 59, 28, 61, 36),
            L(56, 32),
            L(57, 39),
            L(51, 34),
            L(48, 41),
            Z());
        using var rightInner = Mirror(leftInner, 80);
        using var innerBrush = new SolidBrush(Color.FromArgb(230, c.PurpleLight));

        g.FillPath(innerBrush, leftInner);
        g.FillPath(innerBrush, rightInner);

        if (!miniature)
        {
            using var darkTuft = new SolidBrush(Color.FromArgb(130, c.Darkest));
            g.FillPolygon(darkTuft, [Pt(49, 24), Pt(53, 29), Pt(51, 18), Pt(57, 28), Pt(55, 35)]);
            g.FillPolygon(darkTuft, [Pt(111, 24), Pt(107, 29), Pt(109, 18), Pt(103, 28), Pt(105, 35)]);
        }
    }

    private static void DrawCheekFur(Graphics g, GuardianColors c, bool miniature)
    {
        using var left = Path(
            P(37, 62),
            C(35, 70, 37, 77, 42, 84),
            L(36, 83),
            L(43, 90),
            L(40, 92),
            L(50, 96),
            L(47, 87),
            L(55, 89),
            L(50, 79),
            L(58, 81),
            C(54, 70, 47, 64, 37, 62),
            Z());

        using var right = Mirror(left, 80);
        using var brush = new SolidBrush(Color.FromArgb(miniature ? 165 : 145, c.PurpleLight));

        g.FillPath(brush, left);
        g.FillPath(brush, right);
    }

    private static void DrawFace(
        Graphics g,
        GuardianColors c,
        LynxVisualState state,
        bool miniature)
    {
        using var mask = Path(
            P(43, 62),
            C(49, 54, 60, 51, 69, 55),
            C(74, 57, 78, 62, 80, 67),
            C(82, 62, 86, 57, 91, 55),
            C(100, 51, 111, 54, 117, 62),
            C(121, 70, 117, 80, 109, 86),
            C(101, 92, 91, 95, 80, 95),
            C(69, 95, 59, 92, 51, 86),
            C(43, 80, 39, 70, 43, 62),
            Z());

        using var maskBrush = VerticalGradient(
            new RectangleF(40, 50, 80, 46),
            Color.White,
            Color.FromArgb(220, 222, 239));

        g.FillPath(maskBrush, mask);

        var alert = state is LynxVisualState.Attention or LynxVisualState.Conflict;
        DrawEye(g, 60, 58, c, alert, left: true, miniature);
        DrawEye(g, 100, 58, c, alert, left: false, miniature);

        using var browPen = new Pen(
            Color.FromArgb(alert ? 210 : 145, c.Darkest),
            miniature ? 2.0f : (alert ? 2.2f : 1.55f))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        if (alert)
        {
            g.DrawLine(browPen, 49, 47, 66, 52);
            g.DrawLine(browPen, 111, 47, 94, 52);
        }
        else
        {
            g.DrawBezier(browPen, Pt(50, 48), Pt(54, 45), Pt(61, 45), Pt(66, 48));
            g.DrawBezier(browPen, Pt(110, 48), Pt(106, 45), Pt(99, 45), Pt(94, 48));
        }

        using var nose = new GraphicsPath();
        nose.AddBezier(Pt(74, 71), Pt(77, 69), Pt(83, 69), Pt(86, 71));
        nose.AddBezier(Pt(86, 71), Pt(85, 75.5f), Pt(82, 78), Pt(80, 78));
        nose.AddBezier(Pt(80, 78), Pt(78, 78), Pt(75, 75.5f), Pt(74, 71));
        nose.CloseFigure();
        using var noseBrush = new SolidBrush(Color.FromArgb(25, 15, 40));
        g.FillPath(noseBrush, nose);

        using var mouthPen = new Pen(Color.FromArgb(45, 27, 72), miniature ? 2.0f : 1.8f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        g.DrawLine(mouthPen, 80, 78, 80, 83);
        g.DrawBezier(mouthPen, Pt(80, 83), Pt(76, 83), Pt(72, 86), Pt(70, 88));
        g.DrawBezier(mouthPen, Pt(80, 83), Pt(84, 83), Pt(88, 86), Pt(90, 88));
    }

    private static void DrawEye(
        Graphics g,
        float x,
        float y,
        GuardianColors c,
        bool alert,
        bool left,
        bool miniature)
    {
        // Narrower almond eye replaces the round baby-eye look.
        var halfWidth = miniature ? 8.0f : 7.4f;
        var halfHeight = miniature ? 8.0f : 7.3f;
        var topY = y - (alert ? 7.0f : 6.4f);

        using var eye = new GraphicsPath();

        if (left)
        {
            eye.AddBezier(
                Pt(x - halfWidth, y - 1),
                Pt(x - 4.5f, topY - 2),
                Pt(x + 4.8f, topY - 1.2f),
                Pt(x + halfWidth, y - 1.5f));
            eye.AddBezier(
                Pt(x + halfWidth, y - 1.5f),
                Pt(x + 6.6f, y + 6.2f),
                Pt(x + 0.8f, y + halfHeight),
                Pt(x - 2.8f, y + halfHeight - 0.5f));
            eye.AddBezier(
                Pt(x - 2.8f, y + halfHeight - 0.5f),
                Pt(x - 7.2f, y + 4.2f),
                Pt(x - 8.1f, y + 1.0f),
                Pt(x - halfWidth, y - 1));
        }
        else
        {
            eye.AddBezier(
                Pt(x + halfWidth, y - 1),
                Pt(x + 4.5f, topY - 2),
                Pt(x - 4.8f, topY - 1.2f),
                Pt(x - halfWidth, y - 1.5f));
            eye.AddBezier(
                Pt(x - halfWidth, y - 1.5f),
                Pt(x - 6.6f, y + 6.2f),
                Pt(x - 0.8f, y + halfHeight),
                Pt(x + 2.8f, y + halfHeight - 0.5f));
            eye.AddBezier(
                Pt(x + 2.8f, y + halfHeight - 0.5f),
                Pt(x + 7.2f, y + 4.2f),
                Pt(x + 8.1f, y + 1.0f),
                Pt(x + halfWidth, y - 1));
        }

        eye.CloseFigure();

        using var eyeBrush = VerticalGradient(
            new RectangleF(x - halfWidth, topY - 2, halfWidth * 2, halfHeight * 2 + 4),
            Color.FromArgb(33, 18, 57),
            Color.FromArgb(5, 7, 15));

        using var rim = new Pen(Color.FromArgb(175, c.Accent), miniature ? 1.6f : 1.05f);
        g.FillPath(eyeBrush, eye);
        g.DrawPath(rim, eye);

        using var iris = new SolidBrush(Color.FromArgb(155, c.Accent));
        g.FillEllipse(iris, x - 3.4f, y + 0.5f, 6.8f, 5.6f);

        using var pupil = new SolidBrush(Color.FromArgb(210, 3, 4, 10));
        g.FillEllipse(pupil, x - 2.0f, y - 0.2f, 4.0f, 7.2f);

        using var highlight = new SolidBrush(Color.White);
        g.FillEllipse(
            highlight,
            x + (left ? -2.2f : -1.1f),
            y - 4.8f,
            miniature ? 3.4f : 3.0f,
            miniature ? 3.4f : 3.0f);
    }

    private static void DrawCollar(Graphics g, GuardianColors c, bool miniature)
    {
        using var collar = Path(
            P(48, 92),
            L(60, 97),
            L(68, 95),
            L(80, 101),
            L(92, 95),
            L(100, 97),
            L(112, 92),
            L(116, 100),
            L(102, 110),
            L(92, 107),
            L(80, 113),
            L(68, 107),
            L(58, 110),
            L(44, 100),
            Z());

        using var collarBrush = VerticalGradient(
            new RectangleF(44, 92, 72, 22),
            Color.FromArgb(50, 47, 63),
            Color.FromArgb(9, 10, 16));
        using var edge = new Pen(Color.FromArgb(215, c.PurpleLight), miniature ? 1.9f : 1.4f)
        {
            LineJoin = LineJoin.Bevel
        };

        g.FillPath(collarBrush, collar);
        g.DrawPath(edge, collar);

        using var leftFacet = Path(
            P(48, 97),
            L(60, 100),
            L(67, 98),
            L(61, 106),
            L(54, 105),
            Z());
        using var rightFacet = Mirror(leftFacet, 80);
        using var facetBrush = new SolidBrush(Color.FromArgb(205, c.PurpleBright));

        g.FillPath(facetBrush, leftFacet);
        g.FillPath(facetBrush, rightFacet);

        using var shield = Path(
            P(80, 99),
            L(93, 106),
            L(90, 123),
            L(80, 132),
            L(70, 123),
            L(67, 106),
            Z());

        using var shieldBrush = VerticalGradient(
            new RectangleF(66, 99, 28, 34),
            c.PurpleBright,
            c.Darkest);
        using var glow = new Pen(Color.FromArgb(90, c.Accent), miniature ? 4.2f : 4.8f)
        {
            LineJoin = LineJoin.Round
        };
        using var shieldEdge = new Pen(c.PurpleLight, miniature ? 2.1f : 1.8f)
        {
            LineJoin = LineJoin.Round
        };

        g.FillPath(shieldBrush, shield);
        g.DrawPath(glow, shield);
        g.DrawPath(shieldEdge, shield);

        using var inner = Path(
            P(80, 104),
            L(88.5f, 109),
            L(86.5f, 120),
            L(80, 126),
            L(73.5f, 120),
            L(71.5f, 109),
            Z());

        using var innerPen = new Pen(Color.FromArgb(210, c.Accent), miniature ? 1.6f : 1.1f)
        {
            LineJoin = LineJoin.Round
        };
        g.DrawPath(innerPen, inner);

        using var check = new Pen(Color.White, miniature ? 3.8f : 3.2f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        g.DrawLines(check, [Pt(74, 114.5f), Pt(79, 119.4f), Pt(87, 110)]);
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
            g.DrawLine(signal, 80, 27, 80, 36);
            g.DrawLine(signal, 80, 36, 76, 32);
            g.DrawLine(signal, 80, 36, 84, 32);
        }
        else if (state == LynxVisualState.Send)
        {
            g.DrawLine(signal, 80, 36, 80, 27);
            g.DrawLine(signal, 80, 27, 76, 31);
            g.DrawLine(signal, 80, 27, 84, 31);
        }
        else if (state == LynxVisualState.Save)
        {
            using var save = new Pen(Color.FromArgb(230, c.Accent), 2f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            g.DrawLines(save, [Pt(70, 32), Pt(76, 38), Pt(90, 23)]);
        }
        else if (state == LynxVisualState.Conflict)
        {
            using var warning = new SolidBrush(Color.FromArgb(225, c.Accent));
            g.FillPolygon(warning, [Pt(80, 26), Pt(73, 39), Pt(87, 39)]);

            using var mark = new Pen(Color.FromArgb(30, 20, 15), 1.4f);
            g.DrawLine(mark, 80, 30, 80, 35);
            g.FillEllipse(Brushes.Black, 79.2f, 36.2f, 1.6f, 1.6f);
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

        using var anchor = new SolidBrush(Color.FromArgb(230, 240, 189, 97));
        g.FillEllipse(anchor, 77, 100, 6, 6);
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
            // The guardian stays recognisably purple. Palette selection changes
            // undertone and status lighting rather than repainting the animal.
            return new GuardianColors(
                Darkest: Mix(Color.FromArgb(17, 9, 30), palette.Fur, 0.13f),
                BodyDark: Mix(Color.FromArgb(35, 20, 58), palette.Fur, 0.17f),
                Body: Mix(Color.FromArgb(58, 31, 96), palette.Fur, 0.15f),
                PurpleMid: Mix(Color.FromArgb(91, 48, 172), palette.Accent, 0.13f),
                PurpleBright: Mix(Color.FromArgb(119, 63, 220), palette.Accent, 0.12f),
                PurpleLight: Mix(Color.FromArgb(185, 137, 255), palette.Eye, 0.11f),
                Edge: Mix(Color.FromArgb(122, 79, 191), palette.Edge, 0.18f),
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
