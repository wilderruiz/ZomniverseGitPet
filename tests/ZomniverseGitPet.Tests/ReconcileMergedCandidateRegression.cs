using ZomniverseGitPet;

internal static class ReconcileMergedCandidateRegression
{
    internal static async Task RunAsync()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "GitPet-merged-candidate-" + Guid.NewGuid().ToString("N"));
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
            await MustGit(git, source, ["config", "user.name", "Candidate Test"]);
            await MustGit(git, source, ["config", "user.email", "candidate@example.invalid"]);
            await MustGit(git, source, ["branch", "-M", "main"]);

            var file = Path.Combine(source, "shared.txt");
            await File.WriteAllTextAsync(
                file,
                "alpha\nbeta\nmiddle one\nmiddle two\ngamma\n");
            await MustGit(git, source, ["add", "--", "shared.txt"]);
            await MustGit(git, source, ["commit", "-m", "base"]);
            await MustGit(git, source, ["remote", "add", "origin", remote]);
            await MustGit(git, source, ["push", "-u", "origin", "main"]);
            await MustGit(git, remote, ["symbolic-ref", "HEAD", "refs/heads/main"]);

            await MustGit(git, root, ["clone", remote, peer]);
            await MustGit(git, peer, ["config", "user.name", "Remote Candidate Test"]);
            await MustGit(git, peer, ["config", "user.email", "remote-candidate@example.invalid"]);
            var peerFile = Path.Combine(peer, "shared.txt");
            await File.WriteAllTextAsync(
                peerFile,
                "alpha\nbeta remote\nmiddle one\nmiddle two\ngamma\n");
            await MustGit(git, peer, ["add", "--", "shared.txt"]);
            await MustGit(git, peer, ["commit", "-m", "remote"]);
            await MustGit(git, peer, ["push", "origin", "main"]);
            var remoteSha = (await MustGit(git, peer, ["rev-parse", "HEAD"])).Trim();

            await File.WriteAllTextAsync(
                file,
                "alpha\nbeta\nmiddle one\nmiddle two\ngamma local\n");
            await MustGit(git, source, ["add", "--", "shared.txt"]);
            await MustGit(git, source, ["commit", "-m", "local"]);
            var localSha = (await MustGit(git, source, ["rev-parse", "HEAD"])).Trim();
            await MustGit(git, source, ["fetch", "origin", "main"]);

            var model = await new ReconcileInspectorSourceService(git).LoadAsync(
                source,
                new GuardianWorkboardRow("BOTH SIDES", "shared.txt"),
                reconciliationPending: false,
                CancellationToken.None);
            if (!model.MergePreview.Available || model.MergePreview.HasConflicts)
                throw new InvalidOperationException(
                    "Regression fixture did not produce a clean generated candidate.");

            var edited = model.MergePreview.Text + "integration-only fix\n";
            var draft = new ReconcileMergedCandidateDraft(
                "shared.txt",
                "main",
                localSha,
                remoteSha,
                model.MergePreview.Text,
                edited);

            var service = new ReconcileMergedCandidateService(git);
            var validation = await service.ValidateAsync(
                source,
                draft,
                CancellationToken.None);
            if (!validation.Success)
                throw new InvalidOperationException(
                    "Candidate validation failed: " + validation.Message);

            var applied = await service.ApplyAsync(
                source,
                draft,
                CancellationToken.None);
            if (!applied.Success || !applied.ReconciliationStarted)
                throw new InvalidOperationException(
                    "Candidate apply failed: " + applied.Message);

            var headAfter = (await MustGit(git, source, ["rev-parse", "HEAD"])).Trim();
            var mergeHead = (await MustGit(git, source, ["rev-parse", "MERGE_HEAD"])).Trim();
            var remoteAfter = await RemoteTip(git, source);
            var workingText = await File.ReadAllTextAsync(file);
            var unresolved = await MustGit(
                git, source, ["diff", "--name-only", "--diff-filter=U"]);
            var staged = await MustGit(
                git, source, ["diff", "--cached", "--name-only"]);

            if (headAfter != localSha ||
                mergeHead != remoteSha ||
                remoteAfter != remoteSha ||
                !string.Equals(workingText, edited, StringComparison.Ordinal) ||
                !string.IsNullOrWhiteSpace(unresolved) ||
                !staged.Split(
                    ['\r', '\n'],
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Contains("shared.txt", StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Accepted candidate did not preserve history / stage the approved result correctly.");
            }

            await MustGit(git, source, ["merge", "--abort"]);
            var restoredHead = (await MustGit(git, source, ["rev-parse", "HEAD"])).Trim();
            var restoredText = await File.ReadAllTextAsync(file);
            if (restoredHead != localSha ||
                restoredText != "alpha\nbeta\nmiddle one\nmiddle two\ngamma local\n")
                throw new InvalidOperationException(
                    "Merge abort did not restore the pre-candidate local state.");

            if (!ReconcileMergedCandidateService.HasConflictMarkers(
                    "<<<<<<< HEAD\na\n=======\nb\n>>>>>>> origin/main\n"))
                throw new InvalidOperationException("Conflict marker validation regression failed.");

            Console.WriteLine("Reconcile Inspector Phase 8 merged-candidate regression passed.");
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
        var output = await MustGit(
            git, source,
            ["ls-remote", "--heads", "origin", "refs/heads/main"]);
        return output.Split(
            [' ', '\t'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0];
    }

    private static async Task<string> MustGit(
        GitService git,
        string workingDirectory,
        string[] arguments)
    {
        var result = await git.RunGitAsync(
            workingDirectory,
            arguments,
            TimeSpan.FromSeconds(40));
        if (!result.Success)
            throw new InvalidOperationException(
                $"git {string.Join(' ', arguments)} failed: {result.Output}");
        return result.Output;
    }
}
