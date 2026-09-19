namespace LynxLab;

internal sealed class LynxLabForm : Form
{
    private readonly ILynxRenderer _renderer = new GdiLynxRenderer();
    private readonly LynxCanvas _canvas;
    private readonly LynxDesktopPreviewForm _desktopPreview;
    private readonly Label _stateValue;
    private readonly Label _paletteValue;
    private readonly CheckBox _desktopPreviewToggle;
    private readonly CheckBox _debugToggle;
    private readonly CheckBox _topMostToggle;

    public LynxLabForm()
    {
        Text = "Lynx Lab — Phase 0";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(700, 500);
        Size = new Size(1100, 760);
        BackColor = Color.FromArgb(0x09, 0x0D, 0x12);
        ForeColor = Color.FromArgb(0xE8, 0xEE, 0xF5);
        Font = new Font("Segoe UI", 9f);
        AutoScaleMode = AutoScaleMode.Dpi;

        _canvas = new LynxCanvas(_renderer)
        {
            Dock = DockStyle.Fill
        };

        _desktopPreview = new LynxDesktopPreviewForm(_renderer);
        _stateValue = ValueLabel("Idle");
        _paletteValue = ValueLabel(LynxPalette.All[0].Name);
        _desktopPreviewToggle = LabCheckBox("Desktop preview", true);
        _debugToggle = LabCheckBox("Debug geometry", false);
        _topMostToggle = LabCheckBox("Preview always on top", true);

        Controls.Add(BuildLayout());

        Shown += (_, _) => _desktopPreview.Show(this);
        FormClosed += (_, _) => _desktopPreview.Dispose();
    }

    private Control BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(22, 20, 22, 22),
            BackColor = BackColor
        };

        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var header = BuildHeader();
        var workbench = BuildWorkbench();

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(workbench, 0, 1);

        return root;
    }

    private Control BuildHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(0, 0, 0, 16),
            BackColor = BackColor
        };

        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        header.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var kicker = new Label
        {
            Text = "EXPERIMENTS / LYNX LAB / PHASE 0",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4),
            ForeColor = Color.FromArgb(0x79, 0xD8, 0xCA),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
        };

        var title = new Label
        {
            Text = "Functional test host",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4),
            ForeColor = ForeColor,
            Font = new Font("Segoe UI", 18f, FontStyle.Bold)
        };

        var subtitle = new Label
        {
            Text = "Isolated from production GitPet · 160×160 Lynx · 240×246 desktop preview",
            AutoSize = true,
            MaximumSize = new Size(900, 0),
            Margin = new Padding(0, 0, 0, 0),
            ForeColor = Color.FromArgb(0x8D, 0x9A, 0xAA)
        };

        header.Controls.Add(kicker, 0, 0);
        header.Controls.Add(title, 0, 1);
        header.Controls.Add(subtitle, 0, 2);
        return header;
    }

    private Control BuildWorkbench()
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            BorderStyle = BorderStyle.None,
            SplitterWidth = 7,
            SplitterDistance = 720,
            Panel1MinSize = 300,
            Panel2MinSize = 240,
            BackColor = Color.FromArgb(0x1B, 0x28, 0x34),
            Margin = Padding.Empty
        };

        split.Panel1.Padding = Padding.Empty;
        split.Panel2.Padding = Padding.Empty;
        split.Panel1.Controls.Add(BuildPreviewPanel());
        split.Panel2.Controls.Add(BuildControlPanel());

        return split;
    }

    private Control BuildPreviewPanel()
    {
        var shell = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(1),
            BackColor = Color.FromArgb(0x26, 0x33, 0x42)
        };

        var inner = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(16),
            BackColor = Color.FromArgb(0x0F, 0x16, 0x1E)
        };

        inner.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        inner.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        inner.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var caption = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Margin = new Padding(2, 2, 2, 12),
            Text = "LAB VIEWPORT  ·  " + _renderer.Name,
            ForeColor = Color.FromArgb(0x87, 0x97, 0xA9),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };

        inner.Controls.Add(caption, 0, 0);
        inner.Controls.Add(_canvas, 0, 1);
        shell.Controls.Add(inner);
        return shell;
    }

    private Control BuildControlPanel()
    {
        var shell = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14),
            BackColor = Color.FromArgb(0x11, 0x18, 0x20)
        };

        var stack = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = shell.BackColor,
            Padding = new Padding(0, 0, 4, 0)
        };

        stack.Controls.Add(SectionLabel("SIMULATED GIT STATE"));

        foreach (var state in Enum.GetValues<LynxVisualState>())
        {
            var button = LabButton(state.ToString());
            button.Tag = state;
            button.Click += (_, _) => SetState((LynxVisualState)button.Tag);
            stack.Controls.Add(button);
        }

        stack.Controls.Add(Spacer());
        stack.Controls.Add(SectionLabel("PALETTE"));

        var palette = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Height = 32,
            BackColor = Color.FromArgb(0x15, 0x1E, 0x27),
            ForeColor = ForeColor,
            FlatStyle = FlatStyle.Flat,
            DataSource = LynxPalette.All.ToList()
        };
        palette.SelectedIndexChanged += (_, _) =>
        {
            if (palette.SelectedItem is LynxPalette selected)
                SetPalette(selected);
        };
        stack.Controls.Add(palette);

        stack.Controls.Add(Spacer());
        stack.Controls.Add(SectionLabel("VIEW"));

        _desktopPreviewToggle.CheckedChanged += (_, _) =>
        {
            if (_desktopPreviewToggle.Checked)
            {
                if (!_desktopPreview.Visible) _desktopPreview.Show(this);
            }
            else
            {
                _desktopPreview.Hide();
            }
        };

        _debugToggle.CheckedChanged += (_, _) =>
        {
            _canvas.DebugOverlay = _debugToggle.Checked;
            _desktopPreview.DebugOverlay = _debugToggle.Checked;
        };

        _topMostToggle.CheckedChanged += (_, _) =>
            _desktopPreview.TopMost = _topMostToggle.Checked;

        stack.Controls.Add(_desktopPreviewToggle);
        stack.Controls.Add(_debugToggle);
        stack.Controls.Add(_topMostToggle);

        var reset = LabButton("Reset desktop position");
        reset.Click += (_, _) => _desktopPreview.PositionAtBottomRight();
        stack.Controls.Add(reset);

        stack.Controls.Add(Spacer());
        stack.Controls.Add(SectionLabel("TELEMETRY"));
        stack.Controls.Add(KeyValueRow("Renderer", _renderer.Name));
        stack.Controls.Add(KeyValueRow("Canvas", "vector / GDI+"));
        stack.Controls.Add(KeyValueRow("Pet size", "160 × 160"));
        stack.Controls.Add(KeyValueRow("Desktop host", "240 × 246"));
        stack.Controls.Add(KeyValueRow("State", _stateValue));
        stack.Controls.Add(KeyValueRow("Palette", _paletteValue));

        var note = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(420, 0),
            Margin = new Padding(0, 16, 0, 10),
            Text = "Phase 0 proves the lab shell, state controls, palette switching, transparent desktop host and debug geometry. Direct2D comes later.",
            ForeColor = Color.FromArgb(0x78, 0x88, 0x9A)
        };
        stack.Controls.Add(note);

        void ResizeInspectorRows()
        {
            var scrollbar = stack.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0;
            var available = Math.Max(180, stack.ClientSize.Width - scrollbar - stack.Padding.Horizontal - 8);

            foreach (Control control in stack.Controls)
            {
                control.Width = available;

                if (control is Label label && ReferenceEquals(label, note))
                    label.MaximumSize = new Size(available, 0);
            }
        }

        stack.ClientSizeChanged += (_, _) => ResizeInspectorRows();
        stack.ControlAdded += (_, _) => ResizeInspectorRows();

        shell.Controls.Add(stack);

        shell.HandleCreated += (_, _) => BeginInvoke(ResizeInspectorRows);
        return shell;
    }

    private void SetState(LynxVisualState state)
    {
        _canvas.State = state;
        _desktopPreview.State = state;
        _stateValue.Text = state.ToString();
    }

    private void SetPalette(LynxPalette palette)
    {
        _canvas.Palette = palette;
        _desktopPreview.Palette = palette;
        _paletteValue.Text = palette.Name;
    }

    private static Label SectionLabel(string text) =>
        new()
        {
            Text = text,
            AutoSize = false,
            Height = 28,
            Margin = new Padding(0, 4, 0, 5),
            ForeColor = Color.FromArgb(0x79, 0xD8, 0xCA),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };

    private static Button LabButton(string text) =>
        new()
        {
            Text = text,
            Height = 34,
            Margin = new Padding(0, 0, 0, 6),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(0x15, 0x1E, 0x27),
            ForeColor = Color.FromArgb(0xD4, 0xDE, 0xE8)
        };

    private static CheckBox LabCheckBox(string text, bool isChecked) =>
        new()
        {
            Text = text,
            Checked = isChecked,
            AutoSize = false,
            Height = 30,
            ForeColor = Color.FromArgb(0xBF, 0xCB, 0xD7)
        };

    private static Label ValueLabel(string text) =>
        new()
        {
            Text = text,
            AutoEllipsis = true,
            ForeColor = Color.FromArgb(0xDF, 0xE7, 0xEF),
            TextAlign = ContentAlignment.MiddleLeft
        };

    private static Control KeyValueRow(string key, string value) =>
        KeyValueRow(key, ValueLabel(value));

    private static Control KeyValueRow(string key, Control value)
    {
        var row = new TableLayoutPanel
        {
            Height = 26,
            ColumnCount = 2,
            Margin = Padding.Empty
        };

        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));

        row.Controls.Add(new Label
        {
            Text = key,
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            ForeColor = Color.FromArgb(0x72, 0x82, 0x94),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        value.Dock = DockStyle.Fill;
        row.Controls.Add(value, 1, 0);
        return row;
    }

    private static Control Spacer() =>
        new Panel { Height = 12, Margin = Padding.Empty };
}
