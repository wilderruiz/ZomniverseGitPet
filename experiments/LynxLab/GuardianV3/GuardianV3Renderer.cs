using System.Drawing.Drawing2D;

namespace LynxLab;

internal sealed class GuardianV3Renderer : ILynxRenderer
{
    public string Name => "Guardian V3 layered";

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
            graphics.TranslateTransform(bounds.Left, bounds.Top);
            graphics.ScaleTransform(bounds.Width / 160f, bounds.Height / 160f);

            DrawPlaceholderLayers(graphics, palette);
            DrawMarker(graphics, palette);

            if (debugOverlay)
                DrawDebugGeometry(graphics);
        }
        finally
        {
            graphics.Restore(saved);
        }
    }

    private static void DrawPlaceholderLayers(Graphics graphics, LynxPalette palette)
    {
        using var shadow = new SolidBrush(Color.FromArgb(28, palette.Accent));
        using var body = new SolidBrush(Color.FromArgb(58, 31, 96));
        using var head = new SolidBrush(Color.FromArgb(119, 63, 220));
        using var detail = new SolidBrush(Color.FromArgb(232, 226, 255));
        using var edge = new Pen(Color.FromArgb(185, 137, 255), 2f)
        {
            LineJoin = LineJoin.Round
        };

        graphics.FillEllipse(shadow, 25, 144, 110, 9);
        graphics.FillEllipse(body, 42, 78, 76, 72);
        graphics.DrawEllipse(edge, 42, 78, 76, 72);

        var headPoints = new[]
        {
            new PointF(39, 42), new PointF(48, 8), new PointF(66, 28),
            new PointF(80, 22), new PointF(94, 28), new PointF(112, 8),
            new PointF(121, 42), new PointF(116, 91), new PointF(80, 105),
            new PointF(44, 91)
        };
        graphics.FillPolygon(head, headPoints);
        graphics.DrawPolygon(edge, headPoints);

        graphics.FillEllipse(detail, 52, 60, 56, 34);
        graphics.FillPolygon(detail,
        [
            new PointF(66, 93), new PointF(80, 113), new PointF(94, 93),
            new PointF(89, 132), new PointF(80, 143), new PointF(71, 132)
        ]);

        using var collar = new Pen(Color.FromArgb(26, 18, 42), 8f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        graphics.DrawLine(collar, 55, 101, 105, 101);
        graphics.FillPolygon(head,
        [
            new PointF(80, 96), new PointF(92, 103), new PointF(89, 120),
            new PointF(80, 128), new PointF(71, 120), new PointF(68, 103)
        ]);
    }

    private static void DrawMarker(Graphics graphics, LynxPalette palette)
    {
        using var eye = new SolidBrush(Color.FromArgb(24, 13, 39));
        using var accent = new Pen(palette.Accent, 2.4f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        using var font = new Font("Segoe UI", 7f, FontStyle.Bold);
        using var label = new SolidBrush(Color.FromArgb(215, 205, 245));

        graphics.FillEllipse(eye, 57, 55, 12, 14);
        graphics.FillEllipse(eye, 91, 55, 12, 14);
        graphics.DrawLines(accent,
        [
            new PointF(74, 109), new PointF(79, 114), new PointF(87, 105)
        ]);
        graphics.DrawString("V3", font, label, 73, 134);
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
    }
}
