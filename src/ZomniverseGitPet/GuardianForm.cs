namespace ZomniverseGitPet;

public sealed class GuardianForm : Form
{
    private readonly AppConfig _config;
    private readonly ConfigStore _configStore;
    private readonly GitService _git;
    private readonly AuditLog _audit;
    private readonly Func<Task> _chooseRepository;

    private readonly Label _projectTitle = new();
    private readonly Label _onlineLabel = new();
    private readonly Label _commitLabel = new();
    private readonly GuardianStatusChip _healthChip = new();
    private readonly GuardianStatusChip _branchChip = new();
    private readonly GuardianStatusChip _changesChip = new();

    private readonly DataGridView _files = new();
    private readonly Label _emptyState = new();
    private readonly RichTextBox _output = new();
    private readonly Label _activityState = new();
    private readonly CheckBox _automatic = new();
    private readonly FileComparisonPanel _comparisonPanel = new();
    private Panel? _activityPanel;

    private readonly ToolTip _toolTips = new()
    {
        InitialDelay = 350,
        ReshowDelay = 100,
        AutoPopDelay = 15000,
        ShowAlways = true
    };
    private readonly Font _toolTipFont = new("Segoe UI", 9);

    private readonly System.Windows.Forms.Timer _pulseTimer = new() { Interval = 1050 };
    private bool _pulseBright;

    private readonly Button[] _operationButtons;
    private readonly GuardianActionButton _cancelButton;

    private CancellationTokenSource? _operation;
    private CancellationTokenSource? _comparisonLoad;
    private RepositoryStatus? _status;
    private bool _refreshInProgress;
    private bool _exitRequested;
    private string? _reviewedPath;

    public GuardianForm(
        AppConfig config,
        ConfigStore configStore,
        GitService git,
        AuditLog audit,
        Func<Task> chooseRepository)
    {
        _config = config;
        _configStore = configStore;
        _git = git;
        _audit = audit;
        _chooseRepository = chooseRepository;

        Text = "ZomniverseGitPet Guardian";
        Icon = AppIconProvider.Icon;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(920, 670);
        Size = new Size(1180, 820);
        BackColor = GuardianTheme.Window;
        ForeColor = GuardianTheme.Ink;
        Font = new Font("Segoe UI", 9);
        WindowChrome.ApplyGuardianChrome(this);

        ConfigureToolTips();

        var menu = BuildMainMenu();
        var header = BuildHeader();

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 66,
            Padding = new Padding(14, 10, 10, 8),
            WrapContents = true,
            AutoScroll = false,
            BackColor = GuardianTheme.Surface
        };

        var projects = MakeActionButton("Projects ▾", GuardianActionKind.Standard, 112, async () => await _chooseRepository());
        var refresh = MakeActionButton("Refresh", GuardianActionKind.Standard, 92, RefreshAsync);
        var diff = MakeActionButton("Review", GuardianActionKind.Standard, 82, ShowDiffAsync);
        var tests = MakeActionButton("Tests", GuardianActionKind.Standard, 82, RunTestsAsync);
        var checkpoint = MakeActionButton("Save", GuardianActionKind.Primary, 92, CreateCheckpointAsync);
        var pull = MakeActionButton("Get ↓", GuardianActionKind.Pull, 92, PullFromOriginAsync);
        var push = MakeActionButton("Send ↑", GuardianActionKind.Push, 92, PushToOriginAsync);
        var recent = MakeActionButton("History", GuardianActionKind.Standard, 92, RecentCommitsAsync);
        var health = MakeActionButton("Health", GuardianActionKind.Standard, 92, HealthCheckAsync);
        _cancelButton = MakeActionButton("Cancel", GuardianActionKind.Danger, 92, () =>
        {
            _operation?.Cancel();
            return Task.CompletedTask;
        });
        _cancelButton.Visible = false;

        toolbar.Controls.AddRange([projects, refresh, diff, tests, checkpoint, pull, push, recent, health, _cancelButton]);
        _operationButtons = [projects, refresh, diff, tests, checkpoint, pull, push, recent, health];

        _toolTips.SetToolTip(projects,
            "Projects\n\nSwitch between recent projects, open another folder, prepare a normal folder for Git,\n" +
            "or review what Git should ignore. GitPet remembers up to 20 recent projects.");
        _toolTips.SetToolTip(refresh,
            "Refresh\n\nRe-read the current branch and working-tree status.\n" +
            "Background monitoring also refreshes quietly without taking over the mouse cursor.");
        _toolTips.SetToolTip(diff,
            "Review\n\nGit operation: diff / working-tree comparison.\n\n" +
            "Compares the current working item with the version stored in the latest local Git commit.");
        _toolTips.SetToolTip(tests,
            "Tests\n\nRun the test commands saved for THIS project. If none are configured, GitPet opens a friendly setup window\n" +
            "and suggests likely commands for review. Hold Shift while clicking Tests to edit the saved commands later.");
        _toolTips.SetToolTip(checkpoint,
            "Save\n\nGit operation: local commit.\n\n" +
            "Saves the current non-ignored changes as a local Git commit (called a checkpoint internally by GitPet).\n\n" +
            "Nothing is pushed online.");
        _toolTips.SetToolTip(pull,
            "Get updates\n\nGit operation:\ngit pull --ff-only origin <branch>\n\n" +
            "Gets committed updates from the configured remote.\n\n" +
            "GitPet requires a clean working tree and uses fast-forward-only safety. It will never create an automatic merge commit.");
        _toolTips.SetToolTip(push,
            "Send saved updates\n\nGit operation:\ngit push origin <branch>\n\n" +
            "Sends committed history only.\n\nUnsaved / uncommitted working changes are never included.");
        _toolTips.SetToolTip(recent,
            "History\n\nShow the latest 12 local Git commits, including normal commits and checkpoints.");
        _toolTips.SetToolTip(health,
            "Health\n\nRun git fsck --no-progress to check the internal integrity of the local repository.");
        _toolTips.SetToolTip(_cancelButton,
            "Cancel\n\nRequest cancellation of the Git, test, get, send, or health operation currently running.");

        ConfigureFilesGrid();
        var filesPanel = BuildFilesPanel();
        var lowerPanel = BuildLowerPanel();

        var content = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 6,
            SplitterDistance = 360,
            Panel1MinSize = 170,
            Panel2MinSize = 150,
            BackColor = GuardianTheme.BorderSoft,
            BorderStyle = BorderStyle.None
        };
        content.Panel1.Padding = new Padding(14, 10, 14, 4);
        content.Panel2.Padding = new Padding(14, 4, 14, 10);
        content.Panel1.BackColor = GuardianTheme.Window;
        content.Panel2.BackColor = GuardianTheme.Window;
        content.Panel1.Controls.Add(filesPanel);
        content.Panel2.Controls.Add(lowerPanel);

        var options = BuildOptionsPanel();

        Controls.Add(content);
        Controls.Add(options);
        Controls.Add(toolbar);
        Controls.Add(header);
        Controls.Add(menu);
        MainMenuStrip = menu;

        SetNoProjectHeader();
        FormClosing += OnFormClosing;

        _pulseTimer.Tick += (_, _) =>
        {
            _pulseBright = !_pulseBright;
            _onlineLabel.ForeColor = _pulseBright
                ? GuardianTheme.Healthy
                : Color.FromArgb(56, 157, 108);
        };
        _pulseTimer.Start();
    }

    private Control BuildHeader()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 136,
            Padding = new Padding(20, 13, 20, 10),
            BackColor = GuardianTheme.SurfaceRaised
        };

        _projectTitle.Dock = DockStyle.Top;
        _projectTitle.Height = 28;
        _projectTitle.Text = "ZOMNIVERSE GITPET";
        _projectTitle.ForeColor = Color.White;
        _projectTitle.Font = new Font("Segoe UI", 11.5f, FontStyle.Bold);
        _projectTitle.TextAlign = ContentAlignment.MiddleLeft;
        _projectTitle.AutoEllipsis = true;

        _onlineLabel.AutoSize = false;
        _onlineLabel.Width = 180;
        _onlineLabel.Height = 26;
        _onlineLabel.Text = "● GUARDIAN ONLINE";
        _onlineLabel.ForeColor = GuardianTheme.Healthy;
        _onlineLabel.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        _onlineLabel.TextAlign = ContentAlignment.MiddleRight;
        _onlineLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _onlineLabel.Location = new Point(panel.Width - 200, 13);
        panel.Resize += (_, _) => _onlineLabel.Left = panel.ClientSize.Width - _onlineLabel.Width - 20;

        var chips = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 42,
            Padding = new Padding(0, 6, 0, 4),
            WrapContents = false,
            BackColor = GuardianTheme.SurfaceRaised
        };

        _healthChip.Width = 124;
        _branchChip.Width = 150;
        _changesChip.Width = 150;
        _healthChip.Margin = new Padding(0, 0, 8, 0);
        _branchChip.Margin = new Padding(0, 0, 8, 0);
        _changesChip.Margin = new Padding(0);
        chips.Controls.AddRange([_healthChip, _branchChip, _changesChip]);

        _commitLabel.Dock = DockStyle.Fill;
        _commitLabel.ForeColor = GuardianTheme.MutedInk;
        _commitLabel.Font = new Font("Cascadia Mono", 8.5f);
        _commitLabel.TextAlign = ContentAlignment.MiddleLeft;
        _commitLabel.AutoEllipsis = true;
        _commitLabel.Padding = new Padding(1, 2, 0, 0);

        panel.Controls.Add(_commitLabel);
        panel.Controls.Add(chips);
        panel.Controls.Add(_onlineLabel);
        panel.Controls.Add(_projectTitle);

        _toolTips.SetToolTip(_projectTitle,
            "Active project. Closing Guardian with X only hides this window; the fox keeps running.\n" +
            "Use the pet/tray Exit command to quit ZomniverseGitPet.");

        return panel;
    }

    private void ConfigureFilesGrid()
    {
        _files.Dock = DockStyle.Fill;
        _files.ReadOnly = true;
        _files.AllowUserToAddRows = false;
        _files.AllowUserToDeleteRows = false;
        _files.AllowUserToResizeRows = false;
        _files.MultiSelect = false;
        _files.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _files.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _files.BackgroundColor = GuardianTheme.Surface;
        _files.BorderStyle = BorderStyle.None;
        _files.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _files.GridColor = GuardianTheme.BorderSoft;
        _files.RowHeadersVisible = false;
        _files.EnableHeadersVisualStyles = false;
        _files.ColumnHeadersHeight = 40;
        _files.ColumnHeadersDefaultCellStyle.BackColor = GuardianTheme.SurfaceSoft;
        _files.ColumnHeadersDefaultCellStyle.ForeColor = GuardianTheme.MutedInk;
        _files.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        _files.ColumnHeadersDefaultCellStyle.SelectionBackColor = GuardianTheme.SurfaceSoft;
        _files.ColumnHeadersDefaultCellStyle.SelectionForeColor = GuardianTheme.MutedInk;
        _files.DefaultCellStyle.BackColor = GuardianTheme.Surface;
        _files.DefaultCellStyle.ForeColor = GuardianTheme.Ink;
        _files.DefaultCellStyle.SelectionBackColor = Color.FromArgb(57, 42, 77);
        _files.DefaultCellStyle.SelectionForeColor = Color.White;
        _files.DefaultCellStyle.Font = new Font("Segoe UI", 9.25f);
        _files.DefaultCellStyle.Padding = new Padding(7, 2, 7, 2);
        _files.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(31, 25, 42);
        _files.RowTemplate.Height = 34;
        _files.ShowCellToolTips = true;

        _files.Columns.Add("Status", "STATE");
        _files.Columns.Add("Path", "PATH");
        _files.Columns[0].FillWeight = 20;
        _files.Columns[1].FillWeight = 80;
        _files.Columns[0].DefaultCellStyle.Font = new Font("Segoe UI", 8.75f, FontStyle.Bold);
        _files.Columns[0].HeaderCell.ToolTipText =
            "Human-readable Git state. Hover a row for the underlying Git status code.";
        _files.Columns[1].HeaderCell.ToolTipText =
            "Changed item path. Click a row to open its Before / Now review.";

        _files.CellClick += async (_, e) =>
        {
            if (e.RowIndex < 0 || _operation is not null) return;
            _files.ClearSelection();
            _files.Rows[e.RowIndex].Selected = true;
            await ShowSelectedFileComparisonAsync();
        };
        _files.CellDoubleClick += async (_, e) =>
        {
            if (e.RowIndex >= 0 && _operation is null) await ShowSelectedFileComparisonAsync();
        };
    }

    private Control BuildFilesPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = GuardianTheme.Surface
        };

        _emptyState.Dock = DockStyle.Fill;
        _emptyState.BackColor = GuardianTheme.Surface;
        _emptyState.ForeColor = GuardianTheme.MutedInk;
        _emptyState.Font = new Font("Segoe UI", 11);
        _emptyState.TextAlign = ContentAlignment.MiddleCenter;
        _emptyState.Text =
            "ALL CLEAR  ✓\n\nNo unsaved changes in this project.\nThe fox is happy. Everything currently on this PC is saved locally.";
        _emptyState.Visible = false;

        panel.Controls.Add(_files);
        panel.Controls.Add(_emptyState);
        return panel;
    }

    private Control BuildLowerPanel()
    {
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = GuardianTheme.Console
        };

        _activityPanel = BuildActivityPanel();
        _activityPanel.Dock = DockStyle.Fill;
        _comparisonPanel.Dock = DockStyle.Fill;
        _comparisonPanel.Visible = false;
        _comparisonPanel.ActivityRequested += (_, _) => ShowActivityPanel();
        _comparisonPanel.CreateCheckpointRequested += async (_, _) => await CreateCheckpointAsync();

        host.Controls.Add(_activityPanel);
        host.Controls.Add(_comparisonPanel);
        return host;
    }

    private Panel BuildActivityPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = GuardianTheme.Console
        };

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 40,
            Padding = new Padding(14, 0, 12, 0),
            BackColor = GuardianTheme.ConsoleHeader
        };

        var title = new Label
        {
            Dock = DockStyle.Left,
            Width = 220,
            Text = "GUARDIAN ACTIVITY",
            ForeColor = GuardianTheme.MutedInk,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _activityState.Dock = DockStyle.Right;
        _activityState.Width = 110;
        _activityState.Text = "● READY";
        _activityState.ForeColor = GuardianTheme.Healthy;
        _activityState.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        _activityState.TextAlign = ContentAlignment.MiddleRight;

        header.Controls.Add(_activityState);
        header.Controls.Add(title);

        _output.Dock = DockStyle.Fill;
        _output.ReadOnly = true;
        _output.Font = new Font("Cascadia Mono", 9.5f);
        _output.BackColor = GuardianTheme.Console;
        _output.ForeColor = Color.FromArgb(225, 218, 237);
        _output.BorderStyle = BorderStyle.None;
        _output.Padding = new Padding(12);
        _output.Text = "Guardian ready. Click a changed file to open the side-by-side File Review.";

        _toolTips.SetToolTip(_output,
            "Guardian Activity\n\nResults from Tests, Save, Get, Send, History, and Health appear here.\n" +
            "Click a changed file to switch this area to the Before / Now review workspace.");

        panel.Controls.Add(_output);
        panel.Controls.Add(header);
        return panel;
    }

    private Control BuildOptionsPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            BackColor = GuardianTheme.Surface,
            Padding = new Padding(16, 8, 16, 8)
        };

        var label = new Label
        {
            AutoSize = true,
            Location = new Point(16, 9),
            Text = "AUTOMATIC SAVING",
            ForeColor = GuardianTheme.FaintInk,
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold)
        };

        _automatic.Text = "Automatic verified saves";
        _automatic.AutoSize = true;
        _automatic.Location = new Point(16, 28);
        _automatic.ForeColor = GuardianTheme.Ink;
        _automatic.BackColor = GuardianTheme.Surface;
        _automatic.Checked = _config.AutomaticCheckpointsEnabled;

        var note = new Label
        {
            AutoSize = true,
            Location = new Point(250, 29),
            Text = "OFF BY DEFAULT · local saves only · never sends automatically",
            ForeColor = GuardianTheme.FaintInk,
            Font = new Font("Segoe UI", 8)
        };

        _toolTips.SetToolTip(_automatic,
            "Automatic verified saves\n\nOFF by default. When enabled, GitPet may save after the configured quiet period.\n" +
            "Configured tests can be required first.\n\n" +
            "Technically this creates a local Git commit/checkpoint. It never pushes automatically and never configures remotes.");

        _automatic.CheckedChanged += async (_, _) =>
        {
            _config.AutomaticCheckpointsEnabled = _automatic.Checked;
            _configStore.Save(_config);
            await _audit.WriteAsync("automatic_checkpoint_setting_changed", new { enabled = _automatic.Checked });
        };

        panel.Controls.Add(note);
        panel.Controls.Add(_automatic);
        panel.Controls.Add(label);
        return panel;
    }

    private MenuStrip BuildMainMenu()
    {
        var menu = new MenuStrip
        {
            Dock = DockStyle.Top,
            BackColor = GuardianTheme.SurfaceRaised,
            ForeColor = GuardianTheme.Ink,
            GripStyle = ToolStripGripStyle.Hidden,
            RenderMode = ToolStripRenderMode.Professional,
            Renderer = GuardianTheme.CreateMenuRenderer(),
            Padding = new Padding(12, 3, 0, 3)
        };

        var help = new ToolStripMenuItem("Help")
        {
            ForeColor = GuardianTheme.Ink,
            BackColor = GuardianTheme.SurfaceRaised
        };

        var about = new ToolStripMenuItem("About ZomniverseGitPet…")
        {
            ForeColor = GuardianTheme.Ink,
            BackColor = GuardianTheme.SurfaceRaised
        };
        about.Click += (_, _) =>
        {
            using var dialog = new AboutForm();
            dialog.ShowDialog(this);
        };

        help.DropDownItems.Add(about);
        menu.Items.Add(help);
        return menu;
    }

    private GuardianActionButton MakeActionButton(
        string text,
        GuardianActionKind kind,
        int width,
        Func<Task> action)
    {
        var button = new GuardianActionButton
        {
            Text = text,
            Kind = kind,
            Width = width,
            Height = 38,
            Margin = new Padding(4, 2, 4, 2)
        };
        button.Click += async (_, _) => await action();
        return button;
    }

    private void ConfigureToolTips()
    {
        _toolTips.OwnerDraw = true;

        _toolTips.Popup += (_, e) =>
        {
            if (e.AssociatedControl is null) return;
            var text = _toolTips.GetToolTip(e.AssociatedControl);
            if (string.IsNullOrWhiteSpace(text)) return;

            var measured = TextRenderer.MeasureText(
                text,
                _toolTipFont,
                new Size(520, 0),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);

            e.ToolTipSize = new Size(
                Math.Min(548, Math.Max(180, measured.Width + 24)),
                Math.Max(48, measured.Height + 20));
        };

        _toolTips.Draw += (_, e) =>
        {
            e.Graphics.Clear(GuardianTheme.Tooltip);
            using var border = new Pen(GuardianTheme.Violet, 1.2f);
            e.Graphics.DrawRectangle(border, 0, 0, e.Bounds.Width - 1, e.Bounds.Height - 1);

            var textBounds = Rectangle.Inflate(e.Bounds, -12, -9);
            TextRenderer.DrawText(
                e.Graphics,
                e.ToolTipText,
                _toolTipFont,
                textBounds,
                GuardianTheme.Ink,
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
        };
    }

    public async Task RefreshAsync()
    {
        if (_refreshInProgress || IsDisposed || _operation is not null) return;

        if (string.IsNullOrWhiteSpace(_config.RepositoryPath))
        {
            SetNoProjectHeader();
            return;
        }

        _refreshInProgress = true;
        try
        {
            await RefreshRepositoryViewAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            SetHeaderProblem(ex.Message);
            await _audit.WriteAsync("guardian_refresh_error", new { error = ex.Message });
        }
        finally
        {
            _refreshInProgress = false;
        }
    }

    private async Task RefreshRepositoryViewAsync(CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(_config.RepositoryPath)) return;

        var statusTask = _git.GetStatusAsync(_config.RepositoryPath!, token);
        var commitTask = _git.GetLastCommitAsync(_config.RepositoryPath!, token);
        _status = await statusTask;
        var commit = await commitTask;

        UpdateRepositoryHeader(_status, commit);
        _files.Rows.Clear();

        foreach (var file in _status.Files)
        {
            var rowIndex = _files.Rows.Add(HumanizeGitStatus(file.Status), file.Path);
            var row = _files.Rows[rowIndex];
            row.Cells[0].Style.ForeColor = StatusColor(file.Status);
            row.Cells[0].ToolTipText = DescribeGitStatus(file.Status);
            row.Cells[1].ToolTipText =
                $"{file.Path}\n\nClick this row to compare the latest saved version with the current working file.\n\nTechnical baseline: latest local Git commit.";
        }

        var isClean = _status.Healthy && _status.Files.Count == 0;
        _emptyState.Visible = isClean;
        _files.Visible = !isClean;
        if (isClean)
        {
            _emptyState.Text = "ALL CLEAR  ✓\n\nNo unsaved changes in this project.\nThe fox is happy. Everything currently on this PC is saved locally.";
            _emptyState.BringToFront();
        }

        if (!string.IsNullOrWhiteSpace(_reviewedPath) &&
            !_status.Files.Any(file => string.Equals(file.Path, _reviewedPath, StringComparison.OrdinalIgnoreCase)))
        {
            _reviewedPath = null;
            ShowActivityPanel();
        }

        if (!_status.Healthy)
        {
            ShowActivityPanel();
            _output.Text = _status.Error;
            SetActivityState("● ATTENTION", GuardianTheme.Warning);
        }
    }

    private void UpdateRepositoryHeader(RepositoryStatus status, CommandResult commit)
    {
        var path = _config.RepositoryPath ?? "";
        var normalized = string.IsNullOrWhiteSpace(path) ? "" : Path.TrimEndingDirectorySeparator(path);
        var name = string.IsNullOrWhiteSpace(normalized) ? "NO PROJECT" : Path.GetFileName(normalized);
        if (string.IsNullOrWhiteSpace(name)) name = normalized;

        _projectTitle.Text = $"ZOMNIVERSE GITPET  /  {name.ToUpperInvariant()}";
        _toolTips.SetToolTip(_projectTitle, string.IsNullOrWhiteSpace(path)
            ? "No active project."
            : $"Active project\n{path}\n\nClosing Guardian with X only hides this window; the fox keeps running.");

        if (!status.Healthy)
        {
            _healthChip.Text = "● ATTENTION";
            _healthChip.Tone = GuardianChipTone.Warning;
            _branchChip.Text = "BRANCH  ?";
            _branchChip.Tone = GuardianChipTone.Neutral;
            _changesChip.Text = "STATUS UNKNOWN";
            _changesChip.Tone = GuardianChipTone.Warning;
            _commitLabel.Text = "LATEST  unavailable";
            return;
        }

        _healthChip.Text = "● HEALTHY";
        _healthChip.Tone = GuardianChipTone.Healthy;
        _branchChip.Text = $"BRANCH  {status.Branch}";
        _branchChip.Tone = GuardianChipTone.Neutral;
        _changesChip.Text = status.Files.Count == 0
            ? "CLEAN  ✓"
            : $"{status.Files.Count} CHANGE{(status.Files.Count == 1 ? "" : "S")}";
        _changesChip.Tone = status.Files.Count == 0
            ? GuardianChipTone.Healthy
            : GuardianChipTone.Changes;
        _commitLabel.Text =
            $"LATEST  {FormatCommitPreview(commit)}\r\n" +
            FriendlyGitState.FormatSyncSummary(status);
    }

    private void SetNoProjectHeader()
    {
        _projectTitle.Text = "ZOMNIVERSE GITPET  /  NO PROJECT";
        _healthChip.Text = "● WAITING";
        _healthChip.Tone = GuardianChipTone.Neutral;
        _branchChip.Text = "BRANCH  —";
        _branchChip.Tone = GuardianChipTone.Neutral;
        _changesChip.Text = "OPEN PROJECTS";
        _changesChip.Tone = GuardianChipTone.Neutral;
        _commitLabel.Text = "LATEST  Choose or prepare a project to begin.";
        _emptyState.Visible = true;
        _emptyState.Text =
            "READY WHEN YOU ARE\n\nOpen Projects to choose an existing repository\nor safely prepare a normal folder for Git.";
        _files.Visible = false;
        _reviewedPath = null;
        ShowActivityPanel();
    }

    private void SetHeaderProblem(string message)
    {
        _healthChip.Text = "● ATTENTION";
        _healthChip.Tone = GuardianChipTone.Warning;
        _changesChip.Text = "CHECK GUARDIAN";
        _changesChip.Tone = GuardianChipTone.Warning;
        _commitLabel.Text = "LATEST  Repository refresh problem";
        ShowActivityPanel();
        _output.Text = message;
        SetActivityState("● ATTENTION", GuardianTheme.Warning);
    }

    private static string HumanizeGitStatus(string status) => status switch
    {
        "??" => "NEW",
        ".M" => "MODIFIED",
        "M." => "STAGED",
        "A." => "ADDED",
        ".D" => "DELETED",
        "D." => "DELETE STAGED",
        "MM" => "STAGED + EDITED",
        _ when status.Contains('U') => "CONFLICT",
        _ => status
    };

    private static Color StatusColor(string status) => status switch
    {
        "??" or "A." => Color.FromArgb(112, 207, 232),
        ".D" or "D." => GuardianTheme.Warning,
        _ when status.Contains('U') => GuardianTheme.Warning,
        _ => Color.FromArgb(185, 148, 245)
    };

    private static string DescribeGitStatus(string status) => status switch
    {
        "??" => "NEW — Git code: ??\n\nThis file or folder is new and is not yet tracked by Git.",
        ".M" => "MODIFIED — Git code: .M\n\nAn existing tracked file has local unstaged changes.",
        "M." => "STAGED — Git code: M.\n\nThis tracked file has changes already staged in Git's index.",
        ".D" => "DELETED — Git code: .D\n\nThis tracked file was deleted locally but the deletion is not staged.",
        "D." => "DELETE STAGED — Git code: D.\n\nThe deletion of this tracked file is already staged.",
        "A." => "ADDED — Git code: A.\n\nThis new item has already been staged for commit.",
        "MM" => "STAGED + EDITED — Git code: MM\n\nThe file has staged changes plus additional unstaged changes.",
        _ => $"{status} — Git porcelain status code.\n\nThe first character describes the index/staged state and the second describes the working tree."
    };

    private Task ShowDiffAsync() => ShowSelectedFileComparisonAsync();

    private async Task ShowSelectedFileComparisonAsync()
    {
        if (_operation is not null) return;
        if (!HasRepository()) return;

        if (_files.SelectedRows.Count == 0)
        {
            ShowActivityPanel();
            _output.Text = "Select a changed file first. Clicking a row opens its Before / Now review automatically.";
            return;
        }

        var relativePath = Convert.ToString(_files.SelectedRows[0].Cells[1].Value) ?? "";
        if (string.IsNullOrWhiteSpace(relativePath)) return;

        _comparisonLoad?.Cancel();
        _comparisonLoad?.Dispose();
        _comparisonLoad = new CancellationTokenSource();
        var token = _comparisonLoad.Token;
        _reviewedPath = relativePath;

        _comparisonPanel.Visible = true;
        _comparisonPanel.BringToFront();
        _comparisonPanel.ShowLoading(relativePath);

        try
        {
            var repositoryPath = _config.RepositoryPath!;
            var headTask = _git.HasHeadCommitAsync(repositoryPath, token);
            var commitTask = _git.GetLastCommitAsync(repositoryPath, token);
            var workingTask = ReadWorkingPreviewAsync(repositoryPath, relativePath, token);

            var head = await headTask;
            var hasBaseline = head.Success && !string.IsNullOrWhiteSpace(head.Output);
            var commit = await commitTask;
            var working = await workingTask;

            CommandResult? beforeResult = null;
            CommandResult? diffResult = null;
            if (hasBaseline)
            {
                beforeResult = await _git.GetFileAtHeadAsync(repositoryPath, relativePath, token);
                diffResult = await _git.GetDiffAgainstHeadAsync(repositoryPath, relativePath, token);
            }

            var beforeExists = hasBaseline && beforeResult is { Success: true };
            var beforeText = beforeExists ? beforeResult!.Output : "";
            var marks = hasBaseline && diffResult is { Success: true }
                ? DiffLineMap.ParseUnifiedZeroContext(diffResult.Output)
                : DiffLineMap.Empty;

            if (hasBaseline && !beforeExists && working.Exists)
            {
                marks = new DiffLineMap(new HashSet<int>(), AllLineNumbers(working.Text));
            }
            else if (hasBaseline && beforeExists && !working.Exists)
            {
                marks = new DiffLineMap(AllLineNumbers(beforeText), new HashSet<int>());
            }

            var model = new FileComparisonModel(
                relativePath,
                hasBaseline ? FormatCommitPreview(commit) : "no saved version",
                hasBaseline,
                beforeExists,
                beforeText,
                working.Exists,
                working.Text,
                marks,
                hasBaseline && !beforeExists
                    ? "NEW FILE\n\nThis item did not exist in the latest local Git commit/checkpoint."
                    : null,
                !working.Exists
                    ? "DELETED FROM WORKING TREE\n\nThis item existed in the baseline but is no longer present on disk."
                    : null);

            if (!token.IsCancellationRequested) _comparisonPanel.ShowComparison(model);
        }
        catch (OperationCanceledException)
        {
            // A newer file click replaced this review request.
        }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested) _comparisonPanel.ShowProblem(relativePath, ex.Message);
            await _audit.WriteAsync("file_review_error", new { file = relativePath, error = ex.Message });
        }
    }

    private static async Task<WorkingPreview> ReadWorkingPreviewAsync(
        string repositoryPath,
        string relativePath,
        CancellationToken token)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repositoryPath));
        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
        var prefix = root + Path.DirectorySeparatorChar;
        if (!string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase) &&
            !fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The selected path resolves outside the active repository.");

        if (Directory.Exists(fullPath))
            return new(true,
                "FOLDER CHANGE\r\n\r\nGit currently reports this folder as one changed item. " +
                "Once individual files are listed, click a file to review its exact Before / Now content.");

        if (!File.Exists(fullPath)) return new(false, "");

        var info = new FileInfo(fullPath);
        if (info.Length > 1_000_000)
            return new(true,
                $"LARGE FILE PREVIEW\r\n\r\n{info.Name} is {info.Length:N0} bytes. " +
                "GitPet keeps File Review responsive by not rendering files larger than 1 MB here. The file itself is unchanged.");

        var binaryExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".ico", ".pdf",
            ".zip", ".7z", ".rar", ".exe", ".dll", ".pdb", ".doc", ".docx",
            ".xls", ".xlsx", ".ppt", ".pptx", ".mp3", ".wav", ".mp4", ".mov"
        };
        if (binaryExtensions.Contains(info.Extension))
            return new(true,
                $"BINARY FILE\r\n\r\n{info.Name} is tracked by Git, but GitPet does not render binary content as source code.");

        var text = await File.ReadAllTextAsync(fullPath, token);
        if (text.IndexOf('\0') >= 0)
            return new(true,
                $"BINARY-LIKE FILE\r\n\r\n{info.Name} contains binary data, so GitPet is not rendering it as source code.");

        return new(true, text);
    }

    private static HashSet<int> AllLineNumbers(string text)
    {
        var lines = new HashSet<int>();
        if (text.Length == 0) return lines;
        var count = 1;
        foreach (var character in text)
            if (character == '\n') count++;
        for (var line = 1; line <= count; line++) lines.Add(line);
        return lines;
    }

    private async Task RunTestsAsync() => await RunOperationAsync("Preparing project tests...", async token =>
    {
        if (!HasRepository()) return;

        var repositoryPath = _config.RepositoryPath!;
        var commands = _config.GetTestCommandsForRepository(repositoryPath).ToList();
        var editRequested = (ModifierKeys & Keys.Shift) == Keys.Shift;

        if (commands.Count == 0 || editRequested)
        {
            var normalized = Path.TrimEndingDirectorySeparator(repositoryPath);
            var projectName = Path.GetFileName(normalized);
            if (string.IsNullOrWhiteSpace(projectName)) projectName = normalized;

            using var setup = new ProjectTestsForm(projectName, repositoryPath, commands);
            if (setup.ShowDialog(this) != DialogResult.OK)
            {
                _output.Text = commands.Count == 0
                    ? "Tests cancelled. No test commands were saved for this project."
                    : "Test configuration cancelled. Existing project test commands were kept.";
                return;
            }

            _config.SetTestCommandsForRepository(repositoryPath, setup.Commands);
            _configStore.Save(_config);
            commands = setup.Commands.ToList();
            await _audit.WriteAsync("project_tests_configured", new
            {
                repository = repositoryPath,
                count = commands.Count
            });

            if (commands.Count == 0)
            {
                _output.Text = "No test commands are saved for this project.";
                return;
            }

            if (!setup.RunAfterSave)
            {
                _output.Text =
                    $"Saved {commands.Count} test command{(commands.Count == 1 ? "" : "s")} for this project.\n\n" +
                    "Press Tests to run them. Hold Shift while clicking Tests whenever you want to edit this list.";
                return;
            }
        }

        var text = new System.Text.StringBuilder();
        var completed = 0;
        var allPassed = true;
        foreach (var command in commands)
        {
            completed++;
            text.AppendLine($"TEST {completed}/{commands.Count}");
            text.AppendLine("> " + command);
            _output.Text = text.ToString();

            var result = await _git.RunTestCommandAsync(repositoryPath, command, token);
            if (!string.IsNullOrWhiteSpace(result.Output)) text.AppendLine(result.Output);
            text.AppendLine(result.Success ? "✓ PASS" : $"✕ FAIL  (exit {result.ExitCode})").AppendLine();
            _output.Text = text.ToString();

            if (!result.Success)
            {
                allPassed = false;
                break;
            }
        }

        text.Insert(0, allPassed
            ? $"TESTS PASSED ✓  ({completed}/{commands.Count})\n\n"
            : $"TESTS STOPPED ✕  ({completed}/{commands.Count})\n\n");
        _output.Text = text.ToString();
        await _audit.WriteAsync("manual_project_tests", new
        {
            repository = repositoryPath,
            configured = commands.Count,
            completed,
            success = allPassed
        });
    });

    private async Task CreateCheckpointAsync() => await RunOperationAsync("Preparing save...", async token =>
    {
        if (!HasRepository()) return;

        _status = await _git.GetStatusAsync(_config.RepositoryPath!, token);
        if (!_status.Healthy || _status.Files.Count == 0)
        {
            _output.Text = _status.Healthy ? "Everything is already saved locally." : _status.Error;
            return;
        }

        var suspicious = GitService.FindSuspiciousPaths(_status.Files, _config.SuspiciousPathPatterns);
        if (suspicious.Count > 0)
        {
            _output.Text = "Save blocked because suspicious paths are present:\n\n" + string.Join("\n", suspicious);
            await _audit.WriteAsync("checkpoint_blocked_suspicious_paths", new { files = suspicious });
            return;
        }

        var preview = string.Join("\n", _status.Files.Take(20).Select(f => $"{HumanizeGitStatus(f.Status)}  {f.Path}"));
        if (_status.Files.Count > 20) preview += $"\n... and {_status.Files.Count - 20} more";

        var countText = FriendlyGitState.Count(_status.Files.Count, "current change");
        var answer = MessageBox.Show(
            this,
            $"Save all {countText} as a local version?\n\n" + preview +
            "\n\nThis saves the current state on this PC.\nNothing will be sent online.",
            "Save changes",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (answer != DialogResult.Yes) return;
        if (!await EnsureGitIdentityAsync(token)) return;

        var message = $"checkpoint: {DateTime.Now:yyyy-MM-dd HH:mm}";
        var result = await _git.CreateCheckpointAsync(_config.RepositoryPath!, message, token);
        _output.Text = result.Success
            ? "Changes saved locally ✓\n\n" + result.Message
            : result.Message;

        MessageBox.Show(
            this,
            result.Success
                ? "Changes saved locally ✓\n\nNothing was sent online."
                : result.Message,
            "Save changes",
            MessageBoxButtons.OK,
            result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

        await RefreshRepositoryViewAsync(token);
    });

    private async Task<bool> EnsureGitIdentityAsync(CancellationToken token)
    {
        var repositoryPath = _config.RepositoryPath!;
        var nameResult = await _git.GetUserNameAsync(repositoryPath, token);
        var emailResult = await _git.GetUserEmailAsync(repositoryPath, token);
        var currentName = nameResult.Success ? nameResult.Output.Trim() : "";
        var currentEmail = emailResult.Success ? emailResult.Output.Trim() : "";

        if (!string.IsNullOrWhiteSpace(currentName) && !string.IsNullOrWhiteSpace(currentEmail))
            return true;

        var normalized = Path.TrimEndingDirectorySeparator(repositoryPath);
        var projectName = Path.GetFileName(normalized);
        if (string.IsNullOrWhiteSpace(projectName)) projectName = normalized;

        using var identity = new GitIdentityForm(projectName, currentName, currentEmail);
        if (identity.ShowDialog(this) != DialogResult.OK)
        {
            _output.Text = "Save cancelled. Git still needs an author name and email before it can save a local version.";
            return false;
        }

        var save = await _git.SetUserIdentityAsync(
            repositoryPath,
            identity.IdentityName,
            identity.IdentityEmail,
            identity.UseGlobal,
            token);

        if (!save.Success)
        {
            _output.Text = save.Output;
            MessageBox.Show(
                this,
                "GitPet could not save the Git identity. No changes were saved.\n\n" + save.Output,
                "Git identity",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        _output.Text = identity.UseGlobal
            ? "Git identity saved for Git projects on this PC. Saving changes..."
            : "Git identity saved for this project. Saving changes...";
        return true;
    }

    private async Task PullFromOriginAsync() => await RunOperationAsync("Checking Get safety...", async token =>
    {
        if (!HasRepository()) return;

        var repositoryPath = _config.RepositoryPath!;
        var status = await _git.GetStatusAsync(repositoryPath, token);
        if (!status.Healthy)
        {
            _output.Text = "Get unavailable because Git could not read the current project state.\n\n" + status.Error;
            return;
        }

        if (status.Files.Count > 0)
        {
            var countText = FriendlyGitState.Count(status.Files.Count, "unsaved change");
            _output.Text =
                $"Get blocked safely: {countText} detected.\n\n" +
                "Save the current work before getting online updates so the two versions are not accidentally mixed.";
            MessageBox.Show(
                this,
                $"GitPet found {countText}.\n\n" +
                "Save them before getting online updates so the two versions are not accidentally mixed.\n\n" +
                "Save first, then try Get ↓ again.",
                "Save first",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var branchResult = await _git.GetCurrentBranchAsync(repositoryPath, token);
        var branch = branchResult.Success ? branchResult.Output.Trim() : "";
        if (string.IsNullOrWhiteSpace(branch))
        {
            _output.Text = "Get unavailable: the repository is not on a named local branch (detached HEAD or branch lookup failed).";
            return;
        }

        var originResult = await _git.GetOriginUrlAsync(repositoryPath, token);
        if (!originResult.Success || string.IsNullOrWhiteSpace(originResult.Output))
        {
            _output.Text = string.IsNullOrWhiteSpace(originResult.Output)
                ? "Get unavailable: no readable origin remote is configured."
                : originResult.Output;
            return;
        }

        var commit = await _git.GetLastCommitAsync(repositoryPath, token);
        var commitPreview = FormatCommitPreview(commit);
        var answer = MessageBox.Show(
            this,
            "Get committed updates from the online copy into this local branch?\n\n" +
            $"Branch: {branch}\nLatest saved version: {commitPreview}\n\n" +
            "GitPet will only update the branch when this can happen safely without creating a merge commit.",
            "Get updates?",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (answer != DialogResult.Yes)
        {
            _output.Text = "Get cancelled. Nothing was changed.";
            return;
        }

        _output.Text = $"Getting updates from origin/{branch} with fast-forward-only safety...";
        var result = await _git.PullFromOriginAsync(repositoryPath, branch, token);
        var details = string.IsNullOrWhiteSpace(result.Output) ? "Git reported success." : result.Output;

        _output.Text = result.Success
            ? $"Updates received ✓\norigin/{branch} → local {branch}\n\n{details}"
            : $"Get stopped safely.\norigin/{branch}\n\n{details}\n\nGitPet did not create a merge commit.";

        MessageBox.Show(
            this,
            result.Success
                ? "Updates received successfully."
                : "GitPet could not safely get the updates. No merge commit was created. See Guardian Activity for details.",
            "Get updates",
            MessageBoxButtons.OK,
            result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

        await RefreshRepositoryViewAsync(token);
    });

    private async Task PushToOriginAsync() => await RunOperationAsync("Checking what is ready to send...", async token =>
    {
        if (!HasRepository()) return;

        var repositoryPath = _config.RepositoryPath!;
        var status = await _git.GetStatusAsync(repositoryPath, token);
        if (!status.Healthy)
        {
            _output.Text = "Send unavailable because Git could not read the current project state.\n\n" + status.Error;
            return;
        }

        var readiness = FriendlyGitState.GetSendReadiness(status);
        if (readiness == SendReadiness.SaveFirst)
        {
            var countText = FriendlyGitState.Count(status.Files.Count, "unsaved change");
            _output.Text = $"Nothing is ready to send yet.\n\nYou have {countText} on this PC.\nSave them first, then use Send.";
            MessageBox.Show(
                this,
                $"Nothing is ready to send yet.\n\nYou have {countText} on this PC.\n\nSave them first, then use Send.",
                "Save your changes first",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (readiness == SendReadiness.AlreadyUpToDate)
        {
            _output.Text = "Everything saved is already online.\n\nThere is nothing new to send.";
            MessageBox.Show(
                this,
                "Everything saved is already online.\n\nThere is nothing new to send.",
                "Already up to date",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var branchResult = await _git.GetCurrentBranchAsync(repositoryPath, token);
        var branch = branchResult.Success ? branchResult.Output.Trim() : "";
        if (string.IsNullOrWhiteSpace(branch))
        {
            _output.Text = "Send unavailable: the repository is not on a named local branch (detached HEAD or branch lookup failed).";
            return;
        }

        var originResult = await _git.GetOriginUrlAsync(repositoryPath, token);
        if (!originResult.Success || string.IsNullOrWhiteSpace(originResult.Output))
        {
            _output.Text = string.IsNullOrWhiteSpace(originResult.Output)
                ? "Send unavailable: no readable origin remote is configured."
                : originResult.Output;
            return;
        }

        var commit = await _git.GetLastCommitAsync(repositoryPath, token);
        var commitPreview = FormatCommitPreview(commit);
        var savedText = status.HasTrackingInformation
            ? FriendlyGitState.Count(status.Ahead, "saved update")
            : "saved committed history";
        var unsavedText = status.Files.Count > 0
            ? $"\n\nYou also have {FriendlyGitState.Count(status.Files.Count, "unsaved change")}.\n" +
              "Those unsaved changes will stay on this PC and will NOT be sent."
            : "\n\nNothing unsaved will be included.";

        var answer = MessageBox.Show(
            this,
            status.HasTrackingInformation
                ? $"Send {savedText} to the online copy?\n\nBranch: {branch}\nLatest saved version: {commitPreview}" + unsavedText
                : $"Send the saved committed history to the online copy?\n\nBranch: {branch}\nLatest saved version: {commitPreview}" + unsavedText,
            "Send saved updates?",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (answer != DialogResult.Yes)
        {
            _output.Text = "Send cancelled. Nothing was sent online.";
            return;
        }

        _output.Text = $"Sending saved updates to origin/{branch}...";
        var result = await _git.PushToOriginAsync(repositoryPath, branch, token);
        var details = string.IsNullOrWhiteSpace(result.Output) ? "Git reported success." : result.Output;

        _output.Text = result.Success
            ? $"Send completed ✓\norigin/{branch}\n\n{details}"
            : $"Send failed.\norigin/{branch}\n\n{details}";

        MessageBox.Show(
            this,
            result.Success
                ? "Saved updates sent successfully."
                : "Send failed. See Guardian Activity for details.",
            "Send saved updates",
            MessageBoxButtons.OK,
            result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

        await RefreshRepositoryViewAsync(token);
    });

    private static string FormatCommitPreview(CommandResult commit)
    {
        if (!commit.Success || string.IsNullOrWhiteSpace(commit.Output)) return "unavailable";

        var fields = commit.Output.Split('\t');
        if (fields.Length < 4)
        {
            var fallback = commit.Output.Trim();
            return fallback.Length <= 90 ? fallback : fallback[..87] + "...";
        }

        var subject = fields[3].Trim();
        if (subject.Length > 70) subject = subject[..67] + "...";
        return $"{fields[1].Trim()} — {subject}";
    }

    private async Task RecentCommitsAsync() => await RunOperationAsync("Loading recent commits...", async token =>
    {
        if (!HasRepository()) return;
        var result = await _git.GetRecentCommitsAsync(_config.RepositoryPath!, token);
        _output.Text = result.Output;
    });

    private async Task HealthCheckAsync() => await RunOperationAsync("Running Git health check...", async token =>
    {
        if (!HasRepository()) return;

        var result = await _git.HealthCheckAsync(_config.RepositoryPath!, token);
        _output.Text = result.Success
            ? "git fsck passed.\n\n" + result.Output
            : "git fsck failed.\n\n" + result.Output;

        await _audit.WriteAsync("health_check", new { success = result.Success, result.TimedOut });
    });

    private bool HasRepository()
    {
        if (!string.IsNullOrWhiteSpace(_config.RepositoryPath)) return true;

        ShowActivityPanel();
        _output.Text = "Open Projects and choose a Git project first.";
        return false;
    }

    private async Task RunOperationAsync(string message, Func<CancellationToken, Task> action)
    {
        if (_operation is not null) return;

        _comparisonLoad?.Cancel();
        ShowActivityPanel();
        _operation = new CancellationTokenSource();
        foreach (var button in _operationButtons) button.Enabled = false;

        _cancelButton.Visible = true;
        SetActivityState("● WORKING", GuardianTheme.Changes);
        _output.Text = message;

        try
        {
            await action(_operation.Token);
        }
        catch (OperationCanceledException)
        {
            _output.Text = "Operation cancelled.";
        }
        catch (Exception ex)
        {
            _output.Text = ex.Message;
            await _audit.WriteAsync("operation_error", new { error = ex.Message });
        }
        finally
        {
            foreach (var button in _operationButtons) button.Enabled = true;
            _cancelButton.Visible = false;
            SetActivityState("● READY", GuardianTheme.Healthy);

            _operation.Dispose();
            _operation = null;
        }
    }

    private void ShowActivityPanel()
    {
        if (_activityPanel is null) return;
        _comparisonPanel.Visible = false;
        _activityPanel.Visible = true;
        _activityPanel.BringToFront();
    }

    private void SetActivityState(string text, Color color)
    {
        _activityState.Text = text;
        _activityState.ForeColor = color;
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_exitRequested) return;

        e.Cancel = true;
        _operation?.Cancel();
        _comparisonLoad?.Cancel();
        Hide();
    }

    public void CloseForExit()
    {
        _exitRequested = true;
        _operation?.Cancel();
        _comparisonLoad?.Cancel();
        Close();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _pulseTimer.Stop();
            _pulseTimer.Dispose();
            _comparisonLoad?.Cancel();
            _comparisonLoad?.Dispose();
            _toolTips.Dispose();
            _toolTipFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private sealed record WorkingPreview(bool Exists, string Text);
}
