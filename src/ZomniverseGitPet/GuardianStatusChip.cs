namespace ZomniverseGitPet;

internal enum GuardianChipTone
{
    Neutral,
    Healthy,
    Changes,
    Warning
}

internal sealed class GuardianStatusChip : Control
{
    private GuardianChipTone _tone;
    private bool _interactive;
    private bool _hovered;

    public GuardianChipTone Tone
    {
        get => _tone;
        set { _tone = value; Invalidate(); }
    }

    public bool Interactive
    {
        get => _interactive;
        set
        {
            _interactive = value;
            TabStop = value;
            Cursor = value ? Cursors.Hand : Cursors.Default;
            AccessibleRole = value ? AccessibleRole.PushButton : AccessibleRole.StaticText;
            Invalidate();
        }
    }

    public GuardianStatusChip()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.UserPaint |
                 ControlStyles.Selectable, true);
        Size = new Size(132, 30);
        Font = new Font("Cascadia Mono", 8f, FontStyle.Bold);
        ForeColor = GuardianTheme.Ink;
        TabStop = false;
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        if (!Interactive) return;
        _hovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hovered = false;
        Invalidate();
    }

    protected override bool IsInputKey(Keys keyData) =>
        (Interactive && (keyData & Keys.KeyCode) is (Keys.Enter or Keys.Space or Keys.Down or Keys.F4)) || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Interactive && Enabled && e.KeyCode is (Keys.Enter or Keys.Space or Keys.Down or Keys.F4))
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            OnClick(EventArgs.Empty);
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var background = Parent?.BackColor ?? GuardianTheme.Surface;
        e.Graphics.Clear(background);

        if (Interactive)
        {
            using var hover = new SolidBrush(Color.FromArgb(Enabled && (_hovered || Focused) ? 42 : 18, GuardianTheme.Info));
            e.Graphics.FillRectangle(hover, ClientRectangle);
            using var border = new Pen(Enabled ? GuardianTheme.Info : GuardianTheme.FaintInk);
            e.Graphics.DrawRectangle(border, 0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
        }

        var accent = Tone switch
        {
            GuardianChipTone.Healthy => GuardianTheme.Healthy,
            GuardianChipTone.Changes => GuardianTheme.Changes,
            GuardianChipTone.Warning => GuardianTheme.Warning,
            _ => GuardianTheme.Info
        };

        var textBounds = ClientRectangle;
        if (Interactive)
        {
            var arrowWidth = Math.Max(20, (int)Math.Round(24 * DeviceDpi / 96f));
            textBounds = new Rectangle(6, 0, Math.Max(0, Width - arrowWidth - 8), Height);
            using var arrow = new SolidBrush(Enabled ? accent : GuardianTheme.FaintInk);
            var x = Width - arrowWidth / 2f;
            var y = Height / 2f;
            var half = 4f * DeviceDpi / 96f;
            e.Graphics.FillPolygon(arrow,
            [
                new PointF(x - half, y - half / 2),
                new PointF(x + half, y - half / 2),
                new PointF(x, y + half / 2)
            ]);
            if (Focused && ShowFocusCues)
                ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -3, -3));
        }

        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            textBounds,
            Enabled ? accent : GuardianTheme.FaintInk,
            TextFormatFlags.Left |
            TextFormatFlags.VerticalCenter |
            (Interactive ? TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis : TextFormatFlags.WordBreak));
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }
}
