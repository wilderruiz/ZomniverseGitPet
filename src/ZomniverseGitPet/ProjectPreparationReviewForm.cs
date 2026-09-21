namespace ZomniverseGitPet;

internal sealed class ProjectPreparationReviewForm : Form
{
    private static readonly Color Ink = Color.FromArgb(236, 231, 246);
    private static readonly Color MutedInk = Color.FromArgb(188, 176, 208);
    private static readonly Color Surface = Color.FromArgb(30, 23, 45);
    private static readonly Color PanelSurface = Color.FromArgb(45, 31, 66);
    private static readonly Color PreviewSurface = Color.FromArgb(22, 17, 34);
    private static readonly Color CurrentPreviewSurface = Color.FromArgb(17, 29, 43);
    private static readonly Color CurrentHeaderSurface = Color.FromArgb(38, 75, 115);
    private static readonly Color AfterPreviewSurface = Color.FromArgb(29, 20, 43);
    private static readonly Color AfterHeaderSurface = Color.FromArgb(83, 47, 118);
    private static readonly Color DuplicateHighlight = Color.FromArgb(111, 70, 31);
    private static readonly Color Purple = Color.FromArgb(112, 70, 180);
    private static readonly Color HotPink = Color.FromArgb(236, 70, 170);

    private readonly DataGridView _suggestions = new();
    private readonly DataGridView _ruleLibrary = new();
    private readonly IReadOnlyList<GitIgnoreSuggestion> _items;
    private readonly string _folderPath;
    private readonly IReadOnlyList<string> _scopeRules;
    private readonly IReadOnlyList<GitIgnoreDocument> _ignoreDocuments;
    private readonly bool _replaceScope;
    private readonly RichTextBox _beforePreview = new();
    private readonly RichTextBox _afterPreview = new();
    private readonly Label _beforeHeader = new();
    private readonly Label _afterHeader = new();
    private readonly Label _selectionSummary = new();
    private readonly ComboBox _customKind = new();
    private readonly TextBox _customValue = new();
    private Button _removeCustomButton = null!;
    private Button _cleanDuplicatesButton = null!;
    private IReadOnlyList<GitIgnoreDuplicateRule> _currentDuplicates = Array.Empty<GitIgnoreDuplicateRule>();

    public ProjectPreparationReviewForm(
        string folderPath,
        IReadOnlyList<GitIgnoreSuggestion> suggestions,
        bool initializeGit,
        IReadOnlyList<string>? scopeRules = null,
        IReadOnlyList<GitIgnoreDocument>? ignoreDocuments = null,
        string? scopeSummary = null,
        bool replaceScope = false)
    {
        _items = suggestions;
        _folderPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folderPath));
        _scopeRules = scopeRules ?? [];
        _ignoreDocuments = ignoreDocuments ?? [];
        _replaceScope = replaceScope;

        Text = initializeGit ? "Prepare project for Git" : replaceScope ? "Reconfigure project" : "Repository hygiene";
        Icon = AppIconProvider.Icon;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        ShowInTaskbar = false;
        MinimumSize = new Size(1180, 760);
        ClientSize = new Size(1460, 960);
        BackColor = Surface;
        ForeColor = Ink;
        Font = new Font("Segoe UI", 9);
        WindowChrome.ApplyGuardianChrome(this);

        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 58,
            Padding = new Padding(20, 11, 20, 6),
            Text = initializeGit ? "◇ PREPARE THIS PROJECT SAFELY" : "◇ REVIEW REPOSITORY HYGIENE",
            Font = new Font("Segoe UI", 13.5f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = PanelSurface
        };

        var intro = new Label
        {
            Dock = DockStyle.Top,
            Height = 102,
            Padding = new Padding(20, 11, 20, 8),
            ForeColor = Color.FromArgb(219, 211, 234),
            BackColor = Surface,
            Text = initializeGit
                ? "GitPet has your tracking scope. Now choose anything else Git should leave out before this repository is created.\r\n\r\n" +
                  (scopeSummary ?? "The selected project scope will be written safely into the root .gitignore when needed.") + " " +
                  "Nested .gitignore files remain in their own folders and are shown read-only on the right."
                : replaceScope
                    ? "GitPet has your updated tracking scope. Now review privacy, generated files, reusable presets, and any custom ignore rules.\r\n\r\n" +
                      "The AFTER pane shows the exact result, including replacement of GitPet's previous managed scope. Nested .gitignore files remain untouched."
                    : "Review detected hygiene suggestions, reusable ignore presets, and your own custom ignore rules.\r\n\r\n" +
                      "Nested .gitignore files are shown too. Only the project-root .gitignore is changed by this screen, and only after you approve it."
        };

        ConfigureSuggestionsGrid();
        PopulateSuggestions(suggestions);
        ConfigureRuleLibraryGrid();
        foreach (var option in GitIgnoreRuleLibrary.Presets) AddLibraryRow(option);

        WireCheckboxGrid(_suggestions);
        WireCheckboxGrid(_ruleLibrary);

        var detectedHeader = CreateSectionHeader(
            "DETECTED IN THIS PROJECT",
            "GitPet sampled the selected folders and found these project-specific candidates. PRIVACY and RECOMMENDED items start selected; CHECK FIRST waits for you.");

        var detectedHost = new Panel { Dock = DockStyle.Fill, BackColor = Surface, Padding = new Padding(6) };
        detectedHost.Controls.Add(_suggestions);
        detectedHost.Controls.Add(detectedHeader);

        var libraryHeader = CreateSectionHeader(
            "IGNORE LIBRARY + YOUR OWN RULES",
            "Tick reusable categories below. Hover a row to see exactly why GitPet suggests it and the exact Git patterns that will be added.");

        var customBar = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            ColumnCount = 5,
            Padding = new Padding(8, 9, 8, 7),
            BackColor = Color.FromArgb(38, 28, 55)
        };
        customBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        /*
        PATCH: POWERFUL CUSTOM IGNORE BUILDER
        DATE: 2026-09-09
        Widen matcher menu and expose more safe patterns.
        */
        customBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 215));
        customBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        customBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
        customBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));

        var customLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "CUSTOM RULE",
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = MutedInk,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
        };

        _customKind.Dock = DockStyle.Fill;
        _customKind.DropDownStyle = ComboBoxStyle.DropDownList;
        _customKind.DropDownWidth = 250;
        _customKind.BackColor = PreviewSurface;
        _customKind.ForeColor = Ink;
        foreach (CustomIgnoreRuleKind kind in Enum.GetValues<CustomIgnoreRuleKind>())
            _customKind.Items.Add(GitIgnoreRuleLibrary.KindLabel(kind));
        _customKind.SelectedIndexChanged += (_, _) => UpdateCustomBuilderUi();
        _customKind.SelectedIndex = 0;

        _customValue.Dock = DockStyle.Fill;
        _customValue.BackColor = PreviewSurface;
        _customValue.ForeColor = Ink;
        _customValue.BorderStyle = BorderStyle.FixedSingle;
        UpdateCustomBuilderUi();

        var addCustom = MakeActionButton("Add custom", true);
        addCustom.Dock = DockStyle.Fill;
        addCustom.Margin = new Padding(6, 0, 0, 0);
        addCustom.Click += (_, _) => AddCustomRule();
        _customValue.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            AddCustomRule();
        };

        _removeCustomButton = MakeActionButton("Remove custom", false);
        _removeCustomButton.Dock = DockStyle.Fill;
        _removeCustomButton.Margin = new Padding(6, 0, 0, 0);
        _removeCustomButton.Enabled = false;
        _removeCustomButton.Click += (_, _) => RemoveSelectedCustomRule();
        _ruleLibrary.SelectionChanged += (_, _) => UpdateRemoveCustomButtonState();
        _ruleLibrary.CellClick += (_, _) => UpdateRemoveCustomButtonState();

        customBar.Controls.Add(customLabel, 0, 0);
        customBar.Controls.Add(_customKind, 1, 0);
        customBar.Controls.Add(_customValue, 2, 0);
        customBar.Controls.Add(addCustom, 3, 0);
        customBar.Controls.Add(_removeCustomButton, 4, 0);

        var libraryHost = new Panel { Dock = DockStyle.Fill, BackColor = Surface, Padding = new Padding(6) };
        libraryHost.Controls.Add(_ruleLibrary);
        libraryHost.Controls.Add(customBar);
        libraryHost.Controls.Add(libraryHeader);

        var leftRows = new SafeSplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 8,
            PreferredRatio = 0.38,
            PreferredPaneMinimum = 170,
            BackColor = Color.FromArgb(80, 57, 111),
            BorderStyle = BorderStyle.None
        };
        leftRows.Panel1.Controls.Add(detectedHost);
        leftRows.Panel2.Controls.Add(libraryHost);

        _selectionSummary.Dock = DockStyle.Bottom;
        _selectionSummary.Height = 54;
        _selectionSummary.Padding = new Padding(12, 9, 12, 5);
        _selectionSummary.ForeColor = Color.FromArgb(214, 198, 233);
        _selectionSummary.BackColor = Surface;

        var left = new Panel { Dock = DockStyle.Fill, BackColor = Surface, Padding = new Padding(4) };
        left.Controls.Add(leftRows);
        left.Controls.Add(_selectionSummary);

        /*
        PATCH: DISTINCT CURRENT AND AFTER PREVIEWS
        DATE: 2026-09-09
        Use blue current and GitPet-purple proposed states.
        */
        ConfigurePreviewBox(_beforePreview, after: false);
        ConfigurePreviewBox(_afterPreview, after: true);
        ConfigurePreviewHeader(_beforeHeader, after: false);
        ConfigurePreviewHeader(_afterHeader, after: true);

        var target = new Label
        {
            Dock = DockStyle.Top,
            Height = 78,
            Padding = new Padding(12, 8, 12, 6),
            ForeColor = Color.FromArgb(222, 211, 238),
            BackColor = PanelSurface,
            AutoEllipsis = true,
            Text =
                "ROOT FILE GITPET MAY CHANGE\r\n" + Path.Combine(_folderPath, ".gitignore") + "\r\n" +
                "Nested .gitignore files below are read-only and remain in their own folders."
        };

        var previewRows = new SafeSplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 8,
            PreferredRatio = 0.5,
            PreferredPaneMinimum = 170,
            BackColor = Color.FromArgb(80, 57, 111),
            BorderStyle = BorderStyle.None
        };
        previewRows.Panel1.Controls.Add(CreatePreviewSection(_beforeHeader, _beforePreview, after: false));
        previewRows.Panel2.Controls.Add(CreatePreviewSection(_afterHeader, _afterPreview, after: true));

        var right = new Panel { Dock = DockStyle.Fill, BackColor = Surface, Padding = new Padding(8) };
        right.Controls.Add(previewRows);
        right.Controls.Add(target);

        var split = new SafeSplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 8,
            PreferredRatio = 0.54,
            PreferredPaneMinimum = 400,
            BackColor = Color.FromArgb(80, 57, 111),
            BorderStyle = BorderStyle.None
        };
        split.Panel1.Controls.Add(left);
        split.Panel2.Controls.Add(right);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12, 10, 12, 7),
            BackColor = PanelSurface
        };
        var accept = MakeActionButton(
            initializeGit ? "Prepare project" : replaceScope ? "Apply project setup" : "Apply selected changes",
            true);
        var cancel = MakeActionButton("Cancel", false);
        _cleanDuplicatesButton = MakeActionButton("Clean duplicates", false);
        _cleanDuplicatesButton.MinimumSize = new Size(150, 36);
        _cleanDuplicatesButton.Enabled = false;
        _cleanDuplicatesButton.Click += (_, _) => CleanDuplicateRules();
        accept.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        buttons.Controls.Add(accept);
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(_cleanDuplicatesButton);
        AcceptButton = accept;
        CancelButton = cancel;

        Controls.Add(split);
        Controls.Add(buttons);
        Controls.Add(intro);
        Controls.Add(title);

        LoadPreviews();
        UpdatePreview();
    }

    public IReadOnlyList<string> AcceptedRules =>
        GetDetectedRules()
            .Concat(GetLibraryRules())
            .Where(rule => !string.IsNullOrWhiteSpace(rule))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public IReadOnlyList<string> ScopeRules => _scopeRules;

    private IReadOnlyList<string> GetDetectedRules()
    {
        var rules = new List<string>();
        foreach (DataGridViewRow row in _suggestions.Rows)
        {
            if (row.Tag is not GitIgnoreSuggestion suggestion) continue;
            if (Convert.ToBoolean(row.Cells[0].Value)) rules.Add(suggestion.Rule);
        }
        return rules;
    }

    private IReadOnlyList<string> GetLibraryRules()
    {
        var rules = new List<string>();
        foreach (DataGridViewRow row in _ruleLibrary.Rows)
        {
            if (row.Tag is not GitIgnoreRuleOption option) continue;
            if (!Convert.ToBoolean(row.Cells[0].Value)) continue;
            rules.AddRange(option.Rules);
        }
        return rules;
    }

    private void PopulateSuggestions(IReadOnlyList<GitIgnoreSuggestion> suggestions)
    {
        foreach (var suggestion in suggestions)
        {
            var rowIndex = _suggestions.Rows.Add(
                suggestion.DefaultSelected,
                ConfidenceLabel(suggestion.Confidence),
                suggestion.Rule,
                suggestion.Description);
            var row = _suggestions.Rows[rowIndex];
            row.Tag = suggestion;
            var tip = suggestion.Description + "\r\n\r\nGit pattern: " + suggestion.Rule;
            foreach (DataGridViewCell cell in row.Cells) cell.ToolTipText = tip;
        }

        if (suggestions.Count == 0)
        {
            _suggestions.Rows.Add(false, "—", "Nothing detected", "No additional project-specific candidate was found. You can still use the library below or add a custom rule.");
            _suggestions.Rows[0].ReadOnly = true;
        }
    }

    private void AddLibraryRow(GitIgnoreRuleOption option)
    {
        var rowIndex = _ruleLibrary.Rows.Add(
            option.DefaultSelected,
            option.Category,
            option.Label,
            option.RuleSummary);
        var row = _ruleLibrary.Rows[rowIndex];
        row.Tag = option;
        if (option.Custom)
        {
            row.DefaultCellStyle.ForeColor = Color.FromArgb(255, 214, 241);
            row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(88, 45, 104);
        }
        var tip = option.Description + "\r\n\r\nExact Git pattern(s):\r\n" + string.Join("\r\n", option.Rules);
        if (option.Custom) tip += "\r\n\r\nSelect this CUSTOM row to enable Remove custom.";
        foreach (DataGridViewCell cell in row.Cells) cell.ToolTipText = tip;
    }

    private void UpdateCustomBuilderUi()
    {
        if (_customKind.SelectedIndex < 0) return;
        var kind = (CustomIgnoreRuleKind)_customKind.SelectedIndex;
        _customValue.PlaceholderText = GitIgnoreRuleLibrary.KindPlaceholder(kind);
    }

    private void AddCustomRule()
    {
        try
        {
            if (_customKind.SelectedIndex < 0) return;
            var kind = (CustomIgnoreRuleKind)_customKind.SelectedIndex;
            var option = GitIgnoreRuleLibrary.CreateCustom(kind, _customValue.Text);
            AddLibraryRow(option);
            _customValue.Clear();
            _ruleLibrary.ClearSelection();
            var addedRow = _ruleLibrary.Rows[_ruleLibrary.Rows.Count - 1];
            addedRow.Selected = true;
            _ruleLibrary.CurrentCell = addedRow.Cells[2];
            UpdateRemoveCustomButtonState();
            UpdatePreview();
        }
        catch (ArgumentException ex)
        {
            using var dialog = new GuardianConfirmDialog(
                "Custom ignore rule",
                "CHECK CUSTOM RULE",
                ex.Message,
                "OK",
                "",
                showCancel: false);
            dialog.ShowDialog(this);
            _customValue.Focus();
        }
    }

    private void UpdateRemoveCustomButtonState()
    {
        _removeCustomButton.Enabled = _ruleLibrary.CurrentRow?.Tag is GitIgnoreRuleOption { Custom: true };
    }

    private void RemoveSelectedCustomRule()
    {
        var row = _ruleLibrary.CurrentRow;
        if (row?.Tag is not GitIgnoreRuleOption { Custom: true }) return;
        _ruleLibrary.Rows.Remove(row);
        UpdateRemoveCustomButtonState();
        UpdatePreview();
    }

    private void LoadPreviews()
    {
        try
        {
            _beforePreview.Text = ScopedGitIgnoreAdvisor.BuildCurrentOverview(_folderPath, _ignoreDocuments);
            var nestedCount = _ignoreDocuments.Count(document => !document.RelativePath.Equals(".gitignore", StringComparison.OrdinalIgnoreCase));
            _beforeHeader.Text = nestedCount == 0
                ? "CURRENT — root .gitignore only / none yet"
                : $"CURRENT — root .gitignore + {nestedCount} nested file{(nestedCount == 1 ? "" : "s")}";
            RefreshDuplicateHighlight();
        }
        catch (Exception ex)
        {
            _beforeHeader.Text = "CURRENT — preview unavailable";
            _beforePreview.Text = "GitPet could not read the current ignore files.\r\n\r\n" + ex.Message;
            _currentDuplicates = Array.Empty<GitIgnoreDuplicateRule>();
            _cleanDuplicatesButton.Enabled = false;
            _cleanDuplicatesButton.Text = "Clean duplicates";
        }
    }

    private void RefreshDuplicateHighlight()
    {
        var current = GitIgnoreAdvisor.ReadCurrentContent(_folderPath);
        _currentDuplicates = GitIgnoreCleaner.FindDuplicateRules(current);
        var extraLines = _currentDuplicates.Sum(item => item.Occurrences - 1);
        _cleanDuplicatesButton.Enabled = extraLines > 0;
        _cleanDuplicatesButton.Text = extraLines > 0 ? $"Clean duplicates ({extraLines})" : "Clean duplicates";

        _beforePreview.SelectAll();
        _beforePreview.SelectionBackColor = _beforePreview.BackColor;
        _beforePreview.SelectionColor = Color.FromArgb(226, 236, 247);
        _beforePreview.Select(0, 0);

        if (extraLines == 0) return;
        _beforeHeader.Text += $"  •  {extraLines} duplicate{(extraLines == 1 ? "" : "s")}";
        HighlightDuplicateRules(current, _currentDuplicates);
    }

    private void HighlightDuplicateRules(string rootContent, IReadOnlyList<GitIgnoreDuplicateRule> duplicates)
    {
        var rootText = rootContent.TrimEnd('\r', '\n');
        if (rootText.Length == 0) return;
        var rootStart = _beforePreview.Text.IndexOf(rootText, StringComparison.Ordinal);
        if (rootStart < 0) return;

        var duplicateRules = duplicates.Select(item => item.Rule).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var lineStart = 0;
        while (lineStart < rootText.Length)
        {
            var lineEnd = lineStart;
            while (lineEnd < rootText.Length && rootText[lineEnd] != '\r' && rootText[lineEnd] != '\n') lineEnd++;

            var rawLine = rootText[lineStart..lineEnd];
            var trimmed = rawLine.Trim();
            if (duplicateRules.Contains(trimmed))
            {
                var leading = rawLine.Length - rawLine.TrimStart().Length;
                _beforePreview.Select(rootStart + lineStart + leading, trimmed.Length);
                _beforePreview.SelectionBackColor = DuplicateHighlight;
                _beforePreview.SelectionColor = Color.FromArgb(255, 231, 177);
            }

            if (lineEnd >= rootText.Length) break;
            if (rootText[lineEnd] == '\r' && lineEnd + 1 < rootText.Length && rootText[lineEnd + 1] == '\n')
                lineStart = lineEnd + 2;
            else
                lineStart = lineEnd + 1;
        }
        _beforePreview.Select(0, 0);
    }

    private void CleanDuplicateRules()
    {
        try
        {
            var path = Path.Combine(_folderPath, ".gitignore");
            var current = GitIgnoreAdvisor.ReadCurrentContent(_folderPath);
            var duplicates = GitIgnoreCleaner.FindDuplicateRules(current);
            var extraLines = duplicates.Sum(item => item.Occurrences - 1);
            if (extraLines == 0)
            {
                RefreshDuplicateHighlight();
                return;
            }

            var list = string.Join("\r\n", duplicates.Take(10).Select(item => $"• {item.Rule}  ×{item.Occurrences}"));
            if (duplicates.Count > 10) list += $"\r\n• ... and {duplicates.Count - 10} more duplicated rule(s)";

            /*
            PATCH: SAFE GITIGNORE DUPLICATE CLEANER
            DATE: 2026-09-09
            Confirm before removing repeated root ignore rules.
            */
            using var confirm = new GuardianConfirmDialog(
                "Clean .gitignore duplicates",
                "CLEAN DUPLICATES",
                $"GitPet found {extraLines} repeated rule line{(extraLines == 1 ? "" : "s")} in the ROOT .gitignore.\r\n\r\n" +
                list +
                "\r\n\r\nClean keeps the first occurrence of every rule and removes only later duplicates. " +
                "Comments, ordering, unique rules, and nested .gitignore files stay untouched.",
                "Clean",
                "Cancel");

            if (confirm.ShowDialog(this) != DialogResult.Yes) return;

            var cleaned = GitIgnoreCleaner.RemoveDuplicateRules(current, out var removed);
            if (removed == 0) return;
            File.WriteAllText(path, cleaned);

            LoadPreviews();
            UpdatePreview();
            _selectionSummary.Text = $"Cleaned {removed} duplicate rule line{(removed == 1 ? "" : "s")} from the root .gitignore. Review CURRENT and AFTER before continuing.";
        }
        catch (Exception ex)
        {
            using var error = new GuardianConfirmDialog(
                "Clean .gitignore duplicates",
                "CLEAN NEEDS ATTENTION",
                "GitPet could not clean the root .gitignore. No nested .gitignore file was changed.\r\n\r\n" + ex.Message,
                "OK",
                "",
                showCancel: false);
            error.ShowDialog(this);
        }
    }

    private void UpdatePreview()
    {
        var selected = AcceptedRules;
        _selectionSummary.Text = selected.Count == 0
            ? _scopeRules.Count == 0
                ? "No extra ignore rules selected. The root .gitignore will remain unchanged."
                : _replaceScope
                    ? $"Updated tracking scope ready ({_scopeRules.Count} scope rule(s)); GitPet's previous managed scope will be replaced."
                    : $"Tracking scope ready ({_scopeRules.Count} scope rule(s)); no extra ignore rules selected."
            : $"{selected.Count} exact ignore pattern{(selected.Count == 1 ? "" : "s")} selected" +
              (_scopeRules.Count > 0 ? $" + {_scopeRules.Count} tracking-scope rule(s)." : ".") +
              (_replaceScope ? " Previous GitPet scope rules will be replaced." : "") +
              " The AFTER pane is the exact proposed root file.";

        try
        {
            _afterPreview.Text = _replaceScope
                ? ProjectGitIgnoreComposer.BuildPreviewReplacingScope(_folderPath, _scopeRules, selected)
                : ProjectGitIgnoreComposer.BuildPreview(_folderPath, _scopeRules, selected);
            _afterHeader.Text = "AFTER — exact proposed root .gitignore";
        }
        catch (Exception ex)
        {
            _afterHeader.Text = "AFTER — preview unavailable";
            _afterPreview.Text = "GitPet could not build the proposed root .gitignore preview.\r\n\r\n" + ex.Message;
        }
    }

    private void ConfigureSuggestionsGrid() => ConfigureGrid(
        _suggestions,
        new DataGridViewCheckBoxColumn { Name = "Use", HeaderText = "Use", FillWeight = 9, FlatStyle = FlatStyle.Flat },
        new DataGridViewTextBoxColumn { Name = "Confidence", HeaderText = "GitPet says", ReadOnly = true, FillWeight = 18 },
        new DataGridViewTextBoxColumn { Name = "Rule", HeaderText = "Ignore this", ReadOnly = true, FillWeight = 25 },
        new DataGridViewTextBoxColumn { Name = "Reason", HeaderText = "Why", ReadOnly = true, FillWeight = 48 });

    private void ConfigureRuleLibraryGrid() => ConfigureGrid(
        _ruleLibrary,
        new DataGridViewCheckBoxColumn { Name = "Use", HeaderText = "Use", FillWeight = 9, FlatStyle = FlatStyle.Flat },
        new DataGridViewTextBoxColumn { Name = "Category", HeaderText = "Category", ReadOnly = true, FillWeight = 17 },
        new DataGridViewTextBoxColumn { Name = "Label", HeaderText = "Ignore", ReadOnly = true, FillWeight = 31 },
        new DataGridViewTextBoxColumn { Name = "Rules", HeaderText = "Pattern(s)", ReadOnly = true, FillWeight = 43 });

    private static void ConfigureGrid(DataGridView grid, params DataGridViewColumn[] columns)
    {
        grid.Dock = DockStyle.Fill;
        grid.BackgroundColor = PreviewSurface;
        grid.BorderStyle = BorderStyle.None;
        grid.RowHeadersVisible = false;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersHeight = 34;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(63, 43, 93);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.8f, FontStyle.Bold);
        grid.DefaultCellStyle.BackColor = Color.FromArgb(34, 26, 50);
        grid.DefaultCellStyle.ForeColor = Ink;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(73, 51, 105);
        grid.DefaultCellStyle.SelectionForeColor = Color.White;
        grid.GridColor = Color.FromArgb(58, 45, 78);
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.ShowCellToolTips = true;
        grid.Columns.AddRange(columns);
    }

    private void WireCheckboxGrid(DataGridView grid)
    {
        grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        grid.CellValueChanged += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == 0) UpdatePreview();
        };
    }

    private static Panel CreateSectionHeader(string title, string explanation)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 68,
            Padding = new Padding(10, 8, 10, 6),
            BackColor = Color.FromArgb(38, 28, 55)
        };
        var titleLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 23,
            Text = title,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };
        var explanationLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = explanation,
            ForeColor = MutedInk,
            AutoEllipsis = true
        };
        panel.Controls.Add(explanationLabel);
        panel.Controls.Add(titleLabel);
        return panel;
    }

    private static void ConfigurePreviewBox(RichTextBox box, bool after)
    {
        box.Dock = DockStyle.Fill;
        box.ReadOnly = true;
        box.WordWrap = false;
        box.DetectUrls = false;
        box.BorderStyle = BorderStyle.None;
        box.BackColor = after ? AfterPreviewSurface : CurrentPreviewSurface;
        box.ForeColor = after ? Color.FromArgb(241, 229, 247) : Color.FromArgb(226, 236, 247);
        box.Font = new Font("Cascadia Mono", 9f);
        box.ScrollBars = RichTextBoxScrollBars.Both;
    }

    private static void ConfigurePreviewHeader(Label label, bool after)
    {
        label.Dock = DockStyle.Top;
        label.Height = 36;
        label.Padding = new Padding(10, 7, 10, 4);
        label.BackColor = after ? AfterHeaderSurface : CurrentHeaderSurface;
        label.ForeColor = after ? Color.FromArgb(255, 211, 240) : Color.FromArgb(222, 239, 255);
        label.Font = new Font("Segoe UI", 9, FontStyle.Bold);
    }

    private static Control CreatePreviewSection(Label header, RichTextBox preview, bool after)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = after ? AfterPreviewSurface : CurrentPreviewSurface,
            Margin = new Padding(0, 4, 0, 4)
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
            MinimumSize = new Size(primary ? 126 : 90, 36),
            Height = 36,
            Margin = new Padding(6, 1, 0, 1),
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
