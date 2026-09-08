using System.IO.Pipes;

namespace ZomniverseGitPet;

public sealed class ZomniverseGitPetContext : ApplicationContext
{
    private readonly AppConfig _config;
    private readonly ConfigStore _configStore;
    private readonly GitService _git;
    private readonly AuditLog _audit;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly System.Windows.Forms.Timer _timer;
    private readonly PetForm _pet;
    private GuardianForm? _guardian;
    private string _statusFingerprint = "";
    private string _lastAutomaticFingerprint = "";
    private DateTimeOffset _lastChangeAt = DateTimeOffset.UtcNow;
    private bool _automaticCheckpointRunning;

    public ZomniverseGitPetContext(string pipeName, AppConfig config, ConfigStore configStore, GitService git, AuditLog audit)
    {
        _config = config;
        _configStore = configStore;
        _git = git;
        _audit = audit;
        _pet = new PetForm(ShowGuardian, ChooseRepositoryAsync, ExitApplication);
        MainForm = _pet;
        _pet.Show();

        _timer = new System.Windows.Forms.Timer { Interval = Math.Max(5, config.PollSeconds) * 1000 };
        _timer.Tick += async (_, _) => await RefreshAsync(false);
        _timer.Start();

        _ = ListenForActivationAsync(pipeName, _lifetime.Token);
        _ = _audit.WriteAsync("app_started");
        _ = RefreshAsync(false);
    }

    private void ShowGuardian()
    {
        if (_guardian is null || _guardian.IsDisposed)
        {
            _guardian = new GuardianForm(_config, _configStore, _git, _audit, ChooseRepositoryAsync);
            _guardian.FormClosed += (_, _) => _guardian = null;
        }
        _guardian.Show();
        if (_guardian.WindowState == FormWindowState.Minimized) _guardian.WindowState = FormWindowState.Normal;
        _guardian.Activate();
        _ = _guardian.RefreshAsync();
    }

    private async Task ChooseRepositoryAsync()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose the Git repository ZomniverseGitPet should watch",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
            InitialDirectory = _config.RepositoryPath ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };
        if (dialog.ShowDialog(_pet) != DialogResult.OK) return;

        var probe = await _git.RunGitAsync(dialog.SelectedPath, ["rev-parse", "--show-toplevel"]);
        if (!probe.Success)
        {
            MessageBox.Show(_pet, "That folder is not a readable Git repository.\r\n\r\n" + probe.Output,
                "Choose repository", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _config.RepositoryPath = probe.Output.Trim();
        _configStore.Save(_config);
        await _audit.WriteAsync("repository_selected", new { repository = _config.RepositoryPath });
        await RefreshAsync(true);
    }

    private async Task RefreshAsync(bool refreshGuardian)
    {
        if (_pet.RefreshInProgress) return;
        _pet.RefreshInProgress = true;
        try
        {
            if (string.IsNullOrWhiteSpace(_config.RepositoryPath))
            {
                _pet.SetNeedsRepository();
                return;
            }
            var status = await _git.GetStatusAsync(_config.RepositoryPath, _lifetime.Token);
            _pet.SetStatus(status);
            await ConsiderAutomaticCheckpointAsync(status);
            if (refreshGuardian && _guardian is { IsDisposed: false }) await _guardian.RefreshAsync();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _pet.SetError(ex.Message);
            await _audit.WriteAsync("refresh_error", new { error = ex.Message });
        }
        finally
        {
            _pet.RefreshInProgress = false;
        }
    }

    private async Task ConsiderAutomaticCheckpointAsync(RepositoryStatus status)
    {
        if (!status.Healthy) return;
        var fingerprint = string.Join("\n", status.Files.Select(file => $"{file.Status}\t{file.Path}"));
        if (!string.Equals(fingerprint, _statusFingerprint, StringComparison.Ordinal))
        {
            _statusFingerprint = fingerprint;
            _lastChangeAt = DateTimeOffset.UtcNow;
            return;
        }
        if (!_config.AutomaticCheckpointsEnabled || _automaticCheckpointRunning || status.Files.Count == 0 ||
            fingerprint == _lastAutomaticFingerprint ||
            DateTimeOffset.UtcNow - _lastChangeAt < TimeSpan.FromMinutes(Math.Max(1, _config.QuietMinutes))) return;

        var suspicious = GitService.FindSuspiciousPaths(status.Files, _config.SuspiciousPathPatterns);
        if (suspicious.Count > 0)
        {
            await _audit.WriteAsync("automatic_checkpoint_blocked_suspicious_paths", new { files = suspicious });
            return;
        }
        if (_config.RequireTestsForAutomaticCheckpoint)
        {
            if (_config.TestCommands.Count == 0) return;
            foreach (var command in _config.TestCommands)
            {
                var test = await _git.RunTestCommandAsync(_config.RepositoryPath!, command, _lifetime.Token);
                if (!test.Success) return;
            }
        }

        _automaticCheckpointRunning = true;
        try
        {
            var result = await _git.CreateCheckpointAsync(_config.RepositoryPath!,
                $"auto-checkpoint: {DateTime.Now:yyyy-MM-dd HH:mm}", _lifetime.Token);
            if (result.Success) _lastAutomaticFingerprint = fingerprint;
        }
        finally
        {
            _automaticCheckpointRunning = false;
        }
    }

    private async Task ListenForActivationAsync(string pipeName, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(pipeName, PipeDirection.In, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(token);
                _pet.BeginInvoke(ShowGuardian);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { await _audit.WriteAsync("activation_listener_error", new { error = ex.Message }); }
        }
    }

    private void ExitApplication()
    {
        _timer.Stop();
        _lifetime.Cancel();
        _guardian?.CloseForExit();
        _pet.AllowClose = true;
        _pet.Close();
        _ = _audit.WriteAsync("app_exited");
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _lifetime.Cancel();
            _lifetime.Dispose();
            _guardian?.Dispose();
            _pet.Dispose();
        }
        base.Dispose(disposing);
    }
}

