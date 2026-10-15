namespace ZomniverseGitPet;

internal sealed class ChatBirthForm : Form
{
    private readonly NumericUpDown _generation = new();
    private readonly ComboBox _previousOutcome = new();
    private readonly TextBox _previousCommit = new();
    private readonly TextBox _notes = new();

    public int Generation => (int)_generation.Value;
    public ChatSessionOutcome? PreviousOutcome => _previousOutcome.SelectedIndex <= 0
        ? null
        : (ChatSessionOutcome?)_previousOutcome.SelectedItem;
    public string PreviousCommit => _previousCommit.Text.Trim();
    public string Notes => _notes.Text.Trim();

    public ChatBirthForm(int nextGeneration, bool hasActiveSession, string currentHead)
    {
        Text = "Register new coding chat";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(590, 390);
        BackColor = GuardianTheme.Window;
        ForeColor = GuardianTheme.Ink;
        Font = new Font("Segoe UI", 9.5f);
        WindowChrome.ApplyGuardianChrome(this);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 2, RowCount = 7, BackColor = GuardianTheme.Window };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 165));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        for (var i = 1; i < 6; i++) root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var heading = new Label { Text = "NEW CHAT BIRTH", ForeColor = GuardianTheme.Violet, Font = new Font("Segoe UI", 12, FontStyle.Bold), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        root.Controls.Add(heading, 0, 0); root.SetColumnSpan(heading, 2);
        AddRow(root, 1, "Generation", _generation);
        _generation.Minimum = nextGeneration; _generation.Maximum = 9999; _generation.Value = nextGeneration;

        _previousOutcome.DropDownStyle = ComboBoxStyle.DropDownList;
        _previousOutcome.Items.Add("Not supplied (ABANDONED)");
        _previousOutcome.Items.AddRange([ChatSessionOutcome.CleanHandoff, ChatSessionOutcome.HardHang, ChatSessionOutcome.MaxLength, ChatSessionOutcome.Abandoned]);
        _previousOutcome.SelectedIndex = 0;
        _previousOutcome.Enabled = hasActiveSession;
        AddRow(root, 2, "Previous outcome", _previousOutcome);

        _previousCommit.Text = currentHead;
        _previousCommit.Enabled = hasActiveSession;
        AddRow(root, 3, "Previous death anchor", _previousCommit);
        AddRow(root, 4, "Notes (optional)", _notes);

        var explanation = new Label
        {
            Text = hasActiveSession
                ? "The active previous generation will be closed. If no outcome is selected, GitPet conservatively records ABANDONED. Commits are history anchors; they are never described as dead."
                : "Manual recovery birth. Normal coding agents should announce birth automatically from their first conversational turn; GitPet never infers hidden token counts or context remaining.",
            ForeColor = GuardianTheme.MutedInk,
            Dock = DockStyle.Fill,
            AutoSize = false
        };
        root.Controls.Add(explanation, 0, 5); root.SetColumnSpan(explanation, 2);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 12, 0, 0) };
        var register = MakeButton("Register birth", DialogResult.OK, GuardianTheme.Violet);
        var cancel = MakeButton("Cancel", DialogResult.Cancel, GuardianTheme.MutedInk);
        buttons.Controls.Add(register); buttons.Controls.Add(cancel);
        root.Controls.Add(buttons, 0, 6); root.SetColumnSpan(buttons, 2);
        AcceptButton = register; CancelButton = cancel;
        Controls.Add(root);
    }

    private static void AddRow(TableLayoutPanel root, int row, string label, Control control)
    {
        root.Controls.Add(new Label { Text = label, ForeColor = GuardianTheme.SoftInk, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        control.Dock = DockStyle.Fill; control.Margin = new Padding(0, 7, 0, 7); control.BackColor = GuardianTheme.SurfaceRaised; control.ForeColor = GuardianTheme.Ink;
        root.Controls.Add(control, 1, row);
    }

    private static Button MakeButton(string text, DialogResult result, Color color)
    {
        var button = new Button { Text = text, DialogResult = result, Width = 128, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = GuardianTheme.SurfaceRaised, ForeColor = color };
        button.FlatAppearance.BorderColor = color;
        return button;
    }
}

internal sealed class ChatVitalityHistoryForm : Form
{
    public ChatVitalityHistoryForm(string repositoryName, IReadOnlyList<ChatVitalitySnapshot> history)
    {
        Text = "Chat Vitality history";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1100, 520);
        MinimumSize = new Size(820, 420);
        BackColor = GuardianTheme.Window;
        ForeColor = GuardianTheme.Ink;
        WindowChrome.ApplyGuardianChrome(this);

        var title = new Label { Dock = DockStyle.Top, Height = 66, Padding = new Padding(20, 16, 0, 0), Text = $"CHAT VITALITY — {repositoryName.ToUpperInvariant()}\r\nConversation birth + observable repository activity", ForeColor = GuardianTheme.Violet, Font = new Font("Cascadia Mono", 10, FontStyle.Bold) };
        var grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = GuardianTheme.Window, BorderStyle = BorderStyle.None, RowHeadersVisible = false, EnableHeadersVisualStyles = false };
        grid.ColumnHeadersDefaultCellStyle.BackColor = GuardianTheme.SurfaceRaised;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = GuardianTheme.SoftInk;
        grid.DefaultCellStyle.BackColor = GuardianTheme.Surface;
        grid.DefaultCellStyle.ForeColor = GuardianTheme.Ink;
        grid.DefaultCellStyle.SelectionBackColor = GuardianTheme.SurfaceSoft;
        grid.DefaultCellStyle.SelectionForeColor = GuardianTheme.Ink;
        grid.Columns.Add("generation", "GENERATION"); grid.Columns.Add("born", "BORN"); grid.Columns.Add("contact", "FIRST REPO");
        grid.Columns.Add("turns", "TURNS"); grid.Columns.Add("ended", "ENDED"); grid.Columns.Add("duration", "DURATION");
        grid.Columns.Add("outcome", "OUTCOME"); grid.Columns.Add("birth", "BIRTH COMMIT"); grid.Columns.Add("death", "DEATH ANCHOR");
        grid.Columns.Add("risk", "FINAL RISK");
        foreach (var item in history)
        {
            var s = item.Session;
            grid.Rows.Add(
                s.Generation,
                s.FirstTurnAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                s.FirstRepoContactAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "—",
                s.TotalTurns,
                s.EndedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "ACTIVE",
                $"{s.ElapsedMinutes} min",
                FormatOutcome(s.Outcome),
                Short(s.BirthCommit),
                Short(s.DeathAnchorCommit),
                $"{s.RiskScore} · {item.RiskClass}");
        }
        Controls.Add(grid); Controls.Add(title);
    }

    private static string Short(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value.Length <= 8 ? value : value[..8];
    private static string FormatOutcome(ChatSessionOutcome outcome) => outcome switch
    {
        ChatSessionOutcome.CleanHandoff => "CLEAN_HANDOFF",
        ChatSessionOutcome.HardHang => "HARD_HANG",
        ChatSessionOutcome.MaxLength => "MAX_LENGTH",
        ChatSessionOutcome.Abandoned => "ABANDONED",
        _ => "ACTIVE"
    };
}
