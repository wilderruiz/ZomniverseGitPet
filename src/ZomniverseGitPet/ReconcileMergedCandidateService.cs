using System.Text;

namespace ZomniverseGitPet;

internal sealed record ReconcileMergedCandidateDraft(
    string RelativePath,
    string Branch,
    string PinnedLocalCommitSha,
    string PinnedRemoteCommitSha,
    string GeneratedText,
    string EditedText);

internal sealed record ReconcileMergedCandidateResult(
    bool Success,
    string Message,
    int RemainingConflicts = 0,
    bool ReconciliationStarted = false);

internal sealed class ReconcileMergedCandidateService(GitService git)
{
    public async Task<ReconcileMergedCandidateResult> ValidateAsync(
        string repositoryPath,
        ReconcileMergedCandidateDraft draft,
        CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
            return new(false, "The active repository is not available.");

        var relativePath = NormalizeGitPath(draft.RelativePath);
        if (relativePath.Length == 0)
            return new(false, "The candidate path is empty.");

        if (string.IsNullOrWhiteSpace(draft.Branch))
            return new(false, "The inspected branch is not available.");

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repositoryPath));
        var target = Path.GetFullPath(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!target.StartsWith(
                root + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
            return new(false, "The candidate path resolves outside the active repository.");

        var pathspecs = LogicalProjectScopeRuntime.GetPathspecs(
            repositoryPath,
            includeRootGitIgnore: true);
        if (pathspecs.Count > 0 &&
            !LogicalProjectScopeRuntime.ContainsPath(repositoryPath, relativePath))
        {
            return new(false,
                "This file is outside the active GitPet logical-project scope. Nothing was changed.");
        }

        var mergeHead = await git.RunGitAsync(
            repositoryPath,
            ["rev-parse", "--verify", "-q", "MERGE_HEAD"],
            TimeSpan.FromSeconds(8),
            token);
        if (mergeHead.Success)
        {
            return new(false,
                "A reconciliation is already in progress. Finish or cancel it before accepting a new merged candidate.");
        }

        var status = await git.RunGitAsync(
            repositoryPath,
            ["status", "--porcelain"],
            TimeSpan.FromSeconds(20),
            token);
        if (!status.Success)
            return new(false, "GitPet could not verify the working tree before candidate acceptance.");
        if (!string.IsNullOrWhiteSpace(status.Output))
        {
            return new(false,
                "The working tree changed after this candidate was generated. Save or discard those changes, refresh the Inspector, then review the candidate again.");
        }

        var branch = await git.GetCurrentBranchAsync(repositoryPath, token);
        if (!branch.Success ||
            !string.Equals(branch.Output.Trim(), draft.Branch, StringComparison.Ordinal))
        {
            return new(false,
                "The current branch no longer matches the branch used to generate this candidate.");
        }

        var head = await git.RunGitAsync(
            repositoryPath,
            ["rev-parse", "HEAD"],
            TimeSpan.FromSeconds(8),
            token);
        if (!head.Success ||
            !string.Equals(
                FirstLine(head.Output),
                draft.PinnedLocalCommitSha,
                StringComparison.OrdinalIgnoreCase))
        {
            return new(false,
                "LOCAL moved after this candidate was generated. Refresh the Inspector before accepting it.");
        }

        var remoteTracking = await git.RunGitAsync(
            repositoryPath,
            ["rev-parse", $"refs/remotes/origin/{draft.Branch}"],
            TimeSpan.FromSeconds(8),
            token);
        if (!remoteTracking.Success ||
            !string.Equals(
                FirstLine(remoteTracking.Output),
                draft.PinnedRemoteCommitSha,
                StringComparison.OrdinalIgnoreCase))
        {
            return new(false,
                "The local origin tracking ref moved after this candidate was generated. Refresh the Inspector.");
        }

        var liveRemote = await git.RunGitAsync(
            repositoryPath,
            ["ls-remote", "--heads", "origin", $"refs/heads/{draft.Branch}"],
            TimeSpan.FromMinutes(1),
            token);
        if (!liveRemote.Success)
            return new(false, "GitPet could not verify the live REMOTE branch tip.\r\n\r\n" + liveRemote.Output);

        var liveSha = ParseLsRemoteSha(liveRemote.Output);
        if (liveSha.Length == 0 ||
            !string.Equals(
                liveSha,
                draft.PinnedRemoteCommitSha,
                StringComparison.OrdinalIgnoreCase))
        {
            return new(false,
                "REMOTE moved after this candidate was generated. Nothing was reconciled. Refresh the Inspector first.");
        }

        var regenerated = await new ReconcileInspectorSourceService(git).LoadAsync(
            repositoryPath,
            new GuardianWorkboardRow("BOTH SIDES", relativePath),
            reconciliationPending: false,
            token);
        if (!regenerated.MergePreview.Available ||
            !regenerated.MergePreview.Exists ||
            !string.Equals(
                regenerated.Local.CommitSha,
                draft.PinnedLocalCommitSha,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                regenerated.Remote.CommitSha,
                draft.PinnedRemoteCommitSha,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                regenerated.MergePreview.Text,
                draft.GeneratedText,
                StringComparison.Ordinal))
        {
            return new(false,
                "The generated merged candidate no longer matches the pinned LOCAL / REMOTE evidence. Refresh before accepting it.");
        }

        var normalized = ReconcileLocalEditService.NormalizeDraftForOriginal(
            draft.GeneratedText,
            draft.EditedText);
        if (normalized.IndexOf('\0') >= 0)
            return new(false, "The candidate contains a NUL character and cannot be applied as text.");

        if (HasConflictMarkers(normalized))
        {
            return new(false,
                "The edited candidate still contains Git conflict markers. Resolve <<<<<<< / ======= / >>>>>>> before accepting it.");
        }

        return new(true,
            "Merged candidate validation passed · pinned LOCAL/REMOTE/live remote/working-tree evidence still matches.");
    }

    public async Task<ReconcileMergedCandidateResult> ApplyAsync(
        string repositoryPath,
        ReconcileMergedCandidateDraft draft,
        CancellationToken token)
    {
        var validation = await ValidateAsync(repositoryPath, draft, token);
        if (!validation.Success)
            return validation;

        var mergeStarted = false;
        try
        {
            var merge = await git.RunGitAsync(
                repositoryPath,
                ["merge", "--no-commit", "--no-ff", $"origin/{draft.Branch}"],
                TimeSpan.FromMinutes(5),
                token);

            var mergeHead = await git.RunGitAsync(
                repositoryPath,
                ["rev-parse", "--verify", "-q", "MERGE_HEAD"],
                TimeSpan.FromSeconds(8),
                token);
            mergeStarted = mergeHead.Success;

            if (!mergeStarted)
            {
                return new(false,
                    "GitPet could not establish a no-commit reconciliation state. Nothing was accepted.\r\n\r\n" +
                    merge.Output);
            }

            if (!string.Equals(
                    FirstLine(mergeHead.Output),
                    draft.PinnedRemoteCommitSha,
                    StringComparison.OrdinalIgnoreCase))
            {
                return await AbortAsync(
                    repositoryPath,
                    "The merge target changed while reconciliation was starting. GitPet restored the pre-reconcile state.",
                    token);
            }

            var relativePath = NormalizeGitPath(draft.RelativePath);
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repositoryPath));
            var target = Path.GetFullPath(Path.Combine(
                root,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            var normalized = ReconcileLocalEditService.NormalizeDraftForOriginal(
                draft.GeneratedText,
                draft.EditedText);
            await File.WriteAllTextAsync(
                target,
                normalized,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                token);

            var stage = await git.RunGitAsync(
                repositoryPath,
                ["add", "--", relativePath],
                TimeSpan.FromSeconds(30),
                token);
            if (!stage.Success)
            {
                return await AbortAsync(
                    repositoryPath,
                    "GitPet could not stage the accepted candidate. The pre-reconciliation state was restored.\r\n\r\n" +
                    stage.Output,
                    token);
            }

            var selectedUnresolved = await git.RunGitAsync(
                repositoryPath,
                ["diff", "--name-only", "--diff-filter=U", "--", relativePath],
                TimeSpan.FromSeconds(20),
                token);
            if (!selectedUnresolved.Success ||
                !string.IsNullOrWhiteSpace(selectedUnresolved.Output))
            {
                return await AbortAsync(
                    repositoryPath,
                    "The accepted candidate did not fully resolve the selected path. The pre-reconciliation state was restored.",
                    token);
            }

            var remaining = await git.RunGitAsync(
                repositoryPath,
                ["diff", "--name-only", "--diff-filter=U"],
                TimeSpan.FromSeconds(20),
                token);
            if (!remaining.Success)
            {
                return await AbortAsync(
                    repositoryPath,
                    "GitPet could not verify the remaining reconciliation state. The pre-reconciliation state was restored.",
                    token);
            }

            var remainingPaths = remaining.Output
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return new(
                true,
                remainingPaths.Length == 0
                    ? "Merged candidate accepted. Reconciliation is prepared and waiting for Save."
                    : $"Merged candidate accepted. Reconciliation is active with {remainingPaths.Length} other unresolved file{(remainingPaths.Length == 1 ? "" : "s")}.",
                remainingPaths.Length,
                ReconciliationStarted: true);
        }
        catch (OperationCanceledException)
        {
            if (mergeStarted)
                await BestEffortAbortAsync(repositoryPath);
            throw;
        }
        catch (Exception ex)
        {
            if (mergeStarted)
                await BestEffortAbortAsync(repositoryPath);
            return new(false, "Candidate acceptance failed safely: " + ex.Message);
        }
    }

    private async Task<ReconcileMergedCandidateResult> AbortAsync(
        string repositoryPath,
        string message,
        CancellationToken token)
    {
        var abort = await git.RunGitAsync(
            repositoryPath,
            ["merge", "--abort"],
            TimeSpan.FromMinutes(1),
            token);
        return new(
            false,
            abort.Success
                ? message
                : message + "\r\n\r\nGit also reported a problem aborting the reconciliation:\r\n" + abort.Output);
    }

    private async Task BestEffortAbortAsync(string repositoryPath)
    {
        try
        {
            await git.RunGitAsync(
                repositoryPath,
                ["merge", "--abort"],
                TimeSpan.FromMinutes(1),
                CancellationToken.None);
        }
        catch { }
    }

    internal static bool HasConflictMarkers(string text)
    {
        var open = false;
        var middle = false;
        var close = false;
        foreach (var raw in (text ?? "").Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.StartsWith("<<<<<<<", StringComparison.Ordinal)) open = true;
            else if (line.Equals("=======", StringComparison.Ordinal)) middle = true;
            else if (line.StartsWith(">>>>>>>", StringComparison.Ordinal)) close = true;
        }
        return open && middle && close;
    }

    private static string NormalizeGitPath(string path) =>
        (path ?? "").Replace('\\', '/').TrimStart('/');

    private static string FirstLine(string value) =>
        value.Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? "";

    private static string ParseLsRemoteSha(string output) =>
        FirstLine(output)
            .Split(
                [' ', '\t'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? "";
}

internal sealed class ReconcileMergedCandidateRequestEventArgs(
    GuardianWorkboardRow row,
    ReconcileMergedCandidateDraft draft) : EventArgs
{
    public GuardianWorkboardRow Row { get; } = row;
    public ReconcileMergedCandidateDraft Draft { get; } = draft;
}
