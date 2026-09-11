using System.Runtime.CompilerServices;
using ZomniverseGitPet;

internal static class StandaloneProjectPublishingRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "ZGitPet-standalone-publish-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            RunGit(root, "init", "-b", "main");
            RunGit(root, "config", "user.name", "GitPet Regression");
            RunGit(root, "config", "user.email", "gitpet-regression@example.invalid");

            Directory.CreateDirectory(Path.Combine(root, "selected"));
            Directory.CreateDirectory(Path.Combine(root, "unrelated"));
            File.WriteAllText(Path.Combine(root, "selected", "keep.txt"), "keep");
            File.WriteAllText(Path.Combine(root, "selected", "second.txt"), "second");
            File.WriteAllText(Path.Combine(root, "unrelated", "never-send.txt"), "private sibling");
            File.WriteAllText(Path.Combine(root, "root-selected.txt"), "root");
            RunGit(root, "add", "-A");
            RunGit(root, "commit", "-m", "baseline");

            var config = new AppConfig();
            var project = config.RememberProject(
                root,
                root,
                "Scoped publish",
                trackEverything: false,
                [
                    new ProjectScopeEntry("selected", true),
                    new ProjectScopeEntry("root-selected.txt", false)
                ]);
            LogicalProjectScopeRuntime.Initialize(config);
            var service = new GitService(new AuditLog(Path.Combine(root, "audit.jsonl")));

            if (!StandaloneProjectPublishing.IsLogicalProject(config, root))
                throw new InvalidOperationException("Scoped project was not recognized as a logical project.");

            var pathspecs = StandaloneProjectPublishing.GetPublishingPathspecs(config, root);
            if (!pathspecs.Contains("selected") ||
                !pathspecs.Contains("root-selected.txt") ||
                pathspecs.Contains("unrelated"))
                throw new InvalidOperationException("Publishing pathspecs escaped the logical project scope.");

            var firstSnapshot = StandaloneProjectPublishing
                .BuildSnapshotAsync(config, service, root)
                .GetAwaiter().GetResult()
                ?? throw new InvalidOperationException("Standalone snapshot was not created.");
            var files = firstSnapshot.Files;
            if (!files.Contains("selected/keep.txt") ||
                !files.Contains("selected/second.txt") ||
                !files.Contains("root-selected.txt") ||
                files.Contains("unrelated/never-send.txt"))
                throw new InvalidOperationException("Standalone snapshot included an unrelated parent file.");

            /* ==========================================================================
               PATCH: CONTENT FINGERPRINT REGRESSION
               DATE.TIME: 2026-09-11 20:43 +03:00
               Same-path edits must make the project publishable again.
               ========================================================================== */
            File.WriteAllText(Path.Combine(root, "selected", "keep.txt"), "keep changed");
            RunGit(root, "add", "--", "selected/keep.txt");
            RunGit(root, "commit", "-m", "change selected content");
            var secondSnapshot = StandaloneProjectPublishing
                .BuildSnapshotAsync(config, service, root)
                .GetAwaiter().GetResult()
                ?? throw new InvalidOperationException("Updated standalone snapshot was not created.");
            if (string.Equals(firstSnapshot.Fingerprint, secondSnapshot.Fingerprint, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Standalone fingerprint ignored a same-path content change.");

            var workspace = StandaloneProjectPublishing.GetWorkspacePath(project);
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!Path.GetFullPath(workspace).StartsWith(Path.GetFullPath(localAppData), StringComparison.OrdinalIgnoreCase) ||
                Path.GetFullPath(workspace).StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Publishing workspace did not stay outside the source repository.");

            if (!StandaloneProjectPublishing.RemoteEquals(
                    "https://github.com/wilderruiz/zar-ai-v2.git",
                    "https://github.com/wilderruiz/zar-ai-v2"))
                throw new InvalidOperationException("Equivalent GitHub remote URLs stopped matching.");

            Console.WriteLine("Standalone project publishing regression passed (scope boundary + content fingerprint).");
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    private static string RunGit(string workingDirectory, params string[] arguments)
    {
        using var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git.exe",
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        process.Start();
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException(output);
        return output.Trim();
    }
}
