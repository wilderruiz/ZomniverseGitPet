using System.Diagnostics;

namespace ZomniverseGitPet;

public sealed class GuardianForm : Form
{
    private readonly AppConfig _config;
    private readonly ConfigStore _configStore;
    private readonly GitService _git;
    private readonly AuditLog _audit;
    private readonly Func<Task> _chooseRepository;
    private readonly Label _summary = new();
    private readonly DataGridView _files = new();
    private readonly RichTextBox _output = new();
    private readonly CheckBox _automatic = new();
    private readonly ToolTip _toolTips = new()
    {
        InitialDelay = 350,
        ReshowDelay = 100,
        AutoPopDelay = 15000,
        ShowAlways = true
    };
    private readonly Button[] _operationButtons;
    private CancellationTokenSource? _operation;
    private RepositoryStatus? _status;
    private bool _exitRequested;

    public GuardianForm(AppConfig config, ConfigStore configStore, GitService git, AuditLog audit, Func<Task> chooseRepository)
    {
        _config = config;
        _configStore = configStore;
        _git = git;
        _audit = audit;
        _chooseRepository = chooseRepository;

        Text = "ZomniverseGitPet Guardian";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(820, 620);
        Size = new Size(980, 760);
        BackColor = Color.FromArgb(245, 245, 247);

        _summary.Dock = DockStyle.Top;
        _summary.Height = 66;
        _summary.Padding = new Padding(14, 10, 10, 4);
        _summary.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        _summary.Text = "Checking repository...";
        _toolTips.SetToolTip(_summary,
            "Repository summary: current Git health, branch, changed-item count, and latest commit.\r\n\r\n" +
            "Closing this Guardian window with X only hides it; ZomniverseGitPet keeps running.\r\n" +
            "Use the pet/tray Exit command when you actually want to quit the application.");

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, Height = 48, Padding = new Padding(10, 7, 4, 4), WrapContents = false
        };
        var choose = MakeButton("Choose Repo", async () => await _chooseRepository());
        var refresh = MakeButton("Refresh", RefreshAsync);
        var diff = MakeButton("View Diff", ShowDiffAsync);
        var tests = MakeButton("Run Tests", RunTestsAsync);
        var checkpoint = MakeButton("Restore Point", CreateCheckpointAsync, 112);
        var recent = MakeButton("Recent Commits", RecentCommitsAsync, 112);
        var health = MakeButton("Health Check", HealthCheckAsync, 105);
        var cancel = MakeButton("Cancel", () => { _operation?.Cancel(); return Task.CompletedTask; }, 75);
        buttons.Controls.AddRange([choose, refresh, diff, tests, checkpoint, recent, health, cancel]);
        _operationButtons = [choose, refresh, diff, tests, checkpoint, recent, health];

        _toolTips.SetToolTip(choose,
            "Choose Repo\r\n\r\nSelect the local Git repository ZomniverseGitPet should watch.\r\n" +
            "The selected folder is verified as a readable Git repository.\r\n" +
            "This does not create or configure any Git remote.");
        _toolTips.SetToolTip(refresh,
            "Refresh\r\n\r\nRe-read the repository's current branch and working-tree status.\r\n" +
            "This updates the changed-file list without modifying repository files.");
        _toolTips.SetToolTip(diff,
            "View Diff\r\n\r\nSelect a changed file first, then use this to inspect what changed.\r\n" +
            "Tracked files show their Git diff. Small untracked text files can be previewed directly.\r\n" +
            "Viewing does not edit the file.");
        _toolTips.SetToolTip(tests,
            "Run Tests\r\n\r\nRun the test commands configured for this repository in config.json.\r\n" +
            "If no commands are configured, Guardian will tell you instead of running anything.");
        _toolTips.SetToolTip(checkpoint,
            "Restore Point — SAVE the current state; this is not a rollback command.\r\n\r\n" +
            "After showing the files and asking for confirmation, ZomniverseGitPet stages all current\r\n" +
            "non-ignored changes and creates an ordinary LOCAL Git commit.\r\n\r\n" +
            "Think: 'Everything works right now — save this state before the next big change.'\r\n" +
            "ZomniverseGitPet never pushes this commit to GitHub automatically.");
        _toolTips.SetToolTip(recent,
            "Recent Commits\r\n\r\nShow the latest 12 local Git commits, including normal commits and restore-point checkpoints.\r\n" +
            "This is read-only and does not change repository history.");
        _toolTips.SetToolTip(health,
            "Health Check\r\n\r\nRun 'git fsck --no-progress' to check the internal integrity of the Git repository.\r\n" +
            "This is a diagnostic check and does not push, reset, or clean the repository.");
        _toolTips.SetToolTip(cancel,
            "Cancel\r\n\r\nRequest cancellation of the Git, test, or health operation currently running.\r\n" +
            "Nothing happens when there is no active cancellable operation.");

        _files.Dock = DockStyle.Fill;
        _files.ReadOnly = true;
        _files.AllowUserToAddRows = false;
        _files.AllowUserToDeleteRows = false;
        _files.MultiSelect = false;
        _files.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _files.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _files.ShowCellToolTips = true;
        _files.Columns.Add("Status", "Status");
        _files.Columns.Add("Path", "Path");
        _files.Columns[0].FillWeight = 15;
        _files.Columns[1].FillWeight = 85;
        _files.Columns[0].HeaderCell.ToolTipText =
            "Git status code. For example: .M = tracked file modified locally; ?? = new untracked item.";
        _files.Columns[1].HeaderCell.ToolTipText =
            "Path of the changed item. Select a row and choose View Diff, or double-click the row.";
        _files.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0) await ShowDiffAsync(); };

        var options = new Panel { Dock = DockStyle.Bottom, Height = 42 };
        _automatic.Text = "Automatic verified checkpoints (disabled by default)";
        _automatic.AutoSize = true;
        _automatic.Location = new Point(14, 11);
        _automatic.Checked = _config.AutomaticCheckpointsEnabled;
        _toolTips.SetToolTip(_automatic,
            "Automatic verified checkpoints\r\n\r\n" +
            "OFF by default. When enabled, ZomniverseGitPet may create LOCAL checkpoint commits after\r\n" +
            "the configured quiet period when changes are present. If required, configured tests must pass first.\r\n\r\n" +
            "Automatic checkpoints never push to GitHub and never configure remotes.\r\n" +
            "Leave this off if you prefer to create Restore Points manually.");
        _automatic.CheckedChanged += async (_, _) =>
        {
            _config.AutomaticCheckpointsEnabled = _automatic.Checked;
            _configStore.Save(_config);
            await _audit.WriteAsync("automatic_checkpoint_setting_changed", new { enabled = _automatic.Checked });
        };
        options.Controls.Add(_automatic);

        _output.Dock = DockStyle.Bottom;
        _output.Height = 220;
        _output.ReadOnly = true;
        _output.Font = new Font("Consolas", 9);
        _output.BackColor = Color.White;
        _toolTips.SetToolTip(_output,
            "Operation output\r\n\r\nResults from View Diff, Run Tests, Restore Point, Recent Commits, and Health Check appear here.\r\n" +
            "This panel is read-only.");

        Controls.Add(_files);
        Controls.Add(_output);
        Controls.Add(options);
        Controls.Add(buttons);
        Controls.Add(_summary);
        FormClosing += OnFormClosing;
    }

    private static Button MakeButton(string text, Func<Task> action, int width = 88)
    {
        var button = new Button { Text = text, Width = width, Height = 30, Margin = new Padding(3) };
        button.Click += async (_, _) => await action();
        return button;
    }

    public async Task RefreshAsync() => await RunOperationAsync("Refreshing repository status...", async token =>
    {
        if (!HasRepository()) return;
        var statusTask = _git.GetStatusAsync(_config.RepositoryPath!, token);
        var commitTask = _git.GetLastCommitAsync(_config.RepositoryPath!, token);
        _status = await statusTask;
        var commit = await commitTask;
        _summary.Text = _status.Healthy
            ? $"Repository: healthy ✓    Branch: {_status.Branch}    Changed items: {_status.Files.Count}\r\nLast commit: {(commit.Success ? commit.Output : "unavailable")}" 
            : "Repository problem: " + _status.Error;
        _files.Rows.Clear();
        foreach (var file in _status.Files)
        {
            var rowIndex = _files.Rows.Add(file.Status, file.Path);
            var row = _files.Rows[rowIndex];
            row.Cells[0].ToolTipText = DescribeGitStatus(file.Status);
            row.Cells[1].ToolTipText =
                $"{file.Path}\r\n\r\nSelect this row and choose View Diff, or double-click the row, to inspect the change.";
        }
        if (!_status.Healthy) _output.Text = _status.Error;
    });

    private static string DescribeGitStatus(string status) => status switch
    {
        "??" => "?? — Untracked item.\r\n\r\nThis file or folder is new and is not yet tracked by Git.",
        ".M" => ".M — Modified in the working tree.\r\n\r\nThis is an existing tracked file with local unstaged changes.",
        "M." => "M. — Modified and staged.\r\n\r\nThis tracked file has changes already staged in Git's index.",
        ".D" => ".D — Deleted in the working tree.\r\n\r\nThis tracked file has been deleted locally but the deletion is not staged.",
        "D." => "D. — Deletion staged.\r\n\r\nThe deletion of this tracked file is already staged.",
        "A." => "A. — Added and staged.\r\n\r\nThis new item has already been staged for commit.",
        "MM" => "MM — Staged and modified again.\r\n\r\nThe file has staged changes plus additional unstaged changes.",
        _ => $"{status} — Git porcelain status code.\r\n\r\nFor two-character codes, the first position describes the index (staged state) and the second describes the working tree."
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
        if (result.Success && !string.IsNullOrWhiteSpace(result.Output)) _output.Text = result.Output;
        else
        {
            var fullPath = Path.GetFullPath(Path.Combine(_config.RepositoryPath!, path));
            var root = Path.GetFullPath(_config.RepositoryPath!) + Path.DirectorySeparatorChar;
            if (fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) && File.Exists(fullPath))
            {
                var info = new FileInfo(fullPath);
                _output.Text = info.Length <= 150_000
                    ? "UNTRACKED / NO DIFF AVAILABLE\r\n\r\n" + await File.ReadAllTextAsync(fullPath, token)
                    : "Preview skipped because the file is larger than 150 KB.";
            }
            else _output.Text = result.Output.Length == 0 ? "No diff is available." : result.Output;
        }
    });

    private async Task RunTestsAsync() => await RunOperationAsync("Running configured tests...", async token =>
    {
        if (!HasRepository()) return;
        if (_config.TestCommands.Count == 0) { _output.Text = "No test commands are configured in config.json."; return; }
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
            _output.Text = "Restore point blocked because suspicious paths are present:\r\n\r\n" + string.Join("\r\n", suspicious);
            await _audit.WriteAsync("checkpoint_blocked_suspicious_paths", new { files = suspicious });
            return;
        }
        var preview = string.Join("\r\n", _status.Files.Take(20).Select(f => $"{f.Status}  {f.Path}"));
        if (_status.Files.Count > 20) preview += $"\r\n... and {_status.Files.Count - 20} more";
        var answer = MessageBox.Show(this,
            "Create a restore point containing every current non-ignored change?\r\n\r\n" + preview,
            "Create restore point", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (answer != DialogResult.Yes) return;

        var message = $"checkpoint: {DateTime.Now:yyyy-MM-dd HH:mm}";
        var result = await _git.CreateCheckpointAsync(_config.RepositoryPath!, message, token);
        _output.Text = result.Message;
        MessageBox.Show(this, result.Message, "Restore point", MessageBoxButtons.OK,
            result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        await RefreshAsync();
    });

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
        _output.Text = result.Success ? "git fsck passed.\r\n\r\n" + result.Output : "git fsck failed.\r\n\r\n" + result.Output;
        await _audit.WriteAsync("health_check", new { success = result.Success, result.TimedOut });
    });

    private bool HasRepository()
    {
        if (!string.IsNullOrWhiteSpace(_config.RepositoryPath)) return true;
        _output.Text = "Choose a Git repository first.";
        return false;
    }

    private async Task RunOperationAsync(string message, Func<CancellationToken, Task> action)
    {
        if (_operation is not null) return;
        _operation = new CancellationTokenSource();
        foreach (var button in _operationButtons) button.Enabled = false;
        UseWaitCursor = true;
        _output.Text = message;
        try { await action(_operation.Token); }
        catch (OperationCanceledException) { _output.Text = "Operation cancelled."; }
        catch (Exception ex)
        {
            _output.Text = ex.Message;
            await _audit.WriteAsync("operation_error", new { error = ex.Message });
        }
        finally
        {
            UseWaitCursor = false;
            foreach (var button in _operationButtons) button.Enabled = true;
            _operation.Dispose();
            _operation = null;
        }
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
        if (disposing) _toolTips.Dispose();
        base.Dispose(disposing);
    }
}

