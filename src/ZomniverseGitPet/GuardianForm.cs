using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ZomniverseGitPet;

public sealed class GuardianForm : Form
{
    private readonly AppConfig _config;
    private readonly ConfigStore _configStore;
    private readonly GitService _git;
    private readonly AuditLog _audit;
    private readonly Func<Task> _chooseRepository;

    /* ==========================================================================
       PATCH: CURRENT PROJECT PILL CONTROLS
       FUNCTION:
       Separates the current-project context label from the dynamic, scrollable
       project-name pill used in the repository header.

       DATE.TIME ADDED: 2026-09-12 20:05 +03:00

       REASON:
       Replace the repeated application title with a clearer active-project identity.
       ========================================================================== */
    private readonly Label _projectContextLabel = new();
    private readonly ProjectNamePill _projectTitle = new();
    private readonly Label _onlineLabel = new();
    private readonly Label _commitLabel = new();
    private readonly Label _watchingLabel = new();
    private readonly GuardianStatusChip _healthChip = new();
    private readonly GuardianStatusChip _branchChip = new();
    private readonly GuardianStatusChip _changesChip = new();
    private ContextMenuStrip? _repositoryBranchMenu;

    private readonly DataGridView _files = new();
    private readonly Label _emptyState = new();
    private readonly RichTextBox _output = new();
    private readonly Label _activityState = new();
    private readonly Label _activityElapsed = new();
    private GuardianActivityConsole? _activityConsole;
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
    private readonly System.Windows.Forms.Timer _saveTerminalTimer = new() { Interval = 2200 };
    private readonly SaveOperationStateController _saveOperation = new();
    private readonly SaveOperationStateController _getOperation = new(GuardianOperationKind.Get);
    private readonly SaveOperationStateController _sendOperation = new(GuardianOperationKind.Send);
    private readonly SaveOperationStateController _reconcileOperation = new(GuardianOperationKind.Reconcile);
    private bool _pulseBright;

    private readonly Button[] _operationButtons;
    private readonly GuardianActionButton _cancelButton;

    private CancellationTokenSource? _operation;
    private CancellationTokenSource? _comparisonLoad;
    private RepositoryStatus? _status;
    private bool _refreshInProgress;
    private bool _exitRequested;
    private string? _reviewedPath;
    private string? _lastRepositoryStatusError;

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

        /* ==========================================================================
           PATCH: CHANNEL-AWARE WINDOW TITLE
           FUNCTION:
           Use the same central DEV / release / portable identity as taskbar
           grouping, tray labeling, and single-instance handoff.

           DATE.TIME ADDED: 2026-09-19 22:53 +03:00

           REASON:
           Prevent window identity from drifting away from the active application channel.
           ========================================================================== */

        Text = ApplicationIdentity.Current.GuardianTitle;
        Icon = AppIconProvider.Icon;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(920, 670);
        Size = new Size(1180, 820);
        BackColor = GuardianTheme.Window;
        ForeColor = GuardianTheme.Ink;
        Font = new Font("Segoe UI", 9);
        WindowChrome.ApplyGuardianChrome(this);

        ConfigureToolTips();

        _branchChip.AccessibleName = "Repository branch";
        _branchChip.Click += async (_, _) =>
        {
            if (_branchChip.Interactive && _branchChip.Enabled)
                await ShowRepositoryBranchMenuAsync();
        };

        var menu = BuildMainMenu();
        var header = BuildHeader();

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 48,
            Padding = new Padding(10, 6, 8, 5),
            WrapContents = true,
            AutoScroll = false,
            BackColor = GuardianTheme.Surface
        };

        /* ==========================================================================
           PATCH: WIDER PROJECTS MENU BUTTON
           FUNCTION:
           Gives the Projects toolbar button enough horizontal space for its
           complete label and dropdown indicator.

           DATE.TIME ADDED: 2026-09-11 12:39 +03:00

           REASON:
           Prevent the Projects button label and dropdown indicator from being truncated.
           ========================================================================== */
        var projects = MakeActionButton("Projects ▾", GuardianActionKind.Standard, 140, async () => await _chooseRepository());
        var refresh = MakeActionButton("Refresh", GuardianActionKind.Standard, 92, RefreshAsync);
        /* ==========================================================================
           PATCH: WIDER REVIEW BUTTON
           FUNCTION:
           Gives the Review toolbar button enough horizontal space to display
           its complete label without ellipsis.

           DATE.TIME ADDED: 2026-09-11 12:42 +03:00

           REASON:
           Prevent the Review toolbar button label from being truncated.
           ========================================================================== */
        var diff = MakeActionButton("Review", GuardianActionKind.Standard, 112, ShowDiffAsync);
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
        _saveOperation.Changed += OnSaveOperationStateChanged;
        _getOperation.Changed += OnSaveOperationStateChanged;
        _sendOperation.Changed += OnSaveOperationStateChanged;
        _reconcileOperation.Changed += OnSaveOperationStateChanged;
        _saveTerminalTimer.Tick += (_, _) =>
        {
            _saveTerminalTimer.Stop();
            _saveOperation.Transition(SaveOperationPhase.Idle);
            _getOperation.Transition(SaveOperationPhase.Idle);
            _sendOperation.Transition(SaveOperationPhase.Idle);
            _reconcileOperation.Transition(SaveOperationPhase.Idle);
        };
    }

    private Control BuildHeader()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 178,
            Padding = new Padding(10, 10, 10, 10),
            BackColor = GuardianTheme.Window
        };

        var repositoryCard = new OnboardingSurfacePanel
        {
            Dock = DockStyle.Fill,
            FillColor = GuardianTheme.Surface,
            BackColor = GuardianTheme.Surface,
            BorderColor = GuardianTheme.Border,
            CornerRadius = 6,
            Padding = new Padding(16, 12, 16, 12)
        };

        var repositoryLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            BackColor = GuardianTheme.Surface
        };
        /* ==========================================================================
           PATCH: WIDEN COMPLETE REPOSITORY STATUS CARD
           FUNCTION:
           Increases the complete status-card width while allowing the repository
           summary and its separator line to use the remaining space.

           DATE.TIME ADDED: 2026-09-11 23:32 +03:00

           REASON:
           The complete branch name requires more card width without compressing another status column.
           ========================================================================== */
        repositoryLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        repositoryLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 410));

        var summary = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Margin = new Padding(0, 0, 28, 0),
            BackColor = GuardianTheme.Surface
        };
        summary.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        summary.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        summary.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        summary.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));

        /* ==========================================================================
           PATCH: BUILD CURRENT PROJECT PILL HEADER
           FUNCTION:
           Places a muted context label beside a dynamically sized project pill and
           constrains long names to the available header width.

           DATE.TIME ADDED: 2026-09-12 20:05 +03:00

           REASON:
           Clarify which text is the active project while preserving access to long names.
           ========================================================================== */
        var projectHeading = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = GuardianTheme.Surface
        };

        _projectContextLabel.AutoSize = true;
        _projectContextLabel.Text = "CURRENT PROJECT";
        _projectContextLabel.ForeColor = GuardianTheme.MutedInk;
        _projectContextLabel.Font = new Font("Cascadia Mono", 7.5f, FontStyle.Bold);
        _projectContextLabel.Margin = new Padding(0, 7, 10, 0);
        _projectContextLabel.TextAlign = ContentAlignment.MiddleLeft;

        _projectTitle.Text = "NO PROJECT";
        _projectTitle.Margin = Padding.Empty;
        projectHeading.Controls.Add(_projectContextLabel);
        projectHeading.Controls.Add(_projectTitle);
        /* ==========================================================================
           PATCH: USE ALL AVAILABLE PROJECT PILL WIDTH
           FUNCTION:
           Calculates the pill ceiling from the live header width after subtracting
           the context label and both controls' horizontal margins.

           DATE.TIME ADDED: 2026-09-12 20:29 +03:00

           REASON:
           Let full project names use free header space without entering the status region.
           ========================================================================== */
        projectHeading.Resize += (_, _) =>
        {
            var available = projectHeading.ClientSize.Width
                - _projectContextLabel.Width
                - _projectContextLabel.Margin.Horizontal
                - _projectTitle.Margin.Horizontal;
            _projectTitle.MaximumPillWidth = Math.Max(1, available);
        };

        var overview = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Repository overview and most recent checkpoint",
            ForeColor = GuardianTheme.MutedInk,
            Font = new Font("Segoe UI", 8.75f),
            TextAlign = ContentAlignment.TopLeft
        };

        var commitArea = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 8, 0, 0),
            Padding = new Padding(0, 12, 0, 0),
            BackColor = GuardianTheme.Surface
        };
        commitArea.Paint += (_, e) =>
        {
            using var separator = new Pen(GuardianTheme.BorderSoft);
            e.Graphics.DrawLine(separator, 0, 0, commitArea.ClientSize.Width, 0);
        };

        _commitLabel.Dock = DockStyle.Fill;
        _commitLabel.ForeColor = GuardianTheme.MutedInk;
        _commitLabel.Font = new Font("Cascadia Mono", 8.5f);
        _commitLabel.TextAlign = ContentAlignment.MiddleLeft;
        _commitLabel.AutoEllipsis = true;
        commitArea.Controls.Add(_commitLabel);

        _watchingLabel.Dock = DockStyle.Fill;
        _watchingLabel.Text = "Watching this repository";
        _watchingLabel.ForeColor = GuardianTheme.Violet;
        _watchingLabel.Font = new Font("Cascadia Mono", 7.75f);
        _watchingLabel.TextAlign = ContentAlignment.MiddleLeft;

        summary.Controls.Add(projectHeading, 0, 0);
        summary.Controls.Add(overview, 0, 1);
        summary.Controls.Add(commitArea, 0, 2);
        summary.Controls.Add(_watchingLabel, 0, 3);

        /* ==========================================================================
           PATCH: SUBTLE STATUS CARD STRUCTURE
           FUNCTION:
           Removes the bright full-cell grid and softens the rounded status
           card outline to match the approved mockup.

           DATE.TIME ADDED: 2026-09-11 13:57 +03:00

           REASON:
           Replace harsh system grid borders with a quieter GitPet status presentation.
           ========================================================================== */
        var statusCard = new OnboardingSurfacePanel
        {
            Dock = DockStyle.Fill,
            FillColor = GuardianTheme.SurfaceRaised,
            BackColor = GuardianTheme.SurfaceRaised,
            BorderColor = Color.FromArgb(145, GuardianTheme.BorderSoft),
            CornerRadius = 6,
            Padding = new Padding(8)
        };
        var statusGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Margin = Padding.Empty,
            BackColor = GuardianTheme.SurfaceRaised,
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None
        };
        /* ==========================================================================
           PATCH: BALANCE WIDENED STATUS CARD COLUMNS
           FUNCTION:
           Divides the newly widened status card equally so both Branch and Working
           Tree receive sufficient horizontal space.

           DATE.TIME ADDED: 2026-09-11 23:32 +03:00

           REASON:
           Unequal columns transfer branch space from Working Tree instead of widening the card.
           ========================================================================== */
        statusGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        statusGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        statusGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        statusGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        _onlineLabel.Dock = DockStyle.Fill;
        _onlineLabel.Text = "● ACTIVE";
        _onlineLabel.ForeColor = GuardianTheme.Healthy;
        _onlineLabel.Font = new Font("Cascadia Mono", 8.5f, FontStyle.Bold);
        _onlineLabel.TextAlign = ContentAlignment.MiddleLeft;

        var statusCaptions = new[] { "GUARDIAN", "HEALTH", "BRANCH", "WORKING TREE" };
        Control[] statusValues = [_onlineLabel, _healthChip, _branchChip, _changesChip];
        for (var index = 0; index < statusValues.Length; index++)
        {
            var value = statusValues[index];
            value.Dock = DockStyle.Fill;
            value.Margin = Padding.Empty;

            var statusCell = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = new Padding(10, 7, 8, 6),
                BackColor = GuardianTheme.SurfaceRaised
            };
            statusCell.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
            statusCell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            statusCell.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = statusCaptions[index],
                ForeColor = GuardianTheme.FaintInk,
                Font = new Font("Cascadia Mono", 7f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);
            statusCell.Controls.Add(value, 0, 1);
            statusGrid.Controls.Add(statusCell, index % 2, index / 2);
        }

        statusCard.Controls.Add(statusGrid);

        /* ==========================================================================
           PATCH: MUTED STATUS CARD DIVIDERS
           FUNCTION:
           Adds short translucent center dividers while leaving clear spacing
           around the rounded status card edges.

           DATE.TIME ADDED: 2026-09-11 13:57 +03:00

           REASON:
           Match the mockup without restoring bright borders around every status cell.
           ========================================================================== */
        var verticalStatusDivider = new Panel
        {
            BackColor = Color.FromArgb(80, GuardianTheme.Border),
            Enabled = false
        };
        var horizontalStatusDivider = new Panel
        {
            BackColor = Color.FromArgb(80, GuardianTheme.Border),
            Enabled = false
        };

        statusCard.Controls.Add(verticalStatusDivider);
        statusCard.Controls.Add(horizontalStatusDivider);

        statusCard.Layout += (_, _) =>
        {
            var contentBounds = statusGrid.Bounds;

            verticalStatusDivider.Bounds = new Rectangle(
                contentBounds.Left + (contentBounds.Width / 2),
                contentBounds.Top + 18,
                1,
                Math.Max(1, contentBounds.Height - 36));

            horizontalStatusDivider.Bounds = new Rectangle(
                contentBounds.Left + 18,
                contentBounds.Top + (contentBounds.Height / 2),
                Math.Max(1, contentBounds.Width - 36),
                1);

            verticalStatusDivider.BringToFront();
            horizontalStatusDivider.BringToFront();
        };

        repositoryLayout.Controls.Add(summary, 0, 0);
        repositoryLayout.Controls.Add(statusCard, 1, 0);
        repositoryCard.Controls.Add(repositoryLayout);
        panel.Controls.Add(repositoryCard);

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
        _files.ColumnHeadersHeight = 28;
        _files.ColumnHeadersDefaultCellStyle.BackColor = GuardianTheme.SurfaceSoft;
        _files.ColumnHeadersDefaultCellStyle.ForeColor = GuardianTheme.MutedInk;
        _files.ColumnHeadersDefaultCellStyle.Font = new Font("Cascadia Mono", 7.5f, FontStyle.Bold);
        _files.ColumnHeadersDefaultCellStyle.SelectionBackColor = GuardianTheme.SurfaceSoft;
        _files.ColumnHeadersDefaultCellStyle.SelectionForeColor = GuardianTheme.MutedInk;
        _files.DefaultCellStyle.BackColor = GuardianTheme.Surface;
        _files.DefaultCellStyle.ForeColor = GuardianTheme.Ink;
        _files.DefaultCellStyle.SelectionBackColor = Color.FromArgb(21, 28, 35);
        _files.DefaultCellStyle.SelectionForeColor = Color.White;
        _files.DefaultCellStyle.Font = new Font("Cascadia Mono", 8.25f);
        _files.DefaultCellStyle.Padding = new Padding(7, 2, 7, 2);
        _files.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(12, 17, 22);
        _files.RowTemplate.Height = 27;
        _files.ShowCellToolTips = true;

        _files.Columns.Add("Status", "STATE");
        _files.Columns.Add("Path", "PATH");
        _files.Columns[0].FillWeight = 20;
        _files.Columns[1].FillWeight = 80;
        _files.Columns[0].DefaultCellStyle.Font = new Font("Cascadia Mono", 7.75f, FontStyle.Bold);
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

        _activityElapsed.Dock = DockStyle.Right;
        _activityElapsed.Width = 260;
        _activityElapsed.Text = "00 hr 00 min 00 sec 000 ms";
        _activityElapsed.ForeColor = GuardianTheme.MutedInk;
        _activityElapsed.Font = new Font("Cascadia Mono", 8.5f);
        _activityElapsed.TextAlign = ContentAlignment.MiddleRight;

        header.Controls.Add(_activityState);
        header.Controls.Add(_activityElapsed);
        header.Controls.Add(title);

        _output.Dock = DockStyle.Fill;
        _output.ReadOnly = true;
        _output.Font = new Font("Cascadia Mono", 9.5f);
        _output.BackColor = GuardianTheme.Console;
        _output.ForeColor = Color.FromArgb(225, 218, 237);
        _output.BorderStyle = BorderStyle.None;
        _output.Padding = new Padding(12);
        _output.Text = "Guardian ready. Click a changed file to open the side-by-side File Review.";
        _output.HandleCreated += (_, _) => ApplyDarkScrollbarTheme(_output);
        _activityConsole = new GuardianActivityConsole(_output, _activityElapsed, _activityState);

        _toolTips.SetToolTip(_output,
            "Guardian Activity\n\nResults from Tests, Save, Get, Send, History, and Health appear here.\n" +
            "Click a changed file to switch this area to the Before / Now review workspace.");

        panel.Controls.Add(_output);
        panel.Controls.Add(header);
        return panel;
    }

    [DllImport(
        "uxtheme.dll",
        CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(
        IntPtr windowHandle,
        string? subApplicationName,
        string? subIdentifierList);

    private static void ApplyDarkScrollbarTheme(Control control)
    {
        if (!OperatingSystem.IsWindows() || !control.IsHandleCreated) return;

        _ = SetWindowTheme(
            control.Handle,
            "DarkMode_Explorer",
            null);
    }

    private Control BuildOptionsPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            BackColor = GuardianTheme.SurfaceRaised,
            Padding = new Padding(12, 5, 12, 5)
        };

        var label = new Label
        {
            AutoSize = true,
            Location = new Point(16, 9),
            Text = "AUTOMATIC SAVING",
            ForeColor = GuardianTheme.FaintInk,
            Font = new Font("Cascadia Mono", 7f, FontStyle.Bold)
        };

        _automatic.Text = "Automatic verified saves";
        _automatic.AutoSize = true;
        _automatic.Location = new Point(12, 24);
        _automatic.ForeColor = GuardianTheme.Ink;
        _automatic.BackColor = GuardianTheme.Surface;
        _automatic.Checked = _config.AutomaticCheckpointsEnabled;

        var note = new Label
        {
            AutoSize = true,
            Location = new Point(246, 25),
            Text = "OFF BY DEFAULT · local saves only · never sends automatically",
            ForeColor = GuardianTheme.FaintInk,
            Font = new Font("Cascadia Mono", 7.5f)
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
            BackColor = GuardianTheme.Window,
            ForeColor = GuardianTheme.Ink,
            GripStyle = ToolStripGripStyle.Hidden,
            RenderMode = ToolStripRenderMode.Professional,
            Renderer = GuardianTheme.CreateMenuRenderer(),
            Padding = new Padding(10, 2, 0, 2)
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
            Height = 32,
            Margin = new Padding(3, 1, 3, 1)
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
            var friendlyError = GitService.DescribeRepositoryReadFailure(_status.Error);
            if (!string.Equals(_lastRepositoryStatusError, friendlyError, StringComparison.Ordinal))
            {
                ReportActivity(friendlyError, GuardianActivityKind.Error);
                _lastRepositoryStatusError = friendlyError;
            }
            SetActivityState("● ATTENTION", GuardianTheme.Warning);
        }
        else
        {
            _lastRepositoryStatusError = null;
        }
    }

    private void UpdateRepositoryHeader(RepositoryStatus status, CommandResult commit)
    {
        /* ==========================================================================
           PATCH: DISPLAY SAVED PROJECT NAME IN HEADER
           FUNCTION:
           Uses the active GitPet project's saved display name for the project pill,
           falling back to the repository folder name when no display name exists.

           DATE.TIME ADDED: 2026-09-12 20:16 +03:00

           REASON:
           The folder basename hides the complete logical project name.
           ========================================================================== */
        var path = _config.RepositoryPath ?? "";
        var normalized = string.IsNullOrWhiteSpace(path) ? "" : Path.TrimEndingDirectorySeparator(path);
        var activeProject = _config.GetActiveProject();
        var name = !string.IsNullOrWhiteSpace(activeProject?.DisplayName)
            ? activeProject.DisplayName
            : string.IsNullOrWhiteSpace(normalized)
                ? "NO PROJECT"
                : Path.GetFileName(normalized);
        if (string.IsNullOrWhiteSpace(name)) name = normalized;

        _projectTitle.Text = name.ToUpperInvariant();
        _toolTips.SetToolTip(_projectTitle, string.IsNullOrWhiteSpace(path)
            ? "No active project."
            : $"Active project\n{path}\n\nClosing Guardian with X only hides this window; the fox keeps running.");

        if (!status.Healthy)
        {
            _healthChip.Text = "● ATTENTION";
            _healthChip.Tone = GuardianChipTone.Warning;
            _branchChip.Text = "?";
            _branchChip.Tone = GuardianChipTone.Neutral;
            _branchChip.Interactive = false;
            _branchChip.Enabled = false;
            _changesChip.Text = "STATUS UNKNOWN";
            _changesChip.Tone = GuardianChipTone.Warning;
            _commitLabel.Text = "LATEST  unavailable";
            _watchingLabel.Text = "Repository needs attention";
            return;
        }

        _healthChip.Text = "● HEALTHY";
        _healthChip.Tone = GuardianChipTone.Healthy;

        var scopedLogicalProject = StandaloneProjectPublishing.IsLogicalProject(_config, path);
        _branchChip.Interactive = !scopedLogicalProject;
        _branchChip.Enabled = !scopedLogicalProject && _operation is null;
        _branchChip.Text = status.Branch;
        _branchChip.Tone = GuardianChipTone.Neutral;
        _toolTips.SetToolTip(
            _branchChip,
            scopedLogicalProject
                ? $"Parent repository branch\n{status.Branch}\n\nThis is a scoped logical project. Use its standalone Branch ▾ control in the toolbar to change the project-only remote branch without switching the parent repository."
                : $"Repository branch\n{status.Branch}\n\nClick to switch branches inside GitPet. GitPet requires a clean working tree. Online-only branches become local tracking branches when selected.");
        _changesChip.Text = status.Files.Count == 0
            ? "CLEAN  ✓"
            : $"{status.Files.Count} CHANGE{(status.Files.Count == 1 ? "" : "S")}";
        _changesChip.Tone = status.Files.Count == 0
            ? GuardianChipTone.Healthy
            : GuardianChipTone.Changes;
        _commitLabel.Text =
            $"LATEST  {FormatCommitPreview(commit)}\r\n" +
            FriendlyGitState.FormatSyncSummary(status);
        _watchingLabel.Text = "Watching this repository";
    }

    private void SetNoProjectHeader()
    {
        _projectTitle.Text = "NO PROJECT";
        _healthChip.Text = "● WAITING";
        _healthChip.Tone = GuardianChipTone.Neutral;
        _branchChip.Text = "—";
        _branchChip.Tone = GuardianChipTone.Neutral;
        _branchChip.Interactive = false;
        _branchChip.Enabled = false;
        _changesChip.Text = "OPEN PROJECTS";
        _changesChip.Tone = GuardianChipTone.Neutral;
        _commitLabel.Text = "LATEST  Choose or prepare a project to begin.";
        _watchingLabel.Text = "Choose a project to begin";
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
        _watchingLabel.Text = "Repository needs attention";
        ShowActivityPanel();

        var friendlyError = GitService.DescribeRepositoryReadFailure(message);
        if (!string.Equals(_lastRepositoryStatusError, friendlyError, StringComparison.Ordinal))
        {
            ReportActivity(friendlyError, GuardianActivityKind.Warning);
            _lastRepositoryStatusError = friendlyError;
        }
        SetActivityState("● ATTENTION", GuardianTheme.Warning);
    }

    private async Task ShowRepositoryBranchMenuAsync()
    {
        if (_operation is not null || _refreshInProgress || !HasRepository()) return;

        var repositoryPath = _config.RepositoryPath!;
        if (StandaloneProjectPublishing.IsLogicalProject(_config, repositoryPath))
        {
            using var scoped = new GuardianConfirmDialog(
                "Repository branch",
                "PARENT BRANCH STAYS SEPARATE",
                "This GitPet project is a scoped logical project.\r\n\r\n" +
                "Use the standalone Branch ▾ control in the toolbar to change the project-only remote branch. " +
                "The BRANCH value in the status card is the shared parent repository branch.",
                "OK",
                showCancel: false);
            scoped.ShowDialog(this);
            return;
        }

        var status = await _git.GetStatusAsync(repositoryPath);
        if (!status.Healthy)
        {
            using var unhealthy = new GuardianConfirmDialog(
                "Repository branch",
                "REPOSITORY NEEDS ATTENTION",
                GitService.DescribeRepositoryReadFailure(status.Error),
                "OK",
                showCancel: false);
            unhealthy.ShowDialog(this);
            return;
        }

        if (status.Files.Count > 0)
        {
            using var dirty = new GuardianConfirmDialog(
                "Repository branch",
                "SAVE OR DISCARD CHANGES FIRST",
                $"GitPet found {status.Files.Count} unsaved working-tree change{(status.Files.Count == 1 ? "" : "s")}.\r\n\r\n" +
                "Finish those changes before switching the repository branch. GitPet will not stash, reset, or carry unsaved work across branches automatically.",
                "OK",
                showCancel: false);
            dirty.ShowDialog(this);
            return;
        }

        _branchChip.Enabled = false;
        IReadOnlyList<RepositoryBranchOption> branches;
        try
        {
            UseWaitCursor = true;
            branches = await _git.ListRepositoryBranchesAsync(repositoryPath, refreshRemote: true);
        }
        finally
        {
            UseWaitCursor = false;
            _branchChip.Enabled = true;
        }

        if (branches.Count == 0)
        {
            using var empty = new GuardianConfirmDialog(
                "Repository branch",
                "NO BRANCHES FOUND",
                "GitPet could not read local or origin branches for this repository.\r\n\r\n" +
                "The current branch has not been changed.",
                "OK",
                showCancel: false);
            empty.ShowDialog(this);
            return;
        }

        if (IsDisposed || Disposing || _branchChip.IsDisposed) return;

        _repositoryBranchMenu?.Close();
        _repositoryBranchMenu?.Dispose();

        var branchMenu = new ContextMenuStrip
        {
            BackColor = GuardianTheme.SurfaceRaised,
            ForeColor = GuardianTheme.Ink,
            ShowImageMargin = false,
            Font = new Font("Segoe UI", 9f)
        };
        _repositoryBranchMenu = branchMenu;

        foreach (var option in branches
                     .OrderBy(option => option.Name.Equals(status.Branch, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                     .ThenBy(option => option.IsLocal ? 0 : 1)
                     .ThenBy(option => option.Name, StringComparer.OrdinalIgnoreCase))
        {
            var isCurrent = option.Name.Equals(status.Branch, StringComparison.OrdinalIgnoreCase);
            var location = option.IsLocal && option.IsRemote
                ? "local + online"
                : option.IsLocal ? "local" : "online";
            var item = new ToolStripMenuItem(
                isCurrent
                    ? $"✓  {option.Name}    [{location}]"
                    : $"{option.Name}    [{location}]")
            {
                Enabled = !isCurrent,
                ForeColor = isCurrent ? GuardianTheme.Healthy : GuardianTheme.Ink,
                BackColor = GuardianTheme.SurfaceRaised,
                Tag = option
            };

            item.Click += async (_, _) =>
            {
                if (!branchMenu.IsDisposed) branchMenu.Close();
                await SwitchRepositoryBranchAsync(option);
            };
            branchMenu.Items.Add(item);
        }

        branchMenu.Items.Add(new ToolStripSeparator());
        branchMenu.Items.Add(new ToolStripMenuItem(
            "Online-only branches are tracked locally when selected.")
        {
            Enabled = false,
            ForeColor = GuardianTheme.MutedInk,
            BackColor = GuardianTheme.SurfaceRaised
        });

        // Keep the closed menu alive until replacement or form disposal.
        // WinForms still accesses it while completing item-click/close processing.

        branchMenu.Show(_branchChip, new Point(0, _branchChip.Height));
    }

    private async Task SwitchRepositoryBranchAsync(RepositoryBranchOption branch)
    {
        if (_operation is not null || !HasRepository()) return;

        var repositoryPath = _config.RepositoryPath!;
        var current = await _git.GetCurrentBranchAsync(repositoryPath);
        var currentBranch = current.Success && !string.IsNullOrWhiteSpace(current.Output)
            ? current.Output.Trim()
            : _status?.Branch ?? "?";
        if (branch.Name.Equals(currentBranch, StringComparison.OrdinalIgnoreCase)) return;

        var status = await _git.GetStatusAsync(repositoryPath);
        if (!status.Healthy || status.Files.Count > 0)
        {
            using var blocked = new GuardianConfirmDialog(
                "Repository branch",
                "BRANCH SWITCH BLOCKED",
                status.Healthy
                    ? "The working tree changed while the branch menu was open. Save or discard those changes before switching branches."
                    : GitService.DescribeRepositoryReadFailure(status.Error),
                "OK",
                showCancel: false);
            blocked.ShowDialog(this);
            return;
        }

        var remoteOnlyNote = branch.IsLocal
            ? ""
            : "\r\n\r\nThis branch currently exists only on origin. GitPet will create a local tracking branch for it.";

        using var confirmation = new GuardianConfirmDialog(
            "Repository branch",
            "SWITCH REPOSITORY BRANCH",
            $"Switch this repository from:\r\n{currentBranch}\r\n\r\nto:\r\n{branch.Name}?" +
            remoteOnlyNote +
            "\r\n\r\nGitPet will only run Git branch switching. It will not commit, merge, reset, clean, push, or force-update anything.",
            "Switch branch",
            "Cancel",
            confirmWidth: 150,
            dialogSize: new Size(760, 500));
        if (confirmation.ShowDialog(this) != DialogResult.Yes) return;

        await RunOperationAsync($"Switching repository branch to {branch.Name}...", async token =>
        {
            var result = await _git.SwitchRepositoryBranchAsync(repositoryPath, branch, token);
            if (!result.Success)
            {
                ReportActivity(
                    "Branch switch failed.\r\n\r\n" + result.Output,
                    GuardianActivityKind.Error);
                return;
            }

            _reviewedPath = null;
            ShowActivityPanel();
            ReportActivity(
                $"✓ Repository branch switched\r\n{currentBranch}  →  {branch.Name}\r\n\r\n" +
                (branch.IsLocal
                    ? "Existing local branch selected."
                    : "Online branch is now tracked by a new local branch."),
                GuardianActivityKind.Success);

            await _audit.WriteAsync("repository_branch_switched", new
            {
                repository = Path.GetFileName(Path.TrimEndingDirectorySeparator(repositoryPath)),
                from = currentBranch,
                to = branch.Name,
                branch.IsLocal,
                branch.IsRemote
            });

            try { await GuardianSyncState.RefreshAsync(true); } catch { }
            await RefreshRepositoryViewAsync(token);
            try { await GuardianWorkboardRuntime.RefreshNowAsync(); } catch { }
        });
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
            ReportActivity("Select a changed file first. Clicking a row opens its Before / Now review automatically.",
                GuardianActivityKind.Warning);
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
            var commitTask = _git.GetReviewCommitAsync(repositoryPath, token);
            var workingTask = ReadWorkingPreviewAsync(repositoryPath, relativePath, token);

            var commit = await commitTask;
            var commitHash = commit.Success ? commit.Output.Split('\t')[0].Trim() : "";
            var hasBaseline = commitHash.Length > 0;
            var working = await workingTask;

            CommandResult? beforeResult = null;
            CommandResult? diffResult = null;
            if (hasBaseline)
            {
                var beforeTask = _git.GetReviewContentAsync(repositoryPath, relativePath, commitHash, token);
                var diffTask = _git.GetReviewDiffAsync(repositoryPath, relativePath, commitHash, token);
                await Task.WhenAll(beforeTask, diffTask);
                beforeResult = await beforeTask;
                diffResult = await diffTask;
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
                ReportActivity(commands.Count == 0
                    ? "Tests cancelled. No test commands were saved for this project."
                    : "Test configuration cancelled. Existing project test commands were kept.",
                    GuardianActivityKind.Cancelled);
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
                ReportActivity("No test commands are saved for this project.", GuardianActivityKind.Warning);
                return;
            }

            if (!setup.RunAfterSave)
            {
                ReportActivity(
                    $"Saved {commands.Count} test command{(commands.Count == 1 ? "" : "s")} for this project.\n\n" +
                    "Press Tests to run them. Hold Shift while clicking Tests whenever you want to edit this list.",
                    GuardianActivityKind.Success);
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
            ReportActivity($"TEST {completed}/{commands.Count}\n> {command}");

            var result = await _git.RunTestCommandAsync(repositoryPath, command, token);
            if (!string.IsNullOrWhiteSpace(result.Output)) text.AppendLine(result.Output);
            text.AppendLine(result.Success ? "✓ PASS" : $"✕ FAIL  (exit {result.ExitCode})").AppendLine();
            ReportActivity((string.IsNullOrWhiteSpace(result.Output) ? "" : result.Output + "\n") +
                (result.Success ? "✓ PASS" : $"✕ FAIL  (exit {result.ExitCode})"),
                result.Success ? GuardianActivityKind.Success : GuardianActivityKind.Error);

            if (!result.Success)
            {
                allPassed = false;
                break;
            }
        }

        text.Insert(0, allPassed
            ? $"TESTS PASSED ✓  ({completed}/{commands.Count})\n\n"
            : $"TESTS STOPPED ✕  ({completed}/{commands.Count})\n\n");
        ReportActivity(allPassed
                ? $"TESTS PASSED ✓  ({completed}/{commands.Count})"
                : $"TESTS STOPPED ✕  ({completed}/{commands.Count})",
            allPassed ? GuardianActivityKind.Success : GuardianActivityKind.Error);
        await _audit.WriteAsync("manual_project_tests", new
        {
            repository = repositoryPath,
            configured = commands.Count,
            completed,
            success = allPassed
        });
    });

    private async Task CreateCheckpointAsync()
    {
        _saveTerminalTimer.Stop();
        _saveOperation.Transition(SaveOperationPhase.Preparing);
        await Task.Yield();
        await RunOperationAsync("Preparing save...", ExecuteSaveAsync);
    }

    private async Task ExecuteSaveAsync(CancellationToken token)
    {
        if (!HasRepository())
        {
            _saveOperation.Transition(SaveOperationPhase.Failed, "Open a project before saving.");
            return;
        }

        _status = await _git.GetStatusAsync(_config.RepositoryPath!, token);
        var progress = new Progress<GuardianActivityEvent>(HandleSaveProgress);
        var preflight = await _git.GetSavePreflightAsync(
            _config.RepositoryPath!, token, progress, _status);
        if (!_status.Healthy || !preflight.Success)
        {
            ReportActivity(_status.Healthy ? preflight.Error : _status.Error, GuardianActivityKind.Error);
            _saveOperation.Transition(SaveOperationPhase.Failed,
                _status.Healthy ? preflight.Error : _status.Error);
            return;
        }
        if (preflight.NormalChangedFiles.Count == 0 && preflight.IgnoredChangedFiles.Count == 0)
        {
            ReportActivity("Everything is already saved locally.", GuardianActivityKind.Success);
            _saveOperation.Transition(SaveOperationPhase.Completed, "Everything is already saved locally.");
            return;
        }

        var suspicious = GitService.FindSuspiciousPaths(_status.Files, _config.SuspiciousPathPatterns);
        if (suspicious.Count > 0)
        {
            ReportActivity("Save blocked because suspicious paths are present:\n\n" +
                string.Join("\n", suspicious), GuardianActivityKind.Warning);
            await _audit.WriteAsync("checkpoint_blocked_suspicious_paths", new { files = suspicious });
            _saveOperation.Transition(SaveOperationPhase.Warning, "Suspicious paths require attention.");
            return;
        }

        SaveStagePlan? stagePlan = null;
        if (preflight.IgnoredChangedFiles.Count > 0)
        {
            /* ==========================================================================
               PATCH: EXPLICIT FORCE-TRACK APPROVAL
               DATE: 2026-09-11

               Review ignored files before any staging mutation.
               ========================================================================== */
            while (true)
            {
                /* ==========================================================================
                   PATCH: IGNORED FILE PET GUIDANCE
                   FUNCTION:
                   Explains why the ignored-file review appeared and what the user should select.

                   DATE.TIME ADDED: 2026-09-11 17:16 +03:00

                   REASON:
                   Guide users through explicit force-track approval without changing Save behavior.
                   ========================================================================== */
                var guidancePet = Application.OpenForms
                    .OfType<PetForm>()
                    .FirstOrDefault(form => form.Visible && !form.IsDisposed);

                /* ==========================================================================
                   PATCH: HELD IGNORED FILE GUIDANCE
                   FUNCTION:
                   Keeps the review instruction visible until the ignored-file dialog closes.

                   DATE.TIME ADDED: 2026-09-11 17:34 +03:00

                   REASON:
                   Prevent repository refreshes from replacing the dialog-specific pet message.
                   ========================================================================== */
                guidancePet?.BeginGuidanceHold(
                    "⚠ IGNORED FILES FOUND\nReview, then tick files to track");

                using var ignoredDialog = new IgnoredProjectFilesDialog(
                    _config.RepositoryPath!, preflight.IgnoredChangedFiles);
                var ignoredDialogResult = ignoredDialog.ShowDialog(this);

                guidancePet?.EndGuidanceHold();

                if (ignoredDialogResult != DialogResult.Yes)
                {
                    _saveOperation.Transition(SaveOperationPhase.Cancelled);
                    return;
                }

                var selected = ignoredDialog.SelectedPaths;
                if (selected.Count > 0)
                {
                    var details = preflight.IgnoredChangedFiles
                        .Where(item => selected.Contains(item.Path, StringComparer.OrdinalIgnoreCase))
                        .Select(item => $"{item.Path}\r\n  Ignored by: {item.IgnoreSource}" +
                                        (item.IgnoreLine is int line ? $"\r\n  Line {line}: {item.Rule}" : $"\r\n  Rule: {item.Rule}"));
                    /* ==========================================================================
                       PATCH: RESIZABLE FORCE-TRACK CONFIRMATION
                       FUNCTION:
                       Enables scrolling, resizing, and a complete primary action label for this confirmation.

                       DATE.TIME ADDED: 2026-09-11 17:48 +03:00

                       REASON:
                       Keep large approved-file lists and both confirmation actions fully accessible.
                       ========================================================================== */
                    using var confirm = new GuardianConfirmDialog(
                        "Track ignored files?",
                        "TRACK IGNORED FILES?",
                        "GitPet will explicitly track these files even though Git currently ignores them:\r\n\r\n" +
                        string.Join("\r\n\r\n", details) +
                        "\r\n\r\nThis does NOT remove or modify the ignore rule.\r\n" +
                        "Only the exact selected files will be force-added.",
                        "Track selected files",
                        "Back",
                        dialogSize: new Size(900, 680),
                        resizable: true,
                        scrollable: true,
                        confirmWidth: 210);
                    if (confirm.ShowDialog(this) != DialogResult.Yes) continue;
                }

                stagePlan = IgnoredFileSavePolicy.CreateStagePlan(
                    preflight.NormalChangedFiles,
                    preflight.IgnoredChangedFiles,
                    selected);
                break;
            }
        }
        else
        {
            var preview = string.Join("\n", _status.Files.Take(20).Select(f => $"{HumanizeGitStatus(f.Status)}  {f.Path}"));
            if (_status.Files.Count > 20) preview += $"\n... and {_status.Files.Count - 20} more";

            var countText = FriendlyGitState.Count(_status.Files.Count, "current change");
            using var saveDialog = new GuardianConfirmDialog(
                "Save changes",
                "SAVE LOCALLY",
                $"Save all {countText} as a local version?\r\n\r\n" +
                preview +
                "\r\n\r\nThis saves the current state on this PC.\r\n" +
                "Nothing will be sent online.",
                "Save",
                "Cancel");

            if (saveDialog.ShowDialog(this) != DialogResult.Yes)
            {
                _saveOperation.Transition(SaveOperationPhase.Cancelled);
                return;
            }
        }

        if (!await EnsureGitIdentityAsync(token))
        {
            GetActiveOperationController()?.Transition(SaveOperationPhase.Cancelled);
            return;
        }

        var message = $"checkpoint: {DateTime.Now:yyyy-MM-dd HH:mm}";
        var result = await _git.CreateCheckpointAsync(_config.RepositoryPath!, message, stagePlan, token, progress);
        ReportActivity(result.Success
                ? "Changes saved locally ✓\n\n" + result.Message
                : result.Message,
            result.Success ? GuardianActivityKind.Success : GuardianActivityKind.Error);
        _saveOperation.Transition(result.Success ? SaveOperationPhase.Completed : SaveOperationPhase.Failed,
            result.Success ? "Changes saved locally." : result.Message);

            /*
            PATCH: THEMED SAVE RESULT
            DATE: 2026-09-09
            Show Save result using Guardian dialog styling.
            */
            using var savedDialog = new GuardianConfirmDialog(
                "Save changes",
                result.Success ? "SAVED LOCALLY  ✓" : "SAVE NEEDS ATTENTION",
                result.Success
                    ? (stagePlan is null
                        ? "Changes saved locally.\r\n\r\nNothing was sent online."
                        : result.Message + "\r\n\r\nNothing was sent online.")
                    : result.Message,
                "OK",
                "",
                showCancel: false);

            savedDialog.ShowDialog(this);

        await RefreshRepositoryViewAsync(token);
    }

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
            ReportActivity("Save cancelled. Git still needs an author name and email before it can save a local version.",
                GuardianActivityKind.Cancelled);
            _saveOperation.Transition(SaveOperationPhase.Cancelled);
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
            ReportActivity(save.Output, GuardianActivityKind.Error);
            _saveOperation.Transition(SaveOperationPhase.Failed, save.Output);
            MessageBox.Show(
                this,
                "GitPet could not save the Git identity. No changes were saved.\n\n" + save.Output,
                "Git identity",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        ReportActivity(identity.UseGlobal
            ? "Git identity saved for Git projects on this PC. Saving changes..."
            : "Git identity saved for this project. Saving changes...", GuardianActivityKind.Success);
        return true;
    }

    private async Task PullFromOriginAsync()
    {
        _getOperation.Transition(SaveOperationPhase.Preparing, "Checking Get safety...");
        await Task.Yield();
        await RunOperationAsync("Checking Get safety...", async token =>
    {
        if (!HasRepository()) return;

        var repositoryPath = _config.RepositoryPath!;
        var status = await _git.GetStatusAsync(repositoryPath, token);
        if (!status.Healthy)
        {
            ReportActivity("Get unavailable because Git could not read the current project state.\n\n" + status.Error,
                GuardianActivityKind.Error);
            _getOperation.Transition(SaveOperationPhase.Failed, "Git could not read the current project state.");
            return;
        }

        if (status.Files.Count > 0)
        {
            var countText = FriendlyGitState.Count(status.Files.Count, "unsaved change");
            ReportActivity(
                $"Get blocked safely: {countText} detected.\n\n" +
                "Save the current work before getting online updates so the two versions are not accidentally mixed.",
                GuardianActivityKind.Warning);
            MessageBox.Show(
                this,
                $"GitPet found {countText}.\n\n" +
                "Save them before getting online updates so the two versions are not accidentally mixed.\n\n" +
                "Save first, then try Get ↓ again.",
                "Save first",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            _getOperation.Transition(SaveOperationPhase.Warning, "Save local changes before using Get.");
            return;
        }

        var branchResult = await _git.GetCurrentBranchAsync(repositoryPath, token);
        var branch = branchResult.Success ? branchResult.Output.Trim() : "";
        if (string.IsNullOrWhiteSpace(branch))
        {
            ReportActivity("Get unavailable: the repository is not on a named local branch (detached HEAD or branch lookup failed).",
                GuardianActivityKind.Error);
            _getOperation.Transition(SaveOperationPhase.Failed, "The current branch could not be determined.");
            return;
        }

        var originResult = await _git.GetOriginUrlAsync(repositoryPath, token);
        if (!originResult.Success || string.IsNullOrWhiteSpace(originResult.Output))
        {
            ReportActivity(string.IsNullOrWhiteSpace(originResult.Output)
                ? "Get unavailable: no readable origin remote is configured."
                : originResult.Output, GuardianActivityKind.Error);
            _getOperation.Transition(SaveOperationPhase.Failed, "No readable origin remote is configured.");
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
            ReportActivity("Get cancelled. Nothing was changed.", GuardianActivityKind.Cancelled);
            _getOperation.Transition(SaveOperationPhase.Cancelled);
            return;
        }

        _getOperation.Transition(SaveOperationPhase.Staging, $"Getting updates from origin/{branch}...");
        ReportActivity($"Getting updates from origin/{branch} with fast-forward-only safety...");
        var result = await _git.PullFromOriginAsync(repositoryPath, branch, token);
        var details = string.IsNullOrWhiteSpace(result.Output) ? "Git reported success." : result.Output;

        ReportActivity(result.Success
            ? $"Updates received ✓\norigin/{branch} → local {branch}\n\n{details}"
            : $"Get stopped safely.\norigin/{branch}\n\n{details}\n\nGitPet did not create a merge commit.",
            result.Success ? GuardianActivityKind.Success : GuardianActivityKind.Error);
        _getOperation.Transition(result.Success ? SaveOperationPhase.Completed : SaveOperationPhase.Failed,
            result.Success ? "Updates received successfully." : "Get stopped safely.");

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
        if (_getOperation.Current.IsActive)
            _getOperation.Transition(SaveOperationPhase.Warning, "Get stopped before receiving updates.");
    }

    private async Task PushToOriginAsync()
    {
        _sendOperation.Transition(SaveOperationPhase.Preparing, "Checking what is ready to send...");
        await Task.Yield();
        await RunOperationAsync("Checking what is ready to send...", async token =>
    {
        if (!HasRepository()) return;

        var repositoryPath = _config.RepositoryPath!;
        var status = await _git.GetStatusAsync(repositoryPath, token);
        if (!status.Healthy)
        {
            ReportActivity("Send unavailable because Git could not read the current project state.\n\n" + status.Error,
                GuardianActivityKind.Error);
            _sendOperation.Transition(SaveOperationPhase.Failed, "Git could not read the current project state.");
            return;
        }

        var readiness = FriendlyGitState.GetSendReadiness(status);
        if (readiness == SendReadiness.SaveFirst)
        {
            var countText = FriendlyGitState.Count(status.Files.Count, "unsaved change");
            ReportActivity($"Nothing is ready to send yet.\n\nYou have {countText} on this PC.\nSave them first, then use Send.",
                GuardianActivityKind.Warning);
            MessageBox.Show(
                this,
                $"Nothing is ready to send yet.\n\nYou have {countText} on this PC.\n\nSave them first, then use Send.",
                "Save your changes first",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            _sendOperation.Transition(SaveOperationPhase.Warning, "Save local changes before using Send.");
            return;
        }

        if (readiness == SendReadiness.AlreadyUpToDate)
        {
            ReportActivity("Everything saved is already online.\n\nThere is nothing new to send.", GuardianActivityKind.Success);
            _sendOperation.Transition(SaveOperationPhase.Completed, "Everything saved is already online.");
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
            ReportActivity("Send unavailable: the repository is not on a named local branch (detached HEAD or branch lookup failed).",
                GuardianActivityKind.Error);
            _sendOperation.Transition(SaveOperationPhase.Failed, "The current branch could not be determined.");
            return;
        }

        var originResult = await _git.GetOriginUrlAsync(repositoryPath, token);
        if (!originResult.Success || string.IsNullOrWhiteSpace(originResult.Output))
        {
            ReportActivity(string.IsNullOrWhiteSpace(originResult.Output)
                ? "Send unavailable: no readable origin remote is configured."
                : originResult.Output, GuardianActivityKind.Error);
            _sendOperation.Transition(SaveOperationPhase.Failed, "No readable origin remote is configured.");
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

        /*
        PATCH: THEMED SEND CONFIRMATION
        DATE: 2026-09-09
        Use Guardian styling for Send confirmation.
        */
        var sendMessage = status.HasTrackingInformation
            ? $"Send {savedText} to the online copy?\r\n\r\n" +
            $"Branch: {branch}\r\n" +
            $"Latest saved version: {commitPreview}" +
            unsavedText
            : $"Send the saved committed history to the online copy?\r\n\r\n" +
            $"Branch: {branch}\r\n" +
            $"Latest saved version: {commitPreview}" +
            unsavedText;

        using var sendDialog = new GuardianConfirmDialog(
            "Send saved updates",
            "SEND ONLINE",
            sendMessage,
            "Send",
            "Cancel");

        var answer = sendDialog.ShowDialog(this);

        if (answer != DialogResult.Yes)
        {
            ReportActivity("Send cancelled. Nothing was sent online.", GuardianActivityKind.Cancelled);
            _sendOperation.Transition(SaveOperationPhase.Cancelled);
            return;
        }

        _sendOperation.Transition(SaveOperationPhase.Staging, $"Sending saved updates to origin/{branch}...");
        ReportActivity($"Sending saved updates to origin/{branch}...");
        var result = await _git.PushToOriginAsync(repositoryPath, branch, token);
        var details = string.IsNullOrWhiteSpace(result.Output) ? "Git reported success." : result.Output;

        ReportActivity(result.Success
            ? $"Send completed ✓\norigin/{branch}\n\n{details}"
            : $"Send failed.\norigin/{branch}\n\n{details}",
            result.Success ? GuardianActivityKind.Success : GuardianActivityKind.Error);
        _sendOperation.Transition(result.Success ? SaveOperationPhase.Completed : SaveOperationPhase.Failed,
            result.Success ? "Saved updates were sent successfully." : "Send failed.");

        /*
        PATCH: THEMED SEND RESULT
        DATE: 2026-09-09
        Style final Send result with Guardian dialog.
        */
        using var sentDialog = new GuardianConfirmDialog(
            "Send saved updates",
            result.Success ? "SENT ONLINE  ✓" : "SEND NEEDS ATTENTION",
            result.Success
                ? "Saved updates were sent successfully.\r\n\r\nThe online copy is now updated."
                : "Send failed.\r\n\r\nSee Guardian Activity for details.",
            "OK",
            "",
            showCancel: false);

        sentDialog.ShowDialog(this);

        await RefreshRepositoryViewAsync(token);
        });
        if (_sendOperation.Current.IsActive)
            _sendOperation.Transition(SaveOperationPhase.Warning, "Send stopped before uploading updates.");
    }

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
        ReportActivity(result.Output, result.Success ? GuardianActivityKind.Success : GuardianActivityKind.Error);
    });

    private async Task HealthCheckAsync() => await RunOperationAsync("Running Git health check...", async token =>
    {
        if (!HasRepository()) return;

        var result = await _git.HealthCheckAsync(_config.RepositoryPath!, token);
        ReportActivity(result.Success
            ? "git fsck passed.\n\n" + result.Output
            : "git fsck failed.\n\n" + result.Output,
            result.Success ? GuardianActivityKind.Success : GuardianActivityKind.Error);

        await _audit.WriteAsync("health_check", new { success = result.Success, result.TimedOut });
    });

    private bool HasRepository()
    {
        if (!string.IsNullOrWhiteSpace(_config.RepositoryPath)) return true;

        ShowActivityPanel();
        ReportActivity("Open Projects and choose a Git project first.", GuardianActivityKind.Warning);
        return false;
    }

    private async Task RunOperationAsync(string message, Func<CancellationToken, Task> action)
    {
        if (_operation is not null) return;

        _comparisonLoad?.Cancel();
        ShowActivityPanel();
        _operation = new CancellationTokenSource();
        foreach (var button in _operationButtons) button.Enabled = false;
        var branchChipWasEnabled = _branchChip.Enabled;
        _branchChip.Enabled = false;

        _cancelButton.Visible = true;
        SetActivityState("● WORKING", GuardianTheme.Changes);
        var firstOperationEntry = _activityConsole?.EntryCount ?? 0;
        _activityConsole?.Begin(message);
        var operationTimer = Stopwatch.StartNew();
        var outcome = "success";
        await _audit.WriteAsync("operation_started", new { message });

        try
        {
            await action(_operation.Token);
            var hasErrors = _activityConsole?.HasKindSince(firstOperationEntry,
                GuardianActivityKind.Error,
                GuardianActivityKind.LongPathConfigurationFailed,
                GuardianActivityKind.LongPathRetryFailed) == true;
            var hasWarnings = _activityConsole?.HasKindSince(firstOperationEntry, GuardianActivityKind.Warning) == true;
            var finalKind = hasErrors
                ? GuardianActivityKind.Error
                : hasWarnings ? GuardianActivityKind.Warning : GuardianActivityKind.OperationCompleted;
            outcome = hasErrors ? "failed" : hasWarnings ? "warning" : "success";
            _activityConsole?.Finish(finalKind,
                $"Operation finished in {FormatElapsed(operationTimer.Elapsed)}" +
                (hasErrors ? " with errors." : hasWarnings ? " with warnings." : "."));
        }
        catch (OperationCanceledException)
        {
            outcome = "cancelled";
            if (_saveOperation.Current.IsActive)
                _saveOperation.Transition(SaveOperationPhase.Cancelled);
            _activityConsole?.Finish(GuardianActivityKind.Cancelled, "Operation cancelled.");
        }
        catch (Exception ex)
        {
            outcome = "failed";
            GetActiveOperationController()?.Transition(SaveOperationPhase.Failed, ex.Message);
            _activityConsole?.Finish(GuardianActivityKind.Error, ex.Message);
            await _audit.WriteAsync("operation_error", new { error = ex.Message });
        }
        finally
        {
            foreach (var button in _operationButtons) button.Enabled = true;
            _branchChip.Enabled = branchChipWasEnabled;
            _cancelButton.Visible = false;
            SetActivityState("● READY", GuardianTheme.Healthy);

            _operation.Dispose();
            _operation = null;
            if (AllOperationControllers().Any(controller => controller.Current.Phase is
                    SaveOperationPhase.Completed or SaveOperationPhase.Warning or
                    SaveOperationPhase.Failed or SaveOperationPhase.Cancelled))
                ScheduleSaveIdle();
            await _audit.WriteAsync("operation_completed", new
            {
                message,
                outcome,
                elapsedMilliseconds = operationTimer.ElapsedMilliseconds
            });
        }
    }

    private static string FormatElapsed(TimeSpan elapsed) =>
        $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}.{elapsed.Milliseconds:000}";

    private void ReportActivity(string message, GuardianActivityKind kind = GuardianActivityKind.Information) =>
        _activityConsole?.AppendMessage(message, kind);

    internal void BeginProjectSwitchActivity(string projectName)
    {
        ShowActivityPanel();
        _lastRepositoryStatusError = null;
        _activityConsole?.ResetForProjectContext($"Switching to {projectName}...");
        SetActivityState("● SWITCHING", GuardianTheme.Changes);
    }

    internal void ReportProjectSwitchActivity(
        string message,
        GuardianActivityKind kind = GuardianActivityKind.Information)
    {
        ShowActivityPanel();
        ReportActivity(message, kind);

        if (kind == GuardianActivityKind.Error)
            SetActivityState("● SWITCH FAILED", GuardianTheme.Warning);
        else if (kind == GuardianActivityKind.Success || kind == GuardianActivityKind.Cancelled)
            SetActivityState("● READY", GuardianTheme.Healthy);
        else
            SetActivityState("● SWITCHING", GuardianTheme.Changes);
    }

    internal void RefreshSyncActionButtons()
    {
        foreach (var button in _operationButtons.OfType<GuardianActionButton>())
            button.RefreshSyncPresentation();
    }

    private void HandleSaveProgress(GuardianActivityEvent activity)
    {
        var phase = MapActivityToSavePhase(activity.Kind);
        if (phase is SaveOperationPhase next) _saveOperation.Transition(next, activity.Message);
        _activityConsole?.Append(activity);
    }

    internal static SaveOperationPhase? MapActivityToSavePhase(GuardianActivityKind kind) => kind switch
        {
            GuardianActivityKind.LongPathChecking or GuardianActivityKind.LongPathEnabling or
                GuardianActivityKind.LongPathAlreadyEnabled => SaveOperationPhase.CheckingPathSupport,
            GuardianActivityKind.SaveStaging or GuardianActivityKind.LongPathRetrying => SaveOperationPhase.Staging,
            GuardianActivityKind.SaveCreatingCheckpoint => SaveOperationPhase.CreatingCheckpoint,
            GuardianActivityKind.LongPathRetryFailed => SaveOperationPhase.Failed,
            _ => (SaveOperationPhase?)null
        };

    private void OnSaveOperationStateChanged(object? sender, SaveOperationVisualState state)
    {
        GuardianWorkboardRuntime.SetOperationState(this, state);
        foreach (var pet in Application.OpenForms.OfType<PetForm>().Where(pet => !pet.IsDisposed))
            pet.SetOperationState(state);

        var blockConflictingActions = state.IsActive;
        foreach (var button in _operationButtons.Where(button =>
                     button.Text.StartsWith("Save", StringComparison.OrdinalIgnoreCase) ||
                     button.Text.StartsWith("Get", StringComparison.OrdinalIgnoreCase) ||
                     button.Text.StartsWith("Send", StringComparison.OrdinalIgnoreCase) ||
                     button.Text.StartsWith("Refresh", StringComparison.OrdinalIgnoreCase)))
            button.Enabled = !blockConflictingActions && _operation is null;

        if (state.IsActive)
            SetActivityState("● " + state.Phase.ToString().ToUpperInvariant(), GuardianTheme.Changes);
        else if (state.Phase != SaveOperationPhase.Idle)
        {
            if (_operation is null) ScheduleSaveIdle();
        }
    }

    private void ScheduleSaveIdle()
    {
        _saveTerminalTimer.Stop();
        _saveTerminalTimer.Start();
    }

    private IEnumerable<SaveOperationStateController> AllOperationControllers()
    {
        yield return _saveOperation;
        yield return _getOperation;
        yield return _sendOperation;
        yield return _reconcileOperation;
    }

    private SaveOperationStateController? GetActiveOperationController() =>
        AllOperationControllers().FirstOrDefault(controller => controller.Current.IsActive);

    internal void SetReconcileOperationState(SaveOperationPhase phase, string? message = null)
    {
        var text = message ?? SaveOperationStateController.DefaultMessage(phase, GuardianOperationKind.Reconcile);
        _reconcileOperation.Transition(phase, message);
        if (phase == SaveOperationPhase.Idle) return;
        ShowActivityPanel();
        if (phase == SaveOperationPhase.Preparing)
            _activityConsole?.Begin(text);
        else if (phase is SaveOperationPhase.Completed or SaveOperationPhase.Warning or
                 SaveOperationPhase.Failed or SaveOperationPhase.Cancelled)
            _activityConsole?.Finish(phase switch
            {
                SaveOperationPhase.Completed => GuardianActivityKind.OperationCompleted,
                SaveOperationPhase.Warning => GuardianActivityKind.Warning,
                SaveOperationPhase.Failed => GuardianActivityKind.Error,
                _ => GuardianActivityKind.Cancelled
            }, text);
        else
            ReportActivity(text);
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
            _saveTerminalTimer.Stop();
            _saveTerminalTimer.Dispose();
            _saveOperation.Changed -= OnSaveOperationStateChanged;
            _getOperation.Changed -= OnSaveOperationStateChanged;
            _sendOperation.Changed -= OnSaveOperationStateChanged;
            _reconcileOperation.Changed -= OnSaveOperationStateChanged;
            _comparisonLoad?.Cancel();
            _comparisonLoad?.Dispose();
            _repositoryBranchMenu?.Dispose();
            _repositoryBranchMenu = null;
            _toolTips.Dispose();
            _toolTipFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private sealed record WorkingPreview(bool Exists, string Text);
}
