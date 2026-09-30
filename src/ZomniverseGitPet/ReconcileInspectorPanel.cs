namespace ZomniverseGitPet;

internal enum ReconcileInspectorView
{
    Summary,
    BaseLocal,
    LocalRemote,
    BaseRemote,
    MergedPreview
}

internal sealed class ReconcileInspectorPanel : Panel
{
    private readonly Label _pathLabel = new();
    private readonly Label _stateLabel = new();
    private readonly Label _leftTitle = new();
    private readonly Label _rightTitle = new();
    private readonly RichTextBox _leftBody = new();
    private readonly RichTextBox _rightBody = new();
    private readonly Label _footer = new();
    private readonly ReconcileSummaryPanel _summaryPanel = new();
    private readonly SplitContainer _split = new();
    private readonly Dictionary<ReconcileInspectorView, Button> _viewButtons = [];
    private Button? _scrollLinkButton;
    private Button? _prettyButton;
    private Button? _copyButton;
    private Button? _copyEverythingButton;
    private ContextMenuStrip? _copyMenu;
    private Button? _editLocalButton;
    private Button? _validateEditButton;
    private Button? _writeEditButton;
    private Button? _commitEditButton;
    private Button? _cancelEditButton;
    private Button? _activityButton;
    private Button? _maximizeButton;
    private readonly RichTextScrollLink _scrollLink;
    private bool _prettyView = true;
    private GuardianWorkboardRow? _selection;
    private ReconcileInspectorSourceModel? _sourceModel;
    private ReconcileInspectorView _selectedView;
    private bool _localEditMode;
    private string _localEditOriginalEditorText = "";
    private string _localEditOriginalRawText = "";
    private string _localEditPinnedSha = "";

    public ReconcileInspectorPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = GuardianTheme.Console;

        var header = BuildHeader();
        var tabs = BuildTabs();
        BuildSplit();
        _scrollLink = new RichTextScrollLink(_leftBody, _rightBody);

        _footer.Dock = DockStyle.Bottom;
        _footer.Height = 38;
        _footer.Padding = new Padding(14, 0, 12, 0);
        _footer.BackColor = GuardianTheme.ConsoleHeader;
        _footer.ForeColor = GuardianTheme.FaintInk;
        _footer.Font = new Font("Cascadia Mono", 8f);
        _footer.TextAlign = ContentAlignment.MiddleLeft;

        _leftBody.TextChanged += (_, _) =>
        {
            if (_localEditMode)
                UpdateLocalEditDirtyState();
        };

        _summaryPanel.Visible = false;
        Controls.Add(_split);
        Controls.Add(_summaryPanel);
        Controls.Add(_footer);
        Controls.Add(tabs);
        Controls.Add(header);

        SelectView(ReconcileInspectorView.Summary);
    }

    public event EventHandler? ActivityRequested;
    public event EventHandler? MaximizeRequested;
    public event EventHandler<ReconcileLocalEditRequestEventArgs>? LocalEditValidateRequested;
    public event EventHandler<ReconcileLocalEditRequestEventArgs>? LocalEditWriteRequested;
    public event EventHandler<ReconcileLocalEditRequestEventArgs>? LocalEditCommitRequested;

    public void SetMaximizedMode(bool maximized)
    {
        if (_maximizeButton is not null)
            _maximizeButton.Text = maximized ? "Restore" : "Max ⛶";
    }

    internal static ReconcileInspectorView DefaultViewForState(string? state) => state switch
    {
        "LOCAL" => ReconcileInspectorView.BaseLocal,
        "REMOTE" => ReconcileInspectorView.BaseRemote,
        "BOTH SIDES" or "CONFLICT" => ReconcileInspectorView.LocalRemote,
        _ => ReconcileInspectorView.Summary
    };

    public void ShowLoading(GuardianWorkboardRow row)
    {
        _sourceModel = null;
        ResetLocalEditState();
        _selection = row;
        ApplySelectionIdentity(row);
        _footer.ForeColor = GuardianTheme.FaintInk;
        _footer.Text = "Loading pinned BASE / LOCAL / REMOTE snapshots… read-only Git inspection only.";
        _summaryPanel.ShowLoading(row);
        SelectView(DefaultViewForState(row.State));
    }

    public void ShowSources(ReconcileInspectorSourceModel model)
    {
        _sourceModel = model;
        _pathLabel.Text = model.RelativePath;
        _footer.ForeColor = GuardianTheme.Healthy;
        _footer.Text =
            $"Pinned read-only snapshots · BASE {model.Base.ShortSha} · LOCAL {model.Local.ShortSha} · " +
            $"REMOTE {model.Remote.ShortSha} · Pretty view is display-only; Exact preserves source whitespace.";
        _summaryPanel.ShowModel(model, WorkboardDetail());
        RefreshLocalEditActionState();
        SelectView(_selectedView);
    }

    public void ShowProblem(GuardianWorkboardRow row, string message)
    {
        _selection = row;
        _sourceModel = null;
        ApplySelectionIdentity(row);
        _leftBody.Clear();
        _rightBody.Clear();
        _leftTitle.Text = "REVIEW UNAVAILABLE";
        _rightTitle.Text = "DETAILS";
        _footer.ForeColor = GuardianTheme.Warning;
        _footer.Text = string.IsNullOrWhiteSpace(message)
            ? "GitPet could not load the pinned reconciliation sources."
            : message.Replace("\r", " ").Replace("\n", " ");
        _summaryPanel.ShowProblem(message);
        if (_editLocalButton is not null)
        {
            _editLocalButton.Enabled = false;
            _editLocalButton.Visible = false;
        }
    }

    private void ApplySelectionIdentity(GuardianWorkboardRow row)
    {
        _pathLabel.Text = row.Path;
        _stateLabel.Text = row.State;
        _stateLabel.ForeColor = row.State is "BOTH SIDES" or "CONFLICT"
            ? GuardianTheme.Warning
            : GuardianTheme.Changes;
    }

    private Control BuildHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 70,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = GuardianTheme.ConsoleHeader,
            Padding = new Padding(14, 6, 12, 5)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var info = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = GuardianTheme.ConsoleHeader
        };

        var title = new Label
        {
            AutoSize = false,
            Width = 108,
            Height = 24,
            Location = new Point(0, 0),
            Text = "RECONCILE",
            ForeColor = GuardianTheme.Reconcile,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _stateLabel.AutoSize = false;
        _stateLabel.Width = 132;
        _stateLabel.Height = 24;
        _stateLabel.Location = new Point(112, 0);
        _stateLabel.Font = new Font("Cascadia Mono", 8f, FontStyle.Bold);
        _stateLabel.TextAlign = ContentAlignment.MiddleLeft;

        _pathLabel.AutoSize = false;
        _pathLabel.Height = 28;
        _pathLabel.Location = new Point(0, 28);
        _pathLabel.ForeColor = GuardianTheme.Ink;
        _pathLabel.Font = new Font("Cascadia Mono", 8.4f);
        _pathLabel.TextAlign = ContentAlignment.MiddleLeft;
        _pathLabel.AutoEllipsis = true;
        _pathLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        info.Controls.Add(title);
        info.Controls.Add(_stateLabel);
        info.Controls.Add(_pathLabel);
        info.Resize += (_, _) => _pathLabel.Width = Math.Max(120, info.ClientSize.Width);

        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = GuardianTheme.ConsoleHeader,
            Margin = new Padding(8, 8, 0, 0)
        };

        _scrollLinkButton = MakeButton("Scroll ✓");
        _scrollLinkButton.Click += (_, _) =>
        {
            _scrollLink.Enabled = !_scrollLink.Enabled;
            _scrollLinkButton.Text = _scrollLink.Enabled ? "Scroll ✓" : "Scroll off";
        };

        _prettyButton = MakeButton("Pretty ✓");
        _prettyButton.Click += (_, _) =>
        {
            _prettyView = !_prettyView;
            _prettyButton.Text = _prettyView ? "Pretty ✓" : "Exact";
            SelectView(_selectedView);
        };

        _copyButton = MakeButton("Copy ▾");
        _copyButton.Click += (_, _) => ShowCopyMenu(_copyButton);

        _copyEverythingButton = MakeButton("Copy everything");
        _copyEverythingButton.Click += (_, _) => CopyEverythingMarkdown();

        _editLocalButton = MakeButton("Edit local");
        _editLocalButton.Enabled = false;
        _editLocalButton.Click += (_, _) => BeginLocalEdit();

        _validateEditButton = MakeButton("Validate edit");
        _validateEditButton.Visible = false;
        _validateEditButton.Click += (_, _) => RaiseLocalEdit(LocalEditValidateRequested);

        _writeEditButton = MakeButton("Write local");
        _writeEditButton.Visible = false;
        _writeEditButton.Click += (_, _) => RaiseLocalEdit(LocalEditWriteRequested);

        _commitEditButton = MakeButton("Commit local");
        _commitEditButton.Visible = false;
        _commitEditButton.Click += (_, _) => RaiseLocalEdit(LocalEditCommitRequested);

        _cancelEditButton = MakeButton("Cancel edit");
        _cancelEditButton.Visible = false;
        _cancelEditButton.Click += (_, _) => CancelLocalEdit();

        _maximizeButton = MakeButton("Max ⛶");
        _maximizeButton.Click += (_, _) => MaximizeRequested?.Invoke(this, EventArgs.Empty);

        _activityButton = MakeButton("Activity");
        _activityButton.Click += (_, _) => ActivityRequested?.Invoke(this, EventArgs.Empty);

        actions.Controls.Add(_scrollLinkButton);
        actions.Controls.Add(_prettyButton);
        actions.Controls.Add(_copyButton);
        actions.Controls.Add(_copyEverythingButton);
        actions.Controls.Add(_editLocalButton);
        actions.Controls.Add(_validateEditButton);
        actions.Controls.Add(_writeEditButton);
        actions.Controls.Add(_commitEditButton);
        actions.Controls.Add(_cancelEditButton);
        actions.Controls.Add(_maximizeButton);
        actions.Controls.Add(_activityButton);

        header.Controls.Add(info, 0, 0);
        header.Controls.Add(actions, 1, 0);
        return header;
    }

    private Control BuildTabs()
    {
        var tabs = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 42,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = true,
            BackColor = GuardianTheme.SurfaceSoft,
            Padding = new Padding(10, 5, 10, 3)
        };

        AddViewButton(tabs, "Summary", ReconcileInspectorView.Summary);
        AddViewButton(tabs, "Base ↔ Local", ReconcileInspectorView.BaseLocal);
        AddViewButton(tabs, "Local ↔ Remote", ReconcileInspectorView.LocalRemote);
        AddViewButton(tabs, "Base ↔ Remote", ReconcileInspectorView.BaseRemote);
        AddViewButton(tabs, "Merged", ReconcileInspectorView.MergedPreview);

        return tabs;
    }

    private void BuildSplit()
    {
        _split.Dock = DockStyle.Fill;
        _split.Orientation = Orientation.Vertical;
        _split.SplitterWidth = 6;
        _split.PreferredRatio = 0.5;
        _split.PreferredPaneMinimum = 140;
        _split.BackColor = GuardianTheme.BorderSoft;
        _split.BorderStyle = BorderStyle.None;

        _split.Panel1.Controls.Add(BuildSourcePlaceholder(_leftTitle, _leftBody));
        _split.Panel2.Controls.Add(BuildSourcePlaceholder(_rightTitle, _rightBody));
    }

    private static Control BuildSourcePlaceholder(Label title, RichTextBox body)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = GuardianTheme.Console
        };

        title.Dock = DockStyle.Top;
        title.Height = 52;
        title.Padding = new Padding(12, 3, 10, 3);
        title.BackColor = GuardianTheme.SurfaceSoft;
        title.ForeColor = GuardianTheme.MutedInk;
        title.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        title.TextAlign = ContentAlignment.MiddleLeft;

        body.Dock = DockStyle.Fill;
        body.ReadOnly = true;
        body.WordWrap = false;
        body.ScrollBars = RichTextBoxScrollBars.Both;
        body.BorderStyle = BorderStyle.None;
        body.BackColor = GuardianTheme.Console;
        body.ForeColor = GuardianTheme.MutedInk;
        body.Font = new Font("Cascadia Mono", 9f);
        body.DetectUrls = false;
        body.Padding = new Padding(12);

        panel.Controls.Add(body);
        panel.Controls.Add(title);
        return panel;
    }

    private void AddViewButton(
        FlowLayoutPanel tabs,
        string text,
        ReconcileInspectorView view)
    {
        var button = MakeButton(text);
        button.Tag = view;
        button.Click += (_, _) => SelectView(view);
        _viewButtons[view] = button;
        tabs.Controls.Add(button);
    }

    private void SelectView(ReconcileInspectorView view)
    {
        _selectedView = view;

        foreach (var (candidate, button) in _viewButtons)
        {
            var selected = candidate == view;
            button.BackColor = selected ? GuardianTheme.VioletPressed : GuardianTheme.SurfaceRaised;
            button.ForeColor = selected ? Color.White : GuardianTheme.Ink;
            button.FlatAppearance.BorderColor = selected ? GuardianTheme.Violet : GuardianTheme.Border;
        }

        if (_localEditMode && view != ReconcileInspectorView.LocalRemote)
            return;

        var summarySelected = view == ReconcileInspectorView.Summary;
        _summaryPanel.Visible = summarySelected;
        _split.Visible = !summarySelected;
        if (_scrollLinkButton is not null) _scrollLinkButton.Visible = !summarySelected;
        if (_prettyButton is not null) _prettyButton.Visible = !summarySelected;

        if (summarySelected)
        {
            RenderSummary();
            return;
        }

        if (view == ReconcileInspectorView.MergedPreview)
        {
            if (_sourceModel is null)
            {
                _leftTitle.Text = "BEFORE MERGE · LOADING…";
                _rightTitle.Text = "MERGED CANDIDATE · LOADING…";
                _leftBody.Clear();
                _rightBody.Clear();
                return;
            }

            RenderSource(
                _leftTitle,
                _leftBody,
                _sourceModel.Local,
                _sourceModel.Branch,
                _sourceModel.RelativePath,
                _sourceModel.BaseLocalChanges.AfterLines,
                SharedCodeReviewRenderer.LocalChangeBackground,
                _prettyView);
            _leftTitle.Text = "BEFORE MERGE · " + _leftTitle.Text;

            var preview = _sourceModel.MergePreview;
            _rightTitle.Text = "MERGED CANDIDATE · " + preview.Status;

            SharedCodeReviewRenderer.RenderCode(
                _rightBody,
                preview.Available && preview.Exists ? preview.Text : "",
                _sourceModel.RelativePath,
                visualIndent: _prettyView);

            _footer.ForeColor = preview.HasConflicts
                ? GuardianTheme.Warning
                : preview.Available
                    ? GuardianTheme.Healthy
                    : GuardianTheme.Warning;
            _footer.Text =
                $"{_sourceModel.Analysis.OverallLabel} · {preview.Status} · preview only; repository unchanged.";
            return;
        }

        if (_sourceModel is null)
        {
            var (left, right) = PaneTitles(view);
            _leftTitle.Text = left + " · LOADING…";
            _rightTitle.Text = right + " · LOADING…";
            _leftBody.Clear();
            _rightBody.Clear();
            return;
        }

        var (leftSource, rightSource, leftLines, rightLines, leftBackground, rightBackground) =
            view switch
            {
                ReconcileInspectorView.BaseLocal => (
                    _sourceModel.Base,
                    _sourceModel.Local,
                    _sourceModel.BaseLocalChanges.BeforeLines,
                    _sourceModel.BaseLocalChanges.AfterLines,
                    SharedCodeReviewRenderer.BeforeChangeBackground,
                    SharedCodeReviewRenderer.LocalChangeBackground),
                ReconcileInspectorView.LocalRemote => (
                    _sourceModel.Local,
                    _sourceModel.Remote,
                    _sourceModel.BaseLocalChanges.AfterLines,
                    _sourceModel.BaseRemoteChanges.AfterLines,
                    SharedCodeReviewRenderer.LocalChangeBackground,
                    SharedCodeReviewRenderer.RemoteChangeBackground),
                ReconcileInspectorView.BaseRemote => (
                    _sourceModel.Base,
                    _sourceModel.Remote,
                    _sourceModel.BaseRemoteChanges.BeforeLines,
                    _sourceModel.BaseRemoteChanges.AfterLines,
                    SharedCodeReviewRenderer.BeforeChangeBackground,
                    SharedCodeReviewRenderer.RemoteChangeBackground),
                _ => (
                    _sourceModel.Base,
                    _sourceModel.Local,
                    DiffLineMap.Empty.BeforeLines,
                    DiffLineMap.Empty.AfterLines,
                    SharedCodeReviewRenderer.BeforeChangeBackground,
                    SharedCodeReviewRenderer.LocalChangeBackground)
            };

        RenderSource(
            _leftTitle, _leftBody, leftSource, _sourceModel.Branch,
            _sourceModel.RelativePath, leftLines, leftBackground, _prettyView);
        RenderSource(
            _rightTitle, _rightBody, rightSource, _sourceModel.Branch,
            _sourceModel.RelativePath, rightLines, rightBackground, _prettyView);
    }

    private void RenderSummary()
    {
        if (_sourceModel is null)
        {
            _summaryPanel.ShowLoading(_selection);
            _footer.ForeColor = GuardianTheme.FaintInk;
            _footer.Text = "Summary waits for pinned deterministic reconciliation evidence.";
            return;
        }

        _summaryPanel.ShowModel(_sourceModel, WorkboardDetail());

        var presentation = ReconcileSummaryPresentation.Create(
            _sourceModel.Analysis,
            _sourceModel.MergePreview);
        _footer.ForeColor = presentation.AssessmentTone switch
        {
            ReconcileSummaryTone.Positive => GuardianTheme.Healthy,
            ReconcileSummaryTone.Conflict => GuardianTheme.Warning,
            ReconcileSummaryTone.Caution => GuardianTheme.Reconcile,
            _ => GuardianTheme.FaintInk
        };
        _footer.Text =
            $"{presentation.Assessment} · {presentation.ChangeRelationship} · " +
            $"{presentation.Overlap} · {presentation.MergedPreview}";
    }

    private string WorkboardDetail() =>
        string.IsNullOrWhiteSpace(_selection?.Detail)
            ? "No additional workboard detail."
            : _selection!.Detail;

    private static void RenderSource(
        Label title,
        RichTextBox body,
        ReconcileSourceSnapshot source,
        string branch,
        string path,
        IReadOnlySet<int> changedLines,
        Color changeBackground,
        bool prettyView)
    {
        var branchText = source.Role switch
        {
            "LOCAL" => $" · {branch}",
            "REMOTE" when source.RefName == "MERGE_HEAD" => " · MERGE_HEAD",
            "REMOTE" => $" · origin/{branch}",
            _ => ""
        };

        title.Text =
            $"{source.Role}{branchText} @ {source.ShortSha}\r\n" +
            (source.Exists ? source.Locator : source.Locator + " · NOT PRESENT");

        SharedCodeReviewRenderer.RenderCode(
            body,
            source.Exists ? source.Text : "",
            path,
            changedLines,
            changeBackground,
            prettyView);
    }

    private void BeginLocalEdit()
    {
        if (_sourceModel is null || !_sourceModel.Local.Exists || _selection is null)
        {
            SetLocalEditFeedback("LOCAL source is not available for editing.", success: false);
            return;
        }

        _prettyView = false;
        if (_prettyButton is not null)
            _prettyButton.Text = "Exact";

        SelectView(ReconcileInspectorView.LocalRemote);

        _localEditMode = true;
        _localEditOriginalRawText = _sourceModel.Local.Text;
        _localEditPinnedSha = _sourceModel.Local.CommitSha;
        _localEditOriginalEditorText = _leftBody.Text;
        _leftBody.ReadOnly = false;
        _leftTitle.Text = "EDITING LOCAL · " + _leftTitle.Text;
        SetLocalEditUi(editing: true);
        UpdateLocalEditDirtyState();
        _leftBody.Focus();
    }

    private void CancelLocalEdit()
    {
        if (!_localEditMode) return;

        if (!string.Equals(
                _leftBody.Text,
                _localEditOriginalEditorText,
                StringComparison.Ordinal))
        {
            using var confirm = new GuardianConfirmDialog(
                "Cancel local edit",
                "DISCARD LOCAL DRAFT?",
                "Discard the in-Inspector draft and return to the pinned LOCAL source?\r\n\r\n" +
                "Nothing has been written to disk by this draft.",
                "Discard draft",
                "Keep editing");
            if (confirm.ShowDialog(FindForm()) != DialogResult.Yes)
                return;
        }

        EndLocalEditMode();
        SelectView(ReconcileInspectorView.LocalRemote);
        _footer.ForeColor = GuardianTheme.FaintInk;
        _footer.Text = "Local edit cancelled. Repository unchanged.";
    }

    private void RaiseLocalEdit(
        EventHandler<ReconcileLocalEditRequestEventArgs>? handler)
    {
        if (!_localEditMode || _sourceModel is null || _selection is null)
            return;

        if (string.Equals(
                _leftBody.Text,
                _localEditOriginalEditorText,
                StringComparison.Ordinal))
        {
            SetLocalEditFeedback("No LOCAL source changes are present in the editor.", success: false);
            return;
        }

        handler?.Invoke(
            this,
            new ReconcileLocalEditRequestEventArgs(
                _selection,
                new ReconcileLocalEditDraft(
                    _sourceModel.RelativePath,
                    _localEditPinnedSha,
                    _localEditOriginalRawText,
                    _leftBody.Text)));
    }

    public void SetLocalEditBusy(bool busy, string? message = null)
    {
        if (!_localEditMode) return;

        if (_validateEditButton is not null) _validateEditButton.Enabled = !busy;
        if (_writeEditButton is not null) _writeEditButton.Enabled = !busy;
        if (_commitEditButton is not null) _commitEditButton.Enabled = !busy;
        if (_cancelEditButton is not null) _cancelEditButton.Enabled = !busy;

        if (!string.IsNullOrWhiteSpace(message))
        {
            _footer.ForeColor = GuardianTheme.Reconcile;
            _footer.Text = message;
        }
    }

    public void SetLocalEditFeedback(string message, bool success)
    {
        _footer.ForeColor = success ? GuardianTheme.Healthy : GuardianTheme.Warning;
        _footer.Text = message;
        SetLocalEditBusy(false);
    }

    private void UpdateLocalEditDirtyState()
    {
        if (!_localEditMode) return;
        var dirty = !string.Equals(
            _leftBody.Text,
            _localEditOriginalEditorText,
            StringComparison.Ordinal);

        if (_validateEditButton is not null) _validateEditButton.Enabled = dirty;
        if (_writeEditButton is not null) _writeEditButton.Enabled = dirty;
        if (_commitEditButton is not null) _commitEditButton.Enabled = dirty;

        _footer.ForeColor = dirty ? GuardianTheme.Reconcile : GuardianTheme.FaintInk;
        _footer.Text = dirty
            ? "LOCAL DRAFT MODIFIED · not written · not staged · not committed"
            : "Editing pinned LOCAL source · no draft changes yet.";
    }

    private void SetLocalEditUi(bool editing)
    {
        foreach (var (view, button) in _viewButtons)
            button.Enabled = !editing || view == ReconcileInspectorView.LocalRemote;

        if (_scrollLinkButton is not null) _scrollLinkButton.Visible = !editing;
        if (_prettyButton is not null) _prettyButton.Visible = !editing;
        if (_copyButton is not null) _copyButton.Visible = !editing;
        if (_copyEverythingButton is not null) _copyEverythingButton.Visible = !editing;
        RefreshLocalEditActionState();

        if (_validateEditButton is not null) _validateEditButton.Visible = editing;
        if (_writeEditButton is not null) _writeEditButton.Visible = editing;
        if (_commitEditButton is not null) _commitEditButton.Visible = editing;
        if (_cancelEditButton is not null) _cancelEditButton.Visible = editing;
        if (_activityButton is not null) _activityButton.Enabled = !editing;
    }

    private void RefreshLocalEditActionState()
    {
        if (_editLocalButton is null) return;

        var available =
            !_localEditMode &&
            _sourceModel is not null &&
            _sourceModel.Local.Exists;

        _editLocalButton.Visible = available;
        _editLocalButton.Enabled = available;

    }

    private void EndLocalEditMode()
    {
        _leftBody.ReadOnly = true;
        _localEditMode = false;
        _localEditOriginalEditorText = "";
        _localEditOriginalRawText = "";
        _localEditPinnedSha = "";
        SetLocalEditUi(editing: false);
        RefreshLocalEditActionState();
    }

    private void ResetLocalEditState()
    {
        _leftBody.ReadOnly = true;
        _localEditMode = false;
        _localEditOriginalEditorText = "";
        _localEditOriginalRawText = "";
        _localEditPinnedSha = "";
        SetLocalEditUi(editing: false);
        RefreshLocalEditActionState();
    }

    private void ShowCopyMenu(Control anchor)
    {
        if (_copyMenu is null || _copyMenu.IsDisposed)
            _copyMenu = BuildCopyMenu();

        if (_copyMenu.Visible)
            _copyMenu.Close(ToolStripDropDownCloseReason.AppClicked);

        _copyMenu.Show(anchor, new Point(0, anchor.Height));
    }

    private ContextMenuStrip BuildCopyMenu()
    {
        var menu = new ContextMenuStrip
        {
            Renderer = GuardianTheme.CreateMenuRenderer(),
            BackColor = GuardianTheme.SurfaceRaised,
            ForeColor = GuardianTheme.Ink,
            ShowImageMargin = false
        };

        AddCopyItem(menu, "Copy selected", CopySelectedExact);
        AddCopyItem(menu, "Copy changed block", CopyChangedBlock);
        AddCopyItem(menu, "Copy whole file", CopyWholeFile);
        AddCopyItem(menu, "Copy code only", CopyCodeOnly);
        AddCopyItem(menu, "Copy comparison", CopyComparison);
        menu.Items.Add(new ToolStripSeparator());
        AddCopyItem(menu, "Copy everything — Markdown", CopyEverythingMarkdown);
        AddCopyItem(menu, "Copy everything — plain text", CopyEverythingPlainText);

        return menu;
    }

    private static void AddCopyItem(
        ContextMenuStrip menu,
        string text,
        Action action)
    {
        var item = new ToolStripMenuItem(text)
        {
            ForeColor = GuardianTheme.Ink
        };
        item.Click += (_, _) => action();
        menu.Items.Add(item);
    }

    private void CopySelectedExact()
    {
        if (_sourceModel is null)
        {
            CopyUnavailable("Nothing is loaded yet.");
            return;
        }

        if (_selectedView == ReconcileInspectorView.Summary)
        {
            CopyUnavailable("Summary has no source selection. Choose a code comparison tab.");
            return;
        }

        if (_prettyView)
        {
            CopyUnavailable("Copy selected uses exact source only. Switch Pretty to Exact, then select source text.");
            return;
        }

        var box = ActiveBody();
        if (box is null || string.IsNullOrEmpty(box.SelectedText))
        {
            CopyUnavailable("Select source text in the active code pane first.");
            return;
        }

        SetClipboard(box.SelectedText, "Selected exact source copied.");
    }

    private void CopyChangedBlock()
    {
        if (!TryGetActiveSource(out var source, out var changedLines))
            return;

        var text = ReconcileCopyExport.BuildChangedBlock(
            source.Text,
            changedLines);
        if (string.IsNullOrEmpty(text))
        {
            CopyUnavailable($"No changed raw lines are available for {source.Role} in this view.");
            return;
        }

        SetClipboard(text, $"Changed raw source copied from {source.Role}. No Pretty formatting included.");
    }

    private void CopyWholeFile()
    {
        if (!TryGetActiveSource(out var source, out _))
            return;

        if (!source.Exists)
        {
            CopyUnavailable($"{source.Role} is not present at this revision.");
            return;
        }

        SetClipboard(
            source.Text,
            $"Whole {source.Role} file copied from raw backing source.");
    }

    private void CopyCodeOnly()
    {
        if (_sourceModel is null)
        {
            CopyUnavailable("Nothing is loaded yet.");
            return;
        }

        SetClipboard(
            ReconcileCopyExport.BuildCodeOnlyMarkdown(_sourceModel),
            "BASE / LOCAL / REMOTE code-only Markdown copied from raw backing source.");
    }

    private void CopyComparison()
    {
        if (_sourceModel is null)
        {
            CopyUnavailable("Nothing is loaded yet.");
            return;
        }

        if (_selectedView == ReconcileInspectorView.Summary)
        {
            CopyUnavailable("Choose a comparison or Merged tab before copying a comparison.");
            return;
        }

        SetClipboard(
            ReconcileCopyExport.BuildComparison(_sourceModel, _selectedView),
            "Current raw source comparison copied.");
    }

    private void CopyEverythingMarkdown()
    {
        if (_sourceModel is null)
        {
            CopyUnavailable("Nothing is loaded yet.");
            return;
        }

        SetClipboard(
            ReconcileCopyExport.BuildEverythingMarkdown(
                _sourceModel,
                WorkboardDetail()),
            "Copy everything — Markdown copied. Raw pinned source + reconciliation evidence are ready to paste into ChatGPT.");
    }

    private void CopyEverythingPlainText()
    {
        if (_sourceModel is null)
        {
            CopyUnavailable("Nothing is loaded yet.");
            return;
        }

        SetClipboard(
            ReconcileCopyExport.BuildEverythingPlainText(
                _sourceModel,
                WorkboardDetail()),
            "Copy everything — plain text copied from raw backing source.");
    }

    private bool TryGetActiveSource(
        out ReconcileSourceSnapshot source,
        out IReadOnlySet<int> changedLines)
    {
        source = null!;
        changedLines = new HashSet<int>();

        if (_sourceModel is null)
        {
            CopyUnavailable("Nothing is loaded yet.");
            return false;
        }

        if (_selectedView == ReconcileInspectorView.Summary)
        {
            CopyUnavailable("Choose a code comparison tab first.");
            return false;
        }

        var useRight = ReferenceEquals(ActiveBody(), _rightBody);

        switch (_selectedView)
        {
            case ReconcileInspectorView.BaseLocal:
                source = useRight ? _sourceModel.Local : _sourceModel.Base;
                changedLines = useRight
                    ? _sourceModel.BaseLocalChanges.AfterLines
                    : _sourceModel.BaseLocalChanges.BeforeLines;
                return true;

            case ReconcileInspectorView.LocalRemote:
                source = useRight ? _sourceModel.Remote : _sourceModel.Local;
                changedLines = useRight
                    ? _sourceModel.BaseRemoteChanges.AfterLines
                    : _sourceModel.BaseLocalChanges.AfterLines;
                return true;

            case ReconcileInspectorView.BaseRemote:
                source = useRight ? _sourceModel.Remote : _sourceModel.Base;
                changedLines = useRight
                    ? _sourceModel.BaseRemoteChanges.AfterLines
                    : _sourceModel.BaseRemoteChanges.BeforeLines;
                return true;

            case ReconcileInspectorView.MergedPreview:
                if (useRight)
                {
                    source = new ReconcileSourceSnapshot(
                        "MERGED",
                        "preview",
                        "",
                        "",
                        "generated-preview",
                        _sourceModel.MergePreview.Available &&
                        _sourceModel.MergePreview.Exists,
                        _sourceModel.MergePreview.Text);
                    changedLines = new HashSet<int>();
                }
                else
                {
                    source = _sourceModel.Local;
                    changedLines = _sourceModel.BaseLocalChanges.AfterLines;
                }
                return true;

            default:
                CopyUnavailable("This view does not expose a copyable source side.");
                return false;
        }
    }

    private RichTextBox? ActiveBody()
    {
        if (_rightBody.Focused || _rightBody.ContainsFocus)
            return _rightBody;
        if (_leftBody.Focused || _leftBody.ContainsFocus)
            return _leftBody;

        return _leftBody;
    }

    private void SetClipboard(string text, string success)
    {
        try
        {
            Clipboard.SetDataObject(text ?? "", copy: true);
            _footer.ForeColor = GuardianTheme.Healthy;
            _footer.Text = success + " Clipboard uses raw source, not display decoration.";
        }
        catch (Exception ex)
        {
            _footer.ForeColor = GuardianTheme.Warning;
            _footer.Text = "Clipboard copy failed: " + ex.Message;
        }
    }

    private void CopyUnavailable(string message)
    {
        _footer.ForeColor = GuardianTheme.Reconcile;
        _footer.Text = message;
    }

    private static (string Left, string Right) PaneTitles(ReconcileInspectorView view) => view switch
    {
        ReconcileInspectorView.BaseLocal => ("BASE", "LOCAL"),
        ReconcileInspectorView.LocalRemote => ("LOCAL", "REMOTE"),
        ReconcileInspectorView.BaseRemote => ("BASE", "REMOTE"),
        ReconcileInspectorView.MergedPreview => ("BEFORE MERGE", "MERGED CANDIDATE"),
        _ => ("SELECTION", "STATUS")
    };

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _copyMenu?.Dispose();
            _copyMenu = null;
            _scrollLink.Dispose();
        }
        base.Dispose(disposing);
    }

    private static Button MakeButton(string text)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(72, 31),
            Height = 31,
            Padding = new Padding(10, 0, 10, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = GuardianTheme.SurfaceRaised,
            ForeColor = GuardianTheme.Ink,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 8.2f, FontStyle.Bold),
            UseVisualStyleBackColor = false,
            Margin = new Padding(4, 0, 4, 0)
        };
        button.FlatAppearance.BorderColor = GuardianTheme.Border;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = GuardianTheme.VioletHover;
        button.FlatAppearance.MouseDownBackColor = GuardianTheme.VioletPressed;
        return button;
    }
}
