using System.Text;

namespace ZomniverseGitPet;

internal sealed record ReconcileRemoteEditDraft(
    string RelativePath,
    string Branch,
    string PinnedRemoteCommitSha,
    string OriginalText,
    string EditedText);

internal sealed record ReconcileRemoteEditSession(
    string RepositoryPath,
    string WorktreePath,
    string Branch,
    string TemporaryBranch,
    string RelativePath,
    string PinnedRemoteCommitSha,
    string CorrectionCommitSha);

internal sealed record ReconcileRemoteEditResult(
    bool Success,
    string Message,
    ReconcileRemoteEditSession? Session = null,
    string? LiveRemoteSha = null);

internal sealed class ReconcileRemoteEditService(GitService git)
{
    public async Task<ReconcileRemoteEditResult> ValidateAsync(
        string repositoryPath,
        ReconcileRemoteEditDraft draft,
        CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
            return new(false, "The active repository is not available.");
        if (string.IsNullOrWhiteSpace(draft.Branch))
            return new(false, "The inspected remote branch is not available.");

        var relativePath = NormalizeGitPath(draft.RelativePath);
        if (relativePath.Length == 0)
            return new(false, "The edited path is empty.");

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repositoryPath));
        var target = Path.GetFullPath(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return new(false, "The edited path resolves outside the active repository.");

        var pathspecs = LogicalProjectScopeRuntime.GetPathspecs(
            repositoryPath,
            includeRootGitIgnore: true);
        if (pathspecs.Count > 0 &&
            !LogicalProjectScopeRuntime.ContainsPath(repositoryPath, relativePath))
        {
            return new(false,
                "This file is outside the active GitPet logical-project scope. Nothing was changed.");
        }

        var normalized = ReconcileLocalEditService.NormalizeDraftForOriginal(
            draft.OriginalText,
            draft.EditedText);
        if (normalized.IndexOf('\0') >= 0)
            return new(false, "The edited source contains a NUL character and cannot be written as text.");
        if (string.Equals(normalized, draft.OriginalText, StringComparison.Ordinal))
            return new(false, "No REMOTE source changes are present in the editor.");

        var origin = await git.GetOriginUrlAsync(repositoryPath, token);
        if (!origin.Success || string.IsNullOrWhiteSpace(origin.Output))
            return new(false, "No readable origin remote is configured.");

        var live = await GetLiveRemoteTipAsync(repositoryPath, draft.Branch, token);
        if (!live.Success)
            return live;

        if (!string.Equals(
                live.LiveRemoteSha,
                draft.PinnedRemoteCommitSha,
                StringComparison.OrdinalIgnoreCase))
        {
            return new(false,
                "REMOTE moved after this Inspector snapshot was opened. Nothing was prepared or sent. Refresh the Inspector first.",
                LiveRemoteSha: live.LiveRemoteSha);
        }

        var objectCheck = await git.RunGitAsync(
            repositoryPath,
            ["cat-file", "-e", $"{draft.PinnedRemoteCommitSha}^{{commit}}"],
            TimeSpan.FromSeconds(12),
            token);
        if (!objectCheck.Success)
            return new(false, "The pinned REMOTE commit is not available locally. Refresh the Inspector.");

        return new(true, "REMOTE edit safety validation passed.", LiveRemoteSha: live.LiveRemoteSha);
    }

    public async Task<ReconcileRemoteEditResult> PrepareCommitAsync(
        string repositoryPath,
        ReconcileRemoteEditDraft draft,
        CancellationToken token)
    {
        var validation = await ValidateAsync(repositoryPath, draft, token);
        if (!validation.Success)
            return validation;

        var worktree = Path.Combine(
            Path.GetTempPath(),
            "GitPet-remote-edit-" + Guid.NewGuid().ToString("N"));
        var temporaryBranch =
            $"gitpet/reconcile-remote-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
        temporaryBranch = temporaryBranch[..Math.Min(temporaryBranch.Length, 62)];
        var keep = false;

        var add = await git.RunGitAsync(
            repositoryPath,
            ["worktree", "add", "-b", temporaryBranch, worktree, draft.PinnedRemoteCommitSha],
            TimeSpan.FromMinutes(1),
            token);
        if (!add.Success)
            return new(false, "GitPet could not create the isolated REMOTE edit worktree.\r\n\r\n" + add.Output);

        try
        {
            var relativePath = NormalizeGitPath(draft.RelativePath);
            var worktreeRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(worktree));
            var target = Path.GetFullPath(Path.Combine(
                worktreeRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!target.StartsWith(
                    worktreeRoot + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
                return new(false, "The REMOTE edit path escaped the isolated worktree.");

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            var normalized = ReconcileLocalEditService.NormalizeDraftForOriginal(
                draft.OriginalText,
                draft.EditedText);
            await File.WriteAllTextAsync(
                target,
                normalized,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                token);

            var stage = await git.RunGitAsync(
                worktree,
                ["add", "--", relativePath],
                TimeSpan.FromSeconds(30),
                token);
            if (!stage.Success)
                return new(false, "REMOTE correction staging failed.\r\n\r\n" + stage.Output);

            var stagedNames = await git.RunGitAsync(
                worktree,
                ["diff", "--cached", "--name-only"],
                TimeSpan.FromSeconds(15),
                token);
            if (!stagedNames.Success)
                return new(false, "GitPet could not verify the isolated REMOTE staging boundary.");

            var stagedPaths = stagedNames.Output
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (stagedPaths.Length != 1 ||
                !string.Equals(stagedPaths[0], relativePath, StringComparison.OrdinalIgnoreCase))
            {
                return new(false,
                    "REMOTE correction stopped because the isolated worktree staged more than the selected file.");
            }

            var commit = await git.RunGitAsync(
                worktree,
                ["commit", "-m", $"remote correction: {DateTime.Now:yyyy-MM-dd HH:mm}"],
                TimeSpan.FromMinutes(2),
                token);
            if (!commit.Success)
                return new(false, "REMOTE correction commit failed.\r\n\r\n" + commit.Output);

            var sha = await git.RunGitAsync(
                worktree,
                ["rev-parse", "HEAD"],
                TimeSpan.FromSeconds(12),
                token);
            if (!sha.Success || string.IsNullOrWhiteSpace(sha.Output))
                return new(false, "GitPet created the REMOTE correction but could not read its commit SHA.");

            var session = new ReconcileRemoteEditSession(
                repositoryPath,
                worktree,
                draft.Branch,
                temporaryBranch,
                relativePath,
                draft.PinnedRemoteCommitSha,
                FirstLine(sha.Output));
            keep = true;

            return new(
                true,
                "REMOTE correction prepared in an isolated worktree. Nothing has been sent.",
                session,
                validation.LiveRemoteSha);
        }
        finally
        {
            if (!keep)
            {
                await CleanupByIdentityAsync(
                    repositoryPath,
                    worktree,
                    temporaryBranch,
                    CancellationToken.None);
            }
        }
    }

    public async Task<ReconcileRemoteEditResult> SendAsync(
        ReconcileRemoteEditSession session,
        CancellationToken token)
    {
        if (!Directory.Exists(session.WorktreePath))
            return new(false, "The prepared REMOTE worktree is no longer available.", session);

        var live = await GetLiveRemoteTipAsync(
            session.RepositoryPath,
            session.Branch,
            token);
        if (!live.Success)
            return live with { Session = session };

        if (!string.Equals(
                live.LiveRemoteSha,
                session.PinnedRemoteCommitSha,
                StringComparison.OrdinalIgnoreCase))
        {
            return new(false,
                "SEND BLOCKED — REMOTE MOVED.\r\n\r\n" +
                $"Inspected REMOTE: {session.PinnedRemoteCommitSha}\r\n" +
                $"Current REMOTE:   {live.LiveRemoteSha}\r\n\r\n" +
                "Nothing was pushed. Refresh the remote history before sending this correction.",
                session,
                live.LiveRemoteSha);
        }

        var ancestry = await git.RunGitAsync(
            session.WorktreePath,
            ["merge-base", "--is-ancestor",
             session.PinnedRemoteCommitSha,
             session.CorrectionCommitSha],
            TimeSpan.FromSeconds(15),
            token);
        if (!ancestry.Success)
        {
            return new(false,
                "The prepared correction is not a descendant of the pinned REMOTE commit. Nothing was pushed.",
                session,
                live.LiveRemoteSha);
        }

        var push = await git.RunGitAsync(
            session.WorktreePath,
            ["push", "origin",
             $"{session.CorrectionCommitSha}:refs/heads/{session.Branch}"],
            TimeSpan.FromMinutes(5),
            token);
        if (!push.Success)
        {
            return new(false,
                "REMOTE correction Send was rejected/stopped. No force-push was attempted.\r\n\r\n" +
                push.Output,
                session,
                live.LiveRemoteSha);
        }

        var verify = await GetLiveRemoteTipAsync(
            session.RepositoryPath,
            session.Branch,
            token);
        if (!verify.Success ||
            !string.Equals(
                verify.LiveRemoteSha,
                session.CorrectionCommitSha,
                StringComparison.OrdinalIgnoreCase))
        {
            return new(false,
                "Git reported Send success, but GitPet could not verify the new remote tip. Refresh before reconciling.",
                session,
                verify.LiveRemoteSha);
        }

        await git.RunGitAsync(
            session.RepositoryPath,
            ["fetch", "--quiet", "origin",
             $"{session.Branch}:refs/remotes/origin/{session.Branch}"],
            TimeSpan.FromMinutes(1),
            token);

        await CleanupAsync(session, CancellationToken.None);

        return new(
            true,
            "REMOTE correction sent with a normal fast-forward push. Temporary worktree/branch cleaned up.",
            LiveRemoteSha: session.CorrectionCommitSha);
    }

    public Task CleanupAsync(
        ReconcileRemoteEditSession session,
        CancellationToken token) =>
        CleanupByIdentityAsync(
            session.RepositoryPath,
            session.WorktreePath,
            session.TemporaryBranch,
            token);

    private async Task<ReconcileRemoteEditResult> GetLiveRemoteTipAsync(
        string repositoryPath,
        string branch,
        CancellationToken token)
    {
        var result = await git.RunGitAsync(
            repositoryPath,
            ["ls-remote", "--heads", "origin", $"refs/heads/{branch}"],
            TimeSpan.FromMinutes(1),
            token);
        if (!result.Success)
            return new(false, "GitPet could not read the live REMOTE branch tip.\r\n\r\n" + result.Output);

        var first = result.Output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(first))
            return new(false, $"The online branch '{branch}' was not found.");

        var sha = first.Split(
            [' ', '\t'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(sha))
            return new(false, "GitPet could not parse the live REMOTE branch tip.");

        return new(true, "Live REMOTE tip verified.", LiveRemoteSha: sha);
    }

    private async Task CleanupByIdentityAsync(
        string repositoryPath,
        string worktreePath,
        string temporaryBranch,
        CancellationToken token)
    {
        try
        {
            await git.RunGitAsync(
                repositoryPath,
                ["worktree", "remove", "--force", worktreePath],
                TimeSpan.FromMinutes(1),
                token);
        }
        catch { }

        try
        {
            await git.RunGitAsync(
                repositoryPath,
                ["branch", "-D", temporaryBranch],
                TimeSpan.FromSeconds(30),
                token);
        }
        catch { }

        try
        {
            if (Directory.Exists(worktreePath))
            {
                foreach (var file in Directory.EnumerateFiles(
                             worktreePath, "*", SearchOption.AllDirectories))
                    File.SetAttributes(file, FileAttributes.Normal);
                Directory.Delete(worktreePath, recursive: true);
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static string NormalizeGitPath(string path) =>
        (path ?? "").Replace('\\', '/').TrimStart('/');

    private static string FirstLine(string value) =>
        value.Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? "";
}

internal sealed class ReconcileRemoteEditRequestEventArgs(
    GuardianWorkboardRow row,
    ReconcileRemoteEditDraft draft) : EventArgs
{
    public GuardianWorkboardRow Row { get; } = row;
    public ReconcileRemoteEditDraft Draft { get; } = draft;
}
