using System.Drawing.Drawing2D;

namespace ZomniverseGitPet;

internal sealed class PetMessageBubble : UserControl
{
    private const int MinimumBubbleHeight = 66;
    private const int MaximumBubbleHeight = 172;
    private readonly RichTextBox _message;

    public PetMessageBubble()
    {
        BackColor = Color.Transparent;
        DoubleBuffered = true;
        Width = 228;
        Height = MinimumBubbleHeight;
        MinimumSize = new Size(228, MinimumBubbleHeight);
        MaximumSize = new Size(228, MaximumBubbleHeight);

        _message = new RichTextBox
        {
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            BackColor = Color.FromArgb(31, 24, 44),
            ForeColor = Color.FromArgb(246, 240, 252),
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ScrollBars = RichTextBoxScrollBars.Vertical,
            DetectUrls = false,
            TabStop = false,
            Cursor = Cursors.Arrow
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
        var measured = TextRenderer.MeasureText(
            text,
            _message.Font,
            new Size(184, MaximumBubbleHeight),
            TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
        Height = Math.Clamp(measured.Height + 36, MinimumBubbleHeight, MaximumBubbleHeight);
        _message.SelectionStart = 0;
        _message.ScrollToCaret();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var bubbleBounds = new Rectangle(1, 1, Width - 3, Height - 14);
        using var path = GuardianTheme.RoundedRectangle(bubbleBounds, 16);
        using var fill = new SolidBrush(Color.FromArgb(31, 24, 44));
        using var glow = new Pen(Color.FromArgb(135, 86, 202), 2f);

        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(glow, path);

        var tail = new[]
        {
            new Point(102, Height - 14),
            new Point(120, Height - 14),
            new Point(111, Height - 2)
        };
        e.Graphics.FillPolygon(fill, tail);
        e.Graphics.DrawLines(glow, new[] { tail[0], tail[2], tail[1] });
    }

    private void LayoutMessage()
    {
        // Keep the top-right corner visually clear for the integrated minimize control.
        _message.Bounds = new Rectangle(15, 12, Width - 52, Math.Max(24, Height - 38));
    }
}
