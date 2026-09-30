using ZomniverseGitPet;

internal static class ReconcileRemoteEditRegression
{
    internal static async Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "GitPet-remote-edit-" + Guid.NewGuid().ToString("N"));
        var remote = Path.Combine(root, "remote.git");
        var source = Path.Combine(root, "source");
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

            Console.WriteLine("Reconcile Inspector Phase 7 remote-edit regression passed.");
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
