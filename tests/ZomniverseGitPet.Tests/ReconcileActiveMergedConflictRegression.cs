using ZomniverseGitPet;

internal static class ReconcileActiveMergedConflictRegression
{
    internal static async Task RunAsync()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "GitPet-active-merged-conflict-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);

        try
        {
            var git = new GitService(new AuditLog(Path.Combine(root, "audit.jsonl")));
            await MustGit(git, source, ["init"]);
            await MustGit(git, source, ["config", "user.name", "Active Merge Test"]);
            await MustGit(git, source, ["config", "user.email", "active-merge@example.invalid"]);
            await MustGit(git, source, ["branch", "-M", "main"]);

            var file = Path.Combine(source, "shared.txt");
            await File.WriteAllTextAsync(file, "base\n");
            await MustGit(git, source, ["add", "--", "shared.txt"]);
            await MustGit(git, source, ["commit", "-m", "base"]);

            await MustGit(git, source, ["switch", "-c", "remote-side"]);
            await File.WriteAllTextAsync(file, "remote version\n");
            await MustGit(git, source, ["add", "--", "shared.txt"]);
            await MustGit(git, source, ["commit", "-m", "remote"]);

            await MustGit(git, source, ["switch", "main"]);
            await File.WriteAllTextAsync(file, "local version\n");
            await MustGit(git, source, ["add", "--", "shared.txt"]);
            await MustGit(git, source, ["commit", "-m", "local"]);

            var merge = await git.RunGitAsync(
                source,
                ["merge", "--no-commit", "--no-ff", "remote-side"],
                TimeSpan.FromSeconds(40));
            if (merge.Success)
                throw new InvalidOperationException("Fixture did not create an active merge conflict.");

            var unresolvedBefore = await MustGit(
                git,
                source,
                ["diff", "--name-only", "--diff-filter=U"]);
            if (!unresolvedBefore.Split(
                    ['\r', '\n'],
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Contains("shared.txt", StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Expected shared.txt to be unresolved.");
            }

            var candidates = GuardianReconciliation.LoadMergedConflictCandidates(
                source,
                ["shared.txt"]);
            if (!candidates.TryGetValue("shared.txt", out var generated) ||
                !ReconcileMergedCandidateService.HasConflictMarkers(generated))
            {
                throw new InvalidOperationException(
                    "Active merge candidate was not loaded with conflict markers.");
            }

            var resolved = "local version\nremote version\n";
            var applied = await GuardianReconciliation.ApplyResolvedMergedTextAsync(
                git,
                source,
                "shared.txt",
                resolved);
            if (!applied.Success)
                throw new InvalidOperationException(
                    "Resolved merged text was not applied: " + applied.Output);

            var unresolvedAfter = await MustGit(
                git,
                source,
                ["diff", "--name-only", "--diff-filter=U"]);
            var actual = await File.ReadAllTextAsync(file);
            var mergeHead = await MustGit(git, source, ["rev-parse", "--verify", "MERGE_HEAD"]);

            if (!string.IsNullOrWhiteSpace(unresolvedAfter) ||
                !string.Equals(actual, resolved, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(mergeHead))
            {
                throw new InvalidOperationException(
                    "Resolved merged choice did not stage cleanly while preserving MERGE_HEAD.");
            }

            var markerRejected = await GuardianReconciliation.ApplyResolvedMergedTextAsync(
                git,
                source,
                "shared.txt",
                "<<<<<<< HEAD\na\n=======\nb\n>>>>>>> remote-side\n");
            if (markerRejected.Success)
                throw new InvalidOperationException(
                    "Conflict-marker text was incorrectly accepted as resolved.");

            await MustGit(git, source, ["merge", "--abort"]);
            Console.WriteLine("Active reconciliation merged-choice regression passed.");
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
