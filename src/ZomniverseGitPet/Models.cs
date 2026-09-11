namespace ZomniverseGitPet;

public sealed record ChangedFile(string Status, string Path);

public sealed record RepositoryStatus(
    bool Healthy,
    string Branch,
    IReadOnlyList<ChangedFile> Files,
    string Error = "",
    int Ahead = 0,
    int Behind = 0,
    bool HasTrackingInformation = false)
{
    public static RepositoryStatus Failure(string error) => new(false, "?", [], error);
}

public sealed record CommandResult(int ExitCode, string Output, bool TimedOut = false)
{
    public bool Success => ExitCode == 0 && !TimedOut;
}

public sealed record CheckpointResult(
    bool Success,
    string Message,
    string? CommitHash = null,
    int SavedFileCount = 0,
    int SkippedIgnoredCount = 0);

public sealed record IgnoredProjectFile(
    string Path,
    string IgnoreSource,
    int? IgnoreLine,
    string Rule);

public sealed record SavePreflightResult(
    bool Success,
    IReadOnlyList<string> NormalChangedFiles,
    IReadOnlyList<IgnoredProjectFile> IgnoredChangedFiles,
    string Error = "");

public sealed record SaveStagePlan(
    IReadOnlyList<string> NormalFiles,
    IReadOnlyList<string> ApprovedIgnoredFiles,
    IReadOnlyList<string> SkippedIgnoredFiles);

