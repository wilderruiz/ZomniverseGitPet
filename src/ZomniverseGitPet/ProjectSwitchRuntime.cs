using System.Diagnostics;

namespace ZomniverseGitPet;

internal enum ProjectSwitchPhase
{
    Idle,
    Preparing,
    VerifyingRepository,
    LoadingRepository,
    LoadingScope,
    ActivatingProject,
    LoadingRemoteState,
    PreparingWorkboard,
    Completed,
    Failed,
    Cancelled
}

internal sealed record ProjectSwitchVisualState(
    ProjectSwitchPhase Phase,
    string ProjectId,
    string ProjectName,
    string Message,
    DateTimeOffset ChangedAt,
    TimeSpan? Elapsed = null,
    string? Error = null)
{
    public bool IsActive => Phase is ProjectSwitchPhase.Preparing or
        ProjectSwitchPhase.VerifyingRepository or
        ProjectSwitchPhase.LoadingRepository or
        ProjectSwitchPhase.LoadingScope or
        ProjectSwitchPhase.ActivatingProject or
        ProjectSwitchPhase.LoadingRemoteState or
        ProjectSwitchPhase.PreparingWorkboard;

    public static ProjectSwitchVisualState Idle { get; } = new(
        ProjectSwitchPhase.Idle,
        "",
        "",
        "Ready",
        DateTimeOffset.UtcNow);
}

internal static class ProjectSwitchRuntime
{
    private static readonly object Gate = new();
    private static readonly Stopwatch Stopwatch = new();
    private static ProjectSwitchVisualState _current = ProjectSwitchVisualState.Idle;

    public static event EventHandler<ProjectSwitchVisualState>? Changed;

    public static ProjectSwitchVisualState Current
    {
        get
        {
            lock (Gate) return _current;
        }
    }

    public static bool IsSwitching => Current.IsActive;

    public static void Begin(string projectId, string projectName)
    {
        Stopwatch.Restart();
        Publish(new ProjectSwitchVisualState(
            ProjectSwitchPhase.Preparing,
            projectId,
            projectName,
            "Preparing project...",
            DateTimeOffset.UtcNow));
    }

    public static void Transition(ProjectSwitchPhase phase, string message)
    {
        var current = Current;
        if (!current.IsActive) return;
        Publish(current with
        {
            Phase = phase,
            Message = message,
            ChangedAt = DateTimeOffset.UtcNow,
            Elapsed = Stopwatch.Elapsed,
            Error = null
        });
    }

    public static void Complete(TimeSpan? elapsed = null)
    {
        var current = Current;
        Stopwatch.Stop();
        Publish(current with
        {
            Phase = ProjectSwitchPhase.Completed,
            Message = "Ready ✓",
            ChangedAt = DateTimeOffset.UtcNow,
            Elapsed = elapsed ?? Stopwatch.Elapsed,
            Error = null
        });
    }

    public static void Fail(string error, TimeSpan? elapsed = null)
    {
        var current = Current;
        Stopwatch.Stop();
        Publish(current with
        {
            Phase = ProjectSwitchPhase.Failed,
            Message = "Project could not be opened",
            ChangedAt = DateTimeOffset.UtcNow,
            Elapsed = elapsed ?? Stopwatch.Elapsed,
            Error = error
        });
    }

    public static void Cancel(TimeSpan? elapsed = null)
    {
        var current = Current;
        Stopwatch.Stop();
        Publish(current with
        {
            Phase = ProjectSwitchPhase.Cancelled,
            Message = "Project switch cancelled",
            ChangedAt = DateTimeOffset.UtcNow,
            Elapsed = elapsed ?? Stopwatch.Elapsed
        });
    }

    internal static void ResetForTests() =>
        Publish(ProjectSwitchVisualState.Idle with { ChangedAt = DateTimeOffset.UtcNow });

    private static void Publish(ProjectSwitchVisualState next)
    {
        EventHandler<ProjectSwitchVisualState>? changed;
        lock (Gate)
        {
            _current = next;
            changed = Changed;
        }
        changed?.Invoke(null, next);
    }
}
