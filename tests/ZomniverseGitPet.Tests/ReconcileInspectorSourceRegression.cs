using ZomniverseGitPet;

internal static class ReconcileInspectorSourceRegression
{
    internal static async Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "GitPet-reconcile-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var git = new GitService(new AuditLog(Path.Combine(root, "audit.jsonl")));
            await MustGit(git, root, ["init"]);
            await MustGit(git, root, ["config", "user.name", "Reconcile Inspector Test"]);
            await MustGit(git, root, ["config", "user.email", "reconcile@example.invalid"]);
            await MustGit(git, root, ["branch", "-M", "main"]);

            var file = Path.Combine(root, "shared.txt");
            File.WriteAllText(file, "alpha\nbeta\nmiddle one\nmiddle two\ngamma");
            await MustGit(git, root, ["add", "--", "shared.txt"]);
            await MustGit(git, root, ["commit", "-m", "base"]);
            var baseSha = (await MustGit(git, root, ["rev-parse", "HEAD"])).Trim();

            await MustGit(git, root, ["switch", "-c", "remote-side"]);
            File.WriteAllText(file, "alpha\nbeta remote\nmiddle one\nmiddle two\ngamma");
            await MustGit(git, root, ["add", "--", "shared.txt"]);
            await MustGit(git, root, ["commit", "-m", "remote"]);
            var remoteSha = (await MustGit(git, root, ["rev-parse", "HEAD"])).Trim();

            await MustGit(git, root, ["switch", "main"]);
            File.WriteAllText(file, "alpha\nbeta\nmiddle one\nmiddle two\ngamma local");
            await MustGit(git, root, ["add", "--", "shared.txt"]);
            await MustGit(git, root, ["commit", "-m", "local"]);
            var localSha = (await MustGit(git, root, ["rev-parse", "HEAD"])).Trim();

            await MustGit(git, root, ["update-ref", "refs/remotes/origin/main", remoteSha]);

            var beforeStatus = await MustGit(git, root, ["status", "--porcelain"]);
            var model = await new ReconcileInspectorSourceService(git).LoadAsync(
                root,
                new GuardianWorkboardRow("BOTH SIDES", "shared.txt"),
                reconciliationPending: false,
                CancellationToken.None);
            var afterStatus = await MustGit(git, root, ["status", "--porcelain"]);
            var afterHead = (await MustGit(git, root, ["rev-parse", "HEAD"])).Trim();
            var afterRemote = (await MustGit(git, root, ["rev-parse", "refs/remotes/origin/main"])).Trim();

            var failures = new List<string>();
            void Expect(bool condition, string message)
            {
                if (!condition) failures.Add(message);
            }

            Expect(model.Branch == "main", $"branch was '{model.Branch}'");
            Expect(model.Base.CommitSha == baseSha, "BASE SHA mismatch");
            Expect(model.Local.CommitSha == localSha, "LOCAL SHA mismatch");
            Expect(model.Remote.CommitSha == remoteSha, "REMOTE SHA mismatch");
            Expect(model.Base.Text == "alpha\nbeta\nmiddle one\nmiddle two\ngamma", "BASE source mismatch");
            Expect(model.Local.Text == "alpha\nbeta\nmiddle one\nmiddle two\ngamma local", "LOCAL source mismatch");
            Expect(model.Remote.Text == "alpha\nbeta remote\nmiddle one\nmiddle two\ngamma", "REMOTE source mismatch");
            Expect(model.BaseLocalChanges.BeforeLines.SetEquals([5]), "BASE→LOCAL before-line map mismatch");
            Expect(model.BaseLocalChanges.AfterLines.SetEquals([5]), "BASE→LOCAL after-line map mismatch");
            Expect(model.BaseRemoteChanges.BeforeLines.SetEquals([2]), "BASE→REMOTE before-line map mismatch");
            Expect(model.BaseRemoteChanges.AfterLines.SetEquals([2]), "BASE→REMOTE after-line map mismatch");
            Expect(model.Analysis.OverallLabel == "INDEPENDENT CHANGES",
                $"analysis was '{model.Analysis.OverallLabel}'");
            Expect(!model.Analysis.HasOverlap, "analysis incorrectly reported overlap");
            Expect(model.MergePreview.Available, "merge preview unavailable");
            Expect(!model.MergePreview.HasConflicts,
                $"merge preview unexpectedly conflicted: {model.MergePreview.Status}");
            Expect(model.MergePreview.Status == "CLEAN THREE-WAY MERGE",
                $"merge preview status was '{model.MergePreview.Status}'");
            Expect(model.MergePreview.Text == "alpha\nbeta remote\nmiddle one\nmiddle two\ngamma local",
                "merged candidate source mismatch");
            Expect(model.Base.Locator == "merge-base:shared.txt", "BASE locator mismatch");
            Expect(model.Local.Locator == "HEAD:shared.txt", "LOCAL locator mismatch");
            Expect(model.Remote.Locator == "origin/main:shared.txt", "REMOTE locator mismatch");
            Expect(beforeStatus == afterStatus, "working-tree status changed during preview");
            Expect(afterHead == localSha, "HEAD moved during preview");
            Expect(afterRemote == remoteSha, "remote-tracking ref moved during preview");

            if (failures.Count > 0)
                throw new InvalidOperationException(
                    "Reconcile Inspector Phase 4 regression failed: " +
                    string.Join("; ", failures) + ".");

            Console.WriteLine("Reconcile Inspector Phase 4 source/analysis/preview regression passed.");
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

    private static async Task<string> MustGit(GitService git, string root, string[] args)
    {
        var result = await git.RunGitAsync(root, args, TimeSpan.FromSeconds(15));
        if (!result.Success)
            throw new InvalidOperationException($"git {string.Join(' ', args)} failed: {result.Output}");
        return result.Output;
    }
}
