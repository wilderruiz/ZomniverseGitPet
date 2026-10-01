namespace ZomniverseGitPet;

internal sealed class ReconcileMergedConflictForm : Form
{
    private readonly string _generatedText;
    private readonly RichTextBox _generated = new();
    private readonly RichTextBox _edited = new();
    private readonly Label _status = new();
    private readonly Button _accept = new();

    public string MergedText { get; private set; } = "";

    public ReconcileMergedConflictForm(string relativePath, string generatedText)
    {
        _generatedText = generatedText ?? "";

        Text = "Resolve merged version";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1180, 760);
        MinimumSize = new Size(900, 600);
        BackColor = GuardianTheme.Window;
        ForeColor = GuardianTheme.Ink;
        Font = new Font("Segoe UI", 9);
        ShowInTaskbar = false;
        KeyPreview = true;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            ColumnCount = 1,
            Padding = new Padding(14),
            BackColor = GuardianTheme.Window
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));

        var intro = new Label
        {
            Dock = DockStyle.Fill,
            Text =
                "RESOLVE MERGED VERSION\r\n\r\n" +
                relativePath +
                "\r\nEdit the right side into the complete file GitPet should keep. " +
                "The generated Git candidate stays immutable on the left. " +
                "Git conflict markers must be removed before this version can be used.",
            ForeColor = GuardianTheme.MutedInk,
            Font = new Font("Segoe UI", 9.5f),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };

        var split = new SafeSplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 6,
            PreferredRatio = 0.5
        };

        split.Panel1.Controls.Add(BuildPane(
            "GENERATED CANDIDATE · READ ONLY",
            _generated,
            readOnly: true));
        split.Panel2.Controls.Add(BuildPane(
            "RESOLVED MERGED VERSION · EDITABLE",
            _edited,
            readOnly: false));

        _generated.Text = _generatedText;
        _edited.Text = _generatedText;

        _status.Dock = DockStyle.Fill;
        _status.Padding = new Padding(8, 0, 8, 0);
        _status.BackColor = GuardianTheme.ConsoleHeader;
        _status.ForeColor = GuardianTheme.Warning;
        _status.Font = new Font("Cascadia Mono", 8f);
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _status.AccessibleName = "Merged candidate status";

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 10, 0, 0),
            BackColor = GuardianTheme.Window
        };

        _accept.Text = "Use merged version";
        _accept.Width = 180;
        _accept.Height = 38;
        _accept.BackColor = GuardianTheme.Violet;
        _accept.ForeColor = Color.White;
        _accept.FlatStyle = FlatStyle.Flat;
        _accept.FlatAppearance.BorderColor = GuardianTheme.HotPinkSoft;
        _accept.AccessibleName = "Use resolved merged version";
        _accept.Click += (_, _) => AcceptMergedText();

        var cancel = new Button
        {
            Text = "Cancel",
            Width = 100,
            Height = 38,
            BackColor = GuardianTheme.SurfaceRaised,
            ForeColor = GuardianTheme.Ink,
            FlatStyle = FlatStyle.Flat,
            DialogResult = DialogResult.Cancel,
            Margin = new Padding(8, 0, 0, 0)
        };
        cancel.FlatAppearance.BorderColor = GuardianTheme.Border;

        buttons.Controls.Add(_accept);
        buttons.Controls.Add(cancel);

        root.Controls.Add(intro, 0, 0);
        root.Controls.Add(split, 0, 1);
        root.Controls.Add(_status, 0, 2);
        root.Controls.Add(buttons, 0, 3);
        Controls.Add(root);

        _edited.TextChanged += (_, _) => RefreshState();
        Shown += (_, _) =>
        {
            _edited.Focus();
            RefreshState();
        };

        AcceptButton = _accept;
        CancelButton = cancel;

        KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Escape) return;
            e.SuppressKeyPress = true;
            DialogResult = DialogResult.Cancel;
            Close();
        };

        RefreshState();
    }

    private static Control BuildPane(string titleText, RichTextBox box, bool readOnly)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            BackColor = GuardianTheme.Console
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var title = new Label
        {
            Dock = DockStyle.Fill,
            Text = titleText,
            Padding = new Padding(8, 0, 8, 0),
            BackColor = GuardianTheme.ConsoleHeader,
            ForeColor = GuardianTheme.FaintInk,
            Font = new Font("Cascadia Mono", 8f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };

        box.Dock = DockStyle.Fill;
        box.BorderStyle = BorderStyle.None;
        box.BackColor = GuardianTheme.Console;
        box.ForeColor = GuardianTheme.Ink;
        box.Font = new Font("Cascadia Mono", 9f);
        box.WordWrap = false;
        box.AcceptsTab = !readOnly;
        box.DetectUrls = false;
        box.ReadOnly = readOnly;
        box.ScrollBars = RichTextBoxScrollBars.Both;
        box.AccessibleName = readOnly
            ? "Generated merged candidate"
            : "Resolved merged candidate editor";

        panel.Controls.Add(title, 0, 0);
        panel.Controls.Add(box, 0, 1);
        return panel;
    }

    private void RefreshState()
    {
        var text = _edited.Text ?? "";
        if (text.IndexOf('\0') >= 0)
        {
            _accept.Enabled = false;
            _status.ForeColor = GuardianTheme.Warning;
            _status.Text = "Cannot use merged version · binary NUL detected.";
            return;
        }

        if (ReconcileMergedCandidateService.HasConflictMarkers(text))
        {
            _accept.Enabled = false;
            _status.ForeColor = GuardianTheme.Warning;
            _status.Text =
                "Resolve all <<<<<<< / ======= / >>>>>>> conflict blocks before using this merged version.";
            return;
        }

        _accept.Enabled = true;
        _status.ForeColor = GuardianTheme.Healthy;
        _status.Text = "Merged version is conflict-marker free ✓ · Save reconciliation remains a separate step.";
    }

    private void AcceptMergedText()
    {
        RefreshState();
        if (!_accept.Enabled)
        {
            using var blocked = new GuardianConfirmDialog(
                "Merged version is not ready",
                "RESOLVE THE CONFLICT MARKERS FIRST",
                "The merged version still contains unresolved Git conflict markers or binary data.\r\n\r\n" +
                "Edit the right side until the file is complete, then choose Use merged version.",
                confirmText: "OK",
                cancelText: "",
                showCancel: false,
                dialogSize: new Size(680, 360));
            blocked.ShowDialog(this);
            return;
        }

        MergedText = ReconcileLocalEditService.NormalizeDraftForOriginal(
            _generatedText,
            _edited.Text ?? "");

        DialogResult = DialogResult.OK;
        Close();
    }
}
