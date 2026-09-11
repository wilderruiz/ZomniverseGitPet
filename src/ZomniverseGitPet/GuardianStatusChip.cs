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

    public GuardianChipTone Tone
    {
        get => _tone;
        set { _tone = value; Invalidate(); }
    }

    public GuardianStatusChip()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.UserPaint, true);
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

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? GuardianTheme.Surface);

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
