using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ZomniverseGitPet;

/* ========================================================================== 
   FEATURE: SAFE CROSS-DRIVE REPOSITORY MOVE
   DATE.TIME: 2026-10-09

   The original repository relocation path uses Directory.Move, which cannot move a
   directory between volumes. This runtime adds a Guardian command dedicated to that
   missing case and uses a copy -> verify -> reassign -> remove-old-copy transaction.

   Nothing here commits, pulls, pushes or mutates GitHub.
   ========================================================================== */
internal static class CrossDriveRepositoryMoveRuntime
{
    private const string MenuName = "CrossDriveRepositoryMoveMenu";
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

        _timer = new System.Windows.Forms.Timer { Interval = 700 };
        _timer.Tick += (_, _) => AttachToOpenGuardians();
        _timer.Start();
        AttachToOpenGuardians();

        Application.ApplicationExit += (_, _) =>
        {
            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;
        };
    }

    private static void AttachToOpenGuardians()
    {
        foreach (var guardian in Application.OpenForms.OfType<GuardianForm>())
        {
            if (guardian.IsDisposed || guardian.MainMenuStrip is null) continue;
            var menu = guardian.MainMenuStrip;
            if (menu.Items.Cast<ToolStripItem>().Any(item => item.Name == MenuName)) continue;

            var move = new ToolStripMenuItem("Move repository…")
            {
                Name = MenuName,
                ToolTipText = "Move the whole active local Git repository to another drive. GitPet copies it, verifies Git state, updates linked project registrations, then removes the old copy."
            };
            move.Click += async (_, _) => await MoveActiveRepositoryAsync(guardian);

            var projectSetupIndex = menu.Items.Cast<ToolStripItem>()
                .Select((item, index) => new { item, index })
                .FirstOrDefault(x => x.item.Name == "ProjectSetupMenu")?.index ?? -1;
            menu.Items.Insert(Math.Min(projectSetupIndex + 1, menu.Items.Count), move);
        }
    }

    private static async Task MoveActiveRepositoryAsync(GuardianForm guardian)
    {
        if (_operationRunning || ProjectSwitchRuntime.IsSwitching) return;

        var config = GetField<AppConfig>(guardian, "_config");
        var configStore = GetField<ConfigStore>(guardian, "_configStore");
        var git = GetField<GitService>(guardian, "_git");
        var audit = GetField<AuditLog>(guardian, "_audit");
        if (config is null || configStore is null || git is null || audit is null)
        {
            ShowProblem(guardian, "MOVE IS NOT AVAILABLE",
                "GitPet could not access the active Guardian repository services. Nothing was changed.");
            return;
        }

        var active = config.GetActiveProject();
        if (active is null || !Directory.Exists(active.RepositoryRoot))
        {
            ShowProblem(guardian, "NO ACTIVE REPOSITORY",
                "Open a GitPet project with an available local Git repository first.");
            return;
        }

        var oldRoot = Normalize(active.RepositoryRoot);
        using var folder = new FolderBrowserDialog
        {
            Description = "Choose an EMPTY destination folder on another drive for the whole repository.",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
            SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };
        if (folder.ShowDialog(guardian) != DialogResult.OK) return;

        var destination = Normalize(folder.SelectedPath);
        if (PathEquals(oldRoot, destination))
        {
            ShowProblem(guardian, "REPOSITORY LOCATION UNCHANGED",
                $"The repository is already located at:\r\n{oldRoot}");
            return;
        }

        if (destination.StartsWith(oldRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            ShowProblem(guardian, "DESTINATION IS INSIDE THE CURRENT REPOSITORY",
                $"Current repository:\r\n{oldRoot}\r\n\r\nDestination:\r\n{destination}\r\n\r\nChoose a folder outside the repository.");
            return;
        }

        var oldDrive = Path.GetPathRoot(oldRoot) ?? string.Empty;
        var newDrive = Path.GetPathRoot(destination) ?? string.Empty;
        if (string.Equals(oldDrive, newDrive, StringComparison.OrdinalIgnoreCase))
        {
            ShowProblem(guardian, "THIS COMMAND IS FOR ANOTHER DRIVE",
                $"Current drive: {oldDrive}\r\nDestination drive: {newDrive}\r\n\r\n" +
                "For a move on the same drive, use Projects → Manage projects → Reassign / move folder. " +
                "This command exists specifically for the cross-drive case that Directory.Move cannot perform.");
            return;
        }

        if (!Directory.Exists(destination)) Directory.CreateDirectory(destination);
        if (Directory.EnumerateFileSystemEntries(destination).Any())
        {
            ShowProblem(guardian, "DESTINATION MUST BE EMPTY",
                $"GitPet will not merge a repository into existing content.\r\n\r\nSelected destination:\r\n{destination}");
            return;
        }

        var affected = config.RecentRepositories
            .Where(item => PathEquals(item.RepositoryRoot, oldRoot))
            .ToArray();
        if (affected.Length == 0)
        {
            ShowProblem(guardian, "PROJECT REGISTRATION NOT FOUND",
                "GitPet could not find a project registration for the active repository. Nothing was moved.");
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
            "MOVE REPOSITORY ACROSS DRIVES",
            $"Repository:\r\n{oldRoot}\r\n\r\n" +
            $"Move to:\r\n{destination}\r\n\r\n" +
            $"GitPet projects that will be updated: {affected.Length}\r\n\r\n" +
            "GitPet will copy the complete repository first, verify the copied Git root, HEAD and exact porcelain working-tree state, update the GitPet registrations, and only then remove the old folder. " +
            "The .git directory, branches, remotes, local commits, uncommitted changes and untracked files are included. Nothing is committed, pulled, pushed, or deleted from GitHub.",
            confirmText: "Move repository",
            cancelText: "Cancel",
            confirmWidth: 170,
            dialogSize: new Size(840, 610),
            scrollable: true);
        if (confirm.ShowDialog(guardian) != DialogResult.Yes) return;

        _operationRunning = true;
        guardian.UseWaitCursor = true;
        try
        {
            var copy = await CopyAcrossDrivesAsync(oldRoot, destination);
            if (!copy.Success)
            {
                TryDeleteDestination(destination);
                ShowProblem(guardian, "CROSS-DRIVE COPY FAILED",
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

            // Update and persist GitPet first. If persistence fails, the original source is still intact.
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

            var sourceRemoved = false;
            try
            {
                ClearReadOnlyAttributes(oldRoot);
                Directory.Delete(oldRoot, true);
                sourceRemoved = true;
            }
            catch (Exception deleteEx)
            {
                await audit.WriteAsync("git_repository_cross_drive_copy_verified_source_retained", new
                {
                    oldRoot,
                    newRoot = destination,
                    error = deleteEx.Message
                });

                try { await GuardianSyncState.RefreshAsync(true); } catch { }
                try { await guardian.RefreshAsync(); } catch { }
                try { await GuardianWorkboardRuntime.RefreshNowAsync(); } catch { }

                ShowProblem(guardian, "MOVE COMPLETE — OLD COPY COULD NOT BE REMOVED",
                    $"GitPet verified the repository at:\r\n{destination}\r\n\r\n" +
                    "Project registrations now point to the new location, so normal work can continue there. " +
                    $"The old folder could not be deleted and was left as an extra backup copy:\r\n{oldRoot}\r\n\r\n" +
                    deleteEx.Message);
                return;
            }

            await audit.WriteAsync("git_repository_moved_cross_drive", new
            {
                oldRoot,
                newRoot = destination,
                sourceRemoved,
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
                "Git history, branches, remotes and the exact pre-move working-tree state were preserved. The old repository folder was removed only after verification and configuration persistence succeeded.",
                confirmText: "OK",
                cancelText: "",
                showCancel: false,
                dialogSize: new Size(800, 560));
            ready.ShowDialog(guardian);
        }
        catch (Exception ex)
        {
            using var failed = new GuardianConfirmDialog(
                "Move project repository",
                "REPOSITORY MOVE NEEDS ATTENTION",
                "GitPet stopped the move because verification or project reassignment did not complete safely. No commit, pull or push was attempted.\r\n\r\n" +
                ex.Message +
                "\r\n\r\nThe original source repository has not been deliberately removed by this failed path. Inspect the destination copy before retrying.",
                confirmText: "OK",
                cancelText: "",
                showCancel: false,
                dialogSize: new Size(840, 580),
                scrollable: true);
            failed.ShowDialog(guardian);
        }
        finally
        {
            guardian.UseWaitCursor = false;
            _operationRunning = false;
        }
    }

    private static async Task<(bool Success, string Message)> CopyAcrossDrivesAsync(string source, string destination)
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
        // Robocopy exit codes 0-7 are success/no-op/warning outcomes; 8+ means failure.
        return process.ExitCode <= 7
            ? (true, output)
            : (false, $"Robocopy exit code {process.ExitCode}.\r\n\r\n{output}");
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
            "Move project repository",
            heading,
            message,
            confirmText: "OK",
            cancelText: "",
            showCancel: false,
            dialogSize: new Size(820, 540),
            scrollable: true);
        dialog.ShowDialog(owner);
    }
}
