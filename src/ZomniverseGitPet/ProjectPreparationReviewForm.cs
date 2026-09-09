namespace ZomniverseGitPet;

internal sealed class ProjectPreparationReviewForm : Form
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
    private readonly IReadOnlyList<string> _scopeRules;
    private readonly IReadOnlyList<GitIgnoreDocument> _ignoreDocuments;
    private readonly RichTextBox _beforePreview = new();
    private readonly RichTextBox _afterPreview = new();
    private readonly Label _beforeHeader = new();
    private readonly Label _afterHeader = new();
    private readonly Label _selectionSummary = new();

    public ProjectPreparationReviewForm(
        string folderPath,
        IReadOnlyList<GitIgnoreSuggestion> suggestions,
        bool initializeGit,
        IReadOnlyList<string>? scopeRules = null,
        IReadOnlyList<GitIgnoreDocument>? ignoreDocuments = null,
        string? scopeSummary = null)
    {
        _items = suggestions;
        _folderPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folderPath));
        _scopeRules = scopeRules ?? [];
        _ignoreDocuments = ignoreDocuments ?? [];

        Text = initializeGit ? "Prepare project for Git" : "Repository hygiene";
        Icon = AppIconProvider.Icon;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        ShowInTaskbar = false;
        MinimumSize = new Size(1120, 720);
        ClientSize = new Size(1380, 900);
        BackColor = Surface;
        ForeColor = Ink;
        Font = new Font("Segoe UI", 9);
        WindowChrome.ApplyGuardianChrome(this);

        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 68,
            Padding = new Padding(20, 14, 20, 7),
            Text = initializeGit ? "◇ PREPARE THIS PROJECT SAFELY" : "◇ REVIEW REPOSITORY HYGIENE",
            Font = new Font("Segoe UI", 13.5f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = PanelSurface
        };

        var intro = new Label
        {
            Dock = DockStyle.Top,
            Height = 132,
            Padding = new Padding(20, 14, 20, 12),
            ForeColor = Color.FromArgb(219, 211, 234),
            BackColor = Surface,
            Text = initializeGit
                ? "GitPet has your tracking scope. Now review what Git should ignore before the repository is created.\r\n\r\n" +
                  (scopeSummary ?? "The selected project scope will be written safely into the root .gitignore when needed.") + " " +
                  "Nested .gitignore files inside selected folders are shown on the right and stay untouched."
                : "GitPet found common generated, cache, IDE, or privacy-related items worth reviewing.\r\n\r\n" +
                  "Nested .gitignore files are shown too, so you can see rules that already apply inside subfolders. " +
                  "Only the project-root .gitignore is modified by this screen, and only after you approve it."
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
            _suggestions.Rows.Add(false, "—", "Nothing suggested", "GitPet did not find another common ignore candidate in the selected project scope.");
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
            Padding = new Padding(8, 8, 8, 6),
            Text = "PICK THE ITEMS GIT SHOULD LEAVE ALONE\r\n\r\n" +
                   "PRIVACY and RECOMMENDED items start selected. CHECK FIRST items wait for you. " +
                   "These suggestions are separate from the tracking scope you chose on the previous screen.",
            ForeColor = MutedInk,
            BackColor = Surface
        };

        _selectionSummary.Dock = DockStyle.Fill;
        _selectionSummary.Padding = new Padding(8, 10, 8, 6);
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
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
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
            Padding = new Padding(12, 10, 12, 8),
            ForeColor = Color.FromArgb(222, 211, 238),
            BackColor = PanelSurface,
            AutoEllipsis = true,
            Text =
                "ROOT FILE GITPET MAY CHANGE\r\n" + Path.Combine(_folderPath, ".gitignore") + "\r\n" +
                "Nested .gitignore files shown below are read-only here and remain exactly where they are."
        };

        var right = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Surface,
            Padding = new Padding(8)
        };
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 102));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        right.Controls.Add(target, 0, 0);
        right.Controls.Add(CreatePreviewSection(_beforeHeader, _beforePreview), 0, 1);
        right.Controls.Add(CreatePreviewSection(_afterHeader, _afterPreview), 0, 2);

        var split = new SafeSplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 8,
            PreferredRatio = 0.56,
            PreferredPaneMinimum = 360,
            BackColor = Color.FromArgb(80, 57, 111),
            BorderStyle = BorderStyle.None
        };
        split.Panel1.Controls.Add(left);
        split.Panel2.Controls.Add(right);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 66,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12, 13, 12, 9),
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

    public IReadOnlyList<string> ScopeRules => _scopeRules;

    private void LoadPreviews()
    {
        try
        {
            _beforePreview.Text = ScopedGitIgnoreAdvisor.BuildCurrentOverview(_folderPath, _ignoreDocuments);
            var nestedCount = _ignoreDocuments.Count(document => !document.RelativePath.Equals(".gitignore", StringComparison.OrdinalIgnoreCase));
            _beforeHeader.Text = nestedCount == 0
                ? "CURRENT IGNORE FILES — root only / none yet"
                : $"CURRENT IGNORE FILES — root + {nestedCount} nested";
        }
        catch (Exception ex)
        {
            _beforeHeader.Text = "CURRENT IGNORE FILES — preview unavailable";
            _beforePreview.Text = "GitPet could not read the current ignore files.\r\n\r\n" + ex.Message;
        }
    }

    private void UpdatePreview()
    {
        var selected = AcceptedRules;
        _selectionSummary.Text = selected.Count == 0
            ? _scopeRules.Count == 0
                ? "Nothing selected — applying will leave the root .gitignore unchanged."
                : $"Tracking scope is ready ({_scopeRules.Count} scope rule(s)); no extra hygiene suggestions selected."
            : $"{selected.Count} hygiene suggestion(s) selected" +
              (_scopeRules.Count > 0 ? $" + {_scopeRules.Count} tracking-scope rule(s)." : ".") +
              " Review the AFTER panel before approving.";

        try
        {
            _afterPreview.Text = ProjectGitIgnoreComposer.BuildPreview(_folderPath, _scopeRules, selected);
            _afterHeader.Text = "AFTER — exact proposed root .gitignore";
        }
        catch (Exception ex)
        {
            _afterHeader.Text = "AFTER — preview unavailable";
            _afterPreview.Text = "GitPet could not build the proposed root .gitignore preview.\r\n\r\n" + ex.Message;
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
        label.Height = 40;
        label.Padding = new Padding(10, 9, 10, 5);
        label.BackColor = Color.FromArgb(55, 39, 79);
        label.ForeColor = Color.White;
        label.Font = new Font("Segoe UI", 9, FontStyle.Bold);
    }

    private static Control CreatePreviewSection(Label header, RichTextBox preview)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = PreviewSurface,
            Margin = new Padding(0, 5, 0, 5)
        };
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
            MinimumSize = new Size(primary ? 150 : 90, 38),
            Height = 38,
            Margin = new Padding(6, 2, 0, 2),
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Purple : Color.FromArgb(65, 53, 83),
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderSize = primary ? 2 : 1;
        button.FlatAppearance.BorderColor = primary ? HotPink : Color.FromArgb(110, 94, 132);
        button.FlatAppearance.MouseOverBackColor = primary ? GuardianTheme.VioletHover : GuardianTheme.SurfaceRaised;
        return button;
    }

    private static string ConfidenceLabel(GitIgnoreConfidence confidence) => confidence switch
    {
        GitIgnoreConfidence.Security => "PRIVACY",
        GitIgnoreConfidence.High => "RECOMMENDED",
        _ => "CHECK FIRST"
    };
}
