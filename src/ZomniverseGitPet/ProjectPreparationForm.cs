namespace ZomniverseGitPet;

internal sealed class ProjectPreparationForm : Form
{
    private static readonly Color Ink = Color.FromArgb(236, 231, 246);
    private static readonly Color MutedInk = Color.FromArgb(188, 176, 208);
    private static readonly Color Surface = Color.FromArgb(30, 23, 45);
    private static readonly Color PanelSurface = Color.FromArgb(45, 31, 66);
    private static readonly Color PreviewSurface = Color.FromArgb(22, 17, 34);
    private static readonly Color Purple = Color.FromArgb(112, 70, 180);
    private static readonly Color HotPink = Color.FromArgb(236, 70, 170);

    private readonly DataGridView _suggestions = new();
    private readonly IReadOnlyList<GitIgnoreSuggestion> _items;
    private readonly string _folderPath;
    private readonly string _targetFilePath;
    private readonly RichTextBox _beforePreview = new();
    private readonly RichTextBox _afterPreview = new();
    private readonly Label _beforeHeader = new();
    private readonly Label _afterHeader = new();
    private readonly Label _selectionSummary = new();

    public ProjectPreparationForm(
        string folderPath,
        IReadOnlyList<GitIgnoreSuggestion> suggestions,
        bool initializeGit)
    {
        _items = suggestions;
        _folderPath = folderPath;
        _targetFilePath = Path.Combine(folderPath, ".gitignore");

        Text = initializeGit ? "Prepare project for Git" : "Choose what Git should ignore";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        ShowInTaskbar = false;
        MinimumSize = new Size(1050, 620);
        ClientSize = new Size(1220, 720);
        BackColor = Surface;
        ForeColor = Ink;
        Font = new Font("Segoe UI", 9);

        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 62,
            Padding = new Padding(20, 14, 20, 6),
            Text = initializeGit ? "◇ PREPARE THIS PROJECT SAFELY" : "◇ CHOOSE WHAT GIT SHOULD IGNORE",
            Font = new Font("Segoe UI", 13, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = PanelSurface
        };

        var intro = new Label
        {
            Dock = DockStyle.Top,
            Height = 126,
            Padding = new Padding(20, 12, 20, 10),
            ForeColor = Color.FromArgb(219, 211, 234),
            BackColor = Surface,
            AutoEllipsis = true,
            Text = initializeGit
                ? "GitPet can make this folder Git-ready without putting anything online.\r\n\r\n" +
                  "A .gitignore file is simply Git's ‘do not track these files’ list. Tick the suggestions that make sense for this project. " +
                  "The preview on the right shows the exact .gitignore file before and after. Nothing is written until you press Prepare project."
                : "Git does not need to watch every cache, generated file, or private machine setting.\r\n\r\n" +
                  "A .gitignore file is Git's ‘do not track these files’ list. GitPet found some likely candidates. Tick only what you want. " +
                  "The right side shows the exact file before and after, so you can see the change before approving it."
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
            _suggestions.Rows.Add(false, "—", "Nothing suggested", "GitPet did not find a common generated, cache, IDE, or privacy-related item to recommend here.");
            _suggestions.Rows[0].ReadOnly = true;
        }

        _suggestions.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_suggestions.IsCurrentCellDirty) _suggestions.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        _suggestions.CellValueChanged += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == 0) UpdatePreview();
        };

        var leftIntro = new Label
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 6, 8, 4),
            Text = "Pick the items Git should leave alone.\r\nPRIVACY and RECOMMENDED items start selected; CHECK FIRST items wait for you.",
            ForeColor = MutedInk,
            BackColor = Surface
        };

        _selectionSummary.Dock = DockStyle.Fill;
        _selectionSummary.Padding = new Padding(8, 8, 8, 4);
        _selectionSummary.ForeColor = Color.FromArgb(214, 198, 233);
        _selectionSummary.BackColor = Surface;

        var left = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Surface,
            Padding = new Padding(8)
        };
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        left.Controls.Add(leftIntro, 0, 0);
        left.Controls.Add(_suggestions, 0, 1);
        left.Controls.Add(_selectionSummary, 0, 2);

        ConfigurePreviewBox(_beforePreview);
        ConfigurePreviewBox(_afterPreview);
        ConfigurePreviewHeader(_beforeHeader);
        ConfigurePreviewHeader(_afterHeader);

        var target = new Label
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 8, 12, 6),
            ForeColor = Color.FromArgb(222, 211, 238),
            BackColor = PanelSurface,
            AutoEllipsis = true,
            Text = $"FILE GITPET MAY CHANGE\r\n{_targetFilePath}\r\nNothing is written until you approve the action below."
        };

        var right = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Surface,
            Padding = new Padding(8)
        };
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        right.Controls.Add(target, 0, 0);
        right.Controls.Add(CreatePreviewSection(_beforeHeader, _beforePreview), 0, 1);
        right.Controls.Add(CreatePreviewSection(_afterHeader, _afterPreview), 0, 2);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 8,
            SplitterDistance = 540,
            Panel1MinSize = 430,
            Panel2MinSize = 430,
            BackColor = Color.FromArgb(80, 57, 111),
            BorderStyle = BorderStyle.None
        };
        split.Panel1.Controls.Add(left);
        split.Panel2.Controls.Add(right);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 62,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12),
            BackColor = PanelSurface
        };
        var accept = MakeActionButton(initializeGit ? "Prepare project" : "Apply selected changes", true);
        var cancel = MakeActionButton("Cancel", false);
        accept.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        buttons.Controls.Add(accept);
        buttons.Controls.Add(cancel);
        AcceptButton = accept;
        CancelButton = cancel;

        Controls.Add(split);
        Controls.Add(buttons);
        Controls.Add(intro);
        Controls.Add(title);

        LoadPreviews();
        UpdatePreview();
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

    private void LoadPreviews()
    {
        try
        {
            var current = GitIgnoreAdvisor.ReadCurrentContent(_folderPath);
            _beforePreview.Text = current;
            _beforeHeader.Text = string.IsNullOrEmpty(current)
                ? "CURRENT .gitignore — no file yet / empty"
                : "CURRENT .gitignore — read only";
        }
        catch (Exception ex)
        {
            _beforeHeader.Text = "CURRENT .gitignore — preview unavailable";
            _beforePreview.Text = "GitPet could not read the existing .gitignore file.\r\n\r\n" + ex.Message;
        }
    }

    private void UpdatePreview()
    {
        var selected = AcceptedRules;
        _selectionSummary.Text = selected.Count == 0
            ? "Nothing selected — applying will leave .gitignore unchanged."
            : $"{selected.Count} suggestion(s) selected. Review the AFTER panel before approving.";

        try
        {
            var proposed = GitIgnoreAdvisor.BuildPreviewContent(_folderPath, selected);
            _afterPreview.Text = proposed;
            _afterHeader.Text = selected.Count == 0
                ? "AFTER — no changes selected"
                : "AFTER — exact proposed .gitignore";
        }
        catch (Exception ex)
        {
            _afterHeader.Text = "AFTER — preview unavailable";
            _afterPreview.Text = "GitPet could not build the proposed preview.\r\n\r\n" + ex.Message;
        }
    }

    private void ConfigureSuggestionsGrid()
    {
        _suggestions.Dock = DockStyle.Fill;
        _suggestions.BackgroundColor = PreviewSurface;
        _suggestions.BorderStyle = BorderStyle.None;
        _suggestions.RowHeadersVisible = false;
        _suggestions.AllowUserToAddRows = false;
        _suggestions.AllowUserToDeleteRows = false;
        _suggestions.AllowUserToResizeRows = false;
        _suggestions.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _suggestions.EnableHeadersVisualStyles = false;
        _suggestions.ColumnHeadersHeight = 38;
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
        var confidence = new DataGridViewTextBoxColumn { Name = "Confidence", HeaderText = "GitPet says", ReadOnly = true, FillWeight = 20 };
        var rule = new DataGridViewTextBoxColumn { Name = "Rule", HeaderText = "Ignore this", ReadOnly = true, FillWeight = 25 };
        var reason = new DataGridViewTextBoxColumn { Name = "Reason", HeaderText = "Why", ReadOnly = true, FillWeight = 45 };
        _suggestions.Columns.AddRange(selected, confidence, rule, reason);
    }

    private static void ConfigurePreviewBox(RichTextBox box)
    {
        box.Dock = DockStyle.Fill;
        box.ReadOnly = true;
        box.WordWrap = false;
        box.DetectUrls = false;
        box.BorderStyle = BorderStyle.None;
        box.BackColor = PreviewSurface;
        box.ForeColor = Color.FromArgb(235, 229, 246);
        box.Font = new Font("Cascadia Mono", 9f);
        box.ScrollBars = RichTextBoxScrollBars.Both;
    }

    private static void ConfigurePreviewHeader(Label label)
    {
        label.Dock = DockStyle.Top;
        label.Height = 34;
        label.Padding = new Padding(10, 8, 10, 4);
        label.BackColor = Color.FromArgb(55, 39, 79);
        label.ForeColor = Color.White;
        label.Font = new Font("Segoe UI", 9, FontStyle.Bold);
    }

    private static Control CreatePreviewSection(Label header, RichTextBox preview)
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = PreviewSurface, Margin = new Padding(0, 4, 0, 4) };
        panel.Controls.Add(preview);
        panel.Controls.Add(header);
        return panel;
    }

    private static Button MakeActionButton(string text, bool primary)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new Size(primary ? 150 : 90, 36),
            Height = 36,
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
        GitIgnoreConfidence.Security => "PRIVACY",
        GitIgnoreConfidence.High => "RECOMMENDED",
        _ => "CHECK FIRST"
    };
}
