namespace ZomniverseGitPet;

internal sealed class GuardianProjectSwitchOverlay : Panel
{
    private readonly GuardianProgressPanel _progress = new();

    public GuardianProjectSwitchOverlay(string projectName)
    {
        Dock = DockStyle.None;
        BackColor = GuardianTheme.Window;
        TabStop = true;
        _progress.Configure("SWITCHING PROJECT...", projectName);
        _progress.Start("Preparing project");
        Controls.Add(_progress);
        Layout += (_, _) => CenterProgress();
        Resize += (_, _) => CenterProgress();
        CenterProgress();
    }

    public void Apply(ProjectSwitchVisualState state)
    {
        if (IsDisposed) return;
        _progress.SetSubtitle(state.ProjectName);
        _progress.SetStage(state.Message);
    }

    public void Stop() => _progress.Stop();

    private void CenterProgress()
    {
        _progress.Location = new Point(
            Math.Max(0, (ClientSize.Width - _progress.Width) / 2),
            Math.Max(0, (ClientSize.Height - _progress.Height) / 2));
    }
}

internal static class GuardianProjectSwitchOverlayHost
{
    public static IDisposable Begin(GuardianForm? guardian, string projectName)
    {
        if (guardian is null || guardian.IsDisposed || !guardian.Visible)
            return NoopDisposable.Instance;
        return new Session(guardian, projectName);
    }

    private sealed class Session : IDisposable
    {
        private readonly GuardianForm _guardian;
        private readonly GuardianProjectSwitchOverlay _overlay;
        private readonly RichTextBox? _activity;
        private readonly Dictionary<Control, bool> _enabledStates = [];
        private ProjectSwitchPhase? _lastPhase;
        private bool _disposed;

        public Session(GuardianForm guardian, string projectName)
        {
            _guardian = guardian;
            _activity = EnumerateControls(guardian)
                .OfType<RichTextBox>()
                .FirstOrDefault(box => box.ReadOnly && box.BackColor.ToArgb() == GuardianTheme.Console.ToArgb());

            foreach (Control control in guardian.Controls)
            {
                _enabledStates[control] = control.Enabled;
                control.Enabled = false;
            }

            _overlay = new GuardianProjectSwitchOverlay(projectName)
            {
                Bounds = guardian.ClientRectangle,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Enabled = true
            };
            guardian.Controls.Add(_overlay);
            _overlay.BringToFront();
            _overlay.Focus();

            AppendActivity($"○ Switching to {projectName}...", GuardianTheme.Info);
            ProjectSwitchRuntime.Changed += OnChanged;
        }

        private void OnChanged(object? sender, ProjectSwitchVisualState state)
        {
            if (_disposed || _guardian.IsDisposed) return;
            if (_guardian.InvokeRequired)
            {
                try { _guardian.BeginInvoke((Action)(() => Apply(state))); }
                catch (InvalidOperationException) { }
                return;
            }
            Apply(state);
        }

        private void Apply(ProjectSwitchVisualState state)
        {
            if (_disposed || _overlay.IsDisposed) return;
            _overlay.Apply(state);
            if (_lastPhase == state.Phase) return;
            _lastPhase = state.Phase;

            switch (state.Phase)
            {
                case ProjectSwitchPhase.Preparing:
                    AppendActivity("○ Preparing project...", GuardianTheme.Info);
                    break;
                case ProjectSwitchPhase.VerifyingRepository:
                case ProjectSwitchPhase.LoadingRepository:
                case ProjectSwitchPhase.LoadingScope:
                case ProjectSwitchPhase.ActivatingProject:
                case ProjectSwitchPhase.LoadingRemoteState:
                case ProjectSwitchPhase.PreparingWorkboard:
                    AppendActivity("○ " + state.Message, GuardianTheme.Info);
                    break;
                case ProjectSwitchPhase.Completed:
                    AppendActivity("✓ Project ready", GuardianTheme.Healthy);
                    if (state.Elapsed is TimeSpan elapsed)
                        AppendActivity($"✓ Switched in {FormatElapsed(elapsed)}", GuardianTheme.Healthy);
                    break;
                case ProjectSwitchPhase.Failed:
                    AppendActivity("✕ PROJECT COULD NOT BE OPENED", GuardianTheme.Warning);
                    if (!string.IsNullOrWhiteSpace(state.Error))
                        AppendActivity("✕ " + state.Error, GuardianTheme.Warning);
                    break;
                case ProjectSwitchPhase.Cancelled:
                    AppendActivity("■ Project switch cancelled.", GuardianTheme.Changes);
                    break;
            }
        }

        private void AppendActivity(string text, Color color)
        {
            if (_activity is null || _activity.IsDisposed) return;
            if (_activity.TextLength > 0 && _activity.Text[^1] is not ('\r' or '\n'))
                _activity.AppendText(Environment.NewLine);
            _activity.SelectionStart = _activity.TextLength;
            _activity.SelectionLength = 0;
            _activity.SelectionColor = color;
            _activity.AppendText(text + Environment.NewLine);
            _activity.SelectionColor = _activity.ForeColor;
            _activity.SelectionStart = _activity.TextLength;
            _activity.ScrollToCaret();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            ProjectSwitchRuntime.Changed -= OnChanged;
            _overlay.Stop();

            if (!_guardian.IsDisposed)
            {
                if (!_overlay.IsDisposed) _guardian.Controls.Remove(_overlay);
                foreach (var pair in _enabledStates)
                {
                    if (!pair.Key.IsDisposed) pair.Key.Enabled = pair.Value;
                }
            }

            _overlay.Dispose();
        }
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static NoopDisposable Instance { get; } = new();
        public void Dispose() { }
    }

    private static string FormatElapsed(TimeSpan elapsed) =>
        $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}.{elapsed.Milliseconds:000}";

    private static IEnumerable<Control> EnumerateControls(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var descendant in EnumerateControls(child))
                yield return descendant;
        }
    }
}
