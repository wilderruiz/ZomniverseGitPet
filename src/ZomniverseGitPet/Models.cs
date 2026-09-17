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

public enum GuardianActivityKind
{
    OperationStarted,
    PhaseStarted,
    FilePending,
    FileCompleted,
    LongPathChecking,
    LongPathAlreadyEnabled,
    LongPathEnabling,
    LongPathConfigurationFailed,
    LongPathRetrying,
    LongPathRetrySucceeded,
    LongPathRetryFailed,
    SaveStaging,
    SaveCreatingCheckpoint,
    Information,
    Success,
    Warning,
    Error,
    Cancelled,
    OperationCompleted
}

public sealed record GuardianActivityEvent(
    GuardianActivityKind Kind,
    string Message,
    string? Path = null,
    int Completed = 0,
    int Total = 0,
    TimeSpan? Elapsed = null);

public sealed record RepositoryLongPathResult(
    bool Applicable,
    bool Enabled,
    bool Changed,
    string Error = "");

public enum SaveOperationPhase
{
    Idle,
    Preparing,
    CheckingPathSupport,
    Staging,
    CreatingCheckpoint,
    Completed,
    Warning,
    Failed,
    Cancelled
}

public sealed record SaveOperationVisualState(
    SaveOperationPhase Phase,
    string Message,
    DateTimeOffset ChangedAt)
{
    public bool IsActive => Phase is SaveOperationPhase.Preparing or
        SaveOperationPhase.CheckingPathSupport or
        SaveOperationPhase.Staging or
        SaveOperationPhase.CreatingCheckpoint;
}

internal sealed class SaveOperationStateController
{
    public SaveOperationVisualState Current { get; private set; } =
        new(SaveOperationPhase.Idle, "Ready", DateTimeOffset.UtcNow);

    public event EventHandler<SaveOperationVisualState>? Changed;

    public void Transition(SaveOperationPhase phase, string? message = null)
    {
        var next = new SaveOperationVisualState(phase, message ?? DefaultMessage(phase), DateTimeOffset.UtcNow);
        if (Current.Phase == next.Phase && string.Equals(Current.Message, next.Message, StringComparison.Ordinal)) return;
        Current = next;
        Changed?.Invoke(this, next);
    }

    internal static string DefaultMessage(SaveOperationPhase phase) => phase switch
    {
        SaveOperationPhase.Preparing => "Preparing save...",
        SaveOperationPhase.CheckingPathSupport => "Checking repository path support...",
        SaveOperationPhase.Staging => "Staging files...",
        SaveOperationPhase.CreatingCheckpoint => "Creating local checkpoint...",
        SaveOperationPhase.Completed => "Save completed.",
        SaveOperationPhase.Warning => "Save needs attention.",
        SaveOperationPhase.Failed => "Save failed.",
        SaveOperationPhase.Cancelled => "Save cancelled.",
        _ => "Ready"
    };
}

public sealed record SaveStagePlan(
    IReadOnlyList<string> NormalFiles,
    IReadOnlyList<string> ApprovedIgnoredFiles,
    IReadOnlyList<string> SkippedIgnoredFiles);

