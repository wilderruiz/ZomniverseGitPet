using System.Runtime.CompilerServices;
using ZomniverseGitPet;

internal static class LongPathRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        if (!GuardianActivityConsole.NeedsLineSeparator("File Review.") ||
            GuardianActivityConsole.NeedsLineSeparator("File Review.\r\n") ||
            GuardianActivityConsole.NeedsLineSeparator(""))
            throw new InvalidOperationException("Guardian Activity line-separation regression failed.");
    }

    internal static async Task RunAsync()
    {
        await EnablesUnsetLocalSetting();
        await SkipsWriteWhenAlreadyEnabled();
        await SurfacesConfigurationFailure();
        await SkipsNonWindowsConfiguration();
        await RetriesStageExactlyOnce(successOnRetry: true);
        await RetriesStageExactlyOnce(successOnRetry: false);
        Console.WriteLine("Windows long-path regressions passed.");
    }

    private static async Task EnablesUnsetLocalSetting()
    {
        var calls = new List<IReadOnlyList<string>>();
        var service = CreateService();
        var result = await service.EnsureRepositoryLongPathsCoreAsync(
            Environment.CurrentDirectory, true, null, CancellationToken.None,
            arguments =>
            {
                calls.Add(arguments);
                return Task.FromResult(calls.Count == 1
                    ? new CommandResult(1, "")
                    : new CommandResult(0, ""));
            });
        if (!result.Enabled || !result.Changed || calls.Count != 2 ||
            !calls[0].SequenceEqual(["config", "--local", "--get", "core.longpaths"]) ||
            !calls[1].SequenceEqual(["config", "--local", "core.longpaths", "true"]) ||
            calls.SelectMany(call => call).Any(argument => argument is "--global" or "--system"))
            throw new InvalidOperationException("Unset local core.longpaths was not enabled safely.");
    }

    private static async Task SkipsWriteWhenAlreadyEnabled()
    {
        var calls = 0;
        var service = CreateService();
        var result = await service.EnsureRepositoryLongPathsCoreAsync(
            Environment.CurrentDirectory, true, null, CancellationToken.None,
            _ =>
            {
                calls++;
                return Task.FromResult(new CommandResult(0, "true"));
            });
        if (!result.Enabled || result.Changed || calls != 1)
            throw new InvalidOperationException("Enabled core.longpaths caused an unnecessary config write.");
    }

    private static async Task SurfacesConfigurationFailure()
    {
        var calls = 0;
        var service = CreateService();
        var result = await service.EnsureRepositoryLongPathsCoreAsync(
            Environment.CurrentDirectory, true, null, CancellationToken.None,
            _ => Task.FromResult(++calls == 1
                ? new CommandResult(1, "")
                : new CommandResult(1, "config denied")));
        if (result.Enabled || string.IsNullOrWhiteSpace(result.Error) || !result.Error.Contains("config denied"))
            throw new InvalidOperationException("Local core.longpaths configuration failure was not surfaced.");
    }

    private static async Task SkipsNonWindowsConfiguration()
    {
        var calls = 0;
        var result = await CreateService().EnsureRepositoryLongPathsCoreAsync(
            Environment.CurrentDirectory, false, null, CancellationToken.None,
            _ => { calls++; return Task.FromResult(new CommandResult(0, "")); });
        if (result.Applicable || calls != 0)
            throw new InvalidOperationException("Non-Windows repository attempted Windows-specific configuration.");
    }

    private static async Task RetriesStageExactlyOnce(bool successOnRetry)
    {
        var stageCalls = 0;
        var ensureCalls = 0;
        var service = CreateService();
        var result = await service.RunStageWithLongPathRecoveryAsync(
            Environment.CurrentDirectory, ["add", "-A"], null, CancellationToken.None,
            isWindowsOverride: true,
            runStageOverride: () =>
            {
                stageCalls++;
                return Task.FromResult(stageCalls == 1
                    ? new CommandResult(1, "error: filename too long\nfatal: updating files failed")
                    : successOnRetry ? new CommandResult(0, "") : new CommandResult(1, "filename too long"));
            },
            ensureOverride: () =>
            {
                ensureCalls++;
                return Task.FromResult(new RepositoryLongPathResult(true, true, true));
            });
        if (stageCalls != 2 || ensureCalls != 1 || result.Success != successOnRetry)
            throw new InvalidOperationException("Long-path staging recovery did not retry exactly once.");
    }

    private static GitService CreateService() => new(new AuditLog(
        Path.Combine(Path.GetTempPath(), "ZomniverseGitPet.Tests", "longpaths-audit.jsonl")));
}
