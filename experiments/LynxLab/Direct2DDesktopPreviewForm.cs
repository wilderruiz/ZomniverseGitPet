namespace LynxLab;

/// <summary>
/// Production-size Direct2D validation host.
///
/// This is intentionally opaque during Migration 5. It proves the Guardian,
/// activity FX and line weights at the real 160×160 pet footprint before the
/// renderer is moved into a transparent DirectComposition desktop host.
/// </summary>
internal sealed class Direct2DDesktopPreviewForm : Form
{
    private readonly Direct2DTestControl _surface;
    private readonly Label _title;
    private readonly Label _subtitle;

    private Point _dragOrigin;
    private Point _windowOrigin;
    private bool _dragging;

    private LynxVisualState _state = LynxVisualState.Idle;
    private LynxActivityState _activity = LynxActivityState.None;

    public Direct2DDesktopPreviewForm()
    {
        Text = "Direct2D 160×160 Preview";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(240, 246);
        BackColor = Color.FromArgb(0x0B, 0x10, 0x16);

        var bubble = new Panel
        {
            Location = new Point(6, 0),
            Size = new Size(228, 70),
            BackColor = Color.FromArgb(0x11, 0x18, 0x20)
        };

        _title = new Label
        {
            Location = new Point(7, 9),
            Size = new Size(214, 21),
            Text = "● DIRECT2D · 160×160",
            ForeColor = Color.FromArgb(0x57, 0xD7, 0xA0),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _subtitle = new Label
        {
            Location = new Point(7, 32),
            Size = new Size(214, 25),
            Text = "production-size validation",
            ForeColor = Color.FromArgb(0x76, 0x87, 0x9A),
            Font = new Font("Segoe UI", 8f, FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleLeft
        };

        bubble.Controls.Add(_title);
        bubble.Controls.Add(_subtitle);

        _surface = new Direct2DTestControl
        {
            Location = new Point(40, 78),
            Size = new Size(160, 160),
            ShowDiagnosticFrame = false,
            ProductionSizeMode = true,
            Anchor = AnchorStyles.None
        };

        Controls.Add(bubble);
        Controls.Add(_surface);

        foreach (var control in new Control[]
        {
            this,
            bubble,
            _title,
            _subtitle,
            _surface
        })
        {
            control.MouseDown += BeginDrag;
            control.MouseMove += ContinueDrag;
            control.MouseUp += EndDrag;
        }

        PositionAtBottomRight();
    }

    public string BackendStatus => _surface.BackendStatus;
    public long FrameCount => _surface.FrameCount;

    public void SetFrame(
        double seconds,
        LynxPalette palette,
        LynxVisualState state,
        LynxActivityState activity)
    {
        _state = state;
        _activity = activity;

        _surface.SetFrame(
            seconds,
            palette,
            state,
            activity);

        var title =
            activity == LynxActivityState.None
                ? state.ToString().ToUpperInvariant()
                : activity.ToString().ToUpperInvariant();

        _title.Text =
            "● DX 160×160 · " + title;

        _subtitle.Text =
            activity == LynxActivityState.None
                ? StateSubtitle(state)
                : ActivitySubtitle(activity);
    }

    public void PositionAtBottomRight()
    {
        var work =
            Screen.PrimaryScreen?.WorkingArea ??
            Screen.GetWorkingArea(Cursor.Position);

        Location = new Point(
            work.Right - Width - 24,
            work.Bottom - Height - 24);
    }

    public void PositionLeftOf(Form sibling)
    {
        var work =
            Screen.GetWorkingArea(sibling);

        Location = new Point(
            Math.Max(
                work.Left + 24,
                sibling.Left - Width - 14),
            Math.Clamp(
                sibling.Top,
                work.Top + 24,
                work.Bottom - Height - 24));
    }

    private static string StateSubtitle(
        LynxVisualState state) =>
        state switch
        {
            LynxVisualState.Clean =>
                "repository healthy",
            LynxVisualState.Changes =>
                "changes ready to review",
            LynxVisualState.Attention =>
                "Git needs attention",
            LynxVisualState.Save =>
                "checkpoint preview",
            LynxVisualState.Get =>
                "incoming update",
            LynxVisualState.Send =>
                "outgoing update",
            LynxVisualState.Conflict =>
                "reconcile simulation",
            _ =>
                "idle baseline"
        };

    private static string ActivitySubtitle(
        LynxActivityState activity) =>
        activity switch
        {
            LynxActivityState.Thinking =>
                "scanning repository state",
            LynxActivityState.Preparing =>
                "inspecting files",
            LynxActivityState.Sorting =>
                "sorting files for staging",
            LynxActivityState.Packing =>
                "building local checkpoint",
            LynxActivityState.Incoming =>
                "incoming packet traffic",
            LynxActivityState.Outgoing =>
                "outgoing packet traffic",
            LynxActivityState.Reconciling =>
                "combining histories",
            LynxActivityState.Success =>
                "operation completed",
            LynxActivityState.Warning =>
                "review required",
            LynxActivityState.Failure =>
                "operation failed",
            LynxActivityState.Resting =>
                "low activity",
            _ =>
                "activity cleared"
        };

    private void BeginDrag(
        object? sender,
        MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
            return;

        _dragging = true;
        _dragOrigin = Cursor.Position;
        _windowOrigin = Location;
    }

    private void ContinueDrag(
        object? sender,
        MouseEventArgs e)
    {
        if (!_dragging)
            return;

        var now = Cursor.Position;

        Location = new Point(
            _windowOrigin.X +
            now.X -
            _dragOrigin.X,
            _windowOrigin.Y +
            now.Y -
            _dragOrigin.Y);
    }

    private void EndDrag(
        object? sender,
        MouseEventArgs e)
    {
        _dragging = false;
    }
}
