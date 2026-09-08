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
    private RepositoryStatus? _status;
    private bool _refreshInProgress;
    private bool _exitRequested;

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
        var diff = MakeActionButton("Diff", GuardianActionKind.Standard, 82, ShowDiffAsync);
        var tests = MakeActionButton("Tests", GuardianActionKind.Standard, 82, RunTestsAsync);
        var checkpoint = MakeActionButton("Checkpoint", GuardianActionKind.Primary, 118, CreateCheckpointAsync);
        var pull = MakeActionButton("Pull ↓", GuardianActionKind.Pull, 92, PullFromOriginAsync);
        var push = MakeActionButton("Push ↑", GuardianActionKind.Push, 92, PushToOriginAsync);
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
            "Diff\n\nSelect a changed file first, then inspect exactly what changed.\n" +
            "Tracked files show their Git diff; small untracked text files can be previewed directly.");
        _toolTips.SetToolTip(tests,
            "Tests\n\nRun the test commands configured for this repository in config.json.\n" +
            "If none are configured, Guardian tells you instead of running anything.");
        _toolTips.SetToolTip(checkpoint,
            "Checkpoint\n\nSave the current working state as an ordinary LOCAL Git commit.\n" +
            "GitPet previews the files, asks for confirmation, and never pushes this commit automatically.");
        _toolTips.SetToolTip(pull,
            "Pull ↓ — MANUAL ONLY\n\nBring committed changes from origin into the CURRENT branch.\n" +
            "GitPet requires a clean working tree and uses fast-forward only, so it will never create an automatic merge commit.");
        _toolTips.SetToolTip(push,
            "Push ↑ — MANUAL ONLY\n\nPush committed history to the existing origin remote on the CURRENT branch.\n" +
            "GitPet shows the destination and commit first. Uncommitted changes are never included.");
        _toolTips.SetToolTip(recent,
            "History\n\nShow the latest 12 local Git commits, including normal commits and restore-point checkpoints.");
        _toolTips.SetToolTip(health,
            "Health\n\nRun git fsck --no-progress to check the internal integrity of the local repository.");
        _toolTips.SetToolTip(_cancelButton,
            "Cancel\n\nRequest cancellation of the Git, test, pull, push, or health operation currently running.");

        ConfigureFilesGrid();
        var filesPanel = BuildFilesPanel();
        var activityPanel = BuildActivityPanel();

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
        content.Panel2.Controls.Add(activityPanel);

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
            "Changed item path. Select a row and choose Diff, or double-click the row.";
        _files.CellDoubleClick += async (_, e) =>
        {
            if (e.RowIndex >= 0) await ShowDiffAsync();
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
            "ALL CLEAR  ✓\n\nNo uncommitted changes in this project.\nThe fox is happy. Your working tree is clean.";
        _emptyState.Visible = false;

        panel.Controls.Add(_files);
        panel.Controls.Add(_emptyState);
        return panel;
    }

    private Control BuildActivityPanel()
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
        _output.Text = "Guardian ready.";

        _toolTips.SetToolTip(_output,
            "Operation output\n\nResults from Diff, Tests, Checkpoint, Pull, Push, History, and Health appear here.\n" +
            "This console is read-only.");

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
            Text = "CHECKPOINT POLICY",
            ForeColor = GuardianTheme.FaintInk,
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold)
        };

        _automatic.Text = "Automatic verified checkpoints";
        _automatic.AutoSize = true;
        _automatic.Location = new Point(16, 28);
        _automatic.ForeColor = GuardianTheme.Ink;
        _automatic.BackColor = GuardianTheme.Surface;
        _automatic.Checked = _config.AutomaticCheckpointsEnabled;

        var note = new Label
        {
            AutoSize = true,
            Location = new Point(250, 29),
            Text = "OFF BY DEFAULT · local commits only · never auto-pushes",
            ForeColor = GuardianTheme.FaintInk,
            Font = new Font("Segoe UI", 8)
        };

        _toolTips.SetToolTip(_automatic,
            "Automatic verified checkpoints\n\nOFF by default. When enabled, GitPet may create LOCAL checkpoint commits\n" +
            "after the configured quiet period. Configured tests can be required first.\n\n" +
            "Automatic checkpoints never push and never configure remotes.");

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
                $"{file.Path}\n\nSelect this row and choose Diff, or double-click it, to inspect the change.";
        }

        var isClean = _status.Healthy && _status.Files.Count == 0;
        _emptyState.Visible = isClean;
        _files.Visible = !isClean;
        if (isClean)
        {
            _emptyState.Text = "ALL CLEAR  ✓\n\nNo uncommitted changes in this project.\nThe fox is happy. Your working tree is clean.";
            _emptyState.BringToFront();
        }

        if (!_status.Healthy)
        {
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

        _commitLabel.Text = $"LATEST  {FormatCommitPreview(commit)}";
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
    }

    private void SetHeaderProblem(string message)
    {
        _healthChip.Text = "● ATTENTION";
        _healthChip.Tone = GuardianChipTone.Warning;
        _changesChip.Text = "CHECK GUARDIAN";
        _changesChip.Tone = GuardianChipTone.Warning;
        _commitLabel.Text = "LATEST  Repository refresh problem";
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

    private async Task ShowDiffAsync() => await RunOperationAsync("Loading diff...", async token =>
    {
        if (!HasRepository() || _files.SelectedRows.Count == 0)
        {
            _output.Text = "Select a changed file first.";
            return;
        }

        var path = Convert.ToString(_files.SelectedRows[0].Cells[1].Value) ?? "";
        var result = await _git.GetDiffAsync(_config.RepositoryPath!, path, token);

        if (result.Success && !string.IsNullOrWhiteSpace(result.Output))
        {
            _output.Text = result.Output;
        }
        else
        {
            var fullPath = Path.GetFullPath(Path.Combine(_config.RepositoryPath!, path));
            var root = Path.GetFullPath(_config.RepositoryPath!) + Path.DirectorySeparatorChar;

            if (fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) && File.Exists(fullPath))
            {
                var info = new FileInfo(fullPath);
                _output.Text = info.Length <= 150_000
                    ? "UNTRACKED / NO DIFF AVAILABLE\n\n" + await File.ReadAllTextAsync(fullPath, token)
                    : "Preview skipped because the file is larger than 150 KB.";
            }
            else
            {
                _output.Text = result.Output.Length == 0 ? "No diff is available." : result.Output;
            }
        }
    });

    private async Task RunTestsAsync() => await RunOperationAsync("Running configured tests...", async token =>
    {
        if (!HasRepository()) return;

        if (_config.TestCommands.Count == 0)
        {
            _output.Text = "No test commands are configured in config.json.";
            return;
        }

        var text = new System.Text.StringBuilder();
        foreach (var command in _config.TestCommands)
        {
            text.AppendLine("> " + command);
            var result = await _git.RunTestCommandAsync(_config.RepositoryPath!, command, token);
            text.AppendLine(result.Output).AppendLine($"exit: {result.ExitCode}").AppendLine();
            _output.Text = text.ToString();
            if (!result.Success) break;
        }
    });

    private async Task CreateCheckpointAsync() => await RunOperationAsync("Preparing restore point...", async token =>
    {
        if (!HasRepository()) return;

        _status = await _git.GetStatusAsync(_config.RepositoryPath!, token);
        if (!_status.Healthy || _status.Files.Count == 0)
        {
            _output.Text = _status.Healthy ? "The working tree is already clean." : _status.Error;
            return;
        }

        var suspicious = GitService.FindSuspiciousPaths(_status.Files, _config.SuspiciousPathPatterns);
        if (suspicious.Count > 0)
        {
            _output.Text = "Restore point blocked because suspicious paths are present:\n\n" + string.Join("\n", suspicious);
            await _audit.WriteAsync("checkpoint_blocked_suspicious_paths", new { files = suspicious });
            return;
        }

        var preview = string.Join("\n", _status.Files.Take(20).Select(f => $"{f.Status}  {f.Path}"));
        if (_status.Files.Count > 20) preview += $"\n... and {_status.Files.Count - 20} more";

        var answer = MessageBox.Show(
            this,
            "Create a restore point containing every current non-ignored change?\n\n" + preview,
            "Create restore point",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (answer != DialogResult.Yes) return;
        if (!await EnsureGitIdentityAsync(token)) return;

        var message = $"checkpoint: {DateTime.Now:yyyy-MM-dd HH:mm}";
        var result = await _git.CreateCheckpointAsync(_config.RepositoryPath!, message, token);
        _output.Text = result.Message;

        MessageBox.Show(
            this,
            result.Message,
            "Restore point",
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
            _output.Text = "Restore point cancelled. Git still needs an author name and email before it can create a commit.";
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
                "GitPet could not save the Git identity. No restore-point commit was created.\n\n" + save.Output,
                "Git identity",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        _output.Text = identity.UseGlobal
            ? "Git identity saved for Git projects on this PC. Creating restore point..."
            : "Git identity saved for this project. Creating restore point...";
        return true;
    }

    private async Task PullFromOriginAsync() => await RunOperationAsync("Checking pull safety...", async token =>
    {
        if (!HasRepository()) return;

        var repositoryPath = _config.RepositoryPath!;
        var status = await _git.GetStatusAsync(repositoryPath, token);
        if (!status.Healthy)
        {
            _output.Text = "Pull unavailable because Git could not read the working tree.\n\n" + status.Error;
            return;
        }

        if (status.Files.Count > 0)
        {
            _output.Text =
                $"Pull blocked safely: {status.Files.Count} uncommitted change{(status.Files.Count == 1 ? "" : "s")} detected.\n\n" +
                "Create a Checkpoint (or otherwise commit your work) before pulling. GitPet will not risk mixing incoming changes with an uncommitted working tree.";
            MessageBox.Show(
                this,
                "Pull was not started because this project has uncommitted changes.\n\n" +
                "Create a Checkpoint first, then try Pull ↓ again.",
                "Pull blocked safely",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var branchResult = await _git.GetCurrentBranchAsync(repositoryPath, token);
        var branch = branchResult.Success ? branchResult.Output.Trim() : "";
        if (string.IsNullOrWhiteSpace(branch))
        {
            _output.Text = "Pull unavailable: the repository is not on a named local branch (detached HEAD or branch lookup failed).";
            return;
        }

        var originResult = await _git.GetOriginUrlAsync(repositoryPath, token);
        if (!originResult.Success || string.IsNullOrWhiteSpace(originResult.Output))
        {
            _output.Text = string.IsNullOrWhiteSpace(originResult.Output)
                ? "Pull unavailable: no readable origin remote is configured."
                : originResult.Output;
            return;
        }

        var commit = await _git.GetLastCommitAsync(repositoryPath, token);
        var commitPreview = FormatCommitPreview(commit);
        var answer = MessageBox.Show(
            this,
            $"Pull the latest committed changes from origin into this local branch?\n\n" +
            $"Remote: origin\nBranch: {branch}\nCurrent local commit: {commitPreview}\n\n" +
            $"Command:\ngit pull --ff-only origin {branch}\n\n" +
            "FAST-FORWARD ONLY means GitPet will update the branch only when Git can do so without creating a merge commit. " +
            "If local and remote histories have diverged, Pull stops safely and leaves the history unchanged.",
            "Confirm manual pull",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (answer != DialogResult.Yes)
        {
            _output.Text = "Pull cancelled. Nothing was changed.";
            return;
        }

        _output.Text = $"Pulling origin/{branch} with fast-forward-only safety...";
        var result = await _git.PullFromOriginAsync(repositoryPath, branch, token);
        var details = string.IsNullOrWhiteSpace(result.Output) ? "Git reported success." : result.Output;

        _output.Text = result.Success
            ? $"Pull completed ✓\norigin/{branch} → local {branch}\n\n{details}"
            : $"Pull stopped safely.\norigin/{branch}\n\n{details}\n\nGitPet did not create a merge commit.";

        MessageBox.Show(
            this,
            result.Success
                ? $"Pull completed successfully.\n\norigin/{branch} → {branch}"
                : "Pull could not fast-forward safely. No merge commit was created. See Guardian Activity for details.",
            "Pull from origin",
            MessageBoxButtons.OK,
            result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

        await RefreshRepositoryViewAsync(token);
    });

    private async Task PushToOriginAsync() => await RunOperationAsync("Checking push destination...", async token =>
    {
        if (!HasRepository()) return;

        var repositoryPath = _config.RepositoryPath!;
        var branchResult = await _git.GetCurrentBranchAsync(repositoryPath, token);
        var branch = branchResult.Success ? branchResult.Output.Trim() : "";

        if (string.IsNullOrWhiteSpace(branch))
        {
            _output.Text = "Push unavailable: the repository is not on a named local branch (detached HEAD or branch lookup failed).";
            return;
        }

        var originResult = await _git.GetOriginUrlAsync(repositoryPath, token);
        if (!originResult.Success || string.IsNullOrWhiteSpace(originResult.Output))
        {
            _output.Text =
                "Push unavailable: this repository does not have a readable 'origin' remote.\n\n" +
                "ZomniverseGitPet will not create or configure remotes automatically.";
            return;
        }

        var commit = await _git.GetLastCommitAsync(repositoryPath, token);
        var status = await _git.GetStatusAsync(repositoryPath, token);
        var uncommittedCount = status.Healthy ? status.Files.Count : 0;
        var uncommittedNote = uncommittedCount > 0
            ? $"\n\nUncommitted changes: {uncommittedCount}\nThese changes will remain local and will NOT be included in this push."
            : "";
        var commitPreview = FormatCommitPreview(commit);

        var answer = MessageBox.Show(
            this,
            $"Push committed history to the configured origin remote?\n\n" +
            $"Remote: origin\nBranch: {branch}\nCommit: {commitPreview}" +
            uncommittedNote +
            $"\n\nCommand:\ngit push origin {branch}\n\n" +
            "ZomniverseGitPet will not stage, commit, create/configure a remote, or push automatically.",
            "Confirm manual push",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (answer != DialogResult.Yes)
        {
            _output.Text = "Push cancelled. Nothing was sent to the remote.";
            return;
        }

        _output.Text = $"Pushing committed history to origin/{branch}...";
        var result = await _git.PushToOriginAsync(repositoryPath, branch, token);
        var details = string.IsNullOrWhiteSpace(result.Output) ? "Git reported success." : result.Output;

        _output.Text = result.Success
            ? $"Push completed ✓\norigin/{branch}\n\n{details}"
            : $"Push failed.\norigin/{branch}\n\n{details}";

        MessageBox.Show(
            this,
            result.Success
                ? $"Push completed successfully.\n\norigin/{branch}"
                : "Push failed. See the Guardian output panel for details.",
            "Push to origin",
            MessageBoxButtons.OK,
            result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
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

        _output.Text = "Open Projects and choose a Git project first.";
        return false;
    }

    private async Task RunOperationAsync(string message, Func<CancellationToken, Task> action)
    {
        if (_operation is not null) return;

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
        Hide();
    }

    public void CloseForExit()
    {
        _exitRequested = true;
        _operation?.Cancel();
        Close();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _pulseTimer.Stop();
            _pulseTimer.Dispose();
            _toolTips.Dispose();
            _toolTipFont.Dispose();
        }

        base.Dispose(disposing);
    }
}
