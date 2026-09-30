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
    private readonly Dictionary<ReconcileInspectorView, Button> _viewButtons = [];
    private GuardianWorkboardRow? _selection;
    private ReconcileInspectorView _selectedView;

    public ReconcileInspectorPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = GuardianTheme.Console;

        var header = BuildHeader();
        var tabs = BuildTabs();
        var split = BuildSplit();

        _footer.Dock = DockStyle.Bottom;
        _footer.Height = 38;
        _footer.Padding = new Padding(14, 0, 12, 0);
        _footer.BackColor = GuardianTheme.ConsoleHeader;
        _footer.ForeColor = GuardianTheme.FaintInk;
        _footer.Font = new Font("Cascadia Mono", 8f);
        _footer.TextAlign = ContentAlignment.MiddleLeft;

        Controls.Add(split);
        Controls.Add(_footer);
        Controls.Add(tabs);
        Controls.Add(header);

        SelectView(ReconcileInspectorView.Summary);
    }

    public event EventHandler? ActivityRequested;

    internal static ReconcileInspectorView DefaultViewForState(string? state) => state switch
    {
        "LOCAL" => ReconcileInspectorView.BaseLocal,
        "REMOTE" => ReconcileInspectorView.BaseRemote,
        "BOTH SIDES" or "CONFLICT" => ReconcileInspectorView.LocalRemote,
        _ => ReconcileInspectorView.Summary
    };

    public void ShowSelection(GuardianWorkboardRow row)
    {
        _selection = row;
        _pathLabel.Text = row.Path;
        _stateLabel.Text = row.State;
        _stateLabel.ForeColor = row.State is "BOTH SIDES" or "CONFLICT"
            ? GuardianTheme.Warning
            : GuardianTheme.Changes;

        _footer.Text =
            $"{row.State} · review only · opening this inspector does not merge, commit, checkout, reset, or push anything.";

        SelectView(DefaultViewForState(row.State));
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
            Width = 172,
            Height = 24,
            Location = new Point(0, 0),
            Text = "RECONCILE INSPECTOR",
            ForeColor = GuardianTheme.Reconcile,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _stateLabel.AutoSize = false;
        _stateLabel.Width = 112;
        _stateLabel.Height = 24;
        _stateLabel.Location = new Point(176, 0);
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

        var activity = MakeButton("Activity", 86);
        activity.Margin = new Padding(8, 8, 0, 0);
        activity.Click += (_, _) => ActivityRequested?.Invoke(this, EventArgs.Empty);

        header.Controls.Add(info, 0, 0);
        header.Controls.Add(activity, 1, 0);
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
            BackColor = GuardianTheme.SurfaceSoft,
            Padding = new Padding(10, 5, 10, 3)
        };

        AddViewButton(tabs, "Summary", ReconcileInspectorView.Summary, 88);
        AddViewButton(tabs, "Base ↔ Local", ReconcileInspectorView.BaseLocal, 112);
        AddViewButton(tabs, "Local ↔ Remote", ReconcileInspectorView.LocalRemote, 126);
        AddViewButton(tabs, "Base ↔ Remote", ReconcileInspectorView.BaseRemote, 126);
        AddViewButton(tabs, "Merged Preview", ReconcileInspectorView.MergedPreview, 124);

        return tabs;
    }

    private Control BuildSplit()
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 6,
            Panel1MinSize = 0,
            Panel2MinSize = 0,
            BackColor = GuardianTheme.BorderSoft,
            BorderStyle = BorderStyle.None
        };

        split.Panel1.Controls.Add(BuildSourcePlaceholder(_leftTitle, _leftBody));
        split.Panel2.Controls.Add(BuildSourcePlaceholder(_rightTitle, _rightBody));

        split.SizeChanged += (_, _) =>
        {
            var available = split.ClientSize.Width - split.SplitterWidth;
            if (available <= 0) return;
            var target = available / 2;
            try
            {
                if (split.SplitterDistance != target) split.SplitterDistance = target;
            }
            catch (InvalidOperationException)
            {
            }
        };

        return split;
    }

    private static Control BuildSourcePlaceholder(Label title, RichTextBox body)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = GuardianTheme.Console
        };

        title.Dock = DockStyle.Top;
        title.Height = 38;
        title.Padding = new Padding(12, 0, 10, 0);
        title.BackColor = GuardianTheme.SurfaceSoft;
        title.ForeColor = GuardianTheme.MutedInk;
        title.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        title.TextAlign = ContentAlignment.MiddleLeft;

        body.Dock = DockStyle.Fill;
        body.ReadOnly = true;
        body.WordWrap = true;
        body.ScrollBars = RichTextBoxScrollBars.Vertical;
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
        ReconcileInspectorView view,
        int width)
    {
        var button = MakeButton(text, width);
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

        var (left, right) = PaneTitles(view);
        _leftTitle.Text = left;
        _rightTitle.Text = right;

        var path = _selection?.Path ?? "Select a reconciliation row.";
        var detail = string.IsNullOrWhiteSpace(_selection?.Detail)
            ? "No reconciliation detail is available yet."
            : _selection!.Detail;

        if (view == ReconcileInspectorView.Summary)
        {
            _leftBody.Text =
                $"SELECTED PATH\r\n\r\n{path}\r\n\r\n" +
                "Phase 1 provides the inspector shell and safe workboard entry only.";
            _rightBody.Text =
                $"REVIEW STATUS\r\n\r\n{detail}\r\n\r\n" +
                "Exact BASE / LOCAL / REMOTE source snapshots arrive in Phase 2.";
            return;
        }

        _leftBody.Text = SourcePlaceholder(left, path);
        _rightBody.Text = SourcePlaceholder(right, path);
    }

    private static (string Left, string Right) PaneTitles(ReconcileInspectorView view) => view switch
    {
        ReconcileInspectorView.BaseLocal => ("BASE", "LOCAL"),
        ReconcileInspectorView.LocalRemote => ("LOCAL", "REMOTE"),
        ReconcileInspectorView.BaseRemote => ("BASE", "REMOTE"),
        ReconcileInspectorView.MergedPreview => ("BEFORE MERGE", "MERGED CANDIDATE"),
        _ => ("SELECTION", "STATUS")
    };

    private static string SourcePlaceholder(string side, string path) =>
        $"{side} SOURCE\r\n\r\n{path}\r\n\r\n" +
        "Source not loaded in Phase 1.\r\n\r\n" +
        "Opening this view is read-only and does not change Git state.";

    private static Button MakeButton(string text, int width)
    {
        var button = new Button
        {
            Text = text,
            Width = width,
            Height = 31,
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
