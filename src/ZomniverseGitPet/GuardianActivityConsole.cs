using System.Diagnostics;

namespace ZomniverseGitPet;

internal sealed class GuardianActivityConsole : IDisposable
{
    private readonly RichTextBox _output;
    private readonly Label _elapsedLabel;
    private readonly Label _stateLabel;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 125 };
    private readonly Stopwatch _stopwatch = new();
    private readonly GuardianActivityHistory _history = new();
    private DateTime _lastRender = DateTime.MinValue;
    private bool _batchBright;

    internal IReadOnlyList<GuardianActivityEvent> Entries => _history.Entries;
    internal int EntryCount => _history.Entries.Count;
    internal bool HasKindSince(int index, params GuardianActivityKind[] kinds) =>
        _history.Entries.Skip(index).Any(entry => kinds.Contains(entry.Kind));

    public GuardianActivityConsole(RichTextBox output, Label elapsedLabel, Label stateLabel)
    {
        _output = output;
        _elapsedLabel = elapsedLabel;
        _stateLabel = stateLabel;
        _timer.Tick += (_, _) => Tick();
    }

    public void Begin(string message)
    {
        _stopwatch.Restart();
        _timer.Start();
        Append(new(GuardianActivityKind.OperationStarted, message));
        UpdateElapsed();
    }

    public void Append(GuardianActivityEvent activity)
    {
        _history.Add(activity);
        if (activity.Kind == GuardianActivityKind.PhaseStarted && activity.Total > 0)
        {
            _stateLabel.Text = "◌ SCANNING BATCH";
            _stateLabel.Width = 160;
        }
        else if (activity.Kind == GuardianActivityKind.FileCompleted &&
                 activity.Total > 0 && activity.Completed >= activity.Total)
        {
            _stateLabel.Text = "● WORKING";
            _stateLabel.Width = 110;
        }
        var color = activity.Kind switch
        {
            GuardianActivityKind.Success or GuardianActivityKind.OperationCompleted or GuardianActivityKind.FileCompleted
                => GuardianTheme.Healthy,
            GuardianActivityKind.Warning or GuardianActivityKind.Cancelled => GuardianTheme.Changes,
            GuardianActivityKind.Error => GuardianTheme.Warning,
            _ => _output.ForeColor
        };

        var prefix = activity.Kind switch
        {
            GuardianActivityKind.FilePending => "○ ",
            GuardianActivityKind.FileCompleted => "✓ ",
            GuardianActivityKind.Success or GuardianActivityKind.OperationCompleted => "✓ ",
            GuardianActivityKind.Warning => "⚠ ",
            GuardianActivityKind.Error => "✕ ",
            GuardianActivityKind.Cancelled => "■ ",
            GuardianActivityKind.PhaseStarted => "◌ ",
            _ => ""
        };
        var text = activity.Path is null
            ? activity.Message
            : $"{activity.Path}  {activity.Message}";
        if (activity.Total > 0 && activity.Kind is GuardianActivityKind.FileCompleted)
            text += $"  [{activity.Completed}/{activity.Total}]";

        AppendColored(prefix + text + Environment.NewLine, color);
    }

    public void AppendMessage(string message, GuardianActivityKind kind = GuardianActivityKind.Information)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        Append(new(kind, message.TrimEnd()));
    }

    public void Finish(GuardianActivityKind kind, string message)
    {
        _stopwatch.Stop();
        _timer.Stop();
        Append(new(kind, message, Elapsed: _stopwatch.Elapsed));
        UpdateElapsed();
    }

    private void Tick()
    {
        UpdateElapsed();
        if ((DateTime.UtcNow - _lastRender).TotalMilliseconds < 400) return;
        _lastRender = DateTime.UtcNow;
        _batchBright = !_batchBright;
        _elapsedLabel.ForeColor = _batchBright ? GuardianTheme.Info : GuardianTheme.MutedInk;
        if (_stateLabel.Text.Contains("SCANNING", StringComparison.Ordinal))
            _stateLabel.ForeColor = _batchBright ? GuardianTheme.Changes : GuardianTheme.Info;
    }

    private void UpdateElapsed()
    {
        var elapsed = _stopwatch.Elapsed;
        _elapsedLabel.Text = $"{(int)elapsed.TotalHours:00} hr {elapsed.Minutes:00} min {elapsed.Seconds:00} sec {elapsed.Milliseconds:000} ms";
    }

    private void AppendColored(string text, Color color)
    {
        _output.SelectionStart = _output.TextLength;
        _output.SelectionLength = 0;
        _output.SelectionColor = color;
        _output.AppendText(text);
        _output.SelectionColor = _output.ForeColor;
        _output.SelectionStart = _output.TextLength;
        _output.ScrollToCaret();
    }

    public void Dispose() => _timer.Dispose();
}

internal sealed class GuardianActivityHistory
{
    private readonly List<GuardianActivityEvent> _entries = [];
    public IReadOnlyList<GuardianActivityEvent> Entries => _entries;
    public void Add(GuardianActivityEvent activity) => _entries.Add(activity);
}
