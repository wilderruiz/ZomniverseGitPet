using System.Diagnostics;
using System.IO.Pipes;

namespace ZomniverseGitPet;

public sealed class ZomniverseGitPetContext : ApplicationContext
{
    private readonly ApplicationIdentityInfo _identity;
    private readonly AppConfig _config;
    private readonly ConfigStore _configStore;
    private readonly GitService _git;
    private readonly AuditLog _audit;
    private readonly ProjectInspector _projectInspector;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly CancellationToken _lifetimeToken;
    private readonly SemaphoreSlim _projectSwitchGate = new(1, 1);
    private int _disposeState;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly PetForm _pet;
    private GuardianForm? _guardian;
    private ContextMenuStrip? _projectsMenu;
    private string _statusFingerprint = "";
    private string _lastAutomaticFingerprint = "";
    private DateTimeOffset _lastChangeAt = DateTimeOffset.UtcNow;
    private bool _automaticCheckpointRunning;

    internal ZomniverseGitPetContext(
        string pipeName,
        ApplicationIdentityInfo identity,
        AppConfig config,
        ConfigStore configStore,
        GitService git,
        AuditLog audit)
    {
        _identity = identity;
        _config = config;
        _configStore = configStore;
        _git = git;
        _audit = audit;
        _projectInspector = new ProjectInspector(git);
        _lifetimeToken = _lifetime.Token;
        LogicalProjectScopeRuntime.Initialize(_config);

        _pet = new PetForm(ShowGuardian, ChooseRepositoryAsync, ExitApplication);
        MainForm = _pet;
        _pet.Show();

        _timer = new System.Windows.Forms.Timer { Interval = Math.Max(5, config.PollSeconds) * 1000 };
        _timer.Tick += async (_, _) => await RefreshAsync(false);
        _timer.Start();

        _ = ListenForActivationAsync(pipeName, _lifetimeToken);
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
        if (!ProjectSwitchRuntime.IsSwitching) ShowProjectsMenu();
        return Task.CompletedTask;
    }

    private async Task ReconfigureCurrentProjectAsync()
    {
        if (ProjectSwitchRuntime.IsSwitching) return;
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
        if (ProjectSwitchRuntime.IsSwitching) return;
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
        if (ProjectSwitchRuntime.IsSwitching) return;

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

        var createGitHub = new ToolStripMenuItem("Create new GitHub repository…")
        {
            ToolTipText = "Create a brand-new repository under the authenticated GitHub account plus a matching local folder. The current GitPet project is not replaced and nothing is pushed automatically."
        };
        createGitHub.Click += async (_, _) => await CreateGitHubRepositoryAsync();
        menu.Items.Add(createGitHub);

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
            var manage = new ToolStripMenuItem("Manage projects");
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

                var reassign = new ToolStripMenuItem("Reassign / move folder…")
                {
                    ToolTipText = "Point this GitPet project at another existing Git folder, or move its whole local Git repository into an empty destination. Nothing is committed, pulled, pushed, or deleted online."
                };
                reassign.Click += async (_, _) => await ReassignProjectFolderAsync(projectId);
                projectMenu.DropDownItems.Add(reassign);

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

    private async Task CreateGitHubRepositoryAsync()
    {
        if (ProjectSwitchRuntime.IsSwitching) return;

        if (_guardian is { IsDisposed: false } && GuardianOperationInProgress(_guardian))
        {
            using var busy = new GuardianConfirmDialog(
                "Create new GitHub repository",
                "FINISH THE CURRENT OPERATION FIRST",
                "GitPet is already working on another Guardian operation. Finish or cancel it before creating a new repository.",
                confirmText: "OK",
                cancelText: "",
                showCancel: false);
            busy.ShowDialog(DialogOwner);
            return;
        }

        var github = new GitHubAccountService(_audit);
        var account = await github.GetStatusAsync(_lifetimeToken);
        if (!account.Authenticated || string.IsNullOrWhiteSpace(account.Login))
        {
            using var signIn = new GuardianConfirmDialog(
                "Create new GitHub repository",
                "GITHUB CONNECTION REQUIRED",
                "Creating a repository uses the GitHub account authenticated in GitPet.\r\n\r\n" +
                "Open Connection settings now to sign in or choose your GitHub account?",
                confirmText: "Open connection",
                cancelText: "Cancel",
                confirmWidth: 160);

            if (signIn.ShowDialog(DialogOwner) != DialogResult.Yes) return;
            await ShowConnectionSetupAsync();

            account = await github.GetStatusAsync(_lifetimeToken);
            if (!account.Authenticated || string.IsNullOrWhiteSpace(account.Login)) return;
        }

        var active = _config.GetActiveProject();
        var initialParent = active is not null && Directory.Exists(active.RepositoryRoot)
            ? Directory.GetParent(active.RepositoryRoot)?.FullName ?? active.RepositoryRoot
            : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        using var dialog = new NewGitHubRepositoryForm(account.Login, initialParent);
        if (dialog.ShowDialog(DialogOwner) != DialogResult.OK) return;

        var repositoryName = dialog.RepositoryName;
        var destination = dialog.DestinationPath;
        var expectedName = $"{account.Login}/{repositoryName}";

        _pet.BeginGuidanceHold("✨ NEW GITHUB REPOSITORY\nChecking your account first");
        try
        {
            IReadOnlyList<GitHubRepositoryCandidate> candidates;
            try
            {
                candidates = await github.FindRepositoriesAsync(
                    account.Login,
                    [repositoryName],
                    _lifetimeToken);
            }
            catch (Exception ex)
            {
                using var discoveryProblem = new GuardianConfirmDialog(
                    "Create new GitHub repository",
                    "GITHUB CHECK NEEDS ATTENTION",
                    "GitPet could not verify whether that repository already exists in the authenticated account. " +
                    "Nothing was created.\r\n\r\n" + ex.Message,
                    confirmText: "OK",
                    cancelText: "",
                    showCancel: false);
                discoveryProblem.ShowDialog(DialogOwner);
                return;
            }

            var existing = candidates.FirstOrDefault(item =>
                item.NameWithOwner.Equals(expectedName, StringComparison.OrdinalIgnoreCase));

            if (existing is not null)
            {
                using var alreadyExists = new GuardianConfirmDialog(
                    "Create new GitHub repository",
                    "REPOSITORY ALREADY EXISTS",
                    $"GitPet found {existing.NameWithOwner} in the authenticated GitHub account.\r\n\r\n" +
                    $"Clone that existing repository into:\r\n{destination}\r\n\r\n" +
                    "GitPet will preserve its history and will not overwrite a non-empty local folder.",
                    confirmText: "Clone existing",
                    cancelText: "Choose another name",
                    confirmWidth: 160);

                if (alreadyExists.ShowDialog(DialogOwner) != DialogResult.Yes) return;

                _pet.ShowGuidance("📥 COPYING REPOSITORY\nI'll preserve its history");
                var clone = await _git.CloneRepositoryAsync(existing.Url, destination, _lifetimeToken);
                if (!clone.Success)
                {
                    using var cloneProblem = new GuardianConfirmDialog(
                        "Create new GitHub repository",
                        "CLONE NEEDS ATTENTION",
                        "GitPet found the repository online but could not create the local copy.\r\n\r\n" + clone.Output,
                        confirmText: "OK",
                        cancelText: "",
                        showCancel: false,
                        dialogSize: new Size(760, 470),
                        scrollable: true);
                    cloneProblem.ShowDialog(DialogOwner);
                    return;
                }

                var clonedRoot = clone.Output.Trim();
                await _audit.WriteAsync("repository_cloned_from_create_flow", new
                {
                    repository = clonedRoot,
                    online = existing.NameWithOwner
                });

                _pet.ShowGuidance("✅ REPOSITORY COPIED\nNow choose what I guard");
                await ConfigureProjectAsync(
                    clonedRoot,
                    clonedRoot,
                    repositoryName,
                    null,
                    null);
                return;
            }

            try
            {
                Directory.CreateDirectory(dialog.ParentFolder);
                if (!dialog.AddReadme)
                    Directory.CreateDirectory(destination);
            }
            catch (Exception ex)
            {
                using var folderProblem = new GuardianConfirmDialog(
                    "Create new GitHub repository",
                    "LOCAL FOLDER COULD NOT BE CREATED",
                    $"GitPet could not prepare:\r\n{destination}\r\n\r\n{ex.Message}\r\n\r\nNothing was created on GitHub.",
                    confirmText: "OK",
                    cancelText: "",
                    showCancel: false);
                folderProblem.ShowDialog(DialogOwner);
                return;
            }

            if (!dialog.AddReadme)
            {
                var initialize = await _git.InitializeRepositoryAsync(destination, _lifetimeToken);
                if (!initialize.Success)
                {
                    using var initProblem = new GuardianConfirmDialog(
                        "Create new GitHub repository",
                        "LOCAL GIT SETUP NEEDS ATTENTION",
                        "GitPet created the local folder but Git could not initialize it. Nothing was created on GitHub.\r\n\r\n" +
                        initialize.Output,
                        confirmText: "OK",
                        cancelText: "",
                        showCancel: false);
                    initProblem.ShowDialog(DialogOwner);
                    return;
                }
            }

            _pet.ShowGuidance("☁ CREATING REPOSITORY\nUsing your GitHub account");
            var created = await github.CreateRepositoryAsync(
                account.Login,
                repositoryName,
                dialog.IsPrivate,
                dialog.DescriptionText,
                dialog.AddReadme,
                _lifetimeToken);
            if (!created.Success)
            {
                using var createProblem = new GuardianConfirmDialog(
                    "Create new GitHub repository",
                    "GITHUB CREATION NEEDS ATTENTION",
                    (dialog.AddReadme
                        ? "GitHub did not create the online repository. "
                        : "The local repository was initialized, but GitHub did not create the online repository. ") +
                    "Nothing was pushed by GitPet.\r\n\r\n" + created.Message,
                    confirmText: "OK",
                    cancelText: "",
                    showCancel: false,
                    dialogSize: new Size(760, 470),
                    scrollable: true);
                createProblem.ShowDialog(DialogOwner);
                return;
            }

            var projectRoot = destination;
            if (dialog.AddReadme)
            {
                _pet.ShowGuidance("📥 COPYING INITIAL README\nmain now exists on GitHub");
                var clone = await _git.CloneRepositoryAsync(
                    created.RepositoryUrl,
                    destination,
                    _lifetimeToken);
                if (!clone.Success)
                {
                    using var cloneProblem = new GuardianConfirmDialog(
                        "Create new GitHub repository",
                        "REPOSITORY CREATED — LOCAL COPY NEEDS ATTENTION",
                        $"GitHub repository created with README:\r\n{expectedName}\r\n\r\n" +
                        "GitPet could not clone that new repository to the selected local folder. " +
                        "The GitHub repository was left intact.\r\n\r\n" + clone.Output,
                        confirmText: "OK",
                        cancelText: "",
                        showCancel: false,
                        dialogSize: new Size(780, 500),
                        scrollable: true);
                    cloneProblem.ShowDialog(DialogOwner);
                    return;
                }

                projectRoot = clone.Output.Trim();
            }
            else
            {
                var origin = await _git.AddOriginRemoteAsync(
                    destination,
                    created.RepositoryUrl,
                    _lifetimeToken);
                if (!origin.Success)
                {
                    using var originProblem = new GuardianConfirmDialog(
                        "Create new GitHub repository",
                        "REPOSITORY CREATED — CONNECTION NEEDS ATTENTION",
                        $"GitHub repository created:\r\n{expectedName}\r\n\r\n" +
                        "GitPet could not add it as the local origin. Nothing was pushed.\r\n\r\n" +
                        origin.Output,
                        confirmText: "OK",
                        cancelText: "",
                        showCancel: false,
                        dialogSize: new Size(760, 490),
                        scrollable: true);
                    originProblem.ShowDialog(DialogOwner);
                    return;
                }
            }

            var entry = _config.RememberProject(
                projectRoot,
                projectRoot,
                repositoryName,
                trackEverything: true,
                scopeEntries: []);
            _configStore.Save(_config);
            ResetProjectState();

            await _audit.WriteAsync("new_github_repository_created", new
            {
                account = account.Login,
                repository = expectedName,
                localFolder = destination,
                visibility = dialog.IsPrivate ? "private" : "public",
                addReadme = dialog.AddReadme,
                projectId = entry.Id
            });

            await RefreshAsync(true);

            using var ready = new GuardianConfirmDialog(
                "Create new GitHub repository",
                "NEW REPOSITORY READY  ✓",
                $"GitHub repository:\r\n{expectedName}\r\n\r\n" +
                $"Local folder:\r\n{projectRoot}\r\n\r\n" +
                (dialog.AddReadme
                    ? "GitHub created an initial README so main exists, and GitPet cloned that repository locally. No additional files were committed or sent by GitPet."
                    : "GitPet added the new repository as a project and connected origin. No files were committed and nothing was sent online."),
                confirmText: "OK",
                cancelText: "",
                showCancel: false,
                dialogSize: new Size(780, 500));
            ready.ShowDialog(DialogOwner);
        }
        finally
        {
            _pet.EndGuidanceHold();
        }
    }

    private async Task CloneRepositoryAsync()
    {
        using var dialog = new CloneRepositoryForm();
        if (dialog.ShowDialog(DialogOwner) != DialogResult.OK) return;

        var address = dialog.Address;
        _pet.ShowGuidance("📥 COPYING REPOSITORY\nI'll preserve its history");
        var result = await _git.CloneRepositoryAsync(address.CloneSource, dialog.DestinationPath, _lifetimeToken);
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

        var inspection = await _projectInspector.InspectAsync(dialog.SelectedPath, _lifetimeToken);
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

        var initialize = await _git.InitializeRepositoryAsync(inspection.SelectedPath, _lifetimeToken);
        if (!initialize.Success)
        {
            MessageBox.Show(DialogOwner,
                "Git initialization failed. No remote, commit, or push was attempted.\r\n\r\n" + initialize.Output,
                "Prepare project for Git", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var verify = await _git.GetRepositoryRootAsync(inspection.SelectedPath, _lifetimeToken);
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
        var rootResult = await _git.GetRepositoryRootAsync(repositoryPath, _lifetimeToken);
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

    private async Task ReassignProjectFolderAsync(string projectId)
    {
        if (ProjectSwitchRuntime.IsSwitching) return;

        if (_guardian is { IsDisposed: false } && GuardianOperationInProgress(_guardian))
        {
            using var busy = new GuardianConfirmDialog(
                "Reassign project folder",
                "FINISH THE CURRENT OPERATION FIRST",
                "GitPet is already working on another Guardian operation. Finish or cancel it before changing a project's folder assignment.",
                confirmText: "OK",
                cancelText: "",
                showCancel: false);
            busy.ShowDialog(DialogOwner);
            return;
        }

        var project = _config.FindProject(projectId);
        if (project is null) return;

        using var folder = new FolderBrowserDialog
        {
            Description = $"Choose the folder for '{project.DisplayName}'. Select an existing Git folder to reassign only, or an empty normal folder to move the whole local repository there.",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
            InitialDirectory = Directory.Exists(project.Path)
                ? project.Path
                : Directory.Exists(project.RepositoryRoot)
                    ? project.RepositoryRoot
                    : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };
        if (folder.ShowDialog(DialogOwner) != DialogResult.OK) return;

        var inspection = await _projectInspector.InspectAsync(folder.SelectedPath, _lifetimeToken);
        if (inspection.Suitability == ProjectSuitability.CanPrepare)
        {
            await MoveRepositoryForReassignmentAsync(project, inspection.SelectedPath);
            return;
        }

        if (inspection.Suitability is not ProjectSuitability.Ready and not ProjectSuitability.NestedRepository ||
            string.IsNullOrWhiteSpace(inspection.RepositoryRoot))
        {
            using var invalid = new GuardianConfirmDialog(
                "Reassign project folder",
                "THAT FOLDER CANNOT BE USED YET",
                inspection.Message + "\r\n\r\n" +
                "For an existing Git folder, GitPet can reassign the registration only. " +
                "For a normal empty folder, GitPet can move the whole local repository there. " +
                "Folders with broken Git metadata still need to be fixed manually first.",
                confirmText: "OK",
                cancelText: "",
                showCancel: false,
                dialogSize: new Size(780, 520),
                scrollable: true);
            invalid.ShowDialog(DialogOwner);
            return;
        }

        var newProjectPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(inspection.SelectedPath));
        var newRepositoryRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(inspection.RepositoryRoot));
        if (PathEquals(project.Path, newProjectPath) &&
            PathEquals(project.RepositoryRoot, newRepositoryRoot))
        {
            using var unchanged = new GuardianConfirmDialog(
                "Reassign project folder",
                "PROJECT FOLDER UNCHANGED",
                $"'{project.DisplayName}' is already assigned to:\r\n{newProjectPath}",
                confirmText: "OK",
                cancelText: "",
                showCancel: false);
            unchanged.ShowDialog(DialogOwner);
            return;
        }

        var duplicate = _config.FindProjectByPath(newProjectPath);
        if (duplicate is not null &&
            !string.Equals(duplicate.Id, project.Id, StringComparison.OrdinalIgnoreCase))
        {
            using var duplicateDialog = new GuardianConfirmDialog(
                "Reassign project folder",
                "THAT FOLDER IS ALREADY A GITPET PROJECT",
                $"The selected folder is already assigned to:\r\n{duplicate.DisplayName}\r\n\r\n" +
                "GitPet will not silently make two project registrations point at the same folder.",
                confirmText: "OK",
                cancelText: "",
                showCancel: false);
            duplicateDialog.ShowDialog(DialogOwner);
            return;
        }

        var wholeRepository = PathEquals(newProjectPath, newRepositoryRoot);
        var newScope = wholeRepository
            ? Array.Empty<ProjectScopeEntry>()
            : BuildDefaultNestedScope(newRepositoryRoot, newProjectPath);

        using var confirm = new GuardianConfirmDialog(
            "Reassign project folder",
            "REASSIGN PROJECT FOLDER",
            $"Project: {project.DisplayName}\r\n\r\n" +
            $"Current folder:\r\n{project.Path}\r\n\r\n" +
            $"New folder:\r\n{newProjectPath}\r\n\r\n" +
            (wholeRepository
                ? "The selected folder is the repository root, so this GitPet project will use the whole repository."
                : "The selected folder is inside a larger repository, so GitPet will scope this project to that folder.") +
            "\r\n\r\nNo files will be moved, renamed, deleted, committed, pulled, or pushed.",
            confirmText: "Reassign",
            cancelText: "Cancel",
            confirmWidth: 140,
            dialogSize: new Size(800, 580),
            scrollable: true);

        if (confirm.ShowDialog(DialogOwner) != DialogResult.Yes) return;

        var wasActive = string.Equals(project.Id, _config.ActiveProjectId, StringComparison.OrdinalIgnoreCase);
        var oldProjectPath = project.Path;
        var oldRepositoryRoot = project.RepositoryRoot;
        if (!_config.ReassignProjectFolder(
                project.Id,
                newProjectPath,
                newRepositoryRoot,
                trackEverything: wholeRepository,
                scopeEntries: newScope))
        {
            using var failed = new GuardianConfirmDialog(
                "Reassign project folder",
                "PROJECT FOLDER WAS NOT CHANGED",
                "GitPet could not update this project registration. Nothing on disk was changed.",
                confirmText: "OK",
                cancelText: "",
                showCancel: false);
            failed.ShowDialog(DialogOwner);
            return;
        }

        _configStore.Save(_config);
        ResetProjectState();

        await _audit.WriteAsync("logical_project_folder_reassigned", new
        {
            projectId = project.Id,
            project = project.DisplayName,
            oldProjectPath,
            oldRepositoryRoot,
            newProjectPath,
            newRepositoryRoot,
            wholeRepository
        });

        if (wasActive)
            await RefreshAsync(true);

        using var ready = new GuardianConfirmDialog(
            "Reassign project folder",
            "PROJECT FOLDER UPDATED  ✓",
            $"GitPet now points '{project.DisplayName}' to:\r\n{newProjectPath}\r\n\r\n" +
            $"Repository root:\r\n{newRepositoryRoot}\r\n\r\n" +
            "The project identity, saved test commands, and GitPet registration were preserved. No files were moved.",
            confirmText: "OK",
            cancelText: "",
            showCancel: false,
            dialogSize: new Size(760, 500));
        ready.ShowDialog(DialogOwner);
    }

    private async Task MoveRepositoryForReassignmentAsync(
        RecentRepositoryEntry selectedProject,
        string selectedDestination)
    {
        var oldRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(selectedProject.RepositoryRoot));
        var destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(selectedDestination));

        if (!Directory.Exists(oldRoot) || !Directory.Exists(destination))
        {
            using var unavailable = new GuardianConfirmDialog(
                "Move project repository",
                "FOLDER IS NOT AVAILABLE",
                "The current repository or selected destination is no longer available. Nothing was changed.",
                confirmText: "OK",
                cancelText: "",
                showCancel: false);
            unavailable.ShowDialog(DialogOwner);
            return;
        }

        if (PathEquals(oldRoot, destination))
        {
            using var unchanged = new GuardianConfirmDialog(
                "Move project repository",
                "REPOSITORY LOCATION UNCHANGED",
                $"The repository is already located at:\r\n{oldRoot}",
                confirmText: "OK",
                cancelText: "",
                showCancel: false);
            unchanged.ShowDialog(DialogOwner);
            return;
        }

        var oldPrefix = oldRoot + Path.DirectorySeparatorChar;
        if (destination.StartsWith(oldPrefix, StringComparison.OrdinalIgnoreCase))
        {
            using var insideSource = new GuardianConfirmDialog(
                "Move project repository",
                "DESTINATION IS INSIDE THE CURRENT REPOSITORY",
                $"Current repository:\r\n{oldRoot}\r\n\r\nSelected destination:\r\n{destination}\r\n\r\n" +
                "Choose a folder outside the current repository so GitPet cannot create a repository inside itself.",
                confirmText: "OK",
                cancelText: "",
                showCancel: false,
                dialogSize: new Size(780, 500));
            insideSource.ShowDialog(DialogOwner);
            return;
        }

        var oldDrive = Path.GetPathRoot(oldRoot) ?? string.Empty;
        var newDrive = Path.GetPathRoot(destination) ?? string.Empty;
        if (!string.Equals(oldDrive, newDrive, StringComparison.OrdinalIgnoreCase))
        {
            using var crossDrive = new GuardianConfirmDialog(
                "Move project repository",
                "CROSS-DRIVE MOVE IS NOT ENABLED",
                $"Current drive: {oldDrive}\r\nDestination drive: {newDrive}\r\n\r\n" +
                "GitPet currently uses an atomic local folder move for repository relocation. " +
                "Choose a destination on the same drive, or move/copy the repository yourself and then use Reassign folder.",
                confirmText: "OK",
                cancelText: "",
                showCancel: false,
                dialogSize: new Size(780, 500));
            crossDrive.ShowDialog(DialogOwner);
            return;
        }

        var affected = _config.RecentRepositories
            .Where(item => PathEquals(item.RepositoryRoot, oldRoot))
            .ToArray();

        var rebased = new List<(RecentRepositoryEntry Project, string NewPath)>();
        foreach (var item in affected)
        {
            string relative;
            if (PathEquals(item.Path, oldRoot))
            {
                relative = ".";
            }
            else
            {
                relative = Path.GetRelativePath(oldRoot, item.Path);
                if (relative.Equals("..", StringComparison.Ordinal) ||
                    relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                {
                    using var outside = new GuardianConfirmDialog(
                        "Move project repository",
                        "PROJECT REGISTRATION NEEDS ATTENTION",
                        $"GitPet project '{item.DisplayName}' points outside the repository root:\r\n{item.Path}\r\n\r\n" +
                        "The repository was not moved because GitPet could not safely rebase every project registration.",
                        confirmText: "OK",
                        cancelText: "",
                        showCancel: false,
                        dialogSize: new Size(780, 500));
                    outside.ShowDialog(DialogOwner);
                    return;
                }
            }

            var newPath = relative == "."
                ? destination
                : Path.GetFullPath(Path.Combine(destination, relative));
            rebased.Add((item, newPath));
        }

        var directParent = PathEquals(Directory.GetParent(oldRoot)?.FullName, destination);
        if (directParent)
        {
            var extras = Directory.EnumerateFileSystemEntries(destination)
                .Where(path => !PathEquals(path, oldRoot))
                .Take(2)
                .ToArray();
            if (extras.Length > 0)
            {
                using var notEmptyParent = new GuardianConfirmDialog(
                    "Move project repository",
                    "PARENT FOLDER IS NOT EMPTY",
                    $"To flatten:\r\n{oldRoot}\r\n\r\ninto:\r\n{destination}\r\n\r\n" +
                    "GitPet requires the parent to contain only the current repository folder. " +
                    "This avoids silently turning unrelated parent files into repository contents.",
                    confirmText: "OK",
                    cancelText: "",
                    showCancel: false,
                    dialogSize: new Size(800, 540));
                notEmptyParent.ShowDialog(DialogOwner);
                return;
            }
        }
        else if (Directory.EnumerateFileSystemEntries(destination).Any())
        {
            using var notEmpty = new GuardianConfirmDialog(
                "Move project repository",
                "DESTINATION MUST BE EMPTY",
                $"Selected destination:\r\n{destination}\r\n\r\n" +
                "The folder is not a Git repository and it already contains files or folders. " +
                "GitPet will not merge a repository into existing content.",
                confirmText: "OK",
                cancelText: "",
                showCancel: false,
                dialogSize: new Size(760, 500));
            notEmpty.ShowDialog(DialogOwner);
            return;
        }

        using var confirm = new GuardianConfirmDialog(
            "Move project repository",
            "MOVE THE LOCAL GIT REPOSITORY",
            $"Repository:\r\n{oldRoot}\r\n\r\n" +
            $"Move to:\r\n{destination}\r\n\r\n" +
            $"GitPet projects that will be updated: {affected.Length}\r\n\r\n" +
            "This moves the entire local repository, including .git, working files, local commits, uncommitted changes, branches and remotes. " +
            "Nothing is committed, pulled, pushed, or deleted from GitHub.",
            confirmText: "Move repository",
            cancelText: "Cancel",
            confirmWidth: 170,
            dialogSize: new Size(820, 590),
            scrollable: true);

        if (confirm.ShowDialog(DialogOwner) != DialogResult.Yes) return;

        var activeAffected = affected.Any(item =>
            string.Equals(item.Id, _config.ActiveProjectId, StringComparison.OrdinalIgnoreCase));

        try
        {
            if (directParent)
            {
                var children = Directory.EnumerateFileSystemEntries(oldRoot).ToArray();
                foreach (var child in children)
                {
                    var target = Path.Combine(destination, Path.GetFileName(child));
                    if (Directory.Exists(child))
                        Directory.Move(child, target);
                    else
                        File.Move(child, target);
                }
                Directory.Delete(oldRoot, false);
            }
            else
            {
                Directory.Delete(destination, false);
                Directory.Move(oldRoot, destination);
            }

            var verify = await _git.GetRepositoryRootAsync(destination, _lifetimeToken);
            if (!verify.Success ||
                string.IsNullOrWhiteSpace(verify.Output) ||
                !PathEquals(verify.Output.Trim(), destination))
            {
                throw new InvalidOperationException(
                    "The folder move completed, but GitPet could not verify the Git repository at the new location.\r\n\r\n" +
                    verify.Output);
            }

            foreach (var item in rebased)
            {
                _config.ReassignProjectFolder(
                    item.Project.Id,
                    item.NewPath,
                    destination,
                    item.Project.TrackEverything,
                    item.Project.ScopeEntries.Select(scope =>
                        new ProjectScopeEntry(scope.RelativePath, scope.IsDirectory)));
            }

            _configStore.Save(_config);
            ResetProjectState();

            await _audit.WriteAsync("git_repository_moved", new
            {
                oldRoot,
                newRoot = destination,
                projectsUpdated = affected.Select(item => new { item.Id, item.DisplayName }).ToArray()
            });

            if (activeAffected)
                await RefreshAsync(true);

            using var ready = new GuardianConfirmDialog(
                "Move project repository",
                "REPOSITORY MOVED  ✓",
                $"Old location:\r\n{oldRoot}\r\n\r\n" +
                $"New location:\r\n{destination}\r\n\r\n" +
                $"GitPet updated {affected.Length} project registration{(affected.Length == 1 ? "" : "s")}. " +
                "Git history and the configured remote were preserved.",
                confirmText: "OK",
                cancelText: "",
                showCancel: false,
                dialogSize: new Size(780, 520));
            ready.ShowDialog(DialogOwner);
        }
        catch (Exception ex)
        {
            using var failed = new GuardianConfirmDialog(
                "Move project repository",
                "REPOSITORY MOVE NEEDS ATTENTION",
                "GitPet could not complete the repository relocation. No Git commit, pull, or push was attempted.\r\n\r\n" +
                ex.Message,
                confirmText: "OK",
                cancelText: "",
                showCancel: false,
                dialogSize: new Size(820, 560),
                scrollable: true);
            failed.ShowDialog(DialogOwner);
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
        if (string.Equals(projectId, _config.ActiveProjectId, StringComparison.OrdinalIgnoreCase)) return;
        if (!await _projectSwitchGate.WaitAsync(0)) return;

        var previousProjectId = _config.ActiveProjectId;
        var activated = false;
        var timer = Stopwatch.StartNew();
        using var overlay = GuardianProjectSwitchOverlayHost.Begin(_guardian, project.DisplayName);

        try
        {
            ProjectSwitchRuntime.Begin(project.Id, project.DisplayName);
            _pet.SetOperationState(new SaveOperationVisualState(
                SaveOperationPhase.Preparing,
                $"Switching to {project.DisplayName}...",
                DateTimeOffset.UtcNow));
            await _audit.WriteAsync("project_switch_started", new
            {
                fromProjectId = previousProjectId,
                toProjectId = project.Id,
                project = project.DisplayName,
                projectPath = project.Path,
                repository = project.RepositoryRoot
            });

            // Give WinForms one normal message-loop turn so the blocking overlay paints.
            await Task.Yield();

            ProjectSwitchRuntime.Transition(ProjectSwitchPhase.VerifyingRepository, "Reading repository...");
            _pet.SetOperationState(new SaveOperationVisualState(
                SaveOperationPhase.CheckingPathSupport,
                "Reading repository...",
                DateTimeOffset.UtcNow));

            var rootResult = await _git.GetRepositoryRootAsync(project.Path, _lifetimeToken);
            if (!rootResult.Success || string.IsNullOrWhiteSpace(rootResult.Output))
                throw new InvalidOperationException(
                    "That project is no longer inside a readable Git repository.\r\n\r\n" + rootResult.Output);

            var actualRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootResult.Output.Trim()));
            if (!PathEquals(actualRoot, project.RepositoryRoot))
                throw new InvalidOperationException(
                    "This project's Git repository root changed since it was registered.\r\n\r\n" +
                    $"Saved root: {project.RepositoryRoot}\r\nCurrent root: {actualRoot}\r\n\r\n" +
                    "Forget and add the project again so GitPet can rebuild its scope safely.");

            ProjectSwitchRuntime.Transition(ProjectSwitchPhase.LoadingRepository, "Validating Git state...");
            _pet.SetOperationState(new SaveOperationVisualState(
                SaveOperationPhase.CheckingPathSupport,
                "Validating Git state...",
                DateTimeOffset.UtcNow));
            var repositoryState = await _git.ValidateRepositoryStateAsync(project.RepositoryRoot, _lifetimeToken);
            if (!repositoryState.Success)
                throw new InvalidOperationException(
                    "GitPet did not activate this project.\r\n\r\n" +
                    GitService.DescribeRepositoryReadFailure(repositoryState.Output));

            var scopeCount = project.TrackEverything ? 0 : project.ScopeEntries.Count;
            ProjectSwitchRuntime.Transition(
                ProjectSwitchPhase.LoadingScope,
                project.TrackEverything
                    ? "Loading full repository scope..."
                    : $"Loading project scope ({scopeCount} entries)...");
            await Task.Yield();

            ProjectSwitchRuntime.Transition(ProjectSwitchPhase.ActivatingProject, "Activating project...");
            _config.ActivateProject(projectId);
            _configStore.Save(_config);
            ResetProjectState();
            activated = true;

            ProjectSwitchRuntime.Transition(ProjectSwitchPhase.ActivatingProject, "Checking project state...");
            _pet.SetOperationState(new SaveOperationVisualState(
                SaveOperationPhase.Staging,
                "Checking project state...",
                DateTimeOffset.UtcNow));
            var status = await _git.GetStatusAsync(project.RepositoryRoot, _lifetimeToken);
            if (!status.Healthy)
                throw new InvalidOperationException(
                    GitService.DescribeRepositoryReadFailure(status.Error));

            ProjectSwitchRuntime.Transition(ProjectSwitchPhase.LoadingRemoteState, "Checking online updates...");
            await GuardianSyncState.RefreshAsync(true, _lifetimeToken);

            ProjectSwitchRuntime.Transition(ProjectSwitchPhase.PreparingWorkboard, "Preparing workboard...");
            if (_guardian is { IsDisposed: false, Visible: true })
                await _guardian.RefreshAsync();
            await GuardianWorkboardRuntime.RefreshNowAsync(_lifetimeToken);

            timer.Stop();
            _pet.SetOperationState(new SaveOperationVisualState(
                SaveOperationPhase.Idle,
                "Ready",
                DateTimeOffset.UtcNow));
            _pet.SetStatus(status);
            _pet.ShowGuidance($"✅ PROJECT READY\n{project.DisplayName}");
            ProjectSwitchRuntime.Complete(timer.Elapsed);

            await _audit.WriteAsync("logical_project_selected", new
            {
                projectId,
                project = project.DisplayName,
                projectPath = project.Path,
                repository = project.RepositoryRoot
            });
            await _audit.WriteAsync("project_switch_completed", new
            {
                projectId,
                project = project.DisplayName,
                elapsedMilliseconds = timer.ElapsedMilliseconds,
                scopeEntries = scopeCount
            });
        }
        catch (OperationCanceledException) when (_lifetimeToken.IsCancellationRequested)
        {
            timer.Stop();
            if (activated) await RestorePreviousProjectAsync(previousProjectId);
            _pet.SetOperationState(new SaveOperationVisualState(
                SaveOperationPhase.Idle,
                "Ready",
                DateTimeOffset.UtcNow));
            ProjectSwitchRuntime.Cancel(timer.Elapsed);
            await _audit.WriteAsync("project_switch_cancelled", new
            {
                projectId,
                project = project.DisplayName,
                elapsedMilliseconds = timer.ElapsedMilliseconds
            });
        }
        catch (Exception ex)
        {
            timer.Stop();
            if (activated)
            {
                try
                {
                    await RestorePreviousProjectAsync(previousProjectId);
                }
                catch (Exception rollbackError)
                {
                    await _audit.WriteAsync("project_switch_rollback_error", new { error = rollbackError.Message });
                }
            }

            _pet.SetOperationState(new SaveOperationVisualState(
                SaveOperationPhase.Idle,
                "Ready",
                DateTimeOffset.UtcNow));
            _pet.SetError(ex.Message);
            ProjectSwitchRuntime.Fail(ex.Message, timer.Elapsed);
            await _audit.WriteAsync("project_switch_failed", new
            {
                projectId,
                project = project.DisplayName,
                error = ex.Message,
                elapsedMilliseconds = timer.ElapsedMilliseconds
            });

            MessageBox.Show(
                DialogOwner,
                "PROJECT COULD NOT BE OPENED\r\n\r\n" + ex.Message,
                "Open project",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        finally
        {
            _projectSwitchGate.Release();
        }
    }

    private async Task RestorePreviousProjectAsync(string? previousProjectId)
    {
        if (string.IsNullOrWhiteSpace(previousProjectId)) return;
        var previous = _config.FindProject(previousProjectId);
        if (previous is null || !Directory.Exists(previous.Path) || !Directory.Exists(previous.RepositoryRoot)) return;

        _config.ActivateProject(previousProjectId);
        _configStore.Save(_config);
        ResetProjectState();
        await GuardianSyncState.RefreshAsync(true, _lifetimeToken);
        if (_guardian is { IsDisposed: false, Visible: true })
            await _guardian.RefreshAsync();
        await GuardianWorkboardRuntime.RefreshNowAsync(_lifetimeToken);

        var status = await _git.GetStatusAsync(previous.RepositoryRoot, _lifetimeToken);
        _pet.SetOperationState(new SaveOperationVisualState(
            SaveOperationPhase.Idle,
            "Ready",
            DateTimeOffset.UtcNow));
        if (status.Healthy) _pet.SetStatus(status);
    }

    private async Task ActivateRepositoryAsync(string path)
    {
        var rootResult = await _git.GetRepositoryRootAsync(path, _lifetimeToken);
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
        if (ProjectSwitchRuntime.IsSwitching || _pet.RefreshInProgress) return;
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
            var status = await _git.GetStatusAsync(_config.RepositoryPath, _lifetimeToken);
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
        if (!status.Healthy || ProjectSwitchRuntime.IsSwitching) return;
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
                var test = await _git.RunTestCommandAsync(_config.RepositoryPath!, command, _lifetimeToken);
                if (!test.Success) return;
            }
        }

        _automaticCheckpointRunning = true;
        try
        {
            var result = await _git.CreateCheckpointAsync(_config.RepositoryPath!,
                $"auto-checkpoint: {DateTime.Now:yyyy-MM-dd HH:mm}", _lifetimeToken);
            if (result.Success) _lastAutomaticFingerprint = fingerprint;
        }
        finally
        {
            _automaticCheckpointRunning = false;
        }
    }

    /* ==========================================================================
       PATCH: SAFE DEV / RELEASE CHANNEL HANDOFF
       DATE.TIME: 2026-09-19 22:45 +03:00
       Same-channel launches activate the existing window. Cross-channel launches
       ask the running copy for permission before it exits and hands off the mutex.
       ========================================================================== */
    private async Task ListenForActivationAsync(string pipeName, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    pipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(token);

                using var reader = new StreamReader(
                    server,
                    System.Text.Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: true,
                    1024,
                    leaveOpen: true);
                using var writer = new StreamWriter(
                    server,
                    new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    1024,
                    leaveOpen: true)
                {
                    AutoFlush = true
                };

                var request = await reader.ReadLineAsync(token);
                if (!ApplicationInstanceProtocol.TryParseActivationRequest(request, out var requestedChannel))
                {
                    // Preserve compatibility with the original one-byte activation protocol.
                    _pet.BeginInvoke(ShowGuardian);
                    continue;
                }

                var response = await ResolveActivationRequestAsync(requestedChannel, token);
                await _audit.WriteAsync("application_channel_activation", new
                {
                    running = _identity.Channel.ToString(),
                    requested = requestedChannel.ToString(),
                    result = response.ToString()
                });

                await writer.WriteLineAsync(ApplicationInstanceProtocol.BuildResponse(response));
                await writer.FlushAsync();

                if (response == ExistingInstanceResponse.SwitchApproved)
                {
                    // Reply first so the waiting target executable knows it may take
                    // ownership only after this process releases the global mutex.
                    _pet.BeginInvoke(ExitApplication);
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                await _audit.WriteAsync("activation_listener_error", new { error = ex.Message });
            }
        }
    }

    private Task<ExistingInstanceResponse> ResolveActivationRequestAsync(
        ApplicationChannel requestedChannel,
        CancellationToken token)
    {
        if (requestedChannel == _identity.Channel)
        {
            _pet.BeginInvoke(ShowGuardian);
            return Task.FromResult(ExistingInstanceResponse.Activated);
        }

        var completion = new TaskCompletionSource<ExistingInstanceResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        void Prompt()
        {
            try
            {
                if (ChannelSwitchIsBusy())
                {
                    ShowGuardian();
                    using var busy = new GuardianConfirmDialog(
                        "Switch GitPet",
                        "SWITCH WAITING",
                        $"{_identity.DisplayName} is currently busy with a GitPet operation.\r\n\r\n" +
                        "Finish or cancel that operation, then open the other GitPet channel again.\r\n\r\n" +
                        "The running application has not been closed.",
                        "OK",
                        "",
                        showCancel: false,
                        dialogSize: new Size(680, 360));
                    busy.ShowDialog(DialogOwner);
                    completion.TrySetResult(ExistingInstanceResponse.SwitchBusy);
                    return;
                }

                var target = ApplicationIdentity.ForChannel(requestedChannel);
                using var confirm = new GuardianConfirmDialog(
                    "Switch GitPet",
                    $"SWITCH TO {target.DisplayName.ToUpperInvariant()}?",
                    $"{_identity.DisplayName} ({_identity.Description}) is currently running.\r\n\r\n" +
                    $"Switch to {target.DisplayName} ({target.Description})?\r\n\r\n" +
                    $"{_identity.DisplayName} will close cleanly first. The new copy will start only after " +
                    "the current process has fully released GitPet's single-instance lock.",
                    $"Switch to {target.DisplayName}",
                    "Cancel",
                    showCancel: true,
                    dialogSize: new Size(720, 410),
                    confirmWidth: 190);

                var result = confirm.ShowDialog(DialogOwner);
                completion.TrySetResult(result == DialogResult.Yes
                    ? ExistingInstanceResponse.SwitchApproved
                    : ExistingInstanceResponse.SwitchDeclined);
            }
            catch
            {
                completion.TrySetResult(ExistingInstanceResponse.SwitchDeclined);
            }
        }

        try
        {
            _pet.BeginInvoke(Prompt);
        }
        catch
        {
            completion.TrySetResult(ExistingInstanceResponse.SwitchDeclined);
        }

        return completion.Task.WaitAsync(token);
    }

    private bool ChannelSwitchIsBusy() =>
        _automaticCheckpointRunning ||
        ProjectSwitchRuntime.IsSwitching ||
        (_guardian is { IsDisposed: false } && GuardianOperationInProgress(_guardian));

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
        if (!disposing)
        {
            base.Dispose(false);
            return;
        }

        // ApplicationContext shutdown may be followed by the outer using-scope
        // disposal in Program.Main. Cleanup must therefore be idempotent.
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
            return;

        _timer.Stop();
        _timer.Dispose();

        // Cancel once, then dispose. Async operations use the cached
        // CancellationToken struct (_lifetimeToken), so they never access
        // CancellationTokenSource.Token after the source has been disposed.
        try { _lifetime.Cancel(); }
        catch (ObjectDisposedException) { }
        _lifetime.Dispose();

        _projectSwitchGate.Dispose();
        _projectsMenu?.Dispose();
        _guardian?.Dispose();
        _pet.Dispose();

        base.Dispose(true);
    }
}
