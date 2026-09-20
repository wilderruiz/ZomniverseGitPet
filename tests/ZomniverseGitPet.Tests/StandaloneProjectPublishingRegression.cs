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

            /* ==========================================================================
               PATCH: STANDALONE REMOTE BRANCH REGRESSIONS
               DATE.TIME: 2026-09-20 18:18 +03:00
               Keep standalone branch selection isolated from parent repository history.
               ========================================================================== */
            var branchList = StandaloneProjectPublishing.ParseRemoteBranches(
                "aaa\trefs/heads/main\n" +
                "bbb\trefs/heads/feature/home-agentic-v2\n" +
                "ccc\trefs/heads/legacy/home-v1.4.8-professional\n");
            if (!branchList.SequenceEqual(
                    ["main", "feature/home-agentic-v2", "legacy/home-v1.4.8-professional"]))
                throw new InvalidOperationException("Standalone remote branch parsing/order regression failed.");

            foreach (var validBranch in new[]
                     {
                         "main",
                         "feature/home-agentic-v2",
                         "legacy/home-v1.4.8-professional"
                     })
            {
                if (!StandaloneProjectPublishing.IsSafeBranchName(validBranch))
                    throw new InvalidOperationException("Valid standalone branch was rejected: " + validBranch);
            }

            foreach (var invalidBranch in new[]
                     {
                         "../escape",
                         "-danger",
                         "bad branch",
                         "bad..branch",
                         "bad~branch",
                         "bad:branch",
                         "bad\\branch",
                         "refs/heads/main"
                     })
            {
                if (StandaloneProjectPublishing.IsSafeBranchName(invalidBranch))
                    throw new InvalidOperationException("Unsafe standalone branch was accepted: " + invalidBranch);
            }

            var fetchArgs = StandaloneProjectPublishing.BuildFetchArguments(
                "feature/home-agentic-v2",
                quiet: false);
            var pushArgs = StandaloneProjectPublishing.BuildPushArguments(
                "feature/home-agentic-v2");
            if (!fetchArgs.SequenceEqual(
                    ["fetch", "--prune", "origin", "feature/home-agentic-v2"]) ||
                !pushArgs.SequenceEqual(
                    ["push", "-u", "origin", "HEAD:refs/heads/feature/home-agentic-v2"]) ||
                StandaloneProjectPublishing.RemoteTrackingRef("feature/home-agentic-v2") !=
                    "refs/remotes/origin/feature/home-agentic-v2")
                throw new InvalidOperationException("Selected standalone branch did not flow into Git command construction.");

            var legacyEntry = new StandaloneProjectPublishingEntry
            {
                ProjectId = "legacy",
                RemoteUrl = "https://example.invalid/legacy.git",
                RepositoryLabel = "legacy",
                Branch = "",
                LastPublishedSourceCommit = "legacy-source",
                LastPublishedFingerprint = "legacy-fingerprint",
                LastPublishedUtc = DateTimeOffset.Parse("2026-09-11T12:00:00Z")
            };
            StandaloneProjectPublishing.NormalizeEntry(legacyEntry);
            if (legacyEntry.Branch != "main" ||
                !legacyEntry.BranchStates.TryGetValue("main", out var migratedMain) ||
                migratedMain.LastPublishedFingerprint != "legacy-fingerprint" ||
                legacyEntry.LastPublishedFingerprint != "legacy-fingerprint")
                throw new InvalidOperationException("Legacy standalone publishing state did not migrate into main.");

            var branchStateEntry = new StandaloneProjectPublishingEntry
            {
                ProjectId = "branches",
                RemoteUrl = "https://example.invalid/branches.git",
                RepositoryLabel = "branches",
                Branch = "main"
            };
            var mainState = StandaloneProjectPublishing.GetOrCreateBranchState(branchStateEntry, "main");
            mainState.LastPublishedFingerprint = "main-fingerprint";
            mainState.LastPublishedSourceCommit = "main-source";
            var featureState = StandaloneProjectPublishing.GetOrCreateBranchState(
                branchStateEntry,
                "feature/home-agentic-v2");
            featureState.LastPublishedFingerprint = "feature-fingerprint";
            featureState.LastPublishedSourceCommit = "feature-source";

            branchStateEntry.Branch = "feature/home-agentic-v2";
            StandaloneProjectPublishing.ProjectActiveBranchState(branchStateEntry);
            if (branchStateEntry.LastPublishedFingerprint != "feature-fingerprint")
                throw new InvalidOperationException("Feature branch did not restore its own standalone baseline.");

            branchStateEntry.Branch = "main";
            StandaloneProjectPublishing.ProjectActiveBranchState(branchStateEntry);
            if (branchStateEntry.LastPublishedFingerprint != "main-fingerprint")
                throw new InvalidOperationException("Switching back to main did not restore its prior standalone baseline.");

            var mainWorkspace = StandaloneProjectPublishing.GetWorkspacePath(project, "main");
            var featureWorkspace = StandaloneProjectPublishing.GetWorkspacePath(
                project,
                "feature/home-agentic-v2");
            if (PathEquals(mainWorkspace, featureWorkspace) ||
                !featureWorkspace.StartsWith(
                    Path.Combine(mainWorkspace, "branches"),
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Standalone branch workspaces are not isolated.");

            /* ==========================================================================
               PATCH: STANDALONE GET BOUNDARY REGRESSION
               DATE.TIME: 2026-09-20 17:46 +03:00
               Incoming remote changes must remain inside the logical-project scope.
               ========================================================================== */
            var incoming = StandaloneProjectPublishing.ParseRemoteChanges(
                "A\tselected/remote.txt\n" +
                "M\tselected/keep.txt\n" +
                "R100\tselected/old.txt\tselected/new.txt\n" +
                "D\troot-selected.txt\n");
            if (incoming.Count != 4 ||
                incoming[0].Status != "A" ||
                incoming[0].Path != "selected/remote.txt" ||
                incoming[2].PreviousPath != "selected/old.txt" ||
                incoming[2].Path != "selected/new.txt")
                throw new InvalidOperationException("Standalone Get change parsing regression failed.");

            foreach (var incomingPath in new[]
                     {
                         "selected/remote.txt",
                         "selected/keep.txt",
                         "selected/old.txt",
                         "selected/new.txt",
                         "root-selected.txt"
                     })
            {
                if (!StandaloneProjectPublishing.IsPathInsideProjectScope(config, root, incomingPath))
                    throw new InvalidOperationException("Standalone Get rejected an in-scope project path: " + incomingPath);
            }

            if (StandaloneProjectPublishing.IsPathInsideProjectScope(config, root, "unrelated/never-send.txt"))
                throw new InvalidOperationException("Standalone Get allowed an unrelated parent-repository path.");

            Console.WriteLine("Standalone project publishing regression passed (scope boundary + content fingerprint + scoped Get + branch isolation).");
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    private static bool PathEquals(string left, string right) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            StringComparison.OrdinalIgnoreCase);

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
