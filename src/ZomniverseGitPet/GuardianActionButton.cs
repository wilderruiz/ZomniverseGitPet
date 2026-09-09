using System.Drawing.Drawing2D;

namespace ZomniverseGitPet;

internal enum GuardianActionKind
{
    Standard,
    Primary,
    Pull,
    Push,
    Danger
}

internal sealed class GuardianActionButton : Button
{
    private enum SyncRole
    {
        None,
        Save,
        Get,
        Send
    }

    private bool _hovered;
    private bool _pressed;
    /*
    PATCH: OPTIONAL SYNC BUTTON BEHAVIOUR
    DATE: 2026-09-09
    Allow styled buttons without sync counters.
    */
    private bool _applyingSyncState;
    private bool _syncStateAware = true;
    private GuardianActionKind _kind;
    private SyncRole _syncRole;
    private int _badgeCount;

    /*
    PATCH: SYNC STYLE OPT-OUT
    DATE: 2026-09-09
    Disable counters while preserving GitPet styling.
    */
    public bool SyncStateAware
    {
        get => _syncStateAware;
        set
        {
            _syncStateAware = value;

            if (!value)
            {
                _syncRole = SyncRole.None;
                BadgeCount = 0;
            }

            Invalidate();
        }
    }    
    public GuardianActionKind Kind
    {
        get => _kind;
        set { _kind = value; Invalidate(); }
    }

    public int BadgeCount
    {
        get => _badgeCount;
        private set
        {
            var normalized = Math.Max(0, value);
            if (_badgeCount == normalized) return;
            _badgeCount = normalized;
            Invalidate();
        }
    }

    public GuardianActionButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.UserPaint, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        ForeColor = GuardianTheme.Ink;
        Font = new Font("Segoe UI", 9, FontStyle.Bold);
        Cursor = Cursors.Hand;
        TabStop = true;
        Height = 38;

        GuardianSyncState.Changed += OnSyncStateChanged;
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        /*
        PATCH: IGNORE DECORATIVE ACTION BUTTONS
        DATE: 2026-09-09
        Skip sync logic for decorative buttons.
        */
        if (!_syncStateAware || _applyingSyncState) return;        
        if (_syncRole == SyncRole.None)
        {
            if (Text.Equals("Save", StringComparison.OrdinalIgnoreCase)) _syncRole = SyncRole.Save;
            else if (Text.StartsWith("Get", StringComparison.OrdinalIgnoreCase)) _syncRole = SyncRole.Get;
            else if (Text.StartsWith("Send", StringComparison.OrdinalIgnoreCase)) _syncRole = SyncRole.Send;
        }
        ApplySyncState();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplySyncState();
    }

    protected override async void OnClick(EventArgs e)
    {
        var snapshot = GuardianSyncState.Current;
        var owner = FindForm();

        if (Text.Equals("Refresh", StringComparison.OrdinalIgnoreCase))
        {
            await GuardianSyncState.RefreshAsync(true);
            base.OnClick(e);
            return;
        }

        if (_syncRole == SyncRole.Get)
        {
            if (!snapshot.HasRemote)
            {
                await GuardianSyncState.ConnectOriginAsync(owner);
                return;
            }

            if (snapshot.ReconciliationPending)
            {
                await GuardianReconciliation.CancelAsync(owner);
                return;
            }

            if (snapshot.Diverged)
            {
                await GuardianReconciliation.BeginAsync(owner);
                return;
            }
        }
        else if (_syncRole == SyncRole.Save && snapshot.ReconciliationPending)
        {
            await GuardianReconciliation.SaveAsync(owner);
            return;
        }

        base.OnClick(e);
        if (_syncRole != SyncRole.None) _ = RefreshAfterOperationAsync();
    }

    private async Task RefreshAfterOperationAsync()
    {
        for (var attempt = 0; attempt < 1200 && !IsDisposed; attempt++)
        {
            await Task.Delay(250);
            if (OperationInProgress()) continue;

            try
            {
                await GuardianSyncState.RefreshAsync(true);
            }
            catch
            {
                // Keep the last known counters; the next watcher/manual Refresh will retry.
            }
            return;
        }
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
        _pressed = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        base.OnMouseDown(mevent);
        if (mevent.Button == MouseButtons.Left)
        {
            _pressed = true;
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        base.OnMouseUp(mevent);
        _pressed = false;
        Invalidate();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();

        if (_applyingSyncState || _syncRole == SyncRole.None || !Enabled || !IsHandleCreated) return;
        try
        {
            BeginInvoke((Action)ApplySyncState);
        }
        catch (InvalidOperationException)
        {
        }
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

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? GuardianTheme.Surface);

        /*
        PATCH: CORNER BADGE OVERHANG
        DATE: 2026-09-09
        Inset button body; badge floats above corner.
        */
        var bounds = new RectangleF(
            1.5f,
            5.5f,
            Math.Max(1, Width - 3),
            Math.Max(1, Height - 7));        
        using var path = GuardianTheme.RoundedRectangle(bounds, 11f);

        var (fill, border, text) = Palette();
        using var fillBrush = new SolidBrush(fill);
        using var borderPen = new Pen(border,
            Kind is GuardianActionKind.Pull or GuardianActionKind.Push or GuardianActionKind.Primary ? 1.8f : 1.2f);

        e.Graphics.FillPath(fillBrush, path);
        e.Graphics.DrawPath(borderPen, path);

        var textBounds = Rectangle.Round(bounds);
        if (BadgeCount > 0) textBounds.Width = Math.Max(1, textBounds.Width - 14);
        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            textBounds,
            text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        DrawBadge(e.Graphics);

        if (Focused && ShowFocusCues)
        {
            var focus = Rectangle.Inflate(Rectangle.Round(bounds), -5, -5);
            ControlPaint.DrawFocusRectangle(e.Graphics, focus, GuardianTheme.Ink, fill);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) GuardianSyncState.Changed -= OnSyncStateChanged;
        base.Dispose(disposing);
    }

    private void OnSyncStateChanged(object? sender, EventArgs e)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            if (!IsHandleCreated) return;
            try { BeginInvoke((Action)ApplySyncState); } catch (InvalidOperationException) { }
            return;
        }

        ApplySyncState();
    }

    private void ApplySyncState()
    {
        if (_syncRole == SyncRole.None || IsDisposed) return;

        var snapshot = GuardianSyncState.Current;
        var desiredEnabled = false;
        var desiredText = Text;
        var desiredWidth = Width;
        var badge = 0;

        switch (_syncRole)
        {
            case SyncRole.Save:
                desiredText = "Save";
                desiredWidth = 92;
                badge = snapshot.ReconciliationPending
                    ? Math.Max(1, snapshot.Unsaved)
                    : snapshot.Unsaved;
                desiredEnabled = snapshot.HasRepository &&
                                 (snapshot.Unsaved > 0 || snapshot.ReconciliationPending);
                break;

            case SyncRole.Get:
                if (!snapshot.HasRepository)
                {
                    desiredText = "Get ↓";
                    desiredWidth = 92;
                }
                else if (!snapshot.HasRemote)
                {
                    desiredText = "Connect ↗";
                    desiredWidth = 108;
                    desiredEnabled = true;
                }
                else if (snapshot.ReconciliationPending)
                {
                    desiredText = "Cancel ↺";
                    desiredWidth = 108;
                    desiredEnabled = true;
                }
                else if (snapshot.Diverged)
                {
                    desiredText = "Reconcile ↕";
                    desiredWidth = 120;
                    badge = snapshot.Behind;
                    desiredEnabled = snapshot.OnlineReachable && snapshot.Unsaved == 0;
                }
                else
                {
                    desiredText = "Get ↓";
                    desiredWidth = 92;
                    badge = snapshot.Behind;
                    desiredEnabled = snapshot.OnlineReachable &&
                                     snapshot.Unsaved == 0 &&
                                     snapshot.Ahead == 0 &&
                                     snapshot.Behind > 0;
                }
                break;

            case SyncRole.Send:
                desiredText = "Send ↑";
                desiredWidth = 92;
                badge = snapshot.Ahead;
                desiredEnabled = snapshot.HasRepository &&
                                 snapshot.HasRemote &&
                                 snapshot.OnlineReachable &&
                                 !snapshot.ReconciliationPending &&
                                 snapshot.Unsaved == 0 &&
                                 snapshot.Ahead > 0 &&
                                 snapshot.Behind == 0;
                break;
        }

        if (OperationInProgress()) desiredEnabled = false;

        _applyingSyncState = true;
        try
        {
            if (!Text.Equals(desiredText, StringComparison.Ordinal)) Text = desiredText;
            if (Width != desiredWidth) Width = desiredWidth;
            BadgeCount = badge;
            if (Enabled != desiredEnabled) Enabled = desiredEnabled;
            Cursor = desiredEnabled ? Cursors.Hand : Cursors.Default;
        }
        finally
        {
            _applyingSyncState = false;
        }
    }

    private bool OperationInProgress()
    {
        var form = FindForm();
        if (form is null) return false;
        return EnumerateControls(form)
            .OfType<GuardianActionButton>()
            .Any(button => button.Visible && button.Text.Equals("Cancel", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<Control> EnumerateControls(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var descendant in EnumerateControls(child)) yield return descendant;
        }
    }

    private void DrawBadge(Graphics graphics)
    {
        if (BadgeCount <= 0) return;

        var value = BadgeCount > 99 ? "99+" : BadgeCount.ToString();
        var size = value.Length > 2 ? 24f : 19f;
        /*
        PATCH: BADGE CORNER POSITION
        DATE: 2026-09-09
        Move badge upward and outward toward corner.
        */
        var x = Width - size - 0.5f;
        var y = 0f;
        var accent = _syncRole switch
        {
            SyncRole.Get => GuardianTheme.Info,
            SyncRole.Send => GuardianTheme.HotPink,
            _ => GuardianTheme.HotPinkSoft
        };
        var fill = Enabled ? accent : Color.FromArgb(150, accent);

        using var brush = new SolidBrush(fill);
        using var border = new Pen(Color.FromArgb(220, Color.White), 1f);
        graphics.FillEllipse(brush, x, y, size, size);
        graphics.DrawEllipse(border, x, y, size, size);
        using var badgeFont = new Font("Segoe UI", 7f, FontStyle.Bold);
        TextRenderer.DrawText(
            graphics,
            value,
            badgeFont,
            Rectangle.Round(new RectangleF(x, y, size, size)),
            Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    private (Color Fill, Color Border, Color Text) Palette()
    {
        if (!Enabled)
            return (Color.FromArgb(39, 34, 47), Color.FromArgb(64, 56, 75), Color.FromArgb(121, 112, 133));

        return Kind switch
        {
            GuardianActionKind.Primary => (
                _pressed ? GuardianTheme.VioletPressed : _hovered ? GuardianTheme.VioletHover : GuardianTheme.Violet,
                GuardianTheme.HotPinkSoft,
                Color.White),

            GuardianActionKind.Pull => (
                _pressed ? Color.FromArgb(32, 54, 91) : _hovered ? Color.FromArgb(40, 68, 111) : Color.FromArgb(28, 48, 80),
                GuardianTheme.Info,
                Color.White),

            GuardianActionKind.Push => (
                _pressed ? Color.FromArgb(78, 38, 112) : _hovered ? Color.FromArgb(106, 51, 146) : Color.FromArgb(79, 39, 111),
                GuardianTheme.HotPink,
                Color.White),

            GuardianActionKind.Danger => (
                _pressed ? Color.FromArgb(95, 34, 47) : _hovered ? Color.FromArgb(118, 43, 60) : Color.FromArgb(78, 31, 42),
                GuardianTheme.Warning,
                Color.White),

            _ => (
                _pressed ? Color.FromArgb(44, 35, 58) : _hovered ? Color.FromArgb(52, 42, 68) : GuardianTheme.SurfaceRaised,
                _hovered ? Color.FromArgb(101, 78, 132) : GuardianTheme.Border,
                GuardianTheme.Ink)
        };
    }
}
