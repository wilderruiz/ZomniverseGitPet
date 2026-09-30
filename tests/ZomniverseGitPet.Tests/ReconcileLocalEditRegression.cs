using ZomniverseGitPet;

internal static class ReconcileLocalEditRegression
{
    internal static async Task RunAsync()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "GitPet-local-edit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var git = new GitService(new AuditLog(Path.Combine(root, "audit.jsonl")));
            await MustGit(git, root, ["init"]);
            await MustGit(git, root, ["config", "user.name", "Local Edit Test"]);
            await MustGit(git, root, ["config", "user.email", "local-edit@example.invalid"]);
            await MustGit(git, root, ["branch", "-M", "main"]);

            var path = Path.Combine(root, "sample.txt");
            await File.WriteAllTextAsync(path, "alpha\nbeta\n");
            await MustGit(git, root, ["add", "--", "sample.txt"]);
            await MustGit(git, root, ["commit", "-m", "baseline"]);
            var pinned = (await MustGit(git, root, ["rev-parse", "HEAD"])).Trim();

            var service = new ReconcileLocalEditService(git);
            var draft = new ReconcileLocalEditDraft(
                "sample.txt",
                pinned,
                "alpha\nbeta\n",
                "alpha\r\nbeta edited\r\n");

            var validation = await service.ValidateAsync(
                root,
                draft,
                requireCleanIndex: true,
                CancellationToken.None);
            if (!validation.Success ||
                validation.NormalizedEditedText != "alpha\nbeta edited\n")
                throw new InvalidOperationException(
                    "Reconcile local edit validation/normalization failed: " + validation.Message);

            var write = await service.WriteAsync(
                root,
                draft,
                requireCleanIndex: true,
                CancellationToken.None);
            if (!write.Success)
                throw new InvalidOperationException(
                    "Reconcile local edit write failed: " + write.Message);

            var afterWriteHead = (await MustGit(git, root, ["rev-parse", "HEAD"])).Trim();
            var working = await File.ReadAllTextAsync(path);
            var status = await MustGit(git, root, ["status", "--porcelain", "--", "sample.txt"]);
            if (afterWriteHead != pinned ||
                working != "alpha\nbeta edited\n" ||
                string.IsNullOrWhiteSpace(status))
                throw new InvalidOperationException(
                    "Working-file edit did not remain an uncommitted exact local change.");

            var stagePlan = new SaveStagePlan(["sample.txt"], [], []);
            var checkpoint = await git.CreateCheckpointAsync(
                root,
                "reconcile correction: regression",
                stagePlan,
                CancellationToken.None);
            if (!checkpoint.Success)
                throw new InvalidOperationException(checkpoint.Message);

            var newHead = (await MustGit(git, root, ["rev-parse", "HEAD"])).Trim();
            var clean = await MustGit(git, root, ["status", "--porcelain"]);
            if (newHead == pinned || !string.IsNullOrWhiteSpace(clean))
                throw new InvalidOperationException(
                    "Local correction commit did not produce a clean new HEAD.");

            var stale = await service.ValidateAsync(
                root,
                draft,
                requireCleanIndex: false,
                CancellationToken.None);
            if (stale.Success ||
                !stale.Message.Contains("HEAD moved", StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Stale pinned LOCAL edit was not rejected after HEAD moved.");

            Console.WriteLine("Reconcile Inspector Phase 6 local-edit regression passed.");
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
        string root,
        string[] args)
    {
        var result = await git.RunGitAsync(root, args, TimeSpan.FromSeconds(15));
        if (!result.Success)
            throw new InvalidOperationException(
                $"git {string.Join(' ', args)} failed: {result.Output}");
        return result.Output;
    }
}
