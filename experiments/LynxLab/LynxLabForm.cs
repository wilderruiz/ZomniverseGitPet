namespace LynxLab;

internal sealed class LynxLabForm : Form
{
    private readonly IReadOnlyList<ILynxRenderer> _renderers;
    private ILynxRenderer _renderer;
    private readonly LynxCanvas _canvas;
    private readonly Direct2DTestControl _direct2DCanvas;
    private readonly LynxDesktopPreviewForm _desktopPreview;
    private readonly Label _viewportCaption;
    private readonly Label _rendererValue;
    private readonly Label _stateValue;
    private readonly Label _activityValue;
    private readonly Label _paletteValue;
    private readonly Label _motionValue;
    private readonly Label _expressionValue;
    private readonly Label _dxSummaryValue;
    private readonly Label _direct2DValue;
    private readonly Label _directCompositionValue;
    private readonly Label _d3d11Value;
    private readonly Label _featureLevelValue;
    private readonly Label _dxgiValue;
    private readonly Label _direct2DTargetValue;
    private readonly Label _direct2DFramesValue;
    private readonly CheckBox _desktopPreviewToggle;
    private readonly CheckBox _debugToggle;
    private readonly CheckBox _topMostToggle;
    private readonly System.Windows.Forms.Timer _animationTimer;
    private readonly System.Diagnostics.Stopwatch _animationClock =
        System.Diagnostics.Stopwatch.StartNew();
    private double _stateChangedAtSeconds;
    private double _activityChangedAtSeconds;
    private LynxActivityState _activity = LynxActivityState.None;

    public LynxLabForm()
    {
        _renderers =
        [
            new HairyGuardianRenderer(),
            new GuardianV3Renderer()
        ];
        _renderer = _renderers[^1];

        Text = "Lynx Lab — Direct2D Target Phase";
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

        _direct2DCanvas = new Direct2DTestControl
        {
            Dock = DockStyle.Fill
        };

        _desktopPreview = new LynxDesktopPreviewForm(_renderer);
        _viewportCaption = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Margin = new Padding(2, 2, 2, 12),
            Text = "LAB VIEWPORT  ·  " + _renderer.Name,
            ForeColor = Color.FromArgb(0x87, 0x97, 0xA9),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        _rendererValue = ValueLabel(_renderer.Name);
        _stateValue = ValueLabel("Idle");
        _activityValue = ValueLabel("None");
        _paletteValue = ValueLabel(LynxPalette.Default.Name);
        _motionValue = ValueLabel("state loop");
        _expressionValue = ValueLabel("serious neutral");
        _dxSummaryValue = ValueLabel("probing...");
        _direct2DValue = ValueLabel("probing...");
        _directCompositionValue = ValueLabel("probing...");
        _d3d11Value = ValueLabel("probing...");
        _featureLevelValue = ValueLabel("probing...");
        _dxgiValue = ValueLabel("probing...");
        _direct2DTargetValue = ValueLabel("not initialized");
        _direct2DFramesValue = ValueLabel("0");
        _direct2DCanvas.BackendStatusChanged += (_, _) =>
        {
            _direct2DTargetValue.Text = _direct2DCanvas.BackendStatus;
        };
        _desktopPreviewToggle = LabCheckBox("Desktop preview", true);
        _debugToggle = LabCheckBox("Debug geometry", false);
        _topMostToggle = LabCheckBox("Preview always on top", true);

        _animationTimer = new System.Windows.Forms.Timer
        {
            Interval = 33
        };
        _animationTimer.Tick += (_, _) => AdvanceAnimation();

        Controls.Add(BuildLayout());

        Shown += (_, _) =>
        {
            RefreshGraphicsDiagnostics();
            _desktopPreview.Show(this);
            _animationTimer.Start();
        };

        FormClosed += (_, _) =>
        {
            _animationTimer.Stop();
            _animationTimer.Dispose();
            _desktopPreview.Dispose();
        };
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
            Text = "EXPERIMENTS / LYNX LAB / LIVE GUARDIAN",
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
            Text = "Isolated from production GitPet · live Hairy Guardian · 240×246 desktop preview",
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
            BackColor = Color.FromArgb(0x1B, 0x28, 0x34),
            Margin = Padding.Empty
        };

        split.Panel1.Padding = Padding.Empty;
        split.Panel2.Padding = Padding.Empty;
        split.Panel1.Controls.Add(BuildPreviewPanel());
        split.Panel2.Controls.Add(BuildControlPanel());

        void ApplyInitialSplitter()
        {
            var available = split.ClientSize.Width - split.SplitterWidth;
            if (available < 360) return;

            var desired = (int)Math.Round(available * 0.68);
            split.SplitterDistance = Math.Clamp(desired, 180, available - 180);
        }

        split.HandleCreated += (_, _) => BeginInvoke(ApplyInitialSplitter);

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

        inner.Controls.Add(_viewportCaption, 0, 0);
        inner.Controls.Add(BuildBackendComparisonPanel(), 0, 1);
        shell.Controls.Add(inner);
        return shell;
    }

    private Control BuildBackendComparisonPanel()
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            BorderStyle = BorderStyle.None,
            SplitterWidth = 6,
            BackColor = Color.FromArgb(0x1B, 0x28, 0x34),
            Margin = Padding.Empty
        };

        split.Panel1.Padding = Padding.Empty;
        split.Panel2.Padding = Padding.Empty;
        split.Panel1.Controls.Add(
            BackendPanel(
                "GDI+ / GUARDIAN V9",
                _canvas,
                Color.FromArgb(0x8C, 0x73, 0xE8)));

        split.Panel2.Controls.Add(
            BackendPanel(
                "DIRECT2D / NATIVE HWND TARGET",
                _direct2DCanvas,
                Color.FromArgb(0x57, 0xD7, 0xA0)));

        void ApplyInitialSplitter()
        {
            var available =
                split.ClientSize.Width -
                split.SplitterWidth;

            if (available < 260)
                return;

            split.SplitterDistance =
                Math.Clamp(
                    available / 2,
                    130,
                    available - 130);
        }

        split.HandleCreated +=
            (_, _) => BeginInvoke(ApplyInitialSplitter);

        return split;
    }

    private static Control BackendPanel(
        string title,
        Control content,
        Color accent)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Color.FromArgb(0x0B, 0x10, 0x16)
        };

        panel.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(
            new RowStyle(SizeType.Absolute, 24));
        panel.RowStyles.Add(
            new RowStyle(SizeType.Percent, 100));

        var label = new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(7, 0, 0, 0),
            BackColor = Color.FromArgb(0x0F, 0x16, 0x1E),
            ForeColor = accent,
            Font = new Font(
                "Segoe UI",
                7.5f,
                FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };

        content.Dock = DockStyle.Fill;

        panel.Controls.Add(label, 0, 0);
        panel.Controls.Add(content, 0, 1);

        return panel;
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

        stack.Controls.Add(SectionLabel("RENDERER"));

        var rendererSelector = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            DisplayMember = nameof(ILynxRenderer.Name),
            FormattingEnabled = true,
            Height = 32,
            BackColor = Color.FromArgb(0x15, 0x1E, 0x27),
            ForeColor = ForeColor,
            FlatStyle = FlatStyle.Flat
        };
        rendererSelector.Items.AddRange(_renderers.Cast<object>().ToArray());
        rendererSelector.SelectedItem = _renderer;
        rendererSelector.SelectedIndexChanged += (_, _) =>
        {
            if (rendererSelector.SelectedItem is ILynxRenderer selected)
                SetRenderer(selected);
        };
        stack.Controls.Add(rendererSelector);

        stack.Controls.Add(Spacer());

        stack.Controls.Add(SectionLabel("SIMULATED GIT STATE"));

        foreach (var state in Enum.GetValues<LynxVisualState>())
        {
            var button = LabButton(state.ToString());
            button.Tag = state;
            button.Click += (_, _) => SetState((LynxVisualState)button.Tag);
            stack.Controls.Add(button);
        }

        stack.Controls.Add(Spacer());
        stack.Controls.Add(SectionLabel("SIMULATED ACTIVITY"));

        foreach (var activity in Enum.GetValues<LynxActivityState>())
        {
            var button = LabButton(
                activity == LynxActivityState.None
                    ? "Clear activity"
                    : activity.ToString());
            button.Tag = activity;
            button.Click += (_, _) =>
                SetActivity((LynxActivityState)button.Tag);
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
            FlatStyle = FlatStyle.Flat
        };
        palette.Items.AddRange(LynxPalette.All.Cast<object>().ToArray());
        palette.SelectedItem = LynxPalette.Default;
        palette.SelectedIndexChanged += (_, _) =>
        {
            if (palette.SelectedItem is LynxPalette selected)
                SetPalette(selected);
        };
        stack.Controls.Add(palette);

        stack.Controls.Add(Spacer());
        stack.Controls.Add(SectionLabel("GRAPHICS DIAGNOSTICS"));
        stack.Controls.Add(KeyValueRow("DirectX", _dxSummaryValue));
        stack.Controls.Add(KeyValueRow("Direct2D", _direct2DValue));
        stack.Controls.Add(KeyValueRow("DirectComposition", _directCompositionValue));
        stack.Controls.Add(KeyValueRow("D3D11", _d3d11Value));
        stack.Controls.Add(KeyValueRow("Feature level", _featureLevelValue));
        stack.Controls.Add(KeyValueRow("DXGI", _dxgiValue));
        stack.Controls.Add(KeyValueRow("D2D target", _direct2DTargetValue));
        stack.Controls.Add(KeyValueRow("D2D frames", _direct2DFramesValue));

        var probeGraphics = LabButton("Probe graphics again");
        probeGraphics.Click += (_, _) => RefreshGraphicsDiagnostics();
        stack.Controls.Add(probeGraphics);

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
        stack.Controls.Add(KeyValueRow("Renderer", _rendererValue));
        stack.Controls.Add(KeyValueRow("Canvas", "A/B · GDI+ Guardian + Direct2D HWND"));
        stack.Controls.Add(KeyValueRow("Pet size", "160 × 160"));
        stack.Controls.Add(KeyValueRow("Desktop host", "240 × 246"));
        stack.Controls.Add(KeyValueRow("State", _stateValue));
        stack.Controls.Add(KeyValueRow("Activity", _activityValue));
        stack.Controls.Add(KeyValueRow("Motion", _motionValue));
        stack.Controls.Add(KeyValueRow("Expression", _expressionValue));
        stack.Controls.Add(KeyValueRow("Palette", _paletteValue));

        var note = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(420, 0),
            Margin = new Padding(0, 16, 0, 10),
            Text = "Direct2D Target Phase: the left viewport remains the approved Guardian V9 on GDI+. The right viewport is a real native Direct2D HWND render target driven by the same state, activity, palette and animation clock. This validates the hardware path before Guardian geometry is ported layer-by-layer.",
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

    private void RefreshGraphicsDiagnostics()
    {
        _dxSummaryValue.Text = "probing...";
        _direct2DValue.Text = "probing...";
        _directCompositionValue.Text = "probing...";
        _d3d11Value.Text = "probing...";
        _featureLevelValue.Text = "probing...";
        _dxgiValue.Text = "probing...";

        try
        {
            var result = DirectXDiagnosticsResult.Probe();

            _dxSummaryValue.Text = result.Summary;
            _direct2DValue.Text = result.Direct2DFactory
                ? "AVAILABLE ✓ · factory created"
                : result.Direct2DDll
                    ? "DLL found · factory failed"
                    : "UNAVAILABLE";

            _directCompositionValue.Text =
                result.DirectCompositionDll &&
                result.DirectCompositionExport
                    ? "AVAILABLE ✓"
                    : result.DirectCompositionDll
                        ? "DLL found · export unavailable"
                        : "UNAVAILABLE";

            _d3d11Value.Text = result.D3D11Hardware
                ? "HARDWARE ✓"
                : result.D3D11Warp
                    ? "WARP / SOFTWARE ✓"
                    : result.D3D11Dll
                        ? "DLL found · device creation failed"
                        : "UNAVAILABLE";

            _featureLevelValue.Text = result.FeatureLevel;
            _dxgiValue.Text = result.DxgiDll
                ? "AVAILABLE ✓"
                : "UNAVAILABLE";

            if (!string.IsNullOrWhiteSpace(result.Error))
                _dxSummaryValue.Text += " · " + result.Error;
        }
        catch (Exception ex)
        {
            LabCrashLog.Write("DirectX diagnostics", ex);
            _dxSummaryValue.Text = "probe failed · GDI+ remains active";
            _direct2DValue.Text = "probe failed";
            _directCompositionValue.Text = "probe failed";
            _d3d11Value.Text = "probe failed";
            _featureLevelValue.Text = "n/a";
            _dxgiValue.Text = "probe failed";
        }
    }

    private void AdvanceAnimation()
    {
        var now = _animationClock.Elapsed.TotalSeconds;

        if (_renderer is IAnimatedLynxRenderer animated)
        {
            var frame = LynxAnimationFrame.FromSeconds(
                now,
                _canvas.State,
                now - _stateChangedAtSeconds);

            animated.SetAnimationFrame(frame);
            _motionValue.Text = frame.TransitionAmount > 0.015f
                ? "reacting → " + _canvas.State
                : "state loop · " + _canvas.State;
        }

        if (_renderer is IActivityLynxRenderer activityRenderer)
        {
            var activityElapsed = now - _activityChangedAtSeconds;
            activityRenderer.SetActivityFrame(
                _activity,
                activityElapsed);

            if (_activity == LynxActivityState.None)
            {
                _paletteValue.Text = _canvas.Palette.Name;
            }
            else
            {
                var partner = LynxPalette.ActivityPartner(_activity);
                var mix = LynxPalette.ActivityMix(
                    _activity,
                    activityElapsed);

                _paletteValue.Text =
                    $"{_canvas.Palette.Name} ↔ {partner.Name} · {(int)Math.Round(mix * 100f)}%";
            }
        }

        _direct2DCanvas.SetFrame(
            now,
            _canvas.Palette,
            _canvas.State,
            _activity);

        _direct2DTargetValue.Text =
            _direct2DCanvas.BackendStatus;
        _direct2DFramesValue.Text =
            _direct2DCanvas.FrameCount.ToString("N0");

        _canvas.Invalidate();

        if (_desktopPreview.Visible)
            _desktopPreview.Invalidate();
    }

    private void SetState(LynxVisualState state)
    {
        _canvas.State = state;
        _desktopPreview.State = state;
        _stateValue.Text = state.ToString();
        _expressionValue.Text = _activity == LynxActivityState.None
            ? ExpressionName(state)
            : ActivityExpressionName(_activity);
        _stateChangedAtSeconds = _animationClock.Elapsed.TotalSeconds;

        // Render the first reaction frame immediately rather than waiting for
        // the next 33 ms timer tick.
        AdvanceAnimation();
    }

    private void SetActivity(LynxActivityState activity)
    {
        _activity = activity;
        _activityChangedAtSeconds = _animationClock.Elapsed.TotalSeconds;
        _activityValue.Text = ActivityName(activity);
        _expressionValue.Text = activity == LynxActivityState.None
            ? ExpressionName(_canvas.State)
            : ActivityExpressionName(activity);
        _desktopPreview.Activity = activity;

        AdvanceAnimation();
    }

    private void SetRenderer(ILynxRenderer renderer)
    {
        _renderer = renderer;

        var now = _animationClock.Elapsed.TotalSeconds;

        if (renderer is IAnimatedLynxRenderer animated)
        {
            var frame = LynxAnimationFrame.FromSeconds(
                now,
                _canvas.State,
                now - _stateChangedAtSeconds);
            animated.SetAnimationFrame(frame);
            _motionValue.Text = frame.TransitionAmount > 0.015f
                ? "reacting → " + _canvas.State
                : "state loop · " + _canvas.State;
        }

        if (renderer is IActivityLynxRenderer activityRenderer)
        {
            activityRenderer.SetActivityFrame(
                _activity,
                now - _activityChangedAtSeconds);
        }

        _canvas.Renderer = renderer;
        _desktopPreview.Renderer = renderer;
        _viewportCaption.Text = "LAB VIEWPORT  ·  " + renderer.Name;
        _rendererValue.Text = renderer.Name;
    }

    private static string ActivityName(LynxActivityState activity) =>
        activity switch
        {
            LynxActivityState.Thinking => "thinking / scanning",
            LynxActivityState.Preparing => "preparing / inspecting",
            LynxActivityState.Sorting => "sorting / staging",
            LynxActivityState.Packing => "packing checkpoint",
            LynxActivityState.Incoming => "incoming / received",
            LynxActivityState.Outgoing => "outgoing / dispatching",
            LynxActivityState.Reconciling => "reconciling histories",
            LynxActivityState.Success => "success / completed",
            LynxActivityState.Warning => "warning / needs attention",
            LynxActivityState.Failure => "failure / needs help",
            LynxActivityState.Resting => "resting / low activity",
            _ => "none"
        };

    private static string ActivityExpressionName(
        LynxActivityState activity) =>
        activity switch
        {
            LynxActivityState.Thinking => "focused / analysing",
            LynxActivityState.Preparing => "inspecting / deliberate",
            LynxActivityState.Sorting => "busy / coordinated",
            LynxActivityState.Packing => "determined / locking",
            LynxActivityState.Incoming => "surprised / attentive",
            LynxActivityState.Outgoing => "confident / dispatching",
            LynxActivityState.Reconciling => "intense / calculating",
            LynxActivityState.Success => "pleased / complete",
            LynxActivityState.Warning => "concerned / cautious",
            LynxActivityState.Failure => "frustrated / guarded",
            LynxActivityState.Resting => "sleepy / relaxed",
            _ => "base state"
        };

    private static string ExpressionName(LynxVisualState state) =>
        state switch
        {
            LynxVisualState.Clean => "relaxed / safe",
            LynxVisualState.Changes => "alert / investigating",
            LynxVisualState.Attention => "hard focus / vigilant",
            LynxVisualState.Save => "satisfied / acknowledged",
            LynxVisualState.Get => "curious / incoming",
            LynxVisualState.Send => "confident / dispatched",
            LynxVisualState.Conflict => "combat-ready / guarded",
            _ => "serious neutral"
        };

    private void SetPalette(LynxPalette palette)
    {
        _canvas.Palette = palette;
        _desktopPreview.Palette = palette;
        if (_activity == LynxActivityState.None)
        {
            _paletteValue.Text = palette.Name;
        }
        else
        {
            var partner = LynxPalette.ActivityPartner(_activity);
            _paletteValue.Text = $"{palette.Name} ↔ {partner.Name}";
        }
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
