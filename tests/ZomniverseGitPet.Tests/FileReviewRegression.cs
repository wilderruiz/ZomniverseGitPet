using System.Reflection;
using ZomniverseGitPet;

internal static class FileReviewRegression
{
    internal static async Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "GitPet-review-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var git = new GitService(new AuditLog(Path.Combine(root, "audit.jsonl")));
            await git.RunGitAsync(root, ["init"]);
            File.WriteAllText(Path.Combine(root, "sample.txt"), "before\n");
            await git.RunGitAsync(root, ["add", "--", "sample.txt"]);
            var saved = await git.RunGitAsync(root,
                ["-c", "user.name=Review Test", "-c", "user.email=review@example.invalid", "commit", "-m", "baseline"]);
            if (!saved.Success) throw new InvalidOperationException(saved.Output);
            File.WriteAllText(Path.Combine(root, "sample.txt"), "after\n");

            // Hold the ordinary command queue to model a slow background network operation.
            var gate = (SemaphoreSlim)typeof(GitService).GetField("_gitGate",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(git)!;
            await gate.WaitAsync();
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var commit = await git.GetReviewCommitAsync(root, timeout.Token);
                var hash = commit.Output.Split('\t')[0];
                var content = git.GetReviewContentAsync(root, "sample.txt", hash, timeout.Token);
                var diff = git.GetReviewDiffAsync(root, "sample.txt", hash, timeout.Token);
                await Task.WhenAll(content, diff);
                if (!content.Result.Success || content.Result.Output != "before" ||
                    !diff.Result.Success || !diff.Result.Output.Contains("+after"))
                    throw new InvalidOperationException("Interactive review did not load the pinned baseline and current diff.");
                timeout.Cancel();
                try
                {
                    await git.GetReviewCommitAsync(root, timeout.Token);
                    throw new InvalidOperationException("Review ignored cancellation.");
                }
                catch (OperationCanceledException) { }
            }
            finally { gate.Release(); }
            Console.WriteLine("File review queue isolation and cancellation regression passed.");
        }
        finally
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                    File.SetAttributes(file, FileAttributes.Normal);
                Directory.Delete(root, true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
