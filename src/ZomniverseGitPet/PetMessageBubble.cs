using System.Drawing.Drawing2D;

namespace ZomniverseGitPet;

internal sealed class PetMessageBubble : UserControl
{
    private const int MinimumBubbleHeight = 58;
    private const int MaximumBubbleHeight = 160;
    private readonly RichTextBox _message;

    public PetMessageBubble()
    {
        BackColor = Color.Transparent;
        DoubleBuffered = true;
        Width = 220;
        Height = MinimumBubbleHeight;
        MinimumSize = new Size(220, MinimumBubbleHeight);
        MaximumSize = new Size(220, MaximumBubbleHeight);

        _message = new RichTextBox
        {
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            BackColor = Color.FromArgb(255, 252, 245),
            ForeColor = Color.FromArgb(55, 35, 86),
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ScrollBars = RichTextBoxScrollBars.Vertical,
            DetectUrls = false,
            TabStop = false
        };
        Controls.Add(_message);
        Resize += (_, _) => LayoutMessage();
        LayoutMessage();
    }

    public event EventHandler? BubbleDoubleClick
    {
        add => _message.DoubleClick += value;
        remove => _message.DoubleClick -= value;
    }

    public void SetMessage(string text)
    {
        _message.Text = text;
        var measured = TextRenderer.MeasureText(text, _message.Font, new Size(190, MaximumBubbleHeight),
            TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
        Height = Math.Clamp(measured.Height + 32, MinimumBubbleHeight, MaximumBubbleHeight);
        _message.SelectionStart = 0;
        _message.ScrollToCaret();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        using var path = RoundedRectangle(new Rectangle(1, 1, Width - 3, Height - 13), 16);
        using var fill = new SolidBrush(Color.FromArgb(255, 252, 245));
        using var border = new Pen(Color.FromArgb(112, 79, 163), 2f);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(border, path);
        var tail = new[] { new Point(99, Height - 13), new Point(116, Height - 13), new Point(108, Height - 2) };
        e.Graphics.FillPolygon(fill, tail);
        e.Graphics.DrawLines(border, new Point[] { tail[0], tail[2], tail[1] });
    }

    private void LayoutMessage() => _message.Bounds = new Rectangle(14, 10, Width - 28, Math.Max(22, Height - 32));

    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
