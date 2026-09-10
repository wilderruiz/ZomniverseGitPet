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
            GuardianTheme.HotPinkSoft,
            Color.FromArgb(62, 31, 62),
            saveGrid);
        _get = new WorkboardSection(
            "GET — WAITING TO COME IN",
            GuardianTheme.Info,
            GuardianTheme.InfoFill);
        _send = new WorkboardSection(
            "SEND — SAVED UPDATES",
            GuardianTheme.Healthy,
            GuardianTheme.HealthyFill);
        _reconcile = new WorkboardSection(
            "RECONCILE — HISTORIES",
            GuardianTheme.Changes,
            GuardianTheme.ChangesFill);

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
        _save.SetTransientBadge("WORKING…");
        _get.SetTransientBadge("WORKING…");
        _send.SetTransientBadge("WORKING…");
        _reconcile.SetTransientBadge("WORKING…");
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
        private readonly Font _commitFont = new("Cascadia Mono", 8.25f);
        private bool _attention;

        public WorkboardSection(
            string title,
            Color accent,
            Color headerFill,
            DataGridView? existingGrid = null)
        {
            Dock = DockStyle.Fill;
            Margin = new Padding(5);
            Padding = new Padding(1);
            BackColor = GuardianTheme.BorderSoft;

            _normalAccent = accent;
            _normalFill = headerFill;

            _header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 34,
                BackColor = headerFill,
                Padding = new Padding(10, 0, 8, 0)
            };

            _title.Dock = DockStyle.Fill;
            _title.Text = title;
            _title.ForeColor = accent;
            _title.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            _title.TextAlign = ContentAlignment.MiddleLeft;
            _title.AutoEllipsis = true;

            _badge.Dock = DockStyle.Right;
            _badge.Width = 150;
            _badge.Text = "CLEAR";
            _badge.ForeColor = accent;
            _badge.Font = new Font("Segoe UI", 7.75f, FontStyle.Bold);
            _badge.TextAlign = ContentAlignment.MiddleRight;

            _header.Controls.Add(_title);
            _header.Controls.Add(_badge);

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
            _empty.Font = new Font("Segoe UI", 8.5f);
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
                        gridRow.DefaultCellStyle.BackColor = Color.FromArgb(31, 28, 38);
                        gridRow.DefaultCellStyle.Font = _commitFont;
                    }
                }
            }
            finally
            {
                _grid.ResumeLayout();
            }

            _badge.Text = badge;
            _empty.Text = emptyText;
            var hasRows = rows.Count > 0;
            _grid.Visible = hasRows;
            _empty.Visible = !hasRows;
            if (!hasRows) _empty.BringToFront();

            ApplyTone();
        }

        public void SetTransientBadge(string text)
        {
            _badge.Text = text;
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
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 7.5f, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = GuardianTheme.SurfaceSoft;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = GuardianTheme.MutedInk;
            grid.DefaultCellStyle.BackColor = GuardianTheme.Surface;
            grid.DefaultCellStyle.ForeColor = GuardianTheme.Ink;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(57, 42, 77);
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.DefaultCellStyle.Font = new Font("Segoe UI", 8.25f);
            grid.DefaultCellStyle.Padding = new Padding(5, 1, 5, 1);
            grid.RowTemplate.Height = 26;

            grid.Columns.Add("State", "STATE");
            grid.Columns.Add("Path", "ITEM");
            grid.Columns[0].FillWeight = 26;
            grid.Columns[1].FillWeight = 74;
            grid.Columns[0].DefaultCellStyle.Font = new Font("Segoe UI", 7.75f, FontStyle.Bold);
            return grid;
        }
    }
}
