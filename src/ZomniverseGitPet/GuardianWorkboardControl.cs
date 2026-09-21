namespace ZomniverseGitPet;

/* ==========================================================================
   PATCH: GUARDIAN FOUR-PANEL WORKBOARD
   DATE.TIME: 2026-09-11 12:42 +03:00
   Show Save, Get, Send, Reconcile state before operations.
   ========================================================================== */
internal sealed class GuardianWorkboardControl : UserControl
{
    private readonly WorkboardSection _save;
    private readonly WorkboardSection _get;
    private readonly WorkboardSection _send;
    private readonly WorkboardSection _reconcile;

    public GuardianWorkboardControl(DataGridView saveGrid)
    {
        Name = "GuardianWorkboard";
        Dock = DockStyle.Fill;
        BackColor = GuardianTheme.Window;
        Padding = new Padding(2);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = GuardianTheme.Window,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        _save = new WorkboardSection(
            "SAVE — CHANGES ON THIS PC",
            GuardianTheme.Save,
            GuardianTheme.SurfaceSoft,
            saveGrid);
        _get = new WorkboardSection(
            "GET — WAITING TO COME IN",
            GuardianTheme.Get,
            GuardianTheme.SurfaceSoft);
        _send = new WorkboardSection(
            "SEND — SAVED UPDATES",
            GuardianTheme.Send,
            GuardianTheme.SurfaceSoft);
        _reconcile = new WorkboardSection(
            "RECONCILE — HISTORIES",
            GuardianTheme.Reconcile,
            GuardianTheme.SurfaceSoft);

        layout.Controls.Add(_save, 0, 0);
        layout.Controls.Add(_get, 1, 0);
        layout.Controls.Add(_send, 0, 1);
        layout.Controls.Add(_reconcile, 1, 1);
        Controls.Add(layout);
    }

    public void ApplySnapshot(GuardianWorkboardSnapshot snapshot)
    {
        _save.SetRows(
            snapshot.SaveRows,
            snapshot.SaveRows.Count == 0 ? "CLEAR" : $"{snapshot.SaveRows.Count} TO SAVE",
            snapshot.SaveEmptyText);

        _get.SetRows(
            snapshot.GetRows,
            snapshot.GetRows.Count == 0 ? "CLEAR" : $"{CountProjectedFiles(snapshot.GetRows)} INCOMING",
            snapshot.GetEmptyText);

        var sendBadge = snapshot.SendRows.Count == 0
            ? "CLEAR"
            : snapshot.SendCommitCount > 0
                ? $"{snapshot.SendCommitCount} COMMIT{Plural(snapshot.SendCommitCount)} · {snapshot.SendFileCount} FILE{Plural(snapshot.SendFileCount)}"
                : $"{snapshot.SendFileCount} FILE{Plural(snapshot.SendFileCount)}";
        _send.SetRows(snapshot.SendRows, sendBadge, snapshot.SendEmptyText);

        var reconcileCount = CountProjectedFiles(snapshot.ReconcileRows);
        var reconcileBadge = snapshot.ReconcileRows.Count == 0
            ? snapshot.Diverged || snapshot.ReconciliationPending ? "ATTENTION" : "CLEAR"
            : $"{reconcileCount} ITEM{Plural(reconcileCount)}";
        _reconcile.SetAttention(snapshot.Diverged || snapshot.ReconciliationPending);
        _reconcile.SetRows(snapshot.ReconcileRows, reconcileBadge, snapshot.ReconcileEmptyText);
    }

    public void SetBusy(bool busy)
    {
        if (!busy) return;
        if (_save.HasOperationState || _get.HasOperationState || _send.HasOperationState || _reconcile.HasOperationState) return;
        _save.SetTransientBadge("WORKING…");
        _get.SetTransientBadge("WORKING…");
        _send.SetTransientBadge("WORKING…");
        _reconcile.SetTransientBadge("WORKING…");
    }

    public void SetOperationState(SaveOperationVisualState state)
    {
        var section = state.Operation switch
        {
            GuardianOperationKind.Get => _get,
            GuardianOperationKind.Send => _send,
            GuardianOperationKind.Reconcile => _reconcile,
            _ => _save
        };
        section.SetOperationState(state);
    }

    internal static string? SaveBadgeFor(SaveOperationPhase phase) => phase switch
    {
        SaveOperationPhase.Preparing => "PREPARING SAVE…",
        SaveOperationPhase.CheckingPathSupport => "CHECKING PATHS…",
        SaveOperationPhase.Staging => "STAGING…",
        SaveOperationPhase.CreatingCheckpoint => "CREATING SAVE…",
        SaveOperationPhase.Completed => "SAVED ✓",
        SaveOperationPhase.Warning => "ATTENTION",
        SaveOperationPhase.Failed => "FAILED",
        SaveOperationPhase.Cancelled => "CANCELLED",
        _ => null
    };

    internal static string? OperationBadgeFor(GuardianOperationKind operation, SaveOperationPhase phase)
    {
        if (operation == GuardianOperationKind.Save) return SaveBadgeFor(phase);
        return phase switch
        {
            SaveOperationPhase.Preparing => "PREPARING…",
            SaveOperationPhase.CheckingPathSupport => "CHECKING…",
            SaveOperationPhase.Staging or SaveOperationPhase.CreatingCheckpoint => "WORKING…",
            SaveOperationPhase.Completed => operation == GuardianOperationKind.Get ? "RECEIVED ✓" :
                operation == GuardianOperationKind.Send ? "SENT ✓" : "READY ✓",
            SaveOperationPhase.Warning => "ATTENTION",
            SaveOperationPhase.Failed => "FAILED",
            SaveOperationPhase.Cancelled => "CANCELLED",
            _ => null
        };
    }

    private static int CountProjectedFiles(IEnumerable<GuardianWorkboardRow> rows) =>
        rows.Count(row => !row.IsCommit && row.State != "MORE");

    private static string Plural(int count) => count == 1 ? "" : "S";

    private sealed class WorkboardSection : Panel
    {
        private readonly Label _title = new();
        private readonly Label _badge = new();
        private readonly Label _empty = new();
        private readonly DataGridView _grid;
        private readonly Color _normalAccent;
        private readonly Color _normalFill;
        private readonly Panel _header;
        private readonly SaveProgressRing _progressRing = new();
        private readonly Font _commitFont = new("Cascadia Mono", 8.25f);
        private bool _attention;
        private string _normalBadge = "CLEAR";
        private SaveOperationVisualState _operationState =
            new(SaveOperationPhase.Idle, "Ready", DateTimeOffset.UtcNow);
        public bool HasActiveOperation => _operationState.IsActive;
        public bool HasOperationState => _operationState.Phase != SaveOperationPhase.Idle;

        public WorkboardSection(
            string title,
            Color accent,
            Color headerFill,
            DataGridView? existingGrid = null)
        {
            Dock = DockStyle.Fill;
            Margin = new Padding(1);
            Padding = new Padding(1);
            BackColor = GuardianTheme.BorderSoft;

            _normalAccent = accent;
            _normalFill = headerFill;

            _header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 31,
                BackColor = headerFill,
                Padding = new Padding(10, 0, 8, 0)
            };

            _title.Dock = DockStyle.Fill;
            _title.Text = title;
            _title.ForeColor = accent;
            _title.Font = new Font("Cascadia Mono", 8.25f, FontStyle.Bold);
            _title.TextAlign = ContentAlignment.MiddleLeft;
            _title.AutoEllipsis = true;

            _badge.Dock = DockStyle.Right;
            _badge.Width = 150;
            _badge.Text = "CLEAR";
            _badge.ForeColor = accent;
            _badge.Font = new Font("Cascadia Mono", 7.5f, FontStyle.Bold);
            _badge.TextAlign = ContentAlignment.MiddleRight;

            _progressRing.Dock = DockStyle.Right;
            _progressRing.Width = 28;
            _progressRing.Visible = false;

            _header.Controls.Add(_title);
            _header.Controls.Add(_badge);
            _header.Controls.Add(_progressRing);

            var body = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = GuardianTheme.Surface,
                Padding = new Padding(0)
            };

            _grid = existingGrid ?? CreateGrid();
            _grid.Dock = DockStyle.Fill;

            _empty.Dock = DockStyle.Fill;
            _empty.BackColor = GuardianTheme.Surface;
            _empty.ForeColor = GuardianTheme.FaintInk;
            _empty.Font = new Font("Cascadia Mono", 8f);
            _empty.TextAlign = ContentAlignment.MiddleCenter;
            _empty.Padding = new Padding(18, 6, 18, 6);

            body.Controls.Add(_grid);
            body.Controls.Add(_empty);
            Controls.Add(body);
            Controls.Add(_header);
        }

        public void SetRows(
            IReadOnlyList<GuardianWorkboardRow> rows,
            string badge,
            string emptyText)
        {
            _grid.SuspendLayout();
            try
            {
                _grid.Rows.Clear();
                foreach (var row in rows)
                {
                    var rowIndex = _grid.Rows.Add(row.State, row.Path);
                    var gridRow = _grid.Rows[rowIndex];
                    gridRow.Cells[0].Style.ForeColor = StateColor(row);
                    gridRow.Cells[0].ToolTipText = row.Detail;
                    gridRow.Cells[1].ToolTipText = string.IsNullOrWhiteSpace(row.Detail)
                        ? row.Path
                        : $"{row.Path}\n\n{row.Detail}";
                    if (row.IsCommit)
                    {
                        gridRow.DefaultCellStyle.BackColor = GuardianTheme.SurfaceRaised;
                        gridRow.DefaultCellStyle.Font = _commitFont;
                    }
                }
            }
            finally
            {
                _grid.ResumeLayout();
            }

            _normalBadge = badge;
            _badge.Text = badge;
            _empty.Text = emptyText;
            var hasRows = rows.Count > 0;
            _grid.Visible = hasRows;
            _empty.Visible = !hasRows;
            if (!hasRows) _empty.BringToFront();

            ApplyTone();
            if (_operationState.Phase != SaveOperationPhase.Idle)
                ApplyOperationTone(_operationState.Phase);
        }

        public void SetTransientBadge(string text)
        {
            _badge.Text = text;
        }

        public void SetOperationState(SaveOperationVisualState state)
        {
            _operationState = state;
            _progressRing.Active = state.IsActive;
            _progressRing.Visible = state.IsActive;
            _badge.Text = OperationBadgeFor(state.Operation, state.Phase) ?? _normalBadge;
            _attention = state.Phase is SaveOperationPhase.Warning or SaveOperationPhase.Failed;
            ApplyOperationTone(state.Phase);
        }

        public void SetAttention(bool attention)
        {
            _attention = attention;
            ApplyTone();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _commitFont.Dispose();
            base.Dispose(disposing);
        }

        private void ApplyTone()
        {
            var accent = _attention ? GuardianTheme.Warning : _normalAccent;
            var fill = _attention ? GuardianTheme.WarningFill : _normalFill;
            ApplyHeaderTone(accent, fill);
        }

        private void ApplyOperationTone(SaveOperationPhase phase)
        {
            if (phase == SaveOperationPhase.Completed)
                ApplyHeaderTone(GuardianTheme.Healthy, GuardianTheme.HealthyFill);
            else if (phase == SaveOperationPhase.Cancelled)
                ApplyHeaderTone(GuardianTheme.Changes, GuardianTheme.ChangesFill);
            else
                ApplyTone();
        }

        private void ApplyHeaderTone(Color accent, Color fill)
        {
            _header.BackColor = fill;
            _title.ForeColor = accent;
            _badge.ForeColor = accent;
        }

        private Color StateColor(GuardianWorkboardRow row)
        {
            if (row.State is "CONFLICT" or "BOTH SIDES") return GuardianTheme.Warning;
            if (row.State is "LOCAL" or "REMOTE") return GuardianTheme.Changes;
            if (row.IsCommit) return GuardianTheme.HotPinkSoft;
            return _normalAccent;
        }

        private static DataGridView CreateGrid()
        {
            var grid = new DataGridView
            {
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = GuardianTheme.Surface,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor = GuardianTheme.BorderSoft,
                RowHeadersVisible = false,
                EnableHeadersVisualStyles = false,
                ColumnHeadersHeight = 28,
                ShowCellToolTips = true
            };

            grid.ColumnHeadersDefaultCellStyle.BackColor = GuardianTheme.SurfaceSoft;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = GuardianTheme.MutedInk;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Cascadia Mono", 7.25f, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = GuardianTheme.SurfaceSoft;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = GuardianTheme.MutedInk;
            grid.DefaultCellStyle.BackColor = GuardianTheme.Surface;
            grid.DefaultCellStyle.ForeColor = GuardianTheme.Ink;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(21, 28, 35);
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.DefaultCellStyle.Font = new Font("Cascadia Mono", 8f);
            grid.DefaultCellStyle.Padding = new Padding(5, 1, 5, 1);
            grid.RowTemplate.Height = 26;

            grid.Columns.Add("State", "STATE");
            grid.Columns.Add("Path", "ITEM");
            grid.Columns[0].FillWeight = 26;
            grid.Columns[1].FillWeight = 74;
            grid.Columns[0].DefaultCellStyle.Font = new Font("Cascadia Mono", 7.5f, FontStyle.Bold);
            return grid;
        }
    }

    private sealed class SaveProgressRing : Control
    {
        private readonly System.Windows.Forms.Timer _timer = new() { Interval = 70 };
        private int _angle;

        public bool Active
        {
            get => _timer.Enabled;
            set
            {
                if (value) _timer.Start(); else _timer.Stop();
                Invalidate();
            }
        }

        public SaveProgressRing()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            _timer.Tick += (_, _) => { _angle = (_angle + 24) % 360; Invalidate(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (!Active) return;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var size = Math.Min(16, Math.Min(Width - 6, Height - 6));
            var bounds = new Rectangle((Width - size) / 2, (Height - size) / 2, size, size);
            using var track = new Pen(Color.FromArgb(70, GuardianTheme.Violet), 2.2f);
            using var arc = new Pen(GuardianTheme.Violet, 2.2f)
            {
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap = System.Drawing.Drawing2D.LineCap.Round
            };
            e.Graphics.DrawEllipse(track, bounds);
            e.Graphics.DrawArc(arc, bounds, _angle, 105);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _timer.Dispose();
            base.Dispose(disposing);
        }
    }
}
