using System.Runtime.CompilerServices;
using ZomniverseGitPet;

internal static class SavePreflightBatchRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        Console.WriteLine("Running save preflight batch regressions...");
        ParseBatch();
        RejectMalformedBatch();
        RetainFailures();
        Console.WriteLine("Save preflight parsing and activity-history regressions passed.");
    }

    internal static async Task RunAsync()
    {
        Console.WriteLine("  exercising 352-file batch...");
        await BatchHundredsOfFiles();
        Console.WriteLine("  exercising cancellation...");
        await Cancellation();
        Console.WriteLine("Save preflight batch regressions passed.");
    }

    private static void ParseBatch()
    {
        var output = string.Join('\0',
            ".gitignore", "4", "bin/", "src/a/bin/a.dll",
            "src/.gitignore", "9", "obj/", "src/b/obj/δ.props") + "\0";
        if (!IgnoredFileSavePolicy.TryParseCheckIgnoreBatch(output, out var items, out var error) ||
            error.Length > 0 || items.Count != 2 ||
            items[0] is not { IgnoreLine: 4, Rule: "bin/", Path: "src/a/bin/a.dll" } ||
            items[1] is not { IgnoreSource: "src/.gitignore", IgnoreLine: 9, Path: "src/b/obj/δ.props" })
            throw new InvalidOperationException("Batched check-ignore provenance parsing regression failed.");
    }

    private static void RejectMalformedBatch()
    {
        if (IgnoredFileSavePolicy.TryParseCheckIgnoreBatch(".gitignore\04\0bin/\0", out _, out var error) ||
            string.IsNullOrWhiteSpace(error))
            throw new InvalidOperationException("Malformed batched provenance was not rejected safely.");
    }

    private static void RetainFailures()
    {
        var history = new GuardianActivityHistory();
        history.Add(new(GuardianActivityKind.Error, "first failure"));
        history.Add(new(GuardianActivityKind.Success, "later success"));
        if (history.Entries.Count != 2 || history.Entries[0].Kind != GuardianActivityKind.Error)
            throw new InvalidOperationException("Guardian Activity discarded a prior failure.");
    }

    private static async Task BatchHundredsOfFiles()
    {
        var root = CreateRepository();
        try
        {
            File.WriteAllText(Path.Combine(root, ".gitignore"), "generated/\n");
            Directory.CreateDirectory(Path.Combine(root, "generated"));
            for (var index = 0; index < 352; index++)
                File.WriteAllText(Path.Combine(root, "generated", $"item-{index:000}.tmp"), "generated");

            var config = new AppConfig();
            config.RememberProject(root, root, "Batch regression", trackEverything: false,
                [new ProjectScopeEntry("generated", true)]);
            LogicalProjectScopeRuntime.Initialize(config);
            var service = new GitService(new AuditLog(Path.Combine(root, "audit.jsonl")));
            var result = await service.GetSavePreflightAsync(root);

            var checkIgnoreLaunches = service.LaunchedGitSubcommands.Count(command => command == "check-ignore");
            var listLaunches = service.LaunchedGitSubcommands.Count(command => command == "ls-files");
            if (!result.Success || result.IgnoredChangedFiles.Count != 352 ||
                checkIgnoreLaunches != 1 || listLaunches != 1)
                throw new InvalidOperationException(
                    $"Batch preflight launched ls-files={listLaunches}, check-ignore={checkIgnoreLaunches}, " +
                    $"ignored={result.IgnoredChangedFiles.Count}, error={result.Error}");

            var plan = IgnoredFileSavePolicy.CreateStagePlan([], result.IgnoredChangedFiles, []);
            if (plan.ApprovedIgnoredFiles.Count != 0 || plan.SkippedIgnoredFiles.Count != 352)
                throw new InvalidOperationException("Batched preflight weakened ignored-file approval safety.");
        }
        finally { TryDelete(root); }
    }

    private static async Task Cancellation()
    {
        var root = CreateRepository();
        try
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var service = new GitService(new AuditLog(Path.Combine(root, "audit.jsonl")));
            try
            {
                await service.GetSavePreflightAsync(root, cancellation.Token);
                throw new InvalidOperationException("Cancelled preflight unexpectedly completed.");
            }
            catch (OperationCanceledException)
            {
            }
        }
        finally { TryDelete(root); }
    }

    private static string CreateRepository()
    {
        var root = Path.Combine(Path.GetTempPath(), "ZomniverseGitPet.Tests", "batch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        RunGit(root, "init");
        return root;
    }

    private static void RunGit(string root, params string[] arguments)
    {
        using var process = new System.Diagnostics.Process
        {
            StartInfo = new()
            {
                FileName = "git.exe",
                WorkingDirectory = root,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        process.Start();
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException("Test repository setup failed.");
    }

    private static void TryDelete(string root)
    {
        try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
    }
}
