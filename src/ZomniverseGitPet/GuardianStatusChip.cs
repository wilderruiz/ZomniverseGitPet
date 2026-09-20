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
        Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
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
        Interactive && keyData is Keys.Enter or Keys.Space || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Interactive && e.KeyCode is Keys.Enter or Keys.Space)
        {
            e.Handled = true;
            OnClick(EventArgs.Empty);
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var background = Parent?.BackColor ?? GuardianTheme.Surface;
        e.Graphics.Clear(background);

        if (Interactive && (_hovered || Focused))
        {
            using var hover = new SolidBrush(Color.FromArgb(28, GuardianTheme.Info));
            e.Graphics.FillRectangle(hover, ClientRectangle);
        }

        var accent = Tone switch
        {
            GuardianChipTone.Healthy => GuardianTheme.Healthy,
            GuardianChipTone.Changes => GuardianTheme.Changes,
            GuardianChipTone.Warning => GuardianTheme.Warning,
            _ => GuardianTheme.Info
        };

        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            ClientRectangle,
            accent,
            TextFormatFlags.Left |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.WordBreak);
    }
}
