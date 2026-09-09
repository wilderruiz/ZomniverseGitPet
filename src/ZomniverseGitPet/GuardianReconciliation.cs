namespace ZomniverseGitPet;

internal static class GuardianReconciliation
{
    public static async Task BeginAsync(Form? owner)
    {
        var config = GuardianSyncState.Config;
        var git = GuardianSyncState.Git;
        if (config is null || git is null || string.IsNullOrWhiteSpace(config.RepositoryPath)) return;

        await GuardianSyncState.RefreshAsync(true);
        var snapshot = GuardianSyncState.Current;
        if (!snapshot.HasRepository || !snapshot.HasRemote || !snapshot.OnlineReachable)
        {
            MessageBox.Show(owner,
                "GitPet cannot reconcile until the online repository can be reached.",
                "Reconciliation unavailable",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
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
            return;
        }

        if (!snapshot.Diverged)
        {
            MessageBox.Show(owner,
                "Local and online history no longer need reconciliation. GitPet refreshed the control panel.",
                "Already reconciled",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var answer = MessageBox.Show(owner,
            "Both copies contain saved work.\r\n\r\n" +
            $"Local saved updates: {snapshot.Ahead}\r\n" +
            $"Online updates: {snapshot.Behind}\r\n\r\n" +
            "GitPet will combine them on this PC and stop before creating the reconciliation save.\r\n" +
            "Nothing will be sent online.\r\n\r\n" +
            "If the same file was changed differently in both places, GitPet will ask which complete file version to keep.",
            "Reconcile local and online?",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (answer != DialogResult.Yes) return;

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
            await GuardianSyncState.RefreshAsync(false);
            MessageBox.Show(owner,
                "GitPet could not prepare the reconciliation. Nothing was sent online.\r\n\r\n" + merge.Output,
                "Reconciliation stopped",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
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

    public static async Task SaveAsync(Form? owner)
    {
        var config = GuardianSyncState.Config;
        var git = GuardianSyncState.Git;
        if (config is null || git is null || string.IsNullOrWhiteSpace(config.RepositoryPath)) return;

        var repositoryPath = config.RepositoryPath;
        var mergeHead = await git.RunGitAsync(
            repositoryPath,
            ["rev-parse", "--verify", "-q", "MERGE_HEAD"],
            TimeSpan.FromSeconds(8));
        if (!mergeHead.Success)
        {
            await GuardianSyncState.RefreshAsync(true);
            if (owner is GuardianForm staleGuardian) await staleGuardian.RefreshAsync();
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
        if (answer != DialogResult.Yes) return;

        if (!await EnsureGitIdentityAsync(owner, repositoryPath)) return;

        var message = $"reconcile local and online: {DateTime.Now:yyyy-MM-dd HH:mm}";
        var result = await git.CreateCheckpointAsync(repositoryPath, message);
        MessageBox.Show(owner,
            result.Success
                ? "Reconciliation saved locally ✓\r\n\r\nNothing was sent online."
                : result.Message,
            "Save reconciliation",
            MessageBoxButtons.OK,
            result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

        await GuardianSyncState.RefreshAsync(true);
        if (owner is GuardianForm guardian) await guardian.RefreshAsync();
    }

    public static async Task CancelAsync(Form? owner)
    {
        var config = GuardianSyncState.Config;
        var git = GuardianSyncState.Git;
        if (config is null || git is null || string.IsNullOrWhiteSpace(config.RepositoryPath)) return;

        var answer = MessageBox.Show(owner,
            "Cancel the prepared reconciliation and return to the state from before it started?",
            "Cancel reconciliation?",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (answer != DialogResult.Yes) return;

        var result = await git.RunGitAsync(
            config.RepositoryPath,
            ["merge", "--abort"],
            TimeSpan.FromMinutes(1));

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

        if (owner is GuardianForm guardian) await guardian.RefreshAsync();

        MessageBox.Show(owner,
            "Reconciliation is ready for review.\r\n\r\n" +
            "Review the changed files, then press Save.\r\n" +
            "Nothing has been sent online.",
            "Review, then Save",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private static async Task AbortAfterCancelledAsync(string repositoryPath, Form? owner)
    {
        var git = GuardianSyncState.Git!;
        await git.RunGitAsync(repositoryPath, ["merge", "--abort"], TimeSpan.FromMinutes(1));
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
