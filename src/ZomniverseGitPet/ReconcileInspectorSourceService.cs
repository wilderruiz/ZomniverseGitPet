namespace ZomniverseGitPet;

internal sealed record ReconcileSourceSnapshot(
    string Role,
    string RefName,
    string CommitSha,
    string ShortSha,
    string Locator,
    bool Exists,
    string Text);

internal sealed record ReconcileInspectorSourceModel(
    string RelativePath,
    string State,
    string Branch,
    bool RemoteFromMergeHead,
    ReconcileSourceSnapshot Base,
    ReconcileSourceSnapshot Local,
    ReconcileSourceSnapshot Remote,
    DiffLineMap BaseLocalChanges,
    DiffLineMap BaseRemoteChanges);

internal sealed class ReconcileInspectorSourceService(GitService git)
{
    public async Task<ReconcileInspectorSourceModel> LoadAsync(
        string repositoryPath,
        GuardianWorkboardRow row,
        bool reconciliationPending,
        CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
            throw new InvalidOperationException("The project repository is not available.");
        if (!GuardianWorkboardControl.IsReconcileInspectableState(row.State))
            throw new InvalidOperationException("The selected row is not a reconciliation source row.");

        var relativePath = NormalizeGitPath(row.Path);
        if (relativePath.Length == 0)
            throw new InvalidOperationException("The selected reconciliation path is empty.");

        var branchResult = await git.GetReconcileBranchAsync(repositoryPath, token);
        if (!branchResult.Success || string.IsNullOrWhiteSpace(branchResult.Output))
            throw new InvalidOperationException(
                "GitPet could not identify the current local branch.\r\n\r\n" + branchResult.Output);

        var branch = branchResult.Output.Trim();
        var remoteRef = reconciliationPending ? "MERGE_HEAD" : $"refs/remotes/origin/{branch}";
        var remoteDisplay = reconciliationPending ? "MERGE_HEAD" : $"origin/{branch}";

        var localTask = git.GetReconcileRevisionAsync(repositoryPath, "HEAD", token);
        var remoteTask = git.GetReconcileRevisionAsync(repositoryPath, remoteRef, token);
        await Task.WhenAll(localTask, remoteTask);

        var localRevision = await localTask;
        var remoteRevision = await remoteTask;
        if (!localRevision.Success || string.IsNullOrWhiteSpace(localRevision.Output))
            throw new InvalidOperationException("GitPet could not pin local HEAD.\r\n\r\n" + localRevision.Output);
        if (!remoteRevision.Success || string.IsNullOrWhiteSpace(remoteRevision.Output))
            throw new InvalidOperationException(
                $"GitPet could not pin {remoteDisplay}.\r\n\r\n" + remoteRevision.Output);

        var localSha = FirstLine(localRevision.Output);
        var remoteSha = FirstLine(remoteRevision.Output);
        var baseResult = await git.GetReconcileMergeBaseAsync(repositoryPath, localSha, remoteSha, token);
        if (!baseResult.Success || string.IsNullOrWhiteSpace(baseResult.Output))
            throw new InvalidOperationException(
                "GitPet could not determine the common ancestor.\r\n\r\n" + baseResult.Output);

        var baseSha = FirstLine(baseResult.Output);

        var baseSource = ReadAsync(
            repositoryPath, relativePath, "BASE", "merge-base", baseSha,
            $"merge-base:{relativePath}", token);
        var localSource = ReadAsync(
            repositoryPath, relativePath, "LOCAL", "HEAD", localSha,
            $"HEAD:{relativePath}", token);
        var remoteSource = ReadAsync(
            repositoryPath, relativePath, "REMOTE", remoteDisplay, remoteSha,
            $"{remoteDisplay}:{relativePath}", token);
        var baseLocalDiff = git.GetReconcileDiffAsync(
            repositoryPath, relativePath, baseSha, localSha, token);
        var baseRemoteDiff = git.GetReconcileDiffAsync(
            repositoryPath, relativePath, baseSha, remoteSha, token);

        await Task.WhenAll(baseSource, localSource, remoteSource, baseLocalDiff, baseRemoteDiff);

        var localDiffResult = await baseLocalDiff;
        var remoteDiffResult = await baseRemoteDiff;

        return new ReconcileInspectorSourceModel(
            relativePath,
            row.State,
            branch,
            reconciliationPending,
            await baseSource,
            await localSource,
            await remoteSource,
            localDiffResult.Success
                ? DiffLineMap.ParseUnifiedZeroContext(localDiffResult.Output)
                : DiffLineMap.Empty,
            remoteDiffResult.Success
                ? DiffLineMap.ParseUnifiedZeroContext(remoteDiffResult.Output)
                : DiffLineMap.Empty);
    }

    private async Task<ReconcileSourceSnapshot> ReadAsync(
        string repositoryPath,
        string relativePath,
        string role,
        string refName,
        string commitSha,
        string locator,
        CancellationToken token)
    {
        var exists = await git.ReconcilePathExistsAsync(
            repositoryPath, relativePath, commitSha, token);

        if (!exists.Success)
            return new ReconcileSourceSnapshot(
                role, refName, commitSha, Short(commitSha), locator, false, "");

        var content = await git.GetReconcileContentAsync(
            repositoryPath, relativePath, commitSha, token);
        if (!content.Success)
            throw new InvalidOperationException(
                $"GitPet found {relativePath} at {refName}, but could not read it.\r\n\r\n" + content.Output);

        return new ReconcileSourceSnapshot(
            role, refName, commitSha, Short(commitSha), locator, true, content.Output);
    }

    private static string NormalizeGitPath(string path) =>
        (path ?? "").Replace('\\', '/').TrimStart('/');

    private static string FirstLine(string value) =>
        value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? "";

    private static string Short(string sha) => sha.Length <= 8 ? sha : sha[..8];
}
