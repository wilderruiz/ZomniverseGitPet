using System.IO.Pipes;

namespace ZomniverseGitPet;

public sealed class ZomniverseGitPetContext : ApplicationContext
{
    private readonly AppConfig _config;
    private readonly ConfigStore _configStore;
    private readonly GitService _git;
    private readonly AuditLog _audit;
    private readonly ProjectInspector _projectInspector;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly System.Windows.Forms.Timer _timer;
    private readonly PetForm _pet;
    private GuardianForm? _guardian;
    private ContextMenuStrip? _projectsMenu;
    private string _statusFingerprint = "";
    private string _lastAutomaticFingerprint = "";
    private DateTimeOffset _lastChangeAt = DateTimeOffset.UtcNow;
    private bool _automaticCheckpointRunning;

    public ZomniverseGitPetContext(string pipeName, AppConfig config, ConfigStore configStore, GitService git, AuditLog audit)
    {
        _config = config;
        _configStore = configStore;
        _git = git;
        _audit = audit;
        _projectInspector = new ProjectInspector(git);
        _pet = new PetForm(ShowGuardian, ChooseRepositoryAsync, ExitApplication);
        MainForm = _pet;
        _pet.Show();

        _timer = new System.Windows.Forms.Timer { Interval = Math.Max(5, config.PollSeconds) * 1000 };
        _timer.Tick += async (_, _) => await RefreshAsync(false);
        _timer.Start();

        _ = ListenForActivationAsync(pipeName, _lifetime.Token);
        _ = _audit.WriteAsync("app_started");
        _ = RefreshAsync(false);
    }

    private void ShowGuardian()
    {
        if (_guardian is null || _guardian.IsDisposed)
        {
            _guardian = new GuardianForm(_config, _configStore, _git, _audit, ChooseRepositoryAsync);
            _guardian.FormClosed += (_, _) => _guardian = null;
        }
        _guardian.Show();
        if (_guardian.WindowState == FormWindowState.Minimized) _guardian.WindowState = FormWindowState.Normal;
        _guardian.Activate();
        _ = _guardian.RefreshAsync();
    }

    private Task ChooseRepositoryAsync()
    {
        ShowProjectsMenu();
        return Task.CompletedTask;
    }

    private void ShowProjectsMenu()
    {
        _projectsMenu?.Dispose();
        var menu = new ContextMenuStrip
        {
            ShowImageMargin = false,
            Font = new Font("Segoe UI", 9)
        };
        _projectsMenu = menu;

        var heading = new ToolStripMenuItem("Recent projects") { Enabled = false };
        menu.Items.Add(heading);

        var recents = _config.RecentRepositories
            .OrderByDescending(item => item.LastOpenedUtc)
            .Take(AppConfig.RecentRepositoryLimit)
            .ToArray();
        if (recents.Length == 0)
        {
            menu.Items.Add(new ToolStripMenuItem("No recent projects yet") { Enabled = false });
        }
        else
        {
            foreach (var recent in recents)
            {
                var available = Directory.Exists(recent.Path);
                var active = PathEquals(recent.Path, _config.RepositoryPath);
                var label = active ? $"● {recent.DisplayName}" : recent.DisplayName;
                if (!available) label += "  (unavailable)";
                var item = new ToolStripMenuItem(label)
                {
                    Enabled = available,
                    ToolTipText = recent.Path,
                    Font = active ? new Font(menu.Font, FontStyle.Bold) : menu.Font
                };
                item.Click += async (_, _) => await ActivateRegisteredRepositoryAsync(recent.Path);
                menu.Items.Add(item);
            }
        }

        menu.Items.Add(new ToolStripSeparator());
        var openFolder = new ToolStripMenuItem("Open folder…");
        openFolder.Click += async (_, _) => await OpenFolderAsync(false);
        menu.Items.Add(openFolder);

        var prepareFolder = new ToolStripMenuItem("Prepare folder for Git…");
        prepareFolder.Click += async (_, _) => await OpenFolderAsync(true);
        menu.Items.Add(prepareFolder);

        if (!string.IsNullOrWhiteSpace(_config.RepositoryPath) && Directory.Exists(_config.RepositoryPath))
        {
            var hygiene = new ToolStripMenuItem("Review .gitignore suggestions…");
            hygiene.Click += async (_, _) => await ReviewGitIgnoreSuggestionsAsync(_config.RepositoryPath!, true);
            menu.Items.Add(hygiene);
        }

        if (_config.RecentRepositories.Any(item => !Directory.Exists(item.Path)))
        {
            menu.Items.Add(new ToolStripSeparator());
            var forgetMissing = new ToolStripMenuItem("Forget unavailable projects");
            forgetMissing.Click += (_, _) =>
            {
                _config.ForgetUnavailableRepositories();
                _configStore.Save(_config);
            };
            menu.Items.Add(forgetMissing);
        }

        menu.Show(Cursor.Position);
    }

    private async Task OpenFolderAsync(bool preparationRequested)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = preparationRequested
                ? "Choose any folder ZomniverseGitPet should inspect and prepare for local Git"
                : "Choose a project folder for ZomniverseGitPet",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
            InitialDirectory = _config.RepositoryPath ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };
        if (dialog.ShowDialog(DialogOwner) != DialogResult.OK) return;

        var inspection = await _projectInspector.InspectAsync(dialog.SelectedPath, _lifetime.Token);
        await HandleInspectionAsync(inspection, preparationRequested);
    }

    private async Task HandleInspectionAsync(ProjectInspection inspection, bool preparationRequested)
    {
        switch (inspection.Suitability)
        {
            case ProjectSuitability.Ready:
            {
                if (preparationRequested)
                {
                    var answer = MessageBox.Show(DialogOwner,
                        "This folder is already a valid Git repository. Open it in ZomniverseGitPet instead?",
                        "Project already ready", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                    if (answer != DialogResult.Yes) return;
                }
                var root = inspection.RepositoryRoot!;
                var wasKnown = IsRecentRepository(root);
                await ActivateRepositoryAsync(root);
                if (!wasKnown) await ReviewGitIgnoreSuggestionsAsync(root, false);
                break;
            }
            case ProjectSuitability.NestedRepository:
            {
                var answer = MessageBox.Show(DialogOwner,
                    inspection.Message + "\r\n\r\nUse the existing repository root instead?\r\n\r\n" +
                    "ZomniverseGitPet will not create a nested repository automatically.",
                    "Existing parent repository found", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (answer == DialogResult.Yes && !string.IsNullOrWhiteSpace(inspection.RepositoryRoot))
                    await ActivateRepositoryAsync(inspection.RepositoryRoot);
                break;
            }
            case ProjectSuitability.CanPrepare:
                await PrepareRepositoryAsync(inspection);
                break;
            case ProjectSuitability.InvalidRepository:
            case ProjectSuitability.GitUnavailable:
            case ProjectSuitability.Unavailable:
                MessageBox.Show(DialogOwner, inspection.Message, "Project inspection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                break;
        }
    }

    private async Task PrepareRepositoryAsync(ProjectInspection inspection)
    {
        using var preparation = new ProjectPreparationForm(
            inspection.SelectedPath, inspection.IgnoreSuggestions, initializeGit: true);
        if (preparation.ShowDialog(DialogOwner) != DialogResult.OK) return;

        var initialize = await _git.InitializeRepositoryAsync(inspection.SelectedPath, _lifetime.Token);
        if (!initialize.Success)
        {
            MessageBox.Show(DialogOwner,
                "Git initialization failed. No remote, commit, or push was attempted.\r\n\r\n" + initialize.Output,
                "Prepare project for Git", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var verify = await _git.GetRepositoryRootAsync(inspection.SelectedPath, _lifetime.Token);
        if (!verify.Success || string.IsNullOrWhiteSpace(verify.Output))
        {
            MessageBox.Show(DialogOwner,
                "Git reported initialization, but ZomniverseGitPet could not verify the repository root.\r\n\r\n" + verify.Output,
                "Prepare project for Git", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(verify.Output.Trim()));
        var addedRules = 0;
        try
        {
            addedRules = GitIgnoreAdvisor.AppendAcceptedRules(root, preparation.AcceptedRules);
        }
        catch (Exception ex)
        {
            MessageBox.Show(DialogOwner,
                "The Git repository was initialized successfully, but the selected .gitignore rules could not be written.\r\n\r\n" + ex.Message,
                ".gitignore update", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        await _audit.WriteAsync("repository_initialized", new { repository = root, branch = "main", gitignoreRulesAdded = addedRules });
        await ActivateRepositoryAsync(root);

        MessageBox.Show(DialogOwner,
            $"Project ready ✓\r\n\r\nLocal repository: {root}\r\n.gitignore rules added: {addedRules}\r\n\r\n" +
            "No remote was created. No files were staged or committed. Nothing was pushed.",
            "Project prepared", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task ReviewGitIgnoreSuggestionsAsync(string repositoryPath, bool showWhenEmpty)
    {
        if (!Directory.Exists(repositoryPath)) return;
        var suggestions = GitIgnoreAdvisor.Suggest(repositoryPath);
        if (suggestions.Count == 0)
        {
            if (showWhenEmpty)
                MessageBox.Show(DialogOwner,
                    "GitPet did not find any new common .gitignore candidates in the sampled project contents.",
                    "Repository hygiene", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!showWhenEmpty)
        {
            var review = MessageBox.Show(DialogOwner,
                $"GitPet found {suggestions.Count} possible .gitignore rule(s). Review them now?\r\n\r\n" +
                "Nothing will be added unless you explicitly select and accept the suggestions.",
                "Repository hygiene", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (review != DialogResult.Yes) return;
        }

        using var form = new ProjectPreparationForm(repositoryPath, suggestions, initializeGit: false);
        if (form.ShowDialog(DialogOwner) != DialogResult.OK) return;

        try
        {
            var added = GitIgnoreAdvisor.AppendAcceptedRules(repositoryPath, form.AcceptedRules);
            await _audit.WriteAsync("gitignore_rules_added", new { repository = repositoryPath, count = added, rules = form.AcceptedRules });
            MessageBox.Show(DialogOwner,
                added == 0 ? "No new .gitignore rules were added." : $"Added {added} selected rule(s) to .gitignore. Existing content was preserved.",
                "Repository hygiene", MessageBoxButtons.OK, MessageBoxIcon.Information);
            await RefreshAsync(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(DialogOwner, "The .gitignore file could not be updated.\r\n\r\n" + ex.Message,
                "Repository hygiene", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task ActivateRegisteredRepositoryAsync(string path)
    {
        if (!Directory.Exists(path)) return;
        var inspection = await _projectInspector.InspectAsync(path, _lifetime.Token);
        if (inspection.Suitability == ProjectSuitability.Ready && !string.IsNullOrWhiteSpace(inspection.RepositoryRoot))
            await ActivateRepositoryAsync(inspection.RepositoryRoot);
        else
            await HandleInspectionAsync(inspection, false);
    }

    private async Task ActivateRepositoryAsync(string path)
    {
        var rootResult = await _git.GetRepositoryRootAsync(path, _lifetime.Token);
        if (!rootResult.Success || string.IsNullOrWhiteSpace(rootResult.Output))
        {
            MessageBox.Show(DialogOwner, "That project is no longer a readable Git repository.\r\n\r\n" + rootResult.Output,
                "Open project", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootResult.Output.Trim()));
        _config.RememberRepository(root);
        _configStore.Save(_config);
        _statusFingerprint = "";
        _lastAutomaticFingerprint = "";
        _lastChangeAt = DateTimeOffset.UtcNow;
        await _audit.WriteAsync("repository_selected", new { repository = root });
        await RefreshAsync(true);
    }

    private async Task RefreshAsync(bool refreshGuardian)
    {
        if (_pet.RefreshInProgress) return;
        _pet.RefreshInProgress = true;
        try
        {
            if (string.IsNullOrWhiteSpace(_config.RepositoryPath))
            {
                _pet.SetNeedsRepository();
                return;
            }
            var status = await _git.GetStatusAsync(_config.RepositoryPath, _lifetime.Token);
            _pet.SetStatus(status);
            await ConsiderAutomaticCheckpointAsync(status);
            if ((refreshGuardian || _guardian is { Visible: true }) && _guardian is { IsDisposed: false })
                await _guardian.RefreshAsync();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _pet.SetError(ex.Message);
            await _audit.WriteAsync("refresh_error", new { error = ex.Message });
        }
        finally
        {
            _pet.RefreshInProgress = false;
        }
    }

    private async Task ConsiderAutomaticCheckpointAsync(RepositoryStatus status)
    {
        if (!status.Healthy) return;
        var fingerprint = string.Join("\n", status.Files.Select(file => $"{file.Status}\t{file.Path}"));
        if (!string.Equals(fingerprint, _statusFingerprint, StringComparison.Ordinal))
        {
            _statusFingerprint = fingerprint;
            _lastChangeAt = DateTimeOffset.UtcNow;
            return;
        }
        if (!_config.AutomaticCheckpointsEnabled || _automaticCheckpointRunning || status.Files.Count == 0 ||
            fingerprint == _lastAutomaticFingerprint ||
            DateTimeOffset.UtcNow - _lastChangeAt < TimeSpan.FromMinutes(Math.Max(1, _config.QuietMinutes))) return;

        var suspicious = GitService.FindSuspiciousPaths(status.Files, _config.SuspiciousPathPatterns);
        if (suspicious.Count > 0)
        {
            await _audit.WriteAsync("automatic_checkpoint_blocked_suspicious_paths", new { files = suspicious });
            return;
        }
        if (_config.RequireTestsForAutomaticCheckpoint)
        {
            var commands = _config.GetTestCommandsForRepository(_config.RepositoryPath!);
            if (commands.Count == 0) return;
            foreach (var command in commands)
            {
                var test = await _git.RunTestCommandAsync(_config.RepositoryPath!, command, _lifetime.Token);
                if (!test.Success) return;
            }
        }

        _automaticCheckpointRunning = true;
        try
        {
            var result = await _git.CreateCheckpointAsync(_config.RepositoryPath!,
                $"auto-checkpoint: {DateTime.Now:yyyy-MM-dd HH:mm}", _lifetime.Token);
            if (result.Success) _lastAutomaticFingerprint = fingerprint;
        }
        finally
        {
            _automaticCheckpointRunning = false;
        }
    }

    private async Task ListenForActivationAsync(string pipeName, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(pipeName, PipeDirection.In, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(token);
                _pet.BeginInvoke(ShowGuardian);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { await _audit.WriteAsync("activation_listener_error", new { error = ex.Message }); }
        }
    }

    private void ExitApplication()
    {
        _timer.Stop();
        _lifetime.Cancel();
        _projectsMenu?.Dispose();
        _guardian?.CloseForExit();
        _pet.AllowClose = true;
        _pet.Close();
        _ = _audit.WriteAsync("app_exited");
        ExitThread();
    }

    private IWin32Window DialogOwner => _guardian is { Visible: true, IsDisposed: false } ? _guardian : _pet;

    private bool IsRecentRepository(string path) =>
        _config.RecentRepositories.Any(item => PathEquals(item.Path, path));

    private static bool PathEquals(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
        try
        {
            left = Path.TrimEndingDirectorySeparator(Path.GetFullPath(left));
            right = Path.TrimEndingDirectorySeparator(Path.GetFullPath(right));
        }
        catch { }
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _lifetime.Cancel();
            _lifetime.Dispose();
            _projectsMenu?.Dispose();
            _guardian?.Dispose();
            _pet.Dispose();
        }
        base.Dispose(disposing);
    }
}
