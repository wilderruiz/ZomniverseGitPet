namespace ZomniverseGitPet;

public sealed record ChangedFile(string Status, string Path);

public sealed record RepositoryStatus(
    bool Healthy,
    string Branch,
    IReadOnlyList<ChangedFile> Files,
    string Error = "")
{
    public static RepositoryStatus Failure(string error) => new(false, "?", [], error);
}

public sealed record CommandResult(int ExitCode, string Output, bool TimedOut = false)
{
    public bool Success => ExitCode == 0 && !TimedOut;
}

public sealed record CheckpointResult(bool Success, string Message, string? CommitHash = null);

