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
    private readonly SplitContainer _split = new();
    private readonly Dictionary<ReconcileInspectorView, Button> _viewButtons = [];
    private Button? _scrollLinkButton;
    private Button? _prettyButton;
    private Button? _maximizeButton;
    private readonly RichTextScrollLink _scrollLink;
    private bool _prettyView = true;
    private GuardianWorkboardRow? _selection;
    private ReconcileInspectorSourceModel? _sourceModel;
    private ReconcileInspectorView _selectedView;

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

        Controls.Add(_split);
        Controls.Add(_footer);
        Controls.Add(tabs);
        Controls.Add(header);

        SelectView(ReconcileInspectorView.Summary);
    }

    public event EventHandler? ActivityRequested;
    public event EventHandler? MaximizeRequested;

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
        _selection = row;
        _sourceModel = null;
        ApplySelectionIdentity(row);
        _footer.ForeColor = GuardianTheme.FaintInk;
        _footer.Text = "Loading pinned BASE / LOCAL / REMOTE snapshots… read-only Git inspection only.";
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

        _maximizeButton = MakeButton("Max ⛶");
        _maximizeButton.Click += (_, _) => MaximizeRequested?.Invoke(this, EventArgs.Empty);

        var activity = MakeButton("Activity");
        activity.Click += (_, _) => ActivityRequested?.Invoke(this, EventArgs.Empty);

        actions.Controls.Add(_scrollLinkButton);
        actions.Controls.Add(_prettyButton);
        actions.Controls.Add(_maximizeButton);
        actions.Controls.Add(activity);

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

        if (view == ReconcileInspectorView.Summary)
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
        var path = _selection?.Path ?? "Select a reconciliation row.";
        var detail = string.IsNullOrWhiteSpace(_selection?.Detail)
            ? "No reconciliation detail is available yet."
            : _selection!.Detail;

        _leftTitle.Text = "SELECTION";
        _rightTitle.Text = "PINNED REVISIONS";
        _leftBody.WordWrap = true;
        _rightBody.WordWrap = true;

        if (_sourceModel is null)
        {
            SharedCodeReviewRenderer.RenderPlain(
                _leftBody,
                $"Path\r\n{path}\r\n\r\nState\r\n{_selection?.State ?? "—"}",
                wordWrap: true);
            SharedCodeReviewRenderer.RenderPlain(
                _rightBody,
                "Loading immutable Git identities…",
                wordWrap: true);
            return;
        }

        SharedCodeReviewRenderer.RenderPlain(
            _leftBody,
            $"Path\r\n{_sourceModel.RelativePath}\r\n\r\n" +
            $"State\r\n{_sourceModel.State}\r\n\r\n" +
            $"Analysis\r\n{_sourceModel.Analysis.OverallLabel}\r\n" +
            $"{_sourceModel.Analysis.LocalLabel} ({_sourceModel.Analysis.LocalHunkCount} hunks)\r\n" +
            $"{_sourceModel.Analysis.RemoteLabel} ({_sourceModel.Analysis.RemoteHunkCount} hunks)\r\n" +
            $"Overlap: {(_sourceModel.Analysis.HasOverlap ? "YES" : "NO")}\r\n\r\n" +
            $"{_sourceModel.Analysis.Detail}\r\n\r\n" +
            $"Workboard detail\r\n{detail}",
            wordWrap: true);

        SharedCodeReviewRenderer.RenderPlain(
            _rightBody,
            $"BASE\r\n{_sourceModel.Base.CommitSha}\r\n{_sourceModel.Base.Locator}\r\n\r\n" +
            $"LOCAL\r\n{_sourceModel.Local.CommitSha}\r\n{_sourceModel.Local.Locator}\r\n\r\n" +
            $"REMOTE\r\n{_sourceModel.Remote.CommitSha}\r\n{_sourceModel.Remote.Locator}\r\n\r\n" +
            $"MERGED PREVIEW\r\n{_sourceModel.MergePreview.Status}",
            wordWrap: true);
    }

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
            _scrollLink.Dispose();
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
