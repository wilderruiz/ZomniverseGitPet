using System.Drawing.Drawing2D;

namespace ZomniverseGitPet;

/* ==========================================================================
   HELPER: ProjectNamePill
   FUNCTION:
   Displays a dynamically sized project name with custom horizontal scrolling
   and a thin hover-only GitPet scrollbar for unusually long names.

   DATE.TIME ADDED: 2026-09-12 20:05 +03:00

   REASON:
   Make the active project unmistakable while keeping unusually long names accessible.
   ========================================================================== */
internal sealed class ProjectNamePill : Control
{
    private const int HorizontalPadding = 14;
    private const int ScrollStep = 48;
    /* ==========================================================================
       PATCH: FLEXIBLE PROJECT PILL WIDTH CEILING
       FUNCTION:
       Removes the arbitrary default width ceiling so the parent header can supply
       the pill's exact available horizontal space.

       DATE.TIME ADDED: 2026-09-12 20:29 +03:00

       REASON:
       The fixed ceiling truncates project names even when the header has unused space.
       ========================================================================== */
    private int _maximumPillWidth = int.MaxValue;
    private int _scrollOffset;
    private bool _hovered;

    public int MaximumPillWidth
    {
        get => _maximumPillWidth;
        set
        {
            var next = Math.Max(1, value);
            if (_maximumPillWidth == next) return;
            _maximumPillWidth = next;
            UpdatePillWidth();
        }
    }

    public ProjectNamePill()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable |
            ControlStyles.UserPaint,
            true);

        Height = 36;
        MinimumSize = new Size(0, 36);
        Font = new Font("Segoe UI", 11f, FontStyle.Bold);
        ForeColor = GuardianTheme.Info;
        BackColor = GuardianTheme.Surface;
        Cursor = Cursors.Hand;
        TabStop = true;
        AccessibleName = "Current project name";
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        _scrollOffset = 0;
        AccessibleDescription = Text;
        UpdatePillWidth();
        Invalidate();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        UpdatePillWidth();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hovered = false;
        Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        ScrollBy(e.Delta > 0 ? -ScrollStep : ScrollStep);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        switch (e.KeyCode)
        {
            case Keys.Left:
                ScrollBy(-ScrollStep);
                e.Handled = true;
                break;
            case Keys.Right:
                ScrollBy(ScrollStep);
                e.Handled = true;
                break;
            case Keys.Home:
                SetScrollOffset(0);
                e.Handled = true;
                break;
            case Keys.End:
                SetScrollOffset(GetMaximumScrollOffset());
                e.Handled = true;
                break;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var pillBounds = new RectangleF(0.5f, 0.5f, Math.Max(1, Width - 1f), Math.Max(1, Height - 1f));
        using var pillPath = GuardianTheme.RoundedRectangle(pillBounds, Height / 2f);
        using var fill = new SolidBrush(_hovered || Focused ? GuardianTheme.SurfaceSoft : GuardianTheme.SurfaceRaised);
        using var border = new Pen(_hovered || Focused ? GuardianTheme.VioletHover : GuardianTheme.Border);
        e.Graphics.FillPath(fill, pillPath);
        e.Graphics.DrawPath(border, pillPath);

        /* ==========================================================================
           PATCH: CENTER PROJECT NAME INSIDE PILL
           FUNCTION:
           Draws the scrolling project name inside a height-aware rectangle so
           Windows vertically centers the complete glyphs without clipping.

           DATE.TIME ADDED: 2026-09-12 20:11 +03:00

           REASON:
           Point-based text rendering clips the project name against the pill's upper edge.
           ========================================================================== */
        var textBounds = GetTextBounds();
        var textWidth = GetTextWidth();
        var scrollingTextBounds = new Rectangle(
            textBounds.Left - _scrollOffset,
            textBounds.Top,
            textWidth,
            textBounds.Height);
        var state = e.Graphics.Save();
        e.Graphics.SetClip(textBounds);
        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            scrollingTextBounds,
            ForeColor,
            TextFormatFlags.NoPadding |
            TextFormatFlags.SingleLine |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.PreserveGraphicsClipping);
        e.Graphics.Restore(state);

        DrawScrollbar(e.Graphics, textBounds);
    }

    private void UpdatePillWidth()
    {
        if (!IsHandleCreated && string.IsNullOrEmpty(Text)) return;

        var textWidth = TextRenderer.MeasureText(
            Text,
            Font,
            Size.Empty,
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
        Width = Math.Clamp(textWidth + (HorizontalPadding * 2), MinimumSize.Width, MaximumPillWidth);
        SetScrollOffset(_scrollOffset);
    }

    private Rectangle GetTextBounds() =>
        new(HorizontalPadding, 1, Math.Max(1, Width - (HorizontalPadding * 2)), Math.Max(1, Height - 5));

    private int GetTextWidth() => TextRenderer.MeasureText(
        Text,
        Font,
        Size.Empty,
        TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;

    private int GetMaximumScrollOffset() => Math.Max(0, GetTextWidth() - GetTextBounds().Width);

    private void ScrollBy(int distance) => SetScrollOffset(_scrollOffset + distance);

    private void SetScrollOffset(int offset)
    {
        var next = Math.Clamp(offset, 0, GetMaximumScrollOffset());
        if (_scrollOffset == next) return;
        _scrollOffset = next;
        Invalidate();
    }

    private void DrawScrollbar(Graphics graphics, Rectangle textBounds)
    {
        var maximumOffset = GetMaximumScrollOffset();
        if ((!_hovered && !Focused) || maximumOffset <= 0) return;

        var trackWidth = textBounds.Width;
        var thumbWidth = Math.Max(26, (int)Math.Round(trackWidth * (trackWidth / (double)GetTextWidth())));
        var travel = Math.Max(1, trackWidth - thumbWidth);
        var thumbLeft = textBounds.Left + (int)Math.Round(travel * (_scrollOffset / (double)maximumOffset));
        var thumbBounds = new Rectangle(thumbLeft, Height - 4, thumbWidth, 2);

        using var thumb = new SolidBrush(Color.FromArgb(158, 197, 164, 235));
        graphics.FillRectangle(thumb, thumbBounds);
    }
}
