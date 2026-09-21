using System.Drawing.Drawing2D;

namespace LynxLab;

internal interface ILynxRenderer
{
    string Name { get; }

    void Draw(
        Graphics graphics,
        Rectangle bounds,
        LynxPalette palette,
        LynxVisualState state,
        bool debugOverlay);
}

internal sealed class GdiLynxRenderer : ILynxRenderer
{
    public string Name => "GDI+ vector baseline";

    public void Draw(
        Graphics graphics,
        Rectangle bounds,
        LynxPalette palette,
        LynxVisualState state,
        bool debugOverlay)
    {
        var stateColor = ResolveStateColor(palette, state);

        var saved = graphics.Save();
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.TranslateTransform(bounds.Left, bounds.Top);
        graphics.ScaleTransform(bounds.Width / 160f, bounds.Height / 160f);

        DrawLynx(graphics, palette, stateColor, state);

        if (debugOverlay)
        {
            using var debugPen = new Pen(Color.FromArgb(150, 240, 189, 97), 1f)
            {
                DashStyle = DashStyle.Dash
            };
            graphics.DrawRectangle(debugPen, 1, 1, 158, 158);
            graphics.DrawLine(debugPen, 80, 0, 80, 160);
            graphics.DrawLine(debugPen, 0, 80, 160, 80);

            using var anchorBrush = new SolidBrush(Color.FromArgb(230, 240, 189, 97));
            graphics.FillEllipse(anchorBrush, 77, 113, 6, 6);
        }

        graphics.Restore(saved);
    }

    private static void DrawLynx(
        Graphics g,
        LynxPalette palette,
        Color stateColor,
        LynxVisualState state)
    {
        using var furBrush = new SolidBrush(palette.Fur);
        using var earBrush = new SolidBrush(palette.Ear);
        using var darkBrush = new SolidBrush(Color.FromArgb(0x0A, 0x10, 0x15));
        using var muzzleBrush = new SolidBrush(palette.Muzzle);
        using var noseBrush = new SolidBrush(Color.FromArgb(0x91, 0xA5, 0xB6));
        using var collarBrush = new SolidBrush(Color.FromArgb(0x17, 0x25, 0x2E));
        using var coreBrush = new SolidBrush(stateColor);

        using var edgePen = new Pen(palette.Edge, 2f) { LineJoin = LineJoin.Round };
        using var eyePen = new Pen(stateColor, 3f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        using var detailPen = new Pen(Color.FromArgb(0x6A, 0x7F, 0x92), 2f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        using var whiskerPen = new Pen(palette.Edge, 1.5f);
        using var collarPen = new Pen(palette.Detail, 1.5f);

        var head =
            new[]
            {
                P(31, 50), P(48, 20), P(45, 6), P(58, 18), P(74, 38),
                P(86, 38), P(102, 18), P(115, 6), P(112, 20), P(129, 50),
                P(120, 109), P(114, 124), P(101, 137), P(80, 147),
                P(59, 137), P(46, 124), P(40, 109)
            };
        g.FillPolygon(furBrush, head);
        g.DrawPolygon(edgePen, head);

        g.FillPolygon(earBrush, [P(42, 48), P(51, 28), P(63, 44), P(49, 58)]);
        g.FillPolygon(earBrush, [P(118, 48), P(109, 28), P(97, 44), P(111, 58)]);

        g.FillPolygon(darkBrush, [P(47, 72), P(73, 64), P(63, 84), P(46, 87)]);
        g.FillPolygon(darkBrush, [P(113, 72), P(87, 64), P(97, 84), P(114, 87)]);

        g.DrawLine(eyePen, P(55, 72), P(71, 72));
        g.DrawLine(eyePen, P(105, 72), P(89, 72));

        g.FillPolygon(noseBrush, [P(80, 85), P(72, 93), P(88, 93)]);
        g.DrawBezier(detailPen, P(63, 101), P(70, 108), P(90, 108), P(97, 101));

        g.DrawLine(whiskerPen, P(48, 104), P(32, 114));
        g.DrawLine(whiskerPen, P(112, 104), P(128, 114));
        g.DrawLine(whiskerPen, P(51, 110), P(32, 117));
        g.DrawLine(whiskerPen, P(109, 110), P(128, 117));

        var muzzle = new[]
        {
            P(56, 117), P(80, 125), P(104, 117), P(100, 132),
            P(91, 141), P(80, 146), P(69, 141), P(60, 132)
        };
        g.FillPolygon(muzzleBrush, muzzle);
        g.DrawPolygon(edgePen, muzzle);

        g.FillRectangle(collarBrush, 72, 119, 16, 9);
        g.DrawRectangle(collarPen, 72, 119, 16, 9);
        g.FillEllipse(coreBrush, 77.4f, 120.9f, 5.2f, 5.2f);

        if (state is LynxVisualState.Get or LynxVisualState.Send)
        {
            using var signalPen = new Pen(Color.FromArgb(210, stateColor), 2f)
            {
                EndCap = LineCap.Round,
                StartCap = LineCap.Round
            };

            if (state == LynxVisualState.Get)
            {
                g.DrawLine(signalPen, 80, 48, 80, 57);
                g.DrawLine(signalPen, 80, 57, 75, 52);
                g.DrawLine(signalPen, 80, 57, 85, 52);
            }
            else
            {
                g.DrawLine(signalPen, 80, 57, 80, 48);
                g.DrawLine(signalPen, 80, 48, 75, 53);
                g.DrawLine(signalPen, 80, 48, 85, 53);
            }
        }

        if (state == LynxVisualState.Conflict)
        {
            using var warningPen = new Pen(stateColor, 2f);
            g.DrawLine(warningPen, 72, 42, 88, 42);
            g.DrawLine(warningPen, 74, 46, 86, 46);
        }
    }

    private static Color ResolveStateColor(LynxPalette palette, LynxVisualState state) =>
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

    private static PointF P(float x, float y) => new(x, y);
}
