using ZomniverseGitPet;

internal static class ReconcileRemoteEditRegression
{
    internal static async Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "GitPet-remote-edit-" + Guid.NewGuid().ToString("N"));
        var remote = Path.Combine(root, "remote.git");
        var source = Path.Combine(root, "source");
        var peer = Path.Combine(root, "peer");
        Directory.CreateDirectory(root);

        try
        {
            var git = new GitService(new AuditLog(Path.Combine(root, "audit.jsonl")));
            Directory.CreateDirectory(remote);
            await MustGit(git, remote, ["init", "--bare"]);

            Directory.CreateDirectory(source);
            await MustGit(git, source, ["init"]);
            await MustGit(git, source, ["config", "user.name", "Remote Edit Test"]);
            await MustGit(git, source, ["config", "user.email", "remote-edit@example.invalid"]);
            await MustGit(git, source, ["branch", "-M", "main"]);

            var file = Path.Combine(source, "sample.txt");
            await File.WriteAllTextAsync(file, "alpha\nbeta\n");
            await MustGit(git, source, ["add", "--", "sample.txt"]);
            await MustGit(git, source, ["commit", "-m", "baseline"]);
            await MustGit(git, source, ["remote", "add", "origin", remote]);
            await MustGit(git, source, ["push", "-u", "origin", "main"]);
            var pinned = (await MustGit(git, source, ["rev-parse", "HEAD"])).Trim();

            var primaryText = await File.ReadAllTextAsync(file);
            var service = new ReconcileRemoteEditService(git);
            var draft = new ReconcileRemoteEditDraft(
                "sample.txt", "main", pinned,
                "alpha\nbeta\n",
                "alpha\nbeta remote corrected\n");

            var prepared = await service.PrepareCommitAsync(source, draft, CancellationToken.None);
            if (!prepared.Success || prepared.Session is null)
                throw new InvalidOperationException("Prepare failed: " + prepared.Message);

            var session = prepared.Session;
            var headAfterPrepare = (await MustGit(git, source, ["rev-parse", "HEAD"])).Trim();
            var textAfterPrepare = await File.ReadAllTextAsync(file);
            var remoteBefore = await RemoteTip(git, source);

            if (headAfterPrepare != pinned ||
                textAfterPrepare != primaryText ||
                remoteBefore != pinned ||
                !Directory.Exists(session.WorktreePath))
                throw new InvalidOperationException("Prepare mutated primary state or remote.");

            var sent = await service.SendAsync(session, CancellationToken.None);
            if (!sent.Success)
                throw new InvalidOperationException("Send failed: " + sent.Message);

            var remoteAfter = await RemoteTip(git, source);
            var headAfter = (await MustGit(git, source, ["rev-parse", "HEAD"])).Trim();
            var textAfter = await File.ReadAllTextAsync(file);
            var tempBranch = await git.RunGitAsync(
                source,
                ["show-ref", "--verify", $"refs/heads/{session.TemporaryBranch}"]);

            if (remoteAfter != session.CorrectionCommitSha ||
                headAfter != pinned ||
                textAfter != primaryText ||
                Directory.Exists(session.WorktreePath) ||
                tempBranch.Success)
                throw new InvalidOperationException("Send/cleanup mutated primary state or left temp state.");

            var staleDraft = new ReconcileRemoteEditDraft(
                "sample.txt",
                "main",
                session.CorrectionCommitSha,
                "alpha\nbeta remote corrected\n",
                "alpha\nbeta stale prepared correction\n");
            var stalePrepared = await service.PrepareCommitAsync(
                source,
                staleDraft,
                CancellationToken.None);
            if (!stalePrepared.Success || stalePrepared.Session is null)
                throw new InvalidOperationException(
                    "Moved-remote fixture could not prepare stale correction: " +
                    stalePrepared.Message);

            var staleSession = stalePrepared.Session;

            await MustGit(git, root, ["clone", "--branch", "main", remote, peer]);
            await MustGit(git, peer, ["config", "user.name", "Remote Race Test"]);
            await MustGit(git, peer, ["config", "user.email", "remote-race@example.invalid"]);
            var peerFile = Path.Combine(peer, "sample.txt");
            await File.WriteAllTextAsync(
                peerFile,
                "alpha\nbeta moved by another writer\n");
            await MustGit(git, peer, ["add", "--", "sample.txt"]);
            await MustGit(git, peer, ["commit", "-m", "move remote after prepare"]);
            await MustGit(git, peer, ["push", "origin", "main"]);
            var movedRemoteSha = await RemoteTip(git, source);

            var blocked = await service.SendAsync(
                staleSession,
                CancellationToken.None);
            var remoteAfterBlockedSend = await RemoteTip(git, source);
            var primaryHeadAfterBlockedSend =
                (await MustGit(git, source, ["rev-parse", "HEAD"])).Trim();
            var primaryTextAfterBlockedSend = await File.ReadAllTextAsync(file);

            if (blocked.Success ||
                !blocked.Message.Contains("REMOTE moved", StringComparison.OrdinalIgnoreCase) ||
                remoteAfterBlockedSend != movedRemoteSha ||
                remoteAfterBlockedSend == staleSession.CorrectionCommitSha ||
                primaryHeadAfterBlockedSend != pinned ||
                primaryTextAfterBlockedSend != primaryText ||
                !Directory.Exists(staleSession.WorktreePath))
            {
                throw new InvalidOperationException(
                    "Moved REMOTE did not block stale Send without mutating primary state.");
            }

            await service.CleanupAsync(staleSession, CancellationToken.None);
            var staleBranch = await git.RunGitAsync(
                source,
                ["show-ref", "--verify", $"refs/heads/{staleSession.TemporaryBranch}"]);

            if (Directory.Exists(staleSession.WorktreePath) || staleBranch.Success)
                throw new InvalidOperationException(
                    "Moved-remote blocked session did not clean up explicitly.");

            Console.WriteLine(
                "Reconcile Inspector Phase 7/10 remote-edit + moved-remote regressions passed.");
        }
        finally
        {
            try
            {
                foreach (var item in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                    File.SetAttributes(item, FileAttributes.Normal);
                Directory.Delete(root, true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static async Task<string> RemoteTip(GitService git, string source)
    {
        var output = await MustGit(git, source, ["ls-remote", "--heads", "origin", "refs/heads/main"]);
        return output.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries)[0];
    }

    private static async Task<string> MustGit(GitService git, string root, string[] args)
    {
        var result = await git.RunGitAsync(root, args, TimeSpan.FromSeconds(30));
        if (!result.Success)
            throw new InvalidOperationException($"git {string.Join(' ', args)} failed: {result.Output}");
        return result.Output;
    }
}
