using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ZomniverseGitPet;

/* ========================================================================== 
   FEATURE: UNIFIED REASSIGN / MOVE REPOSITORY WORKFLOW
   DATE.TIME: 2026-10-09

   OWNER DECISION (Wilder Ruiz, 2026-10-09)
   ------------------------------------------
   GitPet previously exposed two overlapping repository-location workflows:

       Projects > Manage projects > <project> > Reassign / move folder...
       top-level Move repository...

   The older flow could reassign an existing checkout and perform an atomic same-drive
   Directory.Move, but explicitly rejected cross-drive moves.  The later top-level
   command solved cross-drive relocation, but duplicated the UI and still left the
   same-drive path on a separate implementation.

   This runtime now replaces the legacy submenu action at runtime with ONE command:

       Reassign / move folder...

   Selection semantics are intentionally simple:

       existing Git folder -> reassign this GitPet project only
       empty normal folder -> move the whole local Git repository there

   Repository moves use one copy -> verify -> persist -> remove-old transaction on
   BOTH same-drive and cross-drive destinations.  This removes the old dependency on
   Directory.Move and gives both cases the same verification and recovery behavior.

   Nothing here commits, pulls, pushes, resets, or mutates GitHub.
   ========================================================================== */
internal static class CrossDriveRepositoryMoveRuntime
{
    private const string LegacyActionText = "Reassign / move folder…";
    private const string UnifiedActionPrefix = "UnifiedReassignMove:";
    private static readonly BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
    private static System.Windows.Forms.Timer? _timer;
    private static bool _operationRunning;

    [ModuleInitializer]
    internal static void InitializeModule()
    {
        Application.Idle += StartWhenReady;
    }

    private static void StartWhenReady(object? sender, EventArgs e)
    {
        if (_timer is not null) return;
        Application.Idle -= StartWhenReady;

        _timer = new System.Windows.Forms.Timer { Interval = 350 };
        _timer.Tick += (_, _) => PatchOpenProjectMenus();
        _timer.Start();
        PatchOpenProjectMenus();

        Application.ApplicationExit += (_, _) =>
        {
            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;
        };
    }

    private static void PatchOpenProjectMenus()
    {
        foreach (var guardian in Application.OpenForms.OfType<GuardianForm>())
        {
            if (guardian.IsDisposed) continue;

            var config = GetField<AppConfig>(guardian, "_config");
            var chooser = GetField<Delegate>(guardian, "_chooseRepository");
            var context = chooser?.Target;
            var projectsMenu = context is null
                ? null
                : GetField<ContextMenuStrip>(context, "_projectsMenu");

            if (config is null || projectsMenu is null || projectsMenu.IsDisposed) continue;
            PatchProjectsMenu(guardian, projectsMenu, config);
        }
    }

    private static void PatchProjectsMenu(
        GuardianForm guardian,
        ContextMenuStrip projectsMenu,
        AppConfig config)
    {
        var manage = projectsMenu.Items
            .OfType<ToolStripMenuItem>()
            .FirstOrDefault(item => item.Text.Equals("Manage projects", StringComparison.OrdinalIgnoreCase));
        if (manage is null) return;

        foreach (var projectMenu in manage.DropDownItems.OfType<ToolStripMenuItem>())
        {
            var project = config.RecentRepositories.FirstOrDefault(item =>
                item.DisplayName.Equals(projectMenu.Text, StringComparison.Ordinal));
            if (project is null) continue;

            var replacementName = UnifiedActionPrefix + project.Id;
            if (projectMenu.DropDownItems.Cast<ToolStripItem>()
                .Any(item => item.Name.Equals(replacementName, StringComparison.Ordinal)))
                continue;

            var legacy = projectMenu.DropDownItems
                .OfType<ToolStripMenuItem>()
                .FirstOrDefault(item => item.Text.Equals(LegacyActionText, StringComparison.Ordinal));
            if (legacy is null) continue;

            var legacyIndex = projectMenu.DropDownItems.IndexOf(legacy);
            legacy.Visible = false;
            legacy.Enabled = false;

            var projectId = project.Id;
            var unified = new ToolStripMenuItem(LegacyActionText)
            {
                Name = replacementName,
                ToolTipText = "Choose an existing Git folder to reassign this GitPet project, or choose an empty folder to safely move the whole repository on the same or another drive."
            };
            unified.Click += async (_, _) => await ReassignOrMoveAsync(guardian, projectId);
            projectMenu.DropDownItems.Insert(legacyIndex, unified);
        }
    }

    private static async Task ReassignOrMoveAsync(GuardianForm guardian, string projectId)
    {
        if (_operationRunning || ProjectSwitchRuntime.IsSwitching) return;

        var config = GetField<AppConfig>(guardian, "_config");
        var configStore = GetField<ConfigStore>(guardian, "_configStore");
        var git = GetField<GitService>(guardian, "_git");
        var audit = GetField<AuditLog>(guardian, "_audit");
        if (config is null || configStore is null || git is null || audit is null)
        {
            ShowProblem(guardian, "LOCATION CHANGE IS NOT AVAILABLE",
                "GitPet could not access the Guardian repository services. Nothing was changed.");
            return;
        }

        var project = config.FindProject(projectId);
        if (project is null || !Directory.Exists(project.RepositoryRoot))
        {
            ShowProblem(guardian, "PROJECT IS NOT AVAILABLE",
                "GitPet could not find an available local repository for this project.");
            return;
        }

        using var folder = new FolderBrowserDialog
        {
            Description = $"Choose the folder for '{project.DisplayName}'. Select an existing Git folder to reassign only, or an empty folder to move the whole local repository there.",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
            SelectedPath = Directory.Exists(project.Path)
                ? project.Path
                : project.RepositoryRoot
        };
        if (folder.ShowDialog(guardian) != DialogResult.OK) return;

        var selected = Normalize(folder.SelectedPath);
        var rootProbe = await git.GetRepositoryRootAsync(selected);
        if (rootProbe.Success && !string.IsNullOrWhiteSpace(rootProbe.Output))
        {
            await ReassignExistingCheckoutAsync(
                guardian,
                config,
                configStore,
                git,
                audit,
                project,
                selected,
                Normalize(rootProbe.Output.Trim()));
            return;
        }

        if (!Directory.Exists(selected)) Directory.CreateDirectory(selected);
        if (Directory.EnumerateFileSystemEntries(selected).Any())
        {
            ShowProblem(guardian, "THAT FOLDER CANNOT BE USED",
                $"Selected folder:\r\n{selected}\r\n\r\n" +
                "The folder is not a readable Git checkout and it is not empty. GitPet will not merge repository files into existing content. " +
                "Choose an existing Git folder to reassign, or an empty folder to move the repository.");
            return;
        }

        await MoveWholeRepositoryAsync(
            guardian,
            config,
            configStore,
            git,
            audit,
            project,
            selected);
    }

    private static async Task ReassignExistingCheckoutAsync(
        GuardianForm guardian,
        AppConfig config,
        ConfigStore configStore,
        GitService git,
        AuditLog audit,
        RecentRepositoryEntry project,
        string newProjectPath,
        string newRepositoryRoot)
    {
        if (PathEquals(project.Path, newProjectPath) &&
            PathEquals(project.RepositoryRoot, newRepositoryRoot))
        {
            ShowProblem(guardian, "PROJECT FOLDER UNCHANGED",
                $"'{project.DisplayName}' is already assigned to:\r\n{newProjectPath}");
            return;
        }

        var duplicate = config.FindProjectByPath(newProjectPath);
        if (duplicate is not null &&
            !string.Equals(duplicate.Id, project.Id, StringComparison.OrdinalIgnoreCase))
        {
            ShowProblem(guardian, "THAT FOLDER IS ALREADY A GITPET PROJECT",
                $"The selected folder is already assigned to:\r\n{duplicate.DisplayName}\r\n\r\n" +
                "GitPet will not silently make two project registrations point at the same folder.");
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
            $"Repository root:\r\n{newRepositoryRoot}\r\n\r\n" +
            "Only the GitPet registration changes. No files are moved, renamed, deleted, committed, pulled, or pushed.",
            confirmText: "Reassign",
            cancelText: "Cancel",
            confirmWidth: 140,
            dialogSize: new Size(800, 570),
            scrollable: true);
        if (confirm.ShowDialog(guardian) != DialogResult.Yes) return;

        var oldProjectPath = project.Path;
        var oldRepositoryRoot = project.RepositoryRoot;
        var wasActive = string.Equals(project.Id, config.ActiveProjectId, StringComparison.OrdinalIgnoreCase);

        if (!config.ReassignProjectFolder(
                project.Id,
                newProjectPath,
                newRepositoryRoot,
                trackEverything: wholeRepository,
                scopeEntries: newScope))
        {
            ShowProblem(guardian, "PROJECT FOLDER WAS NOT CHANGED",
                "GitPet could not update this project registration. Nothing on disk was changed.");
            return;
        }

        configStore.Save(config);
        await audit.WriteAsync("logical_project_folder_reassigned_unified", new
        {
            projectId = project.Id,
            project = project.DisplayName,
            oldProjectPath,
            oldRepositoryRoot,
            newProjectPath,
            newRepositoryRoot,
            wholeRepository
        });

        try { await GuardianSyncState.RefreshAsync(true); } catch { }
        if (wasActive)
        {
            try { await guardian.RefreshAsync(); } catch { }
            try { await GuardianWorkboardRuntime.RefreshNowAsync(); } catch { }
        }

        using var ready = new GuardianConfirmDialog(
            "Reassign project folder",
            "PROJECT FOLDER UPDATED  ✓",
            $"GitPet now points '{project.DisplayName}' to:\r\n{newProjectPath}\r\n\r\n" +
            "The existing checkout was not moved or modified.",
            confirmText: "OK",
            cancelText: "",
            showCancel: false,
            dialogSize: new Size(760, 490));
        ready.ShowDialog(guardian);
    }

    private static async Task MoveWholeRepositoryAsync(
        GuardianForm guardian,
        AppConfig config,
        ConfigStore configStore,
        GitService git,
        AuditLog audit,
        RecentRepositoryEntry selectedProject,
        string destination)
    {
        var oldRoot = Normalize(selectedProject.RepositoryRoot);
        destination = Normalize(destination);

        if (PathEquals(oldRoot, destination))
        {
            ShowProblem(guardian, "REPOSITORY LOCATION UNCHANGED",
                $"The repository is already located at:\r\n{oldRoot}");
            return;
        }

        if (destination.StartsWith(oldRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            ShowProblem(guardian, "DESTINATION IS INSIDE THE CURRENT REPOSITORY",
                $"Current repository:\r\n{oldRoot}\r\n\r\nDestination:\r\n{destination}\r\n\r\n" +
                "Choose an empty folder outside the current repository.");
            return;
        }

        if (!Directory.Exists(destination)) Directory.CreateDirectory(destination);
        if (Directory.EnumerateFileSystemEntries(destination).Any())
        {
            ShowProblem(guardian, "DESTINATION MUST BE EMPTY",
                $"Selected destination:\r\n{destination}\r\n\r\nGitPet will not merge a repository into existing content.");
            return;
        }

        var oldDrive = Path.GetPathRoot(oldRoot) ?? string.Empty;
        var newDrive = Path.GetPathRoot(destination) ?? string.Empty;
        var crossDrive = !string.Equals(oldDrive, newDrive, StringComparison.OrdinalIgnoreCase);

        var affected = config.RecentRepositories
            .Where(item => PathEquals(item.RepositoryRoot, oldRoot))
            .ToArray();
        if (affected.Length == 0)
        {
            ShowProblem(guardian, "PROJECT REGISTRATION NOT FOUND",
                "GitPet could not find project registrations for the current repository. Nothing was moved.");
            return;
        }

        var rebased = new List<(RecentRepositoryEntry Project, string NewPath)>();
        foreach (var item in affected)
        {
            var relative = PathEquals(item.Path, oldRoot) ? "." : Path.GetRelativePath(oldRoot, item.Path);
            if (relative.Equals("..", StringComparison.Ordinal) ||
                relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                ShowProblem(guardian, "PROJECT REGISTRATION NEEDS ATTENTION",
                    $"GitPet project '{item.DisplayName}' points outside the repository root:\r\n{item.Path}\r\n\r\nThe repository was not moved.");
                return;
            }

            rebased.Add((item, relative == "."
                ? destination
                : Path.GetFullPath(Path.Combine(destination, relative))));
        }

        var sourceHead = await git.RunGitAsync(oldRoot, ["rev-parse", "HEAD"], TimeSpan.FromSeconds(30));
        var sourceStatus = await git.RunGitAsync(
            oldRoot,
            ["status", "--porcelain=v2", "--branch", "--untracked-files=all"],
            TimeSpan.FromMinutes(2));
        if (!sourceStatus.Success)
        {
            ShowProblem(guardian, "SOURCE REPOSITORY COULD NOT BE VERIFIED",
                "GitPet could not read the complete working-tree state before moving it. Nothing was changed.\r\n\r\n" + sourceStatus.Output);
            return;
        }

        using var confirm = new GuardianConfirmDialog(
            "Move project repository",
            crossDrive ? "MOVE REPOSITORY ACROSS DRIVES" : "MOVE THE LOCAL GIT REPOSITORY",
            $"Repository:\r\n{oldRoot}\r\n\r\n" +
            $"Move to:\r\n{destination}\r\n\r\n" +
            $"GitPet projects that will be updated: {affected.Length}\r\n\r\n" +
            "GitPet will copy the complete repository, verify the copied Git root, HEAD and exact working-tree state, persist all affected GitPet registrations, and only then remove the old folder. " +
            "This same verified transaction is used whether the destination is on the same drive or another drive. Nothing is committed, pulled, pushed, reset, or deleted from GitHub.",
            confirmText: "Move repository",
            cancelText: "Cancel",
            confirmWidth: 170,
            dialogSize: new Size(850, 620),
            scrollable: true);
        if (confirm.ShowDialog(guardian) != DialogResult.Yes) return;

        _operationRunning = true;
        guardian.UseWaitCursor = true;
        try
        {
            var copy = await CopyRepositoryAsync(oldRoot, destination);
            if (!copy.Success)
            {
                TryDeleteDestination(destination);
                ShowProblem(guardian, "REPOSITORY COPY FAILED",
                    "GitPet could not copy the complete repository. The source repository was left untouched.\r\n\r\n" + copy.Message);
                return;
            }

            var rootCheck = await git.GetRepositoryRootAsync(destination);
            if (!rootCheck.Success || string.IsNullOrWhiteSpace(rootCheck.Output) || !PathEquals(rootCheck.Output.Trim(), destination))
                throw new InvalidOperationException("GitPet copied the folder but could not verify a Git repository at the destination.\r\n\r\n" + rootCheck.Output);

            var destinationHead = await git.RunGitAsync(destination, ["rev-parse", "HEAD"], TimeSpan.FromSeconds(30));
            var destinationStatus = await git.RunGitAsync(
                destination,
                ["status", "--porcelain=v2", "--branch", "--untracked-files=all"],
                TimeSpan.FromMinutes(2));

            if (!destinationStatus.Success || !SameText(sourceStatus.Output, destinationStatus.Output))
                throw new InvalidOperationException(
                    "The copied repository does not reproduce the exact pre-move Git working-tree state. The source folder will not be removed.");

            if (sourceHead.Success != destinationHead.Success ||
                (sourceHead.Success && !string.Equals(sourceHead.Output.Trim(), destinationHead.Output.Trim(), StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException(
                    "The destination repository HEAD does not match the source repository. The source folder will not be removed.");

            foreach (var item in rebased)
            {
                if (!config.ReassignProjectFolder(
                        item.Project.Id,
                        item.NewPath,
                        destination,
                        item.Project.TrackEverything,
                        item.Project.ScopeEntries.Select(scope =>
                            new ProjectScopeEntry(scope.RelativePath, scope.IsDirectory))))
                    throw new InvalidOperationException($"GitPet could not reassign project '{item.Project.DisplayName}'. The source repository has not been removed.");
            }
            configStore.Save(config);

            try
            {
                ClearReadOnlyAttributes(oldRoot);
                Directory.Delete(oldRoot, true);
            }
            catch (Exception deleteEx)
            {
                await audit.WriteAsync("git_repository_move_verified_source_retained", new
                {
                    oldRoot,
                    newRoot = destination,
                    crossDrive,
                    error = deleteEx.Message
                });

                try { await GuardianSyncState.RefreshAsync(true); } catch { }
                try { await guardian.RefreshAsync(); } catch { }
                try { await GuardianWorkboardRuntime.RefreshNowAsync(); } catch { }

                ShowProblem(guardian, "MOVE COMPLETE — OLD COPY COULD NOT BE REMOVED",
                    $"GitPet verified the repository at:\r\n{destination}\r\n\r\n" +
                    "Project registrations now point to the new location. The old repository could not be deleted, so it was left as an extra backup copy:\r\n" +
                    oldRoot + "\r\n\r\n" + deleteEx.Message);
                return;
            }

            await audit.WriteAsync("git_repository_moved_unified", new
            {
                oldRoot,
                newRoot = destination,
                crossDrive,
                projectsUpdated = affected.Select(item => new { item.Id, item.DisplayName }).ToArray()
            });

            try { await GuardianSyncState.RefreshAsync(true); } catch { }
            try { await guardian.RefreshAsync(); } catch { }
            try { await GuardianWorkboardRuntime.RefreshNowAsync(); } catch { }

            using var ready = new GuardianConfirmDialog(
                "Move project repository",
                "REPOSITORY MOVED  ✓",
                $"Old location:\r\n{oldRoot}\r\n\r\n" +
                $"New location:\r\n{destination}\r\n\r\n" +
                $"GitPet verified the destination repository and updated {affected.Length} project registration{(affected.Length == 1 ? "" : "s")}.\r\n\r\n" +
                "Git history, branches, remotes and the exact pre-move working-tree state were preserved.",
                confirmText: "OK",
                cancelText: "",
                showCancel: false,
                dialogSize: new Size(800, 550));
            ready.ShowDialog(guardian);
        }
        catch (Exception ex)
        {
            ShowProblem(guardian, "REPOSITORY MOVE NEEDS ATTENTION",
                "GitPet stopped the move because copying, verification, or project reassignment did not complete safely. No commit, pull, push, or reset was attempted.\r\n\r\n" +
                ex.Message +
                "\r\n\r\nThe source repository was not deliberately removed by this failed path. Inspect any destination copy before retrying.");
        }
        finally
        {
            guardian.UseWaitCursor = false;
            _operationRunning = false;
        }
    }

    private static async Task<(bool Success, string Message)> CopyRepositoryAsync(string source, string destination)
    {
        var start = new ProcessStartInfo
        {
            FileName = "robocopy.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add(source);
        start.ArgumentList.Add(destination);
        start.ArgumentList.Add("/E");
        start.ArgumentList.Add("/COPY:DAT");
        start.ArgumentList.Add("/DCOPY:DAT");
        start.ArgumentList.Add("/R:2");
        start.ArgumentList.Add("/W:1");
        start.ArgumentList.Add("/XJ");
        start.ArgumentList.Add("/NP");
        start.ArgumentList.Add("/NFL");
        start.ArgumentList.Add("/NDL");
        start.ArgumentList.Add("/NJH");
        start.ArgumentList.Add("/NJS");

        using var process = new Process { StartInfo = start };
        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromHours(2));
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            return (false, "Repository copy timed out after two hours.");
        }

        var output = ((await stdout) + Environment.NewLine + (await stderr)).Trim();
        // Robocopy exit codes 0-7 are successful/no-op/warning outcomes; 8+ is a copy failure.
        return process.ExitCode <= 7
            ? (true, output)
            : (false, $"Robocopy exit code {process.ExitCode}.\r\n\r\n{output}");
    }

    private static IReadOnlyList<ProjectScopeEntry> BuildDefaultNestedScope(
        string repositoryRoot,
        string projectPath)
    {
        var relative = LogicalProjectScopeRuntime.TryGetRelativePath(repositoryRoot, projectPath);
        return string.IsNullOrWhiteSpace(relative)
            ? []
            : [new ProjectScopeEntry(relative, IsDirectory: true)];
    }

    private static void ClearReadOnlyAttributes(string root)
    {
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            try
            {
                var attributes = File.GetAttributes(file);
                if ((attributes & FileAttributes.ReadOnly) != 0)
                    File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
            }
            catch { }
        }
    }

    private static void TryDeleteDestination(string destination)
    {
        try
        {
            if (!Directory.Exists(destination)) return;
            ClearReadOnlyAttributes(destination);
            Directory.Delete(destination, true);
        }
        catch { }
    }

    private static T? GetField<T>(object instance, string name) where T : class =>
        instance.GetType().GetField(name, InstancePrivate)?.GetValue(instance) as T;

    private static string Normalize(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static bool PathEquals(string left, string right) =>
        string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);

    private static bool SameText(string left, string right) =>
        string.Equals(left.Replace("\r\n", "\n").Trim(), right.Replace("\r\n", "\n").Trim(), StringComparison.Ordinal);

    private static void ShowProblem(IWin32Window owner, string heading, string message)
    {
        using var dialog = new GuardianConfirmDialog(
            "Project repository location",
            heading,
            message,
            confirmText: "OK",
            cancelText: "",
            showCancel: false,
            dialogSize: new Size(830, 550),
            scrollable: true);
        dialog.ShowDialog(owner);
    }
}