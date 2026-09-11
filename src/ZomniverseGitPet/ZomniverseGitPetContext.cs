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
        LogicalProjectScopeRuntime.Initialize(_config);

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
            InstallProjectSetupMenu(_guardian);
            InstallConnectionMenu(_guardian);
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

    private async Task ReconfigureCurrentProjectAsync()
    {
        if (_guardian is { IsDisposed: false } && GuardianOperationInProgress(_guardian))
        {
            MessageBox.Show(
                DialogOwner,
                "Finish or cancel the current Guardian operation before changing Project setup.",
                "Project setup",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var active = _config.GetActiveProject();
        if (active is null || !Directory.Exists(active.RepositoryRoot))
        {
            ShowProjectsMenu();
            return;
        }

        await ReconfigureRepositoryAsync(active.RepositoryRoot);
    }

    private void InstallProjectSetupMenu(GuardianForm guardian)
    {
        var menu = guardian.MainMenuStrip;
        if (menu is null || menu.Items.Cast<ToolStripItem>().Any(item => item.Name == "ProjectSetupMenu")) return;

        var setup = new ToolStripMenuItem("Project setup")
        {
            Name = "ProjectSetupMenu",
            ToolTipText = "Reopen CHOOSE WHAT BELONGS TO THIS PROJECT, then REVIEW REPOSITORY HYGIENE. No git init, commit, pull, or push is performed."
        };
        setup.Click += async (_, _) => await ReconfigureCurrentProjectAsync();
        menu.Items.Insert(0, setup);
    }

    private void InstallConnectionMenu(GuardianForm guardian)
    {
        var menu = guardian.MainMenuStrip;
        if (menu is null || menu.Items.Cast<ToolStripItem>().Any(item => item.Name == "ConnectionMenu")) return;

        var connection = new ToolStripMenuItem("Connection")
        {
            Name = "ConnectionMenu",
            ToolTipText = "Connect your own GitHub account or keep GitPet in Local Git Only mode."
        };
        connection.Click += async (_, _) => await ShowConnectionSetupAsync();
        menu.Items.Insert(Math.Min(1, menu.Items.Count), connection);
    }

    private async Task ShowConnectionSetupAsync()
    {
        if (_guardian is { IsDisposed: false } && GuardianOperationInProgress(_guardian))
        {
            MessageBox.Show(DialogOwner,
                "Finish or cancel the current Guardian operation before changing connection settings.",
                "Connection", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _pet.ShowGuidance("🔑 CONNECTION SETTINGS\nGitHub or local Git");
        using var setup = new FirstRunSetupForm(_config, _configStore, _git, _audit, _pet.ShowGuidance);
        if (setup.ShowDialog(DialogOwner) == DialogResult.OK)
            await RefreshAsync(true);
    }

    private static bool GuardianOperationInProgress(Control root)
    {
        foreach (var control in EnumerateControls(root))
        {
            if (control is GuardianActionButton button &&
                button.Visible &&
                button.Text.Equals("Cancel", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static IEnumerable<Control> EnumerateControls(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var descendant in EnumerateControls(child)) yield return descendant;
        }
    }

    private void ShowProjectsMenu()
    {
        _projectsMenu?.Dispose();
        var menu = new ContextMenuStrip
        {
            ShowImageMargin = false,
            ShowCheckMargin = false,
            BackColor = GuardianTheme.SurfaceRaised,
            ForeColor = GuardianTheme.Ink,
            Font = new Font("Segoe UI", 9.5f),
            Padding = new Padding(4),
            RenderMode = ToolStripRenderMode.Professional,
            Renderer = GuardianTheme.CreateMenuRenderer()
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
                var available = Directory.Exists(recent.Path) && Directory.Exists(recent.RepositoryRoot);
                var active = string.Equals(recent.Id, _config.ActiveProjectId, StringComparison.OrdinalIgnoreCase);
                var label = active ? $"● {recent.DisplayName}" : recent.DisplayName;
                if (!available) label += "  (unavailable)";
                var shared = _config.RecentRepositories.Count(other =>
                    PathEquals(other.RepositoryRoot, recent.RepositoryRoot)) > 1;
                var item = new ToolStripMenuItem(label)
                {
                    Enabled = available,
                    ToolTipText = shared
                        ? $"Project: {recent.Path}\nShared Git repository: {recent.RepositoryRoot}"
                        : recent.Path,
                    Font = active ? new Font(menu.Font, FontStyle.Bold) : menu.Font
                };
                var projectId = recent.Id;
                item.Click += async (_, _) => await ActivateRegisteredProjectAsync(projectId);
                menu.Items.Add(item);
            }
        }

        menu.Items.Add(new ToolStripSeparator());

        var clone = new ToolStripMenuItem("Clone repository to this computer…")
        {
            ToolTipText = "Copy a Git/GitHub repository and its version history to this PC, then add the local copy to GitPet Projects."
        };
        clone.Click += async (_, _) => await CloneRepositoryAsync();
        menu.Items.Add(clone);

        var openFolder = new ToolStripMenuItem("Add / open project folder…")
        {
            ToolTipText = "A folder may become its own GitPet project even when several projects share one Git repository."
        };
        openFolder.Click += async (_, _) => await OpenFolderAsync(false);
        menu.Items.Add(openFolder);

        var prepareFolder = new ToolStripMenuItem("Prepare / reconfigure folder…")
        {
            ToolTipText = "Open the project-scope tree and ignore-rule review. Existing Git repositories are not initialized again."
        };
        prepareFolder.Click += async (_, _) => await OpenFolderAsync(true);
        menu.Items.Add(prepareFolder);

        var activeProject = _config.GetActiveProject();
        if (activeProject is not null && Directory.Exists(activeProject.RepositoryRoot))
        {
            var reconfigure = new ToolStripMenuItem("Reconfigure current project scope…")
            {
                ToolTipText = "Change only this GitPet project's local scope. Sibling projects in the same repository are not hidden or rewritten."
            };
            reconfigure.Click += async (_, _) => await ReconfigureRepositoryAsync(activeProject.RepositoryRoot);
            menu.Items.Add(reconfigure);

            var hygiene = new ToolStripMenuItem("Review .gitignore suggestions only…");
            hygiene.Click += async (_, _) => await ReviewGitIgnoreSuggestionsAsync(activeProject.RepositoryRoot, true);
            menu.Items.Add(hygiene);
        }

        if (recents.Length > 0)
        {
            menu.Items.Add(new ToolStripSeparator());
            var manage = new ToolStripMenuItem("Manage recent projects");
            foreach (var recent in recents)
            {
                var projectId = recent.Id;
                var projectMenu = new ToolStripMenuItem(recent.DisplayName);

                var rename = new ToolStripMenuItem("Rename project…")
                {
                    ToolTipText = "Change only GitPet's display name. The folder and Git repository are not renamed."
                };
                rename.Click += async (_, _) => await RenameProjectAsync(projectId);
                projectMenu.DropDownItems.Add(rename);

                var active = string.Equals(recent.Id, _config.ActiveProjectId, StringComparison.OrdinalIgnoreCase);
                var remove = new ToolStripMenuItem(active ? "Forget current project…" : "Forget project…")
                {
                    ToolTipText = "Forget only this GitPet registration. The folder, .git repository, files, commits, remotes, and GitHub repository are untouched."
                };
                remove.Click += async (_, _) => await ForgetRecentProjectAsync(projectId);
                projectMenu.DropDownItems.Add(remove);
                manage.DropDownItems.Add(projectMenu);
            }

            if (_config.RecentRepositories.Any(item => !Directory.Exists(item.Path) || !Directory.Exists(item.RepositoryRoot)))
            {
                manage.DropDownItems.Add(new ToolStripSeparator());
                var forgetMissing = new ToolStripMenuItem("Forget all unavailable projects");
                forgetMissing.Click += async (_, _) =>
                {
                    var activeBefore = _config.ActiveProjectId;
                    _config.ForgetUnavailableRepositories();
                    _configStore.Save(_config);
                    if (!string.Equals(activeBefore, _config.ActiveProjectId, StringComparison.OrdinalIgnoreCase))
                        await RefreshAsync(true);
                };
                manage.DropDownItems.Add(forgetMissing);
            }
            menu.Items.Add(manage);
        }

        menu.Show(Cursor.Position);
    }

    private async Task CloneRepositoryAsync()
    {
        using var dialog = new CloneRepositoryForm();
        if (dialog.ShowDialog(DialogOwner) != DialogResult.OK) return;

        var address = dialog.Address;
        _pet.ShowGuidance("📥 COPYING REPOSITORY\nI'll preserve its history");
        var result = await _git.CloneRepositoryAsync(address.CloneSource, dialog.DestinationPath, _lifetime.Token);
        if (!result.Success)
        {
            _pet.ShowGuidance("⚠️ CLONE NEEDS HELP\nOpen Guardian for details");
            MessageBox.Show(DialogOwner,
                "GitPet could not copy that repository. No existing files were overwritten.\r\n\r\n" + result.Output,
                "Clone repository", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var root = result.Output.Trim();
        await _audit.WriteAsync("repository_cloned", new
        {
            sourceKind = address.IsGitHub ? "github" : "git",
            repository = root
        });

        _pet.ShowGuidance("✅ REPOSITORY COPIED\nNow choose what I guard");
        await ConfigureProjectAsync(root, root, GetFolderName(root), null, null);
    }

    private async Task OpenFolderAsync(bool preparationRequested)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = preparationRequested
                ? "Choose a folder to prepare or reconfigure. GitPet will show Project Scope first, then Repository Hygiene."
                : "Choose a project folder. A folder inside a larger repository can still be its own GitPet project.",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
            InitialDirectory = _config.GetActiveProject()?.Path ?? _config.RepositoryPath ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
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
                var root = inspection.RepositoryRoot!;
                var existing = _config.FindProjectByPath(inspection.SelectedPath);
                if (!preparationRequested && existing is not null)
                    await ActivateRegisteredProjectAsync(existing.Id);
                else
                    await ConfigureProjectAsync(
                        root,
                        inspection.SelectedPath,
                        existing?.DisplayName ?? GetFolderName(inspection.SelectedPath),
                        existing?.Id,
                        GetConfiguredScope(existing));
                break;
            }
            case ProjectSuitability.NestedRepository:
            {
                if (string.IsNullOrWhiteSpace(inspection.RepositoryRoot)) break;
                var root = inspection.RepositoryRoot;
                var selected = inspection.SelectedPath;
                var existing = _config.FindProjectByPath(selected);
                if (!preparationRequested && existing is not null)
                {
                    await ActivateRegisteredProjectAsync(existing.Id);
                    break;
                }

                using var registration = new ProjectRegistrationForm(
                    selected,
                    root,
                    existing?.DisplayName ?? GetFolderName(selected));
                if (registration.ShowDialog(DialogOwner) != DialogResult.OK) break;

                if (registration.SelectedAction == ProjectRegistrationAction.UseWholeRepository)
                {
                    var rootProject = _config.FindProjectByPath(root);
                    await ConfigureProjectAsync(
                        root,
                        root,
                        rootProject?.DisplayName ?? GetFolderName(root),
                        rootProject?.Id,
                        GetConfiguredScope(rootProject));
                    break;
                }

                if (registration.SelectedAction == ProjectRegistrationAction.AddProject)
                {
                    var initialScope = GetConfiguredScope(existing) ?? BuildDefaultNestedScope(root, selected);
                    await ConfigureProjectAsync(
                        root,
                        selected,
                        registration.ProjectName,
                        existing?.Id,
                        initialScope);
                }
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

        var plan = preparation.ScopePlan ?? ProjectScopePlanner.Create(root, [], trackEverything: true);
        var entry = _config.RememberProject(
            root,
            root,
            preparation.ProjectName,
            plan.TrackEverything,
            plan.Entries);
        _configStore.Save(_config);
        ResetProjectState();

        await _audit.WriteAsync("repository_initialized", new
        {
            repository = root,
            projectId = entry.Id,
            branch = "main",
            gitignoreRulesAdded = addedRules,
            scopeEntries = plan.Entries.Count
        });
        await RefreshAsync(true);

        MessageBox.Show(DialogOwner,
            $"Project ready ✓\r\n\r\nGitPet project: {entry.DisplayName}\r\nLocal repository: {root}\r\n.gitignore rules added: {addedRules}\r\n\r\n" +
            "No remote was created. No files were staged or committed. Nothing was pushed.",
            "Project prepared", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task ReconfigureRepositoryAsync(string repositoryPath)
    {
        var active = _config.GetActiveProject();
        var root = repositoryPath;
        var projectPath = active is not null && PathEquals(active.RepositoryRoot, repositoryPath)
            ? active.Path
            : repositoryPath;
        var displayName = active is not null && PathEquals(active.RepositoryRoot, repositoryPath)
            ? active.DisplayName
            : GetFolderName(projectPath);
        var projectId = active is not null && PathEquals(active.RepositoryRoot, repositoryPath)
            ? active.Id
            : null;
        var initialScope = GetConfiguredScope(active);

        await ConfigureProjectAsync(root, projectPath, displayName, projectId, initialScope);
    }

    private async Task ConfigureProjectAsync(
        string repositoryPath,
        string projectPath,
        string displayName,
        string? projectId,
        IReadOnlyList<ProjectScopeEntry>? initialScope)
    {
        if (!Directory.Exists(repositoryPath) || !Directory.Exists(projectPath)) return;
        var rootResult = await _git.GetRepositoryRootAsync(repositoryPath, _lifetime.Token);
        if (!rootResult.Success || string.IsNullOrWhiteSpace(rootResult.Output))
        {
            MessageBox.Show(DialogOwner,
                "GitPet could not verify this existing repository before project setup. Nothing was changed.\r\n\r\n" + rootResult.Output,
                "Project setup", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootResult.Output.Trim()));
        var normalizedProject = Path.TrimEndingDirectorySeparator(Path.GetFullPath(projectPath));
        var restored = initialScope ?? ProjectGitIgnoreComposer.ReadManagedScope(root);
        if (restored is null && !PathEquals(root, normalizedProject))
            restored = BuildDefaultNestedScope(root, normalizedProject);

        var suggestions = GitIgnoreAdvisor.Suggest(root);
        using var preparation = new ProjectPreparationForm(
            root,
            suggestions,
            initializeGit: false,
            chooseScope: true,
            initialScope: restored,
            projectPath: normalizedProject,
            projectName: displayName);
        if (preparation.ShowDialog(DialogOwner) != DialogResult.OK) return;

        try
        {
            ProjectGitIgnoreComposer.SplitCombinedRules(
                preparation.AcceptedRules,
                out _,
                out var suggestionRules);

            // Any old GitPet managed scope is migrated out of .gitignore. Scope now belongs
            // to the logical project registration, so sibling projects remain visible to Git.
            var gitignoreChanged = ProjectGitIgnoreComposer.ApplyReplacingScope(root, [], suggestionRules);
            var plan = preparation.ScopePlan ?? ProjectScopePlanner.Create(root, [], trackEverything: true);
            var entry = _config.RememberProject(
                normalizedProject,
                root,
                preparation.ProjectName,
                plan.TrackEverything,
                plan.Entries,
                projectId);
            _configStore.Save(_config);
            ResetProjectState();

            await _audit.WriteAsync("logical_project_configured", new
            {
                projectId = entry.Id,
                project = entry.DisplayName,
                projectPath = entry.Path,
                repository = root,
                gitignoreChanged,
                trackEverything = entry.TrackEverything,
                scopeEntries = entry.ScopeEntries.Select(scope => scope.RelativePath).ToArray(),
                suggestionRules
            });
            await RefreshAsync(true);

            var sharedCount = _config.RecentRepositories.Count(item => PathEquals(item.RepositoryRoot, root));
            MessageBox.Show(DialogOwner,
                "GitPet project saved ✓\r\n\r\n" +
                $"Project name: {entry.DisplayName}\r\nProject folder: {entry.Path}\r\nShared repository: {root}\r\n" +
                $"GitPet projects using this repository: {sharedCount}\r\nRoot .gitignore changed: {(gitignoreChanged ? "yes" : "no")}\r\n\r\n" +
                "Save is limited to this project's scope. Get, Send and Reconcile still use the shared repository history. " +
                "No commit, pull, push, or nested repository was created by project setup.",
                "Project setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(DialogOwner,
                "GitPet could not apply the approved project registration/.gitignore changes. No commit, pull, or push was attempted.\r\n\r\n" + ex.Message,
                "Project setup", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task ReviewGitIgnoreSuggestionsAsync(string repositoryPath, bool showWhenEmpty)
    {
        if (!Directory.Exists(repositoryPath)) return;
        var suggestions = GitIgnoreAdvisor.Suggest(repositoryPath);
        if (suggestions.Count == 0)
        {
            if (showWhenEmpty)
                MessageBox.Show(DialogOwner,
                    "GitPet did not find any new common .gitignore candidates in the sampled project contents.\r\n\r\n" +
                    "Use Project setup if you want the full folder tree, ignore library, and custom rule builder.",
                    "Repository hygiene", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
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

    private async Task RenameProjectAsync(string projectId)
    {
        var project = _config.FindProject(projectId);
        if (project is null) return;

        using var dialog = new ProjectRegistrationForm(
            project.Path,
            project.RepositoryRoot,
            project.DisplayName,
            renameOnly: true);
        if (dialog.ShowDialog(DialogOwner) != DialogResult.OK ||
            dialog.SelectedAction != ProjectRegistrationAction.Rename) return;

        if (!_config.RenameProject(projectId, dialog.ProjectName)) return;
        _configStore.Save(_config);
        await _audit.WriteAsync("logical_project_renamed", new
        {
            projectId,
            project = dialog.ProjectName,
            repository = project.RepositoryRoot
        });
        await RefreshAsync(true);
    }

    private async Task ForgetRecentProjectAsync(string projectId)
    {
        var recent = _config.FindProject(projectId);
        if (recent is null) return;
        var wasActive = string.Equals(projectId, _config.ActiveProjectId, StringComparison.OrdinalIgnoreCase);

        var answer = MessageBox.Show(DialogOwner,
            $"Forget '{recent.DisplayName}' from GitPet?\r\n\r\n" +
            "This removes only this GitPet project registration, its local scope, and its saved local test-command profile.\r\n\r\n" +
            "It does NOT delete the folder, remove .git, change .gitignore, erase commits, remove remotes, or delete anything from GitHub. " +
            "Other GitPet projects that share the same repository are untouched.",
            "Forget recent project", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (answer != DialogResult.Yes) return;

        if (!_config.ForgetProject(projectId)) return;
        _configStore.Save(_config);
        ResetProjectState();
        await _audit.WriteAsync("logical_project_forgotten", new
        {
            projectId,
            project = recent.DisplayName,
            repository = recent.RepositoryRoot,
            wasActive
        });

        if (wasActive) await RefreshAsync(true);
    }

    private async Task ActivateRegisteredProjectAsync(string projectId)
    {
        var project = _config.FindProject(projectId);
        if (project is null || !Directory.Exists(project.Path) || !Directory.Exists(project.RepositoryRoot)) return;

        var rootResult = await _git.GetRepositoryRootAsync(project.Path, _lifetime.Token);
        if (!rootResult.Success || string.IsNullOrWhiteSpace(rootResult.Output))
        {
            MessageBox.Show(DialogOwner,
                "That project is no longer inside a readable Git repository.\r\n\r\n" + rootResult.Output,
                "Open project", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var actualRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootResult.Output.Trim()));
        if (!PathEquals(actualRoot, project.RepositoryRoot))
        {
            MessageBox.Show(DialogOwner,
                "This project's Git repository root changed since it was registered.\r\n\r\n" +
                $"Saved root: {project.RepositoryRoot}\r\nCurrent root: {actualRoot}\r\n\r\n" +
                "Forget and add the project again so GitPet can rebuild its scope safely.",
                "Project root changed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _config.ActivateProject(projectId);
        _configStore.Save(_config);
        ResetProjectState();
        await _audit.WriteAsync("logical_project_selected", new
        {
            projectId,
            project = project.DisplayName,
            projectPath = project.Path,
            repository = project.RepositoryRoot
        });
        await RefreshAsync(true);
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
        var existing = _config.FindProjectByPath(path) ?? _config.FindProjectByPath(root);
        if (existing is not null)
        {
            await ActivateRegisteredProjectAsync(existing.Id);
            return;
        }

        var entry = _config.RememberProject(root, root, GetFolderName(root), true, []);
        _configStore.Save(_config);
        ResetProjectState();
        await _audit.WriteAsync("logical_project_selected", new
        {
            projectId = entry.Id,
            project = entry.DisplayName,
            repository = root
        });
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
                if ((refreshGuardian || _guardian is { Visible: true }) && _guardian is { IsDisposed: false })
                    await _guardian.RefreshAsync();
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

    private static IReadOnlyList<ProjectScopeEntry>? GetConfiguredScope(RecentRepositoryEntry? project)
    {
        if (project is null || project.TrackEverything) return null;
        var entries = (project.ScopeEntries ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.RelativePath))
            .Select(item => new ProjectScopeEntry(item.RelativePath, item.IsDirectory))
            .ToArray();
        return entries.Length == 0 ? null : entries;
    }

    private static IReadOnlyList<ProjectScopeEntry> BuildDefaultNestedScope(string repositoryRoot, string projectPath)
    {
        var relative = LogicalProjectScopeRuntime.TryGetRelativePath(repositoryRoot, projectPath);
        return string.IsNullOrWhiteSpace(relative)
            ? []
            : [new ProjectScopeEntry(relative, IsDirectory: true)];
    }

    private void ResetProjectState()
    {
        _statusFingerprint = "";
        _lastAutomaticFingerprint = "";
        _lastChangeAt = DateTimeOffset.UtcNow;
    }

    private static string GetFolderName(string path)
    {
        var normalized = Path.TrimEndingDirectorySeparator(path);
        var name = Path.GetFileName(normalized);
        return string.IsNullOrWhiteSpace(name) ? normalized : name;
    }

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
