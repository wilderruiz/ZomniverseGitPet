namespace ZomniverseGitPet;

internal sealed class ReconcileSummaryPanel : Panel
{
    private readonly TableLayoutPanel _content = new();
    private readonly Label _assessment = new();
    private readonly Label _relationshipValue = new();
    private readonly Label _overlapValue = new();
    private readonly Label _previewValue = new();
    private readonly Label _interpretation = new();
    private readonly Label _localValue = new();
    private readonly Label _localHunks = new();
    private readonly Label _remoteValue = new();
    private readonly Label _remoteHunks = new();
    private readonly Label _technical = new();
    private readonly Label _workboard = new();

    public ReconcileSummaryPanel()
    {
        Dock = DockStyle.Fill;
        AutoScroll = true;
        BackColor = GuardianTheme.Console;
        Padding = new Padding(16, 14, 16, 18);

        _content.Dock = DockStyle.Top;
        _content.AutoSize = true;
        _content.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _content.ColumnCount = 1;
        _content.RowCount = 0;
        _content.BackColor = GuardianTheme.Console;
        _content.Padding = new Padding(0);
        _content.GrowStyle = TableLayoutPanelGrowStyle.AddRows;

        _content.Controls.Add(BuildAssessmentHeader(), 0, _content.RowCount++);
        _content.Controls.Add(BuildPrimaryCards(), 0, _content.RowCount++);
        _content.Controls.Add(BuildInterpretation(), 0, _content.RowCount++);
        _content.Controls.Add(BuildSideCards(), 0, _content.RowCount++);
        _content.Controls.Add(BuildTechnicalSection(), 0, _content.RowCount++);

        Controls.Add(_content);
    }

    public void ShowLoading(GuardianWorkboardRow? row)
    {
        _assessment.Text = "LOADING EVIDENCE…";
        ApplyTone(_assessment, ReconcileSummaryTone.Neutral, strong: true);

        SetCard(_relationshipValue, "CHANGE RELATIONSHIP", "Loading…", ReconcileSummaryTone.Neutral);
        SetCard(_overlapValue, "OVERLAP", "Loading…", ReconcileSummaryTone.Neutral);
        SetCard(_previewValue, "MERGED PREVIEW", "Loading…", ReconcileSummaryTone.Neutral);

        _interpretation.Text =
            "GitPet is pinning BASE / LOCAL / REMOTE and calculating deterministic reconciliation evidence.";
        _localValue.Text = "Loading…";
        _localHunks.Text = "";
        _remoteValue.Text = "Loading…";
        _remoteHunks.Text = "";

        _technical.Text = row is null
            ? "Pinned revision identity will appear here."
            : $"PATH\r\n{row.Path}\r\n\r\nSTATE\r\n{row.State}";
        _workboard.Text = "";
    }

    public void ShowModel(ReconcileInspectorSourceModel model, string workboardDetail)
    {
        var summary = ReconcileSummaryPresentation.Create(model.Analysis, model.MergePreview);

        _assessment.Text = summary.Assessment;
        ApplyTone(_assessment, summary.AssessmentTone, strong: true);

        SetCard(
            _relationshipValue,
            "CHANGE RELATIONSHIP",
            summary.ChangeRelationship,
            summary.ChangeTone);
        SetCard(
            _overlapValue,
            "OVERLAP",
            summary.Overlap,
            summary.OverlapTone);
        SetCard(
            _previewValue,
            "MERGED PREVIEW",
            summary.MergedPreview,
            summary.PreviewTone);

        _interpretation.Text = summary.Interpretation;

        _localValue.Text = model.Analysis.LocalLabel.Replace(" — LOCAL", "");
        _localHunks.Text = HunkText(model.Analysis.LocalHunkCount);
        _remoteValue.Text = model.Analysis.RemoteLabel.Replace(" — REMOTE", "");
        _remoteHunks.Text = HunkText(model.Analysis.RemoteHunkCount);

        _technical.Text =
            $"PATH\r\n{model.RelativePath}\r\n\r\n" +
            $"STATE\r\n{model.State}\r\n\r\n" +
            $"BASE\r\n{model.Base.CommitSha}\r\n{model.Base.Locator}\r\n\r\n" +
            $"LOCAL\r\n{model.Local.CommitSha}\r\n{model.Local.Locator}\r\n\r\n" +
            $"REMOTE\r\n{model.Remote.CommitSha}\r\n{model.Remote.Locator}";

        _workboard.Text = string.IsNullOrWhiteSpace(workboardDetail)
            ? ""
            : "WORKBOARD DETAIL\r\n" + workboardDetail;
    }

    public void ShowProblem(string message)
    {
        _assessment.Text = "REVIEW UNAVAILABLE";
        ApplyTone(_assessment, ReconcileSummaryTone.Conflict, strong: true);

        SetCard(_relationshipValue, "CHANGE RELATIONSHIP", "UNAVAILABLE", ReconcileSummaryTone.Neutral);
        SetCard(_overlapValue, "OVERLAP", "UNKNOWN", ReconcileSummaryTone.Caution);
        SetCard(_previewValue, "MERGED PREVIEW", "UNAVAILABLE", ReconcileSummaryTone.Caution);

        _interpretation.Text = string.IsNullOrWhiteSpace(message)
            ? "GitPet could not load the reconciliation evidence."
            : message;
        _localValue.Text = "—";
        _localHunks.Text = "";
        _remoteValue.Text = "—";
        _remoteHunks.Text = "";
    }

    private Control BuildAssessmentHeader()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = GuardianTheme.SurfaceRaised,
            Padding = new Padding(18, 14, 16, 14),
            Margin = new Padding(0, 0, 0, 12)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var title = new Label
        {
            AutoSize = true,
            Text = "RECONCILE ASSESSMENT",
            ForeColor = GuardianTheme.Ink,
            Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
            Margin = new Padding(0, 4, 16, 0)
        };

        _assessment.AutoSize = true;
        _assessment.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        _assessment.Padding = new Padding(12, 6, 12, 6);
        _assessment.Margin = new Padding(0);

        panel.Controls.Add(title, 0, 0);
        panel.Controls.Add(_assessment, 1, 0);
        return panel;
    }

    private Control BuildPrimaryCards()
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = GuardianTheme.Console,
            Margin = new Padding(0, 0, 0, 12)
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.334f));

        grid.Controls.Add(BuildStatusCard("CHANGE RELATIONSHIP", _relationshipValue), 0, 0);
        grid.Controls.Add(BuildStatusCard("OVERLAP", _overlapValue), 1, 0);
        grid.Controls.Add(BuildStatusCard("MERGED PREVIEW", _previewValue), 2, 0);
        return grid;
    }

    private static Control BuildStatusCard(string caption, Label value)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 94,
            BackColor = GuardianTheme.SurfaceRaised,
            Margin = new Padding(0, 0, 10, 0),
            Padding = new Padding(14, 12, 14, 10),
            BorderStyle = BorderStyle.FixedSingle
        };

        var captionLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 23,
            Text = caption,
            ForeColor = GuardianTheme.MutedInk,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };

        value.Dock = DockStyle.Fill;
        value.AutoEllipsis = true;
        value.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
        value.TextAlign = ContentAlignment.MiddleLeft;
        value.Padding = new Padding(0, 5, 0, 0);

        panel.Controls.Add(value);
        panel.Controls.Add(captionLabel);
        return panel;
    }

    private Control BuildInterpretation()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            BackColor = GuardianTheme.SurfaceSoft,
            Padding = new Padding(16, 13, 16, 13),
            Margin = new Padding(0, 0, 0, 12)
        };

        _interpretation.AutoSize = true;
        _interpretation.Dock = DockStyle.Top;
        _interpretation.MaximumSize = new Size(1800, 0);
        _interpretation.ForeColor = GuardianTheme.SoftInk;
        _interpretation.Font = new Font("Segoe UI", 9.5f);
        _interpretation.Padding = new Padding(0);
        panel.Controls.Add(_interpretation);
        return panel;
    }

    private Control BuildSideCards()
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = GuardianTheme.Console,
            Margin = new Padding(0, 0, 0, 12)
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        grid.Controls.Add(BuildSideCard("LOCAL", _localValue, _localHunks), 0, 0);
        grid.Controls.Add(BuildSideCard("REMOTE", _remoteValue, _remoteHunks), 1, 0);
        return grid;
    }

    private static Control BuildSideCard(string title, Label value, Label hunks)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 104,
            BackColor = GuardianTheme.SurfaceRaised,
            Padding = new Padding(14, 10, 14, 10),
            Margin = title == "LOCAL"
                ? new Padding(0, 0, 6, 0)
                : new Padding(6, 0, 0, 0),
            BorderStyle = BorderStyle.FixedSingle
        };

        var heading = new Label
        {
            Dock = DockStyle.Top,
            Height = 24,
            Text = title,
            ForeColor = title == "LOCAL" ? GuardianTheme.Healthy : GuardianTheme.Info,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };

        value.Dock = DockStyle.Top;
        value.Height = 31;
        value.AutoEllipsis = true;
        value.ForeColor = GuardianTheme.Ink;
        value.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        value.TextAlign = ContentAlignment.MiddleLeft;

        hunks.Dock = DockStyle.Fill;
        hunks.ForeColor = GuardianTheme.MutedInk;
        hunks.Font = new Font("Cascadia Mono", 8.5f);
        hunks.TextAlign = ContentAlignment.MiddleLeft;

        panel.Controls.Add(hunks);
        panel.Controls.Add(value);
        panel.Controls.Add(heading);
        return panel;
    }

    private Control BuildTechnicalSection()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = GuardianTheme.SurfaceSoft,
            Padding = new Padding(16, 13, 16, 14),
            Margin = new Padding(0)
        };

        var heading = new Label
        {
            AutoSize = true,
            Text = "TECHNICAL IDENTITY",
            ForeColor = GuardianTheme.MutedInk,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 8)
        };

        _technical.AutoSize = true;
        _technical.MaximumSize = new Size(1800, 0);
        _technical.ForeColor = GuardianTheme.SoftInk;
        _technical.Font = new Font("Cascadia Mono", 8.2f);
        _technical.Margin = new Padding(0, 0, 0, 10);

        _workboard.AutoSize = true;
        _workboard.MaximumSize = new Size(1800, 0);
        _workboard.ForeColor = GuardianTheme.FaintInk;
        _workboard.Font = new Font("Segoe UI", 8.5f);
        _workboard.Margin = new Padding(0);

        panel.Controls.Add(heading, 0, 0);
        panel.Controls.Add(_technical, 0, 1);
        panel.Controls.Add(_workboard, 0, 2);
        return panel;
    }

    private static string HunkText(int count) =>
        count == 1 ? "1 hunk" : $"{count} hunks";

    private static void SetCard(
        Label label,
        string caption,
        string value,
        ReconcileSummaryTone tone)
    {
        label.AccessibleName = $"{caption}: {value}";
        label.Text = value;
        ApplyTone(label, tone, strong: false);
    }

    private static void ApplyTone(
        Label label,
        ReconcileSummaryTone tone,
        bool strong)
    {
        var (foreground, background) = tone switch
        {
            ReconcileSummaryTone.Positive => (GuardianTheme.Healthy, GuardianTheme.HealthyFill),
            ReconcileSummaryTone.Conflict => (GuardianTheme.Warning, GuardianTheme.WarningFill),
            ReconcileSummaryTone.Caution => (GuardianTheme.Reconcile, GuardianTheme.ChangesFill),
            _ => (GuardianTheme.SoftInk, GuardianTheme.SurfaceSoft)
        };

        label.ForeColor = foreground;
        if (strong)
            label.BackColor = background;
    }
}
