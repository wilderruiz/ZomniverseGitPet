namespace ZomniverseGitPet;

internal sealed class GuardianRemoteWatcher : IDisposable
{
    private readonly System.Windows.Forms.Timer _timer;
    private readonly CancellationTokenSource _lifetime = new();
    private bool _busy;

    public GuardianRemoteWatcher(AppConfig config, GitService git)
    {
        GuardianSyncState.Initialize(config, git);
        _timer = new System.Windows.Forms.Timer
        {
            Interval = Math.Max(60, config.PollSeconds) * 1000
        };
        _timer.Tick += async (_, _) => await TickAsync();
        _timer.Start();
        _ = TickAsync();
    }

    private async Task TickAsync()
    {
        if (_busy || _lifetime.IsCancellationRequested) return;
        _busy = true;
        try
        {
            await GuardianSyncState.RefreshAsync(true, _lifetime.Token);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch
        {
            // Keep the last known remote state until the next poll.
        }
        finally
        {
            _busy = false;
        }
    }

    public void Dispose()
    {
        _lifetime.Cancel();
        _timer.Stop();
        _timer.Dispose();
        _lifetime.Dispose();
    }
}
