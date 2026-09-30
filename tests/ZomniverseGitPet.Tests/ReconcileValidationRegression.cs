using ZomniverseGitPet;

internal static class ReconcileValidationRegression
{
    internal static async Task RunAsync()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "GitPet-validation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var git = new GitService(new AuditLog(Path.Combine(root, "audit.jsonl")));
            await MustGit(git, root, ["init"]);
            await MustGit(git, root, ["config", "user.name", "Validation Test"]);
            await MustGit(git, root, ["config", "user.email", "validation@example.invalid"]);
            await MustGit(git, root, ["branch", "-M", "main"]);

            var file = Path.Combine(root, "sample.json");
            await File.WriteAllTextAsync(file, "{\"value\":1}\n");
            await MustGit(git, root, ["add", "--", "sample.json"]);
            await MustGit(git, root, ["commit", "-m", "base"]);
            var baseline = (await MustGit(git, root, ["rev-parse", "HEAD"])).Trim();

            var service = new ReconcileValidationService(git);

            var invalid = await service.ValidateAsync(
                root,
                new ReconcileValidationRequest(
                    "sample.json",
                    baseline,
                    "{\"value\": }",
                    []),
                CancellationToken.None);
            if (invalid.SyntaxPassed || invalid.BlockingPassed)
                throw new InvalidOperationException(
                    "Invalid JSON unexpectedly passed deterministic validation.");

            var passed = await service.ValidateAsync(
                root,
                new ReconcileValidationRequest(
                    "sample.json",
                    baseline,
                    "{\"value\":2}\n",
                    ["exit /b 0"]),
                CancellationToken.None);
            if (!passed.BlockingPassed ||
                !passed.TestsPassed ||
                passed.TestsCompleted != 1)
            {
                throw new InvalidOperationException(
                    "Passing isolated saved-test validation did not report success.");
            }

            var failedTest = await service.ValidateAsync(
                root,
                new ReconcileValidationRequest(
                    "sample.json",
                    baseline,
                    "{\"value\":3}\n",
                    ["exit /b 7", "exit /b 0"]),
                CancellationToken.None);
            if (!failedTest.BlockingPassed ||
                failedTest.TestsPassed ||
                failedTest.TestsCompleted != 1)
            {
                throw new InvalidOperationException(
                    "Saved-test failure should be reported as evidence without turning valid syntax into a blocking syntax failure.");
            }

            using var cancellation = new CancellationTokenSource();
            var running = service.ValidateAsync(
                root,
                new ReconcileValidationRequest(
                    "sample.json",
                    baseline,
                    "{\"value\":4}\n",
                    ["ping 127.0.0.1 -n 30 >nul"]),
                cancellation.Token);
            await Task.Delay(250);
            cancellation.Cancel();

            try
            {
                await running;
                throw new InvalidOperationException(
                    "Validation cancellation did not propagate.");
            }
            catch (OperationCanceledException)
            {
            }

            var worktrees = await MustGit(git, root, ["worktree", "list", "--porcelain"]);
            if (worktrees.Contains(
                    "GitPet-reconcile-validate-",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Cancelled validation left a temporary worktree registered.");
            }

            Console.WriteLine(
                "Reconcile Inspector Phase 9 validation sandbox regression passed.");
        }
        finally
        {
            try
            {
                foreach (var item in Directory.EnumerateFiles(
                             root,
                             "*",
                             SearchOption.AllDirectories))
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
