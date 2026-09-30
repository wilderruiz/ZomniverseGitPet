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
            File.WriteAllText(file, "alpha\nbeta\ngamma");
            await MustGit(git, root, ["add", "--", "shared.txt"]);
            await MustGit(git, root, ["commit", "-m", "base"]);
            var baseSha = (await MustGit(git, root, ["rev-parse", "HEAD"])).Trim();

            await MustGit(git, root, ["switch", "-c", "remote-side"]);
            File.WriteAllText(file, "alpha\nbeta remote\ngamma");
            await MustGit(git, root, ["add", "--", "shared.txt"]);
            await MustGit(git, root, ["commit", "-m", "remote"]);
            var remoteSha = (await MustGit(git, root, ["rev-parse", "HEAD"])).Trim();

            await MustGit(git, root, ["switch", "main"]);
            File.WriteAllText(file, "alpha\nbeta\ngamma local");
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

            if (model.Branch != "main" ||
                model.Base.CommitSha != baseSha ||
                model.Local.CommitSha != localSha ||
                model.Remote.CommitSha != remoteSha ||
                model.Base.Text != "alpha\nbeta\ngamma" ||
                model.Local.Text != "alpha\nbeta\ngamma local" ||
                model.Remote.Text != "alpha\nbeta remote\ngamma" ||
                !model.BaseLocalChanges.BeforeLines.SetEquals([3]) ||
                !model.BaseLocalChanges.AfterLines.SetEquals([3]) ||
                !model.BaseRemoteChanges.BeforeLines.SetEquals([2]) ||
                !model.BaseRemoteChanges.AfterLines.SetEquals([2]) ||
                model.Base.Locator != "merge-base:shared.txt" ||
                model.Local.Locator != "HEAD:shared.txt" ||
                model.Remote.Locator != "origin/main:shared.txt" ||
                beforeStatus != afterStatus ||
                afterHead != localSha ||
                afterRemote != remoteSha)
                throw new InvalidOperationException(
                    "Reconcile Inspector did not preserve pinned BASE / LOCAL / REMOTE source identity.");

            Console.WriteLine("Reconcile Inspector Phase 2 pinned-source regression passed.");
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
