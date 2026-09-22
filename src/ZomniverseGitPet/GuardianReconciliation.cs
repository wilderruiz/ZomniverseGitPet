namespace ZomniverseGitPet;

internal enum ReconciliationPathBlockKind
{
    ConfirmedProcessLock,
    OwnerlessMissingPath,
    AccessDenied
}

internal static class GuardianReconciliation
{
    private static bool _restoreAutomaticSaving;

    public static async Task BeginAsync(Form? owner)
    {
        SetState(owner, SaveOperationPhase.Preparing, "Checking reconciliation safety...");
        await Task.Yield();
        var config = GuardianSyncState.Config;
        var git = GuardianSyncState.Git;
        if (config is null || git is null || string.IsNullOrWhiteSpace(config.RepositoryPath))
        {
            SetState(owner, SaveOperationPhase.Failed, "No repository is available to reconcile.");
            return;
        }

        if (StandaloneProjectPublishing.IsLogicalProject(config, config.RepositoryPath))
        {
            await BeginStandaloneFileReconciliationAsync(owner, config, git);
            return;
        }

        await GuardianSyncState.RefreshAsync(true);
        var snapshot = GuardianSyncState.Current;
        if (!snapshot.HasRepository || !snapshot.HasRemote || !snapshot.OnlineReachable)
        {
            MessageBox.Show(owner,
                "GitPet cannot reconcile until the online repository can be reached.",
                "Reconciliation unavailable",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            SetState(owner, SaveOperationPhase.Warning, "The online repository cannot be reached.");
            return;
        }

        if (snapshot.Unsaved > 0)
        {
            MessageBox.Show(owner,
                $"You have {FriendlyGitState.Count(snapshot.Unsaved, "unsaved change")} on this PC.\r\n\r\n" +
                "Save them first. GitPet will then combine the saved local history with the online history.",
                "Save first",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            SetState(owner, SaveOperationPhase.Warning, "Save local changes before reconciling.");
            return;
        }

        if (!snapshot.Diverged)
        {
            MessageBox.Show(owner,
                "Local and online history no longer need reconciliation. GitPet refreshed the control panel.",
                "Already reconciled",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            SetState(owner, SaveOperationPhase.Completed, "Local and online history already agree.");
            return;
        }

        /* ==========================================================================
           PATCH: THEMED RECONCILIATION CONFIRMATION
           FUNCTION:
           Displays the existing reconciliation warning through the Guardian-themed
           confirmation dialog while preserving its Yes-or-No result contract.

           DATE.TIME ADDED: 2026-09-10 22:56 +03:00

           REASON:
           Match reconciliation confirmation styling with the Guardian interface without changing Git behavior.
           ========================================================================== */

        using var confirmation = new GuardianConfirmDialog(
            "Reconcile local and online?",
            "RECONCILE LOCAL + ONLINE",
            "Both copies contain saved work.\r\n\r\n" +
            $"Local saved updates: {snapshot.Ahead}\r\n" +
            $"Online updates: {snapshot.Behind}\r\n\r\n" +
            "GitPet will combine them on this PC and stop before creating the reconciliation save.\r\n" +
            "Nothing will be sent online.\r\n\r\n" +
            "If the same file was changed differently in both places, GitPet will ask which complete file version to keep.",
            confirmText: "Reconcile",
            cancelText: "Not now");

        var answer = confirmation.ShowDialog(owner);
        if (answer != DialogResult.Yes)
        {
            SetState(owner, SaveOperationPhase.Cancelled);
            return;
        }

        SuspendAutomaticSaving(owner);
        SetState(owner, SaveOperationPhase.Staging, "Combining local and online history...");
        var repositoryPath = config.RepositoryPath;
        var merge = await git.RunGitAsync(
            repositoryPath,
            ["merge", "--no-commit", "--no-ff", $"origin/{snapshot.Branch}"],
            TimeSpan.FromMinutes(5));

        if (merge.Success)
        {
            await MarkReadyAsync(owner);
            return;
        }

        var conflictsResult = await git.RunGitAsync(
            repositoryPath,
            ["diff", "--name-only", "--diff-filter=U"],
            TimeSpan.FromSeconds(20));
        var conflicts = conflictsResult.Output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (conflicts.Length == 0)
        {
            RestoreAutomaticSaving(owner);
            await GuardianSyncState.RefreshAsync(false);
            var blockedPaths = ExtractPermissionDeniedPaths(merge.Output);
            if (blockedPaths.Count > 0)
            {
                await ShowBlockedFilesAsync(owner, repositoryPath, blockedPaths);
                SetState(owner, SaveOperationPhase.Failed,
                    $"Reconciliation stopped because Windows denied access to {FriendlyGitState.Count(blockedPaths.Count, "file path")}.");
            }
            else
            {
                MessageBox.Show(owner,
                    "GitPet could not prepare the reconciliation. Nothing was sent online.\r\n\r\n" + merge.Output,
                    "Reconciliation stopped",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                SetState(owner, SaveOperationPhase.Failed, "Git could not prepare the reconciliation.");
            }
            return;
        }

        using var choices = new ReconcileConflictsForm(conflicts);
        if (choices.ShowDialog(owner) != DialogResult.OK)
        {
            await AbortAfterCancelledAsync(repositoryPath, owner);
            return;
        }

        foreach (var conflict in conflicts)
        {
            if (!choices.Choices.TryGetValue(conflict, out var choice))
            {
                await AbortAfterCancelledAsync(repositoryPath, owner);
                return;
            }

            var side = choice == ReconcileChoice.Local ? "--ours" : "--theirs";
            var selectedRef = choice == ReconcileChoice.Local ? "HEAD" : $"origin/{snapshot.Branch}";
            var selectedVersion = await git.RunGitAsync(
                repositoryPath,
                ["cat-file", "-e", $"{selectedRef}:{conflict}"],
                TimeSpan.FromSeconds(20));

            CommandResult applyVersion;
            if (selectedVersion.Success)
            {
                applyVersion = await git.RunGitAsync(
                    repositoryPath,
                    ["checkout", side, "--", conflict],
                    TimeSpan.FromSeconds(30));
            }
            else
            {
                // The selected side deleted this path. Keep that deletion without exposing Git index terminology.
                applyVersion = await git.RunGitAsync(
                    repositoryPath,
                    ["rm", "-f", "--", conflict],
                    TimeSpan.FromSeconds(30));
            }

            if (!applyVersion.Success)
            {
                await AbortAfterFailureAsync(repositoryPath, owner,
                    "GitPet could not apply the selected file version. The reconciliation was cancelled safely.\r\n\r\n" + applyVersion.Output);
                return;
            }

            if (selectedVersion.Success)
            {
                var add = await git.RunGitAsync(
                    repositoryPath,
                    ["add", "--", conflict],
                    TimeSpan.FromSeconds(30));
                if (!add.Success)
                {
                    await AbortAfterFailureAsync(repositoryPath, owner,
                        "GitPet could not mark the selected file as resolved. The reconciliation was cancelled safely.\r\n\r\n" + add.Output);
                    return;
                }
            }
        }

        var remaining = await git.RunGitAsync(
            repositoryPath,
            ["diff", "--name-only", "--diff-filter=U"],
            TimeSpan.FromSeconds(20));
        if (!string.IsNullOrWhiteSpace(remaining.Output))
        {
            await AbortAfterFailureAsync(repositoryPath, owner,
                "Some files are still unresolved. GitPet restored the pre-reconciliation state instead of leaving Git half-finished.");
            return;
        }

        await MarkReadyAsync(owner);
    }

    private static async Task BeginStandaloneFileReconciliationAsync(
        Form? owner,
        AppConfig config,
        GitService git)
    {
        var repositoryPath = config.RepositoryPath!;
        var status = await git.GetStatusAsync(repositoryPath);
        if (!status.Healthy)
        {
            SetState(owner, SaveOperationPhase.Failed, "GitPet could not verify the standalone project.");
            MessageBox.Show(owner,
                "GitPet could not verify the standalone project before reconciliation.\r\n\r\n" + status.Error,
                "Reconciliation unavailable",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (status.Files.Count > 0)
        {
            SetState(owner, SaveOperationPhase.Warning, "Save local changes before standalone reconciliation.");
            MessageBox.Show(owner,
                $"You have {FriendlyGitState.Count(status.Files.Count, "unsaved change")} on this PC.\r\n\r\n" +
                "Save or discard them before reconciling the standalone project files.",
                "Save first",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var link = StandaloneProjectPublishing.GetLink(config);
        if (link is null)
        {
            SetState(owner, SaveOperationPhase.Warning, "No standalone project remote is connected.");
            return;
        }

        SetState(owner, SaveOperationPhase.Preparing, "Checking standalone project files...");
        var inspection = await StandaloneProjectPublishing.InspectRemoteAsync(
            config,
            git,
            repositoryPath,
            fetchRemote: true);

        if (!inspection.OnlineReachable)
        {
            SetState(owner, SaveOperationPhase.Warning, "The standalone online repository cannot be reached.");
            MessageBox.Show(owner,
                "GitPet cannot reconcile the standalone project until its online repository can be reached.\r\n\r\n" +
                inspection.Message,
                "Reconciliation unavailable",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (!inspection.RemoteBranchExists)
        {
            SetState(owner, SaveOperationPhase.Warning, "The standalone branch does not exist online.");
            MessageBox.Show(owner,
                $"The standalone repository does not currently have branch '{link.Branch}'.",
                "Reconciliation unavailable",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (inspection.Changes.Count == 0)
        {
            await GuardianSyncState.RefreshAsync(false);
            if (owner is GuardianForm currentGuardian) await currentGuardian.RefreshAsync();
            SetState(owner, SaveOperationPhase.Completed, "No standalone online file changes remain.");
            return;
        }

        using var confirmation = new GuardianConfirmDialog(
            "Reconcile project files?",
            "RECONCILE PROJECT FILES",
            $"The standalone online project has {inspection.Changes.Count} incoming file change" +
            $"{(inspection.Changes.Count == 1 ? "" : "s")}.\r\n\r\n" +
            "This project intentionally uses an independent Git history. GitPet will NOT merge Git histories.\r\n\r\n" +
            "Instead, choose the complete local or online version for each incoming file. " +
            "Chosen online versions will be copied into the local project as UNSAVED changes for Review and Save.\r\n\r\n" +
            "Nothing will be sent online.",
            "Choose files",
            "Not now",
            dialogSize: new Size(800, 540),
            resizable: true,
            scrollable: true,
            confirmWidth: 150);

        if (confirmation.ShowDialog(owner) != DialogResult.Yes)
        {
            SetState(owner, SaveOperationPhase.Cancelled);
            return;
        }

        var paths = inspection.Changes
            .Select(change => change.Path)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        using var choices = new ReconcileConflictsForm(
            paths,
            title: "Reconcile standalone project files",
            introText:
                "STANDALONE PROJECT — FILE-LEVEL RECONCILIATION\r\n\r\n" +
                "These files changed in the independent project-only online repository. " +
                "Choose which complete file version GitPet should keep locally. " +
                "No Git histories will be merged and nothing will be sent online.",
            fileHeader: "INCOMING PROJECT FILE");

        if (choices.ShowDialog(owner) != DialogResult.OK)
        {
            SetState(owner, SaveOperationPhase.Cancelled);
            return;
        }

        SetState(owner, SaveOperationPhase.Staging, "Applying selected standalone file versions...");
        var result = await StandaloneProjectPublishing.ReconcileFilesAsync(
            config,
            git,
            choices.Choices);

        if (!result.Success)
        {
            SetState(owner, SaveOperationPhase.Failed, result.Message);
            MessageBox.Show(owner,
                result.Message,
                "Standalone reconciliation stopped",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        await GuardianSyncState.RefreshAsync(false);
        if (owner is GuardianForm guardian) await guardian.RefreshAsync();

        SetState(owner, SaveOperationPhase.Completed, "Standalone project files are ready for Review and Save.");

        using var ready = new GuardianConfirmDialog(
            "Review, then Save",
            "PROJECT FILES READY ✓",
            result.Message,
            "OK",
            showCancel: false,
            dialogSize: new Size(760, 470),
            resizable: true,
            scrollable: true);
        ready.ShowDialog(owner);
    }

    public static async Task SaveAsync(Form? owner)
    {
        SetState(owner, SaveOperationPhase.Preparing, "Preparing reconciliation save...");
        await Task.Yield();
        var config = GuardianSyncState.Config;
        var git = GuardianSyncState.Git;
        if (config is null || git is null || string.IsNullOrWhiteSpace(config.RepositoryPath))
        {
            SetState(owner, SaveOperationPhase.Failed, "No repository is available to reconcile.");
            return;
        }

        var repositoryPath = config.RepositoryPath;
        var mergeHead = await git.RunGitAsync(
            repositoryPath,
            ["rev-parse", "--verify", "-q", "MERGE_HEAD"],
            TimeSpan.FromSeconds(8));
        if (!mergeHead.Success)
        {
            await GuardianSyncState.RefreshAsync(true);
            if (owner is GuardianForm staleGuardian) await staleGuardian.RefreshAsync();
            SetState(owner, SaveOperationPhase.Cancelled, "No prepared reconciliation was found.");
            return;
        }

        var status = await git.GetStatusAsync(repositoryPath);
        var suspicious = GitService.FindSuspiciousPaths(status.Files, config.SuspiciousPathPatterns);
        if (suspicious.Count > 0)
        {
            MessageBox.Show(owner,
                "Save is blocked because suspicious paths are present:\r\n\r\n" + string.Join("\r\n", suspicious),
                "Review before saving",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        var answer = MessageBox.Show(owner,
            "Save the reconciled local + online version on this PC?\r\n\r\n" +
            "This creates the reconciliation commit locally. Nothing will be sent online until you use Send ↑.",
            "Save reconciliation",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (answer != DialogResult.Yes)
        {
            SetState(owner, SaveOperationPhase.Cancelled);
            return;
        }

        if (!await EnsureGitIdentityAsync(owner, repositoryPath))
        {
            SetState(owner, SaveOperationPhase.Cancelled);
            return;
        }

        SetState(owner, SaveOperationPhase.CreatingCheckpoint, "Saving reconciliation locally...");
        var message = $"reconcile local and online: {DateTime.Now:yyyy-MM-dd HH:mm}";
        var result = await git.CreateCheckpointAsync(repositoryPath, message);
        MessageBox.Show(owner,
            result.Success
                ? "Reconciliation saved locally ✓\r\n\r\nNothing was sent online."
                : result.Message,
            "Save reconciliation",
            MessageBoxButtons.OK,
            result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

        if (result.Success) RestoreAutomaticSaving(owner);
        SetState(owner, result.Success ? SaveOperationPhase.Completed : SaveOperationPhase.Failed,
            result.Success ? "Reconciliation saved locally." : result.Message);
        await GuardianSyncState.RefreshAsync(true);
        if (owner is GuardianForm guardian) await guardian.RefreshAsync();
    }

    public static async Task CancelAsync(Form? owner)
    {
        SetState(owner, SaveOperationPhase.Preparing, "Preparing to cancel reconciliation...");
        await Task.Yield();
        var config = GuardianSyncState.Config;
        var git = GuardianSyncState.Git;
        if (config is null || git is null || string.IsNullOrWhiteSpace(config.RepositoryPath)) return;

        var answer = MessageBox.Show(owner,
            "Cancel the prepared reconciliation and return to the state from before it started?",
            "Cancel reconciliation?",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (answer != DialogResult.Yes)
        {
            SetState(owner, SaveOperationPhase.Cancelled);
            return;
        }

        SetState(owner, SaveOperationPhase.Staging, "Restoring the pre-reconciliation state...");
        var result = await git.RunGitAsync(
            config.RepositoryPath,
            ["merge", "--abort"],
            TimeSpan.FromMinutes(1));

        if (result.Success) RestoreAutomaticSaving(owner);
        SetState(owner, result.Success ? SaveOperationPhase.Cancelled : SaveOperationPhase.Failed,
            result.Success ? "Reconciliation cancelled safely." : result.Output);

        MessageBox.Show(owner,
            result.Success
                ? "Reconciliation cancelled. Your earlier local history was restored."
                : "GitPet could not cancel the reconciliation automatically.\r\n\r\n" + result.Output,
            "Cancel reconciliation",
            MessageBoxButtons.OK,
            result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

        await GuardianSyncState.RefreshAsync(true);
        if (owner is GuardianForm guardian) await guardian.RefreshAsync();
    }

    private static async Task MarkReadyAsync(Form? owner)
    {
        var config = GuardianSyncState.Config!;
        var git = GuardianSyncState.Git!;
        var status = await git.GetStatusAsync(config.RepositoryPath!);
        GuardianSyncState.PublishReconciliationPending(status);
        SetState(owner, SaveOperationPhase.Completed, "Reconciliation is ready for review and Save.");

        if (owner is GuardianForm guardian) await guardian.RefreshAsync();

        /* ==========================================================================
           PATCH: THEMED RECONCILIATION READY NOTICE
           FUNCTION:
           Displays the reconciliation-ready notification through the Guardian-themed
           dialog with one acknowledgement button.

           DATE.TIME ADDED: 2026-09-10 22:58 +03:00

           REASON:
           Match the reconciliation completion notice with the Guardian interface without changing workflow behavior.
           ========================================================================== */

        using var readyNotice = new GuardianConfirmDialog(
            "Review, then Save",
            "RECONCILIATION READY ✓",
            "Reconciliation is ready for review.\r\n\r\n" +
            "Review the changed files, then press Save.\r\n" +
            "Nothing has been sent online.",
            confirmText: "OK",
            cancelText: "",
            showCancel: false);

        readyNotice.ShowDialog(owner);
    }

    private static async Task AbortAfterCancelledAsync(string repositoryPath, Form? owner)
    {
        var git = GuardianSyncState.Git!;
        var abort = await git.RunGitAsync(repositoryPath, ["merge", "--abort"], TimeSpan.FromMinutes(1));
        if (abort.Success) RestoreAutomaticSaving(owner);
        SetState(owner, abort.Success ? SaveOperationPhase.Cancelled : SaveOperationPhase.Failed,
            abort.Success ? "Reconciliation cancelled safely." : abort.Output);
        await GuardianSyncState.RefreshAsync(true);
        if (owner is GuardianForm guardian) await guardian.RefreshAsync();
        MessageBox.Show(owner,
            "Reconciliation cancelled. Nothing was sent online and the earlier local state was restored.",
            "Reconciliation cancelled",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private static async Task AbortAfterFailureAsync(string repositoryPath, Form? owner, string message)
    {
        var git = GuardianSyncState.Git!;
        var abort = await git.RunGitAsync(repositoryPath, ["merge", "--abort"], TimeSpan.FromMinutes(1));
        if (abort.Success) RestoreAutomaticSaving(owner);
        SetState(owner, SaveOperationPhase.Failed, message);
        await GuardianSyncState.RefreshAsync(true);
        if (owner is GuardianForm guardian) await guardian.RefreshAsync();
        if (!abort.Success)
            message += "\r\n\r\nGit also reported a problem restoring the earlier state:\r\n" + abort.Output;

        MessageBox.Show(owner,
            message,
            "Reconciliation stopped",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
    }

    private static void SuspendAutomaticSaving(Form? owner)
    {
        var config = GuardianSyncState.Config;
        if (config is null) return;

        _restoreAutomaticSaving = config.AutomaticCheckpointsEnabled;
        config.AutomaticCheckpointsEnabled = false;

        var checkbox = FindAutomaticSavingCheckBox(owner);
        if (checkbox is { Checked: true }) checkbox.Checked = false;
    }

    private static void RestoreAutomaticSaving(Form? owner)
    {
        if (!_restoreAutomaticSaving) return;

        var config = GuardianSyncState.Config;
        if (config is not null) config.AutomaticCheckpointsEnabled = true;

        var checkbox = FindAutomaticSavingCheckBox(owner);
        if (checkbox is { Checked: false }) checkbox.Checked = true;
        _restoreAutomaticSaving = false;
    }

    private static CheckBox? FindAutomaticSavingCheckBox(Form? owner)
    {
        if (owner is null) return null;
        return EnumerateControls(owner)
            .OfType<CheckBox>()
            .FirstOrDefault(box => box.Text.StartsWith("Automatic verified save", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<Control> EnumerateControls(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var descendant in EnumerateControls(child)) yield return descendant;
        }
    }

    private static void SetState(Form? owner, SaveOperationPhase phase, string? message = null)
    {
        if (owner is GuardianForm guardian)
            guardian.SetReconcileOperationState(phase, message);
    }

    internal static IReadOnlyList<string> ExtractPermissionDeniedPaths(string? output)
    {
        const string prefix = "error: unable to create file ";
        const string suffix = ": Permission denied";
        if (string.IsNullOrWhiteSpace(output)) return [];

        return output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                           line.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            .Select(line => line[prefix.Length..^suffix.Length].Trim())
            .Where(path => path.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    internal static ReconciliationPathBlockKind ClassifyPermissionDeniedPaths(
        string repositoryPath,
        IReadOnlyList<string> blockedPaths,
        IReadOnlyList<LockingProcessInfo> lockingProcesses)
    {
        if (lockingProcesses.Count > 0)
            return ReconciliationPathBlockKind.ConfirmedProcessLock;
        if (blockedPaths.Count == 0)
            return ReconciliationPathBlockKind.AccessDenied;

        string repositoryRoot;
        try
        {
            repositoryRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repositoryPath)) +
                             Path.DirectorySeparatorChar;
        }
        catch
        {
            return ReconciliationPathBlockKind.AccessDenied;
        }

        foreach (var relativePath in blockedPaths)
        {
            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(Path.Combine(
                    repositoryPath,
                    relativePath.Replace('/', Path.DirectorySeparatorChar)));
            }
            catch
            {
                return ReconciliationPathBlockKind.AccessDenied;
            }

            if (!fullPath.StartsWith(repositoryRoot, StringComparison.OrdinalIgnoreCase) ||
                File.Exists(fullPath) ||
                Directory.Exists(fullPath))
            {
                return ReconciliationPathBlockKind.AccessDenied;
            }
        }

        return ReconciliationPathBlockKind.OwnerlessMissingPath;
    }

    private static async Task ShowBlockedFilesAsync(
        Form? owner,
        string repositoryPath,
        IReadOnlyList<string> blockedPaths)
    {
        // Restart Manager is the authoritative branch for a confirmed process lock.
        // A process merely mentioning the repository in its command line is not proof
        // that it owns the affected paths, so do not offer to terminate such processes.
        var lockingProcesses = WindowsFileLockService.FindLockingProcesses(repositoryPath, blockedPaths);
        var blockKind = ClassifyPermissionDeniedPaths(repositoryPath, blockedPaths, lockingProcesses);
        var confirmedLock = blockKind == ReconciliationPathBlockKind.ConfirmedProcessLock;

        var explanation = blockKind switch
        {
            ReconciliationPathBlockKind.ConfirmedProcessLock =>
                "GitPet confirmed these processes are holding one or more affected file paths:\r\n" +
                string.Join("\r\n", lockingProcesses.Select(process =>
                    $"• {process.Name} (PID {process.ProcessId})")) +
                "\r\n\r\nGitPet can close only the processes listed above, and only with your permission.",

            ReconciliationPathBlockKind.OwnerlessMissingPath =>
                "Sorry — Windows is still refusing these file paths even though the files are no longer present, " +
                "and GitPet could not identify an application holding them.\r\n\r\n" +
                "This can happen when Windows keeps a recently deleted path unavailable until the PC restarts.\r\n\r\n" +
                "Save your work in other applications and restart Windows. After restarting, reopen GitPet, " +
                "press Refresh, then try Reconcile again.",

            _ =>
                "Windows denied access to these file paths, but GitPet could not identify a process holding them.\r\n\r\n" +
                "Close applications or security tools that may be using the repository, then press Refresh and try " +
                "Reconcile again. If the affected files are absent and the same paths remain blocked, save your work " +
                "and restart Windows."
        };

        var heading = blockKind switch
        {
            ReconciliationPathBlockKind.ConfirmedProcessLock => "CLOSE THE BLOCKING APPS?",
            ReconciliationPathBlockKind.OwnerlessMissingPath => "WINDOWS IS STILL HOLDING THESE FILE PATHS",
            _ => "WINDOWS DENIED ACCESS TO THESE FILE PATHS"
        };

        using var blocked = new GuardianConfirmDialog(
            confirmedLock ? "Reconciliation blocked by files in use" : "Reconciliation blocked by Windows",
            heading,
            "GitPet could not restore these files:\r\n\r\n" +
            string.Join("\r\n", blockedPaths.Select(path => $"• {path}")) +
            "\r\n\r\n" + explanation +
            "\r\n\r\nNothing was sent online and GitPet did not leave a reconciliation in progress.",
            confirmText: confirmedLock ? "Close blocking apps" : "OK",
            cancelText: "Not now",
            showCancel: confirmedLock,
            dialogSize: new Size(780, 560),
            resizable: true,
            scrollable: true,
            confirmWidth: confirmedLock ? 190 : 120);

        var answer = blocked.ShowDialog(owner);
        if (answer != DialogResult.Yes || !confirmedLock) return;

        var result = WindowsFileLockService.Terminate(lockingProcesses);
        await Task.Delay(500);
        await GuardianSyncState.RefreshAsync(true);
        if (owner is GuardianForm guardian) await guardian.RefreshAsync();

        var resultMessage = result.Closed.Count == 0
            ? "GitPet could not close any confirmed blocking process."
            : "Closed:\r\n" + string.Join("\r\n", result.Closed.Select(process =>
                $"• {process.Name} (PID {process.ProcessId})"));
        if (result.Failed.Count > 0)
        {
            resultMessage += "\r\n\r\nCould not close:\r\n" +
                string.Join("\r\n", result.Failed.Select(failure =>
                    $"• {failure.Process.Name} (PID {failure.Process.ProcessId}): {failure.Error}"));
        }

        resultMessage += "\r\n\r\nPress Refresh, then try Reconcile again. GitPet did not retry automatically.";
        using var resultDialog = new GuardianConfirmDialog(
            "Blocking apps",
            result.Failed.Count == 0 ? "BLOCKING APPS CLOSED" : "REVIEW CLOSURE RESULTS",
            resultMessage,
            confirmText: "OK",
            cancelText: "",
            showCancel: false,
            dialogSize: new Size(720, 460),
            scrollable: true);
        resultDialog.ShowDialog(owner);
    }

    private static async Task<bool> EnsureGitIdentityAsync(Form? owner, string repositoryPath)
    {
        var git = GuardianSyncState.Git!;
        var nameResult = await git.GetUserNameAsync(repositoryPath);
        var emailResult = await git.GetUserEmailAsync(repositoryPath);
        var currentName = nameResult.Success ? nameResult.Output.Trim() : "";
        var currentEmail = emailResult.Success ? emailResult.Output.Trim() : "";
        if (!string.IsNullOrWhiteSpace(currentName) && !string.IsNullOrWhiteSpace(currentEmail)) return true;

        var normalized = Path.TrimEndingDirectorySeparator(repositoryPath);
        var projectName = Path.GetFileName(normalized);
        if (string.IsNullOrWhiteSpace(projectName)) projectName = normalized;

        using var identity = new GitIdentityForm(projectName, currentName, currentEmail);
        if (identity.ShowDialog(owner) != DialogResult.OK) return false;

        var save = await git.SetUserIdentityAsync(
            repositoryPath,
            identity.IdentityName,
            identity.IdentityEmail,
            identity.UseGlobal);
        if (save.Success) return true;

        MessageBox.Show(owner,
            "GitPet could not save the Git identity.\r\n\r\n" + save.Output,
            "Git identity",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return false;
    }
}
