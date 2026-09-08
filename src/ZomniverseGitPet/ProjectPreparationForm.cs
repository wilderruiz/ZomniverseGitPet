namespace ZomniverseGitPet;

internal sealed class ProjectPreparationForm : Form
{
    private static readonly Color Ink = Color.FromArgb(236, 231, 246);
    private static readonly Color Surface = Color.FromArgb(30, 23, 45);
    private static readonly Color PanelSurface = Color.FromArgb(45, 31, 66);
    private static readonly Color Purple = Color.FromArgb(112, 70, 180);
    private static readonly Color HotPink = Color.FromArgb(236, 70, 170);
    private readonly DataGridView _suggestions = new();
    private readonly IReadOnlyList<GitIgnoreSuggestion> _items;

    public ProjectPreparationForm(
        string folderPath,
        IReadOnlyList<GitIgnoreSuggestion> suggestions,
        bool initializeGit)
    {
        _items = suggestions;
        Text = initializeGit ? "Prepare project for Git" : "Repository hygiene";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(760, 560);
        BackColor = Surface;
        ForeColor = Ink;
        Font = new Font("Segoe UI", 9);

        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 64,
            Padding = new Padding(18, 14, 18, 6),
            Text = initializeGit ? "◇ PREPARE PROJECT FOR GIT" : "◇ GITIGNORE REVIEW",
            Font = new Font("Segoe UI", 13, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = PanelSurface
        };

        var intro = new Label
        {
            Dock = DockStyle.Top,
            Height = initializeGit ? 92 : 78,
            Padding = new Padding(18, 12, 18, 8),
            ForeColor = Color.FromArgb(214, 205, 230),
            BackColor = Surface,
            AutoEllipsis = true,
            Text = initializeGit
                ? $"{folderPath}\r\n\r\nZomniverseGitPet will initialize LOCAL Git metadata only. It will not create a remote, stage files, commit, or push. Review the optional ignore suggestions below first."
                : $"{folderPath}\r\n\r\nSelect only the rules you want appended to this repository's existing .gitignore. Existing content is preserved."
        };

        ConfigureSuggestionsGrid();
        foreach (var suggestion in suggestions)
        {
            var rowIndex = _suggestions.Rows.Add(
                suggestion.DefaultSelected,
                ConfidenceLabel(suggestion.Confidence),
                suggestion.Rule,
                suggestion.Description);
            _suggestions.Rows[rowIndex].Tag = suggestion;
        }

        if (suggestions.Count == 0)
        {
            _suggestions.Rows.Add(false, "—", "No suggestions", "No common generated, IDE, cache, or security-sensitive ignore candidates were detected in the sampled folder contents.");
            _suggestions.Rows[0].ReadOnly = true;
        }

        var note = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 58,
            Padding = new Padding(18, 8, 18, 6),
            Text = "High-confidence and security recommendations are preselected. Review-level suggestions stay unchecked because some projects intentionally commit those folders.",
            ForeColor = Color.FromArgb(183, 169, 207),
            BackColor = Surface
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 58,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(10),
            BackColor = PanelSurface
        };
        var accept = MakeActionButton(initializeGit ? "Prepare for Git" : "Add selected rules", true);
        var cancel = MakeActionButton("Cancel", false);
        accept.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        buttons.Controls.Add(accept);
        buttons.Controls.Add(cancel);
        AcceptButton = accept;
        CancelButton = cancel;

        Controls.Add(_suggestions);
        Controls.Add(note);
        Controls.Add(buttons);
        Controls.Add(intro);
        Controls.Add(title);
    }

    public IReadOnlyList<string> AcceptedRules
    {
        get
        {
            if (_items.Count == 0) return [];
            var rules = new List<string>();
            foreach (DataGridViewRow row in _suggestions.Rows)
            {
                if (row.Tag is not GitIgnoreSuggestion suggestion) continue;
                if (Convert.ToBoolean(row.Cells[0].Value)) rules.Add(suggestion.Rule);
            }
            return rules;
        }
    }

    private void ConfigureSuggestionsGrid()
    {
        _suggestions.Dock = DockStyle.Fill;
        _suggestions.Margin = new Padding(18);
        _suggestions.BackgroundColor = Color.FromArgb(25, 19, 38);
        _suggestions.BorderStyle = BorderStyle.None;
        _suggestions.RowHeadersVisible = false;
        _suggestions.AllowUserToAddRows = false;
        _suggestions.AllowUserToDeleteRows = false;
        _suggestions.AllowUserToResizeRows = false;
        _suggestions.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _suggestions.EnableHeadersVisualStyles = false;
        _suggestions.ColumnHeadersHeight = 36;
        _suggestions.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(63, 43, 93);
        _suggestions.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        _suggestions.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
        _suggestions.DefaultCellStyle.BackColor = Color.FromArgb(34, 26, 50);
        _suggestions.DefaultCellStyle.ForeColor = Ink;
        _suggestions.DefaultCellStyle.SelectionBackColor = Color.FromArgb(73, 51, 105);
        _suggestions.DefaultCellStyle.SelectionForeColor = Color.White;
        _suggestions.GridColor = Color.FromArgb(58, 45, 78);
        _suggestions.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;

        var selected = new DataGridViewCheckBoxColumn
        {
            Name = "Use",
            HeaderText = "Use",
            FillWeight = 10,
            FlatStyle = FlatStyle.Flat
        };
        var confidence = new DataGridViewTextBoxColumn { Name = "Confidence", HeaderText = "Confidence", ReadOnly = true, FillWeight = 18 };
        var rule = new DataGridViewTextBoxColumn { Name = "Rule", HeaderText = ".gitignore rule", ReadOnly = true, FillWeight = 24 };
        var reason = new DataGridViewTextBoxColumn { Name = "Reason", HeaderText = "Why GitPet suggests it", ReadOnly = true, FillWeight = 48 };
        _suggestions.Columns.AddRange(selected, confidence, rule, reason);
    }

    private static Button MakeActionButton(string text, bool primary)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new Size(primary ? 132 : 90, 34),
            Height = 34,
            Margin = new Padding(6, 2, 0, 2),
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Purple : Color.FromArgb(65, 53, 83),
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };
        button.FlatAppearance.BorderSize = primary ? 2 : 1;
        button.FlatAppearance.BorderColor = primary ? HotPink : Color.FromArgb(110, 94, 132);
        return button;
    }

    private static string ConfidenceLabel(GitIgnoreConfidence confidence) => confidence switch
    {
        GitIgnoreConfidence.Security => "SECURITY",
        GitIgnoreConfidence.High => "HIGH",
        _ => "REVIEW"
    };
}
