namespace ZomniverseGitPet;

internal sealed class ApplicationUpdateCoordinator : IDisposable
{
    private static readonly TimeSpan FirstCheckDelay = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan RepeatCheckDelay = TimeSpan.FromHours(6);

    private readonly ApplicationUpdateService _service;
    private readonly AuditLog _audit;
    private readonly System.Windows.Forms.Timer _timer;
    private bool _checking;
    private string _lastPromptedVersion = string.Empty;

    public ApplicationUpdateCoordinator(AuditLog audit)
    {
        _audit = audit;
        _service = new ApplicationUpdateService();
        _timer = new System.Windows.Forms.Timer
        {
            Interval = (int)FirstCheckDelay.TotalMilliseconds
        };
        _timer.Tick += OnTimerTick;
    }

    public void Start()
    {
        // Development/portable builds stay on the developer workflow and never
        // self-update. Only a copy installed by the GitPet installer participates.
        if (!_service.IsInstalledBuild) return;
        _timer.Start();
    }

    private async void OnTimerTick(object? sender, EventArgs e)
    {
        _timer.Stop();
        await CheckAsync();
        if (!_timer.Enabled)
        {
            _timer.Interval = (int)RepeatCheckDelay.TotalMilliseconds;
            _timer.Start();
        }
    }

    private async Task CheckAsync()
    {
        if (_checking) return;
        _checking = true;

        try
        {
            var update = await _service.CheckForUpdateAsync(CancellationToken.None);
            if (update is null) return;
            if (string.Equals(_lastPromptedVersion, update.AvailableVersion, StringComparison.OrdinalIgnoreCase)) return;

            _lastPromptedVersion = update.AvailableVersion;
            await _audit.WriteAsync("application_update_available", new
            {
                installed = update.CurrentVersion,
                available = update.AvailableVersion,
                tag = update.ReleaseTag
            });

            using var prompt = new ApplicationUpdateForm(update);
            if (prompt.ShowDialog() != DialogResult.OK)
            {
                await _audit.WriteAsync("application_update_deferred", new
                {
                    available = update.AvailableVersion
                });
                return;
            }

            using var progressForm = new ApplicationUpdateProgressForm(update.AvailableVersion);
            progressForm.Show();
            progressForm.BringToFront();
            progressForm.Refresh();

            var progress = new Progress<int>(progressForm.SetProgress);
            string installerPath;
            try
            {
                installerPath = await _service.DownloadAndVerifyInstallerAsync(
                    update,
                    progress,
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                progressForm.Hide();
                await _audit.WriteAsync("application_update_download_failed", new
                {
                    available = update.AvailableVersion,
                    error = ex.Message
                });

                using var failed = new GuardianConfirmDialog(
                    "Update ZomniverseGitPet",
                    "UPDATE NEEDS ATTENTION",
                    "GitPet could not download and verify the update.\r\n\r\n" +
                    ex.Message +
                    "\r\n\r\nNothing was installed.",
                    "OK",
                    "",
                    showCancel: false);
                failed.ShowDialog();
                return;
            }

            progressForm.Hide();

            try
            {
                ApplicationUpdateService.LaunchInstaller(installerPath);
                await _audit.WriteAsync("application_update_installer_started", new
                {
                    available = update.AvailableVersion,
                    installer = installerPath
                });

                Application.Exit();
            }
            catch (Exception ex)
            {
                await _audit.WriteAsync("application_update_installer_start_failed", new
                {
                    available = update.AvailableVersion,
                    error = ex.Message
                });

                using var failed = new GuardianConfirmDialog(
                    "Update ZomniverseGitPet",
                    "INSTALLER COULD NOT START",
                    "The update package was downloaded and verified, but Windows could not start the installer.\r\n\r\n" +
                    ex.Message,
                    "OK",
                    "",
                    showCancel: false);
                failed.ShowDialog();
            }
        }
        catch (Exception ex)
        {
            // Background update discovery must never interfere with Git work or
            // show network-error dialogs. Record it quietly and retry later.
            await _audit.WriteAsync("application_update_check_failed", new { error = ex.Message });
        }
        finally
        {
            _checking = false;
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        _timer.Dispose();
    }
}
