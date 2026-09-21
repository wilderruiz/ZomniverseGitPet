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
               PATCH: READ-ONLY PUBLISHING CACHE REGRESSION
               DATE.TIME: 2026-09-21
               Windows Git metadata can become read-only inside the preserved .git tree.
               ========================================================================== */
            var cleanupWorkspace = Path.Combine(root, "publishing-cleanup-regression");
            var cleanupGitInfo = Path.Combine(cleanupWorkspace, ".git", "objects", "info");
            var staleDirectory = Path.Combine(cleanupWorkspace, "stale", "nested");
            Directory.CreateDirectory(cleanupGitInfo);
            Directory.CreateDirectory(staleDirectory);

            var commitGraphChain = Path.Combine(cleanupGitInfo, "commit-graph-chain");
            var staleReadOnlyFile = Path.Combine(staleDirectory, "readonly.txt");
            var rootStaleFile = Path.Combine(cleanupWorkspace, "root-stale.txt");

            File.WriteAllText(commitGraphChain, "chain");
            File.WriteAllText(staleReadOnlyFile, "stale");
            File.WriteAllText(rootStaleFile, "root stale");

            File.SetAttributes(
                commitGraphChain,
                File.GetAttributes(commitGraphChain) | FileAttributes.ReadOnly);
            File.SetAttributes(
                staleReadOnlyFile,
                File.GetAttributes(staleReadOnlyFile) | FileAttributes.ReadOnly);
            File.SetAttributes(
                rootStaleFile,
                File.GetAttributes(rootStaleFile) | FileAttributes.ReadOnly);

            StandaloneProjectPublishing.CleanPublishingWorkspace(cleanupWorkspace);

            if (!Directory.Exists(Path.Combine(cleanupWorkspace, ".git")))
                throw new InvalidOperationException("Publishing cleanup deleted the preserved .git directory.");
            if (!File.Exists(commitGraphChain))
                throw new InvalidOperationException("Publishing cleanup deleted preserved Git metadata.");
            if ((File.GetAttributes(commitGraphChain) & FileAttributes.ReadOnly) != 0)
                throw new InvalidOperationException("Publishing cleanup left preserved Git metadata read-only.");
            if (Directory.Exists(Path.Combine(cleanupWorkspace, "stale")))
                throw new InvalidOperationException("Publishing cleanup did not remove a stale directory containing a read-only file.");
            if (File.Exists(rootStaleFile))
                throw new InvalidOperationException("Publishing cleanup did not remove a stale read-only root file.");

            /* ==========================================================================
               PATCH: PUBLISHING WORKSPACE SERIALIZATION REGRESSION
               DATE.TIME: 2026-09-21
               Send/Get/inspection must not mutate the same isolated .git concurrently.
               ========================================================================== */
            var gateWorkspace = Path.Combine(root, "publishing-workspace-gate");
            Directory.CreateDirectory(gateWorkspace);

            using (var firstLease = StandaloneProjectPublishing
                       .EnterWorkspaceAsync(gateWorkspace)
                       .GetAwaiter().GetResult())
            {
                var secondLeaseTask = StandaloneProjectPublishing.EnterWorkspaceAsync(gateWorkspace);

                if (secondLeaseTask.Wait(TimeSpan.FromMilliseconds(120)))
                {
                    secondLeaseTask.Result.Dispose();
                    throw new InvalidOperationException(
                        "Publishing workspace gate allowed concurrent access to the same isolated workspace.");
                }

                // firstLease is released at the end of this using scope.
            }

            using (var verifiedLease = StandaloneProjectPublishing
                       .EnterWorkspaceAsync(gateWorkspace)
                       .GetAwaiter().GetResult())
            {
            }

            if (OperatingSystem.IsWindows())
            {
                var retryWorkspace = Path.Combine(root, "publishing-retry-regression");
                var retryNested = Path.Combine(retryWorkspace, "stale");
                Directory.CreateDirectory(retryNested);
                var retryFile = Path.Combine(retryNested, "briefly-locked.txt");
                File.WriteAllText(retryFile, "lock");

                using var held = new FileStream(
                    retryFile,
                    FileMode.Open,
                    FileAccess.ReadWrite,
                    FileShare.None);

                var cleanupTask = Task.Run(() =>
                    StandaloneProjectPublishing.CleanPublishingWorkspace(retryWorkspace));

                Thread.Sleep(180);
                held.Dispose();

                if (!cleanupTask.Wait(TimeSpan.FromSeconds(3)))
                    throw new InvalidOperationException(
                        "Publishing cleanup did not recover after a transient Windows file lock.");

                cleanupTask.GetAwaiter().GetResult();

                if (Directory.Exists(retryNested))
                    throw new InvalidOperationException(
                        "Publishing cleanup left the transiently locked stale directory behind.");
            }

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

            Console.WriteLine("Standalone project publishing regression passed (scope boundary + content fingerprint + scoped Get + branch isolation + read-only cache cleanup + workspace serialization).");
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
