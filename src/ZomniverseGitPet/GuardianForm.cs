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

        _files.Dock = DockStyle.Fill;
        _files.ReadOnly = true;
        _files.AllowUserToAddRows = false;
        _files.AllowUserToDeleteRows = false;
        _files.MultiSelect = false;
        _files.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _files.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _files.Columns.Add("Status", "Status");
        _files.Columns.Add("Path", "Path");
        _files.Columns[0].FillWeight = 15;
        _files.Columns[1].FillWeight = 85;
        _files.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0) await ShowDiffAsync(); };

        var options = new Panel { Dock = DockStyle.Bottom, Height = 42 };
        _automatic.Text = "Automatic verified checkpoints (disabled by default)";
        _automatic.AutoSize = true;
        _automatic.Location = new Point(14, 11);
        _automatic.Checked = _config.AutomaticCheckpointsEnabled;
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
        foreach (var file in _status.Files) _files.Rows.Add(file.Status, file.Path);
        if (!_status.Healthy) _output.Text = _status.Error;
    });

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
}

