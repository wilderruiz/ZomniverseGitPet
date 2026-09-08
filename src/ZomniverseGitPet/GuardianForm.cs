using System.Diagnostics;

namespace ZomniverseGitPet;

public sealed class GuardianForm : Form
{
    private static readonly Color Ink = Color.FromArgb(38, 25, 61);
    private static readonly Color Purple = Color.FromArgb(91, 58, 145);
    private static readonly Color PurpleHover = Color.FromArgb(115, 76, 175);
    private static readonly Color LavenderSurface = Color.FromArgb(247, 244, 251);
    private static readonly Color DarkSurface = Color.FromArgb(31, 24, 46);
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
    private bool _refreshInProgress;
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
        MinimumSize = new Size(900, 650);
        Size = new Size(1120, 800);
        BackColor = LavenderSurface;
        Font = new Font("Segoe UI", 9);

        var summaryPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 92,
            Padding = new Padding(18, 12, 18, 10),
            BackColor = Color.FromArgb(55, 37, 84)
        };
        _summary.Dock = DockStyle.Fill;
        _summary.Padding = new Padding(0);
        _summary.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        _summary.ForeColor = Color.White;
        _summary.TextAlign = ContentAlignment.MiddleLeft;
        _summary.AutoEllipsis = true;
        _summary.Text = "Checking repository...";
        summaryPanel.Controls.Add(_summary);
        _toolTips.SetToolTip(_summary,
            "Repository summary: current Git health, branch, changed-item count, and latest commit.\r\n\r\n" +
            "Closing this Guardian window with X only hides it; ZomniverseGitPet keeps running.\r\n" +
            "Use the pet/tray Exit command when you actually want to quit the application.");

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, Height = 82, Padding = new Padding(12, 10, 8, 8),
            WrapContents = true, AutoScroll = true, BackColor = Color.FromArgb(234, 226, 246)
        };
        var choose = MakeButton("Projects ▾", async () => await _chooseRepository(), 112);
        var refresh = MakeButton("Refresh", RefreshAsync, 96);
        var diff = MakeButton("View Diff", ShowDiffAsync, 100);
        var tests = MakeButton("Run Tests", RunTestsAsync, 104);
        var checkpoint = MakeButton("Restore Point", CreateCheckpointAsync, 124);
        var push = MakePushButton("Push ↑", PushToOriginAsync, 92);
        var recent = MakeButton("Recent Commits", RecentCommitsAsync, 126);
        var health = MakeButton("Health Check", HealthCheckAsync, 112);
        var cancel = MakeButton("Cancel", () => { _operation?.Cancel(); return Task.CompletedTask; }, 88);
        buttons.Controls.AddRange([choose, refresh, diff, tests, checkpoint, push, recent, health, cancel]);
        _operationButtons = [choose, refresh, diff, tests, checkpoint, push, recent, health];

        _toolTips.SetToolTip(choose,
            "Projects\r\n\r\nSwitch between recent projects, open another folder, prepare a normal folder for Git,\r\n" +
            "or review what Git should ignore. GitPet remembers up to 20 recent projects.");
        _toolTips.SetToolTip(refresh,
            "Refresh\r\n\r\nRe-read the repository's current branch and working-tree status.\r\n" +
            "Background monitoring also refreshes quietly without taking over your mouse cursor.");
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
            "If Git does not yet know your author name/email, GitPet will ask for them first.\r\n" +
            "ZomniverseGitPet never pushes this commit automatically.");
        _toolTips.SetToolTip(push,
            "Push ↑ — MANUAL ONLY\r\n\r\nPush committed history to the existing 'origin' remote on the CURRENT branch.\r\n" +
            "For this repository on main, this is equivalent to: git push origin main\r\n\r\n" +
            "You will see the remote, branch, and latest commit and must confirm before the push runs.\r\n" +
            "Uncommitted changes are never included. ZomniverseGitPet will not stage, commit, create/configure\r\n" +
            "a remote, or push automatically.");
        _toolTips.SetToolTip(recent,
            "Recent Commits\r\n\r\nShow the latest 12 local Git commits, including normal commits and restore-point checkpoints.\r\n" +
            "This is read-only and does not change repository history.");
        _toolTips.SetToolTip(health,
            "Health Check\r\n\r\nRun 'git fsck --no-progress' to check the internal integrity of the Git repository.\r\n" +
            "This is a diagnostic check and does not push, reset, or clean the repository.");
        _toolTips.SetToolTip(cancel,
            "Cancel\r\n\r\nRequest cancellation of the Git, test, push, or health operation currently running.\r\n" +
            "Nothing happens when there is no active cancellable operation.");

        _files.Dock = DockStyle.Fill;
        _files.ReadOnly = true;
        _files.AllowUserToAddRows = false;
        _files.AllowUserToDeleteRows = false;
        _files.MultiSelect = false;
        _files.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _files.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _files.BackgroundColor = Color.FromArgb(241, 237, 247);
        _files.BorderStyle = BorderStyle.None;
        _files.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _files.GridColor = Color.FromArgb(222, 213, 235);
        _files.RowHeadersVisible = false;
        _files.EnableHeadersVisualStyles = false;
        _files.ColumnHeadersHeight = 38;
        _files.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(78, 51, 116);
        _files.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        _files.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
        _files.DefaultCellStyle.BackColor = Color.White;
        _files.DefaultCellStyle.ForeColor = Ink;
        _files.DefaultCellStyle.SelectionBackColor = Color.FromArgb(220, 205, 242);
        _files.DefaultCellStyle.SelectionForeColor = Ink;
        _files.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(249, 247, 252);
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

        var options = new Panel { Dock = DockStyle.Bottom, Height = 48, BackColor = Color.FromArgb(234, 226, 246) };
        _automatic.Text = "Automatic verified checkpoints (disabled by default)";
        _automatic.AutoSize = true;
        _automatic.Location = new Point(16, 14);
        _automatic.ForeColor = Ink;
        _automatic.Checked = _config.AutomaticCheckpointsEnabled;
        _toolTips.SetToolTip(_automatic,
            "Automatic verified checkpoints\r\n\r\n" +
            "OFF by default. When enabled, ZomniverseGitPet may create LOCAL checkpoint commits after\r\n" +
            "the configured quiet period when changes are present. If required, configured tests must pass first.\r\n\r\n" +
            "Automatic checkpoints never push to a remote and never configure remotes.\r\n" +
            "Leave this off if you prefer to create Restore Points manually.");
        _automatic.CheckedChanged += async (_, _) =>
        {
            _config.AutomaticCheckpointsEnabled = _automatic.Checked;
            _configStore.Save(_config);
            await _audit.WriteAsync("automatic_checkpoint_setting_changed", new { enabled = _automatic.Checked });
        };
        options.Controls.Add(_automatic);

        _output.Dock = DockStyle.Fill;
        _output.ReadOnly = true;
        _output.Font = new Font("Cascadia Mono", 9.5f);
        _output.BackColor = DarkSurface;
        _output.ForeColor = Color.FromArgb(235, 228, 248);
        _output.BorderStyle = BorderStyle.None;
        _output.Padding = new Padding(10);
        _toolTips.SetToolTip(_output,
            "Operation output\r\n\r\nResults from View Diff, Run Tests, Restore Point, Push, Recent Commits, and Health Check appear here.\r\n" +
            "This panel is read-only.");

        var content = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 8,
            Size = new Size(1000, 600),
            SplitterDistance = 360,
            Panel1MinSize = 140,
            Panel2MinSize = 120,
            BackColor = Color.FromArgb(173, 151, 204),
            BorderStyle = BorderStyle.None
        };
        content.Panel1.Padding = new Padding(12, 10, 12, 4);
        content.Panel2.Padding = new Padding(12, 4, 12, 10);
        content.Panel1.BackColor = LavenderSurface;
        content.Panel2.BackColor = LavenderSurface;
        content.Panel1.Controls.Add(_files);
        content.Panel2.Controls.Add(_output);

        Controls.Add(content);
        Controls.Add(options);
        Controls.Add(buttons);
        Controls.Add(summaryPanel);
        FormClosing += OnFormClosing;
    }

    private static Button MakeButton(string text, Func<Task> action, int width = 88)
    {
        var button = new Button
        {
            Text = text, Width = width, Height = 34, Margin = new Padding(4),
            FlatStyle = FlatStyle.Flat, BackColor = Purple, ForeColor = Color.White,
            Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = PurpleHover;
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(70, 42, 108);
        button.Click += async (_, _) => await action();
        return button;
    }

    private static Button MakePushButton(string text, Func<Task> action, int width)
    {
        var button = new GuardianPushButton { Text = text, Width = width, Height = 34, Margin = new Padding(4) };
        button.Click += async (_, _) => await action();
        return button;
    }

    public async Task RefreshAsync()
    {
        if (_refreshInProgress || IsDisposed || _operation is not null) return;
        if (string.IsNullOrWhiteSpace(_config.RepositoryPath))
        {
            _summary.Text = "No project selected — open Projects to choose or prepare a folder.";
            return;
        }

        _refreshInProgress = true;
        try
        {
            await RefreshRepositoryViewAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _summary.Text = "Repository refresh problem: " + ex.Message;
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
        _summary.Text = _status.Healthy
            ? $"Repository healthy ✓     Branch: {_status.Branch}     Changed items: {_status.Files.Count}\r\nLatest commit: {FormatCommitPreview(commit)}"
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
    }

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

        if (!await EnsureGitIdentityAsync(token)) return;

        var message = $"checkpoint: {DateTime.Now:yyyy-MM-dd HH:mm}";
        var result = await _git.CreateCheckpointAsync(_config.RepositoryPath!, message, token);
        _output.Text = result.Message;
        MessageBox.Show(this, result.Message, "Restore point", MessageBoxButtons.OK,
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
        if (!string.IsNullOrWhiteSpace(currentName) && !string.IsNullOrWhiteSpace(currentEmail)) return true;

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
            MessageBox.Show(this,
                "GitPet could not save the Git identity. No restore-point commit was created.\r\n\r\n" + save.Output,
                "Git identity", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        _output.Text = identity.UseGlobal
            ? "Git identity saved for Git projects on this PC. Creating restore point..."
            : "Git identity saved for this project. Creating restore point...";
        return true;
    }

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
                "Push unavailable: this repository does not have a readable 'origin' remote.\r\n\r\n" +
                "ZomniverseGitPet will not create or configure remotes automatically.";
            return;
        }

        var commit = await _git.GetLastCommitAsync(repositoryPath, token);
        var status = await _git.GetStatusAsync(repositoryPath, token);
        var uncommittedCount = status.Healthy ? status.Files.Count : 0;
        var uncommittedNote = uncommittedCount > 0
            ? $"\r\n\r\nUncommitted changes: {uncommittedCount}\r\nThese changes will remain local and will NOT be included in this push."
            : "";
        var commitPreview = FormatCommitPreview(commit);

        var answer = MessageBox.Show(this,
            $"Push committed history to the configured origin remote?\r\n\r\n" +
            $"Remote: origin\r\nBranch: {branch}\r\nCommit: {commitPreview}" +
            uncommittedNote +
            $"\r\n\r\nCommand:\r\ngit push origin {branch}\r\n\r\n" +
            "ZomniverseGitPet will not stage, commit, create/configure a remote, or push automatically.",
            "Confirm manual push", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (answer != DialogResult.Yes)
        {
            _output.Text = "Push cancelled. Nothing was sent to the remote.";
            return;
        }

        _output.Text = $"Pushing committed history to origin/{branch}...";
        var result = await _git.PushToOriginAsync(repositoryPath, branch, token);
        var details = string.IsNullOrWhiteSpace(result.Output) ? "Git reported success." : result.Output;
        _output.Text = result.Success
            ? $"Push completed ✓\r\norigin/{branch}\r\n\r\n{details}"
            : $"Push failed.\r\norigin/{branch}\r\n\r\n{details}";

        MessageBox.Show(this,
            result.Success ? $"Push completed successfully.\r\n\r\norigin/{branch}" : "Push failed. See the Guardian output panel for details.",
            "Push to origin", MessageBoxButtons.OK,
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
        _output.Text = result.Success ? "git fsck passed.\r\n\r\n" + result.Output : "git fsck failed.\r\n\r\n" + result.Output;
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
