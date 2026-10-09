using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace ZomniverseGitPet;

/* ========================================================================== 
   PATCH: REVIEW-FIRST PUBLICATION WORKSPACE COMPATIBILITY
   DATE.TIME: 2026-10-09 13:23 +03:00

   RUNTIME CORRECTION
   DATE.TIME: 2026-10-09 13:52 +03:00

   OWNER DECISION (Wilder Ruiz, 2026-10-09)
   -----------------------------------------
   The public Zomniverse project is an independently curated, review-first
   publication surface. It is not a mirror of the private Zomniverse project.

   Two important consequences are enforced here:

   1. The scoped-mirror Get/Send runtime must not race this publication runtime.
      While ZOMNIVERSE-PROJECT is active, the legacy standalone UI timer is
      suspended instead of allowing two timers to alternately show/hide buttons.
      It is resumed when the user leaves publication mode.

   2. GitPet must not silently choose Documents/OneDrive as the checkout location.
      On first Public Get, the user chooses where the independent checkout lives.
      The selected workspace is remembered locally under LocalApplicationData and
      is never written into either Git repository.

   Existing scoped-mirror projects retain their original behavior unchanged.
   ========================================================================== */
internal static class PublicationWorkspaceCompatibilityRuntime
{
    private const string TargetOwner = "wilderruiz";
    private const string TargetRepository = "zomniverse-project";
    private const string PublicBoundaryMarker = "WHAT_IS_PUBLISHED_HERE.md";

    private static readonly BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
    private static System.Windows.Forms.Timer? _timer;
    private static bool _tickRunning;
    private static bool _operationRunning;
    private static bool _legacyTimerSuppressed;
    private static AppConfig? _config;

    private static readonly Dictionary<GuardianForm, GuardianActionButton> GetButtons = [];
    private static readonly Dictionary<GuardianForm, GuardianActionButton> OpenButtons = [];

    private sealed record GitRunResult(bool Success, int ExitCode, string Output);

    [ModuleInitializer]
    internal static void InitializeModule()
    {
        Application.Idle += StartWhenReady;
    }

    private static void StartWhenReady(object? sender, EventArgs e)
    {
        if (_timer is not null) return;

        _config = typeof(StandaloneProjectPublishingUiRuntime)
            .GetField("_config", StaticPrivate)?
            .GetValue(null) as AppConfig;

        if (_config is null) return;

        Application.Idle -= StartWhenReady;
        _timer = new System.Windows.Forms.Timer { Interval = 500 };
        _timer.Tick += async (_, _) => await TickAsync();
        _timer.Start();

        Application.ApplicationExit += (_, _) =>
        {
            ResumeLegacyStandaloneTimer();
            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;
            GetButtons.Clear();
            OpenButtons.Clear();
        };
    }

    private static Task TickAsync()
    {
        if (_tickRunning || _config is null) return Task.CompletedTask;
        _tickRunning = true;
        try
        {
            var project = _config.GetActiveProject();
            var link = StandaloneProjectPublishing.GetLink(_config);
            var publicationMode = project is not null &&
                                  link is not null &&
                                  IsTargetPublicationRemote(link.RemoteUrl);

            SetLegacyStandaloneTimerSuppressed(publicationMode);

            var guardians = Application.OpenForms
                .OfType<GuardianForm>()
                .Where(form => !form.IsDisposed)
                .ToArray();

            foreach (var stale in GetButtons.Keys.Where(form => !guardians.Contains(form)).ToArray())
                GetButtons.Remove(stale);
            foreach (var stale in OpenButtons.Keys.Where(form => !guardians.Contains(form)).ToArray())
                OpenButtons.Remove(stale);

            foreach (var guardian in guardians)
                UpdateGuardian(guardian, project, link, publicationMode);
        }
        finally
        {
            _tickRunning = false;
        }

        return Task.CompletedTask;
    }

    private static void UpdateGuardian(
        GuardianForm guardian,
        RecentRepositoryEntry? project = null,
        StandaloneProjectPublishingEntry? link = null,
        bool? publicationModeOverride = null)
    {
        if (_config is null) return;

        project ??= _config.GetActiveProject();
        link ??= StandaloneProjectPublishing.GetLink(_config);
        var publicationMode = publicationModeOverride ??
                              (project is not null && link is not null && IsTargetPublicationRemote(link.RemoteUrl));

        var oldGet = FindControl(guardian, "StandaloneProjectGetButton") as GuardianActionButton;
        var oldSend = FindControl(guardian, "StandaloneProjectSendButton") as GuardianActionButton;
        var branch = FindControl(guardian, "StandaloneProjectBranchButton") as GuardianActionButton;

        if (!publicationMode)
        {
            if (GetButtons.TryGetValue(guardian, out var get)) get.Visible = false;
            if (OpenButtons.TryGetValue(guardian, out var open)) open.Visible = false;
            return;
        }

        SetLegacyStandaloneTimerSuppressed(true);

        if (oldGet is null || oldSend is null || oldGet.Parent is null) return;
        EnsureButtons(guardian, oldGet);

        oldGet.Visible = false;
        oldGet.Enabled = false;
        oldSend.Visible = false;
        oldSend.Enabled = false;

        if (branch is not null) branch.Visible = true;

        var workspace = project is null || link is null
            ? null
            : GetSavedWorkspacePath(link.Branch);
        var workspaceExists = !string.IsNullOrWhiteSpace(workspace) &&
                              Directory.Exists(Path.Combine(workspace, ".git"));

        var publicGet = GetButtons[guardian];
        publicGet.Visible = true;
        publicGet.Enabled = !_operationRunning;
        publicGet.Text = _operationRunning ? "Public Get…" : "Public Get ↓";
        publicGet.Cursor = publicGet.Enabled ? Cursors.Hand : Cursors.Default;

        var publicOpen = OpenButtons[guardian];
        publicOpen.Visible = true;
        publicOpen.Enabled = workspaceExists && !_operationRunning;
        publicOpen.Text = "Open public";
        publicOpen.Cursor = publicOpen.Enabled ? Cursors.Hand : Cursors.Default;
    }

    private static void EnsureButtons(GuardianForm guardian, GuardianActionButton oldGet)
    {
        if (!GetButtons.ContainsKey(guardian))
        {
            var button = new GuardianActionButton
            {
                Name = "PublicationWorkspaceGetButton",
                Text = "Public Get ↓",
                Width = 112,
                Kind = GuardianActionKind.Pull,
                SyncStateAware = false,
                Margin = oldGet.Margin
            };
            button.Click += async (_, _) => await GetPublicationWorkspaceAsync(guardian);
            oldGet.Parent!.Controls.Add(button);
            oldGet.Parent.Controls.SetChildIndex(button, oldGet.Parent.Controls.GetChildIndex(oldGet));
            GetButtons[guardian] = button;
        }

        if (!OpenButtons.ContainsKey(guardian))
        {
            var button = new GuardianActionButton
            {
                Name = "PublicationWorkspaceOpenButton",
                Text = "Open public",
                Width = 105,
                Kind = GuardianActionKind.Standard,
                SyncStateAware = false,
                Margin = oldGet.Margin
            };
            button.Click += (_, _) => OpenPublicationWorkspace(guardian);
            oldGet.Parent!.Controls.Add(button);
            var getIndex = oldGet.Parent.Controls.GetChildIndex(GetButtons[guardian]);
            oldGet.Parent.Controls.SetChildIndex(button, Math.Min(getIndex + 1, oldGet.Parent.Controls.Count - 1));
            OpenButtons[guardian] = button;
        }

        guardian.Disposed += (_, _) =>
        {
            GetButtons.Remove(guardian);
            OpenButtons.Remove(guardian);
        };
    }

    private static async Task GetPublicationWorkspaceAsync(GuardianForm guardian)
    {
        if (_config is null || _operationRunning) return;
        var project = _config.GetActiveProject();
        var link = StandaloneProjectPublishing.GetLink(_config);
        if (project is null || link is null || !IsTargetPublicationRemote(link.RemoteUrl)) return;

        SetLegacyStandaloneTimerSuppressed(true);

        var workspace = GetSavedWorkspacePath(link.Branch);
        if (string.IsNullOrWhiteSpace(workspace))
        {
            workspace = ChooseWorkspacePath(guardian);
            if (string.IsNullOrWhiteSpace(workspace)) return;
        }

        using var confirmation = new GuardianConfirmDialog(
            "Get public publication workspace",
            "REVIEW-FIRST PUBLICATION WORKSPACE",
            $"Update the independent public checkout from:\r\n{link.RepositoryLabel} / {link.Branch}\r\n\r\n" +
            $"Local publication workspace:\r\n{workspace}\r\n\r\n" +
            "This does NOT copy public-only files into the private Zomniverse repository and does NOT publish anything automatically. " +
            "The chosen workspace remains independent and can be used by local coding agents for reviewed public work.",
            "Get public ↓",
            "Cancel",
            confirmWidth: 150,
            dialogSize: new Size(820, 560),
            scrollable: true,
            resizable: true);

        if (confirmation.ShowDialog(guardian) != DialogResult.Yes) return;

        _operationRunning = true;
        guardian.UseWaitCursor = true;
        UpdateGuardian(guardian, project, link, true);
        try
        {
            var result = await EnsurePublicationWorkspaceAsync(link.RemoteUrl, link.Branch, workspace);
            if (!result.Success)
            {
                using var problem = new GuardianConfirmDialog(
                    "Public publication workspace",
                    "PUBLIC GET NEEDS REVIEW",
                    result.Message,
                    "OK",
                    showCancel: false,
                    dialogSize: new Size(820, 540),
                    scrollable: true,
                    resizable: true);
                problem.ShowDialog(guardian);
                return;
            }

            SaveWorkspacePath(link.Branch, workspace);

            var marker = Path.Combine(workspace, PublicBoundaryMarker);
            var markerNote = File.Exists(marker)
                ? $"\r\n\r\nPublication boundary marker verified: {PublicBoundaryMarker}"
                : $"\r\n\r\nWARNING: {PublicBoundaryMarker} is missing. Review this checkout before treating it as the curated public surface.";

            using var ready = new GuardianConfirmDialog(
                "Public publication workspace",
                "PUBLIC WORKSPACE READY  ✓",
                $"The public repository is synchronized in the location you selected:\r\n\r\n{workspace}" +
                markerNote +
                "\r\n\r\nThe private Zomniverse working tree was not modified. GitPet will remember this local publication-workspace location for the selected branch.",
                "Open folder",
                "Close",
                confirmWidth: 140,
                dialogSize: new Size(820, 560),
                scrollable: true,
                resizable: true);

            if (ready.ShowDialog(guardian) == DialogResult.Yes)
                OpenFolder(workspace);
        }
        finally
        {
            guardian.UseWaitCursor = false;
            _operationRunning = false;
            UpdateGuardian(guardian, project, link, true);
        }
    }

    private static string? ChooseWorkspacePath(IWin32Window owner)
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        using var picker = new FolderBrowserDialog
        {
            Description =
                "Choose where ZGitPet should keep the PUBLIC zomniverse-project checkout. " +
                "Choose a parent folder and GitPet will use/create a zomniverse-project folder inside it. " +
                "You may also select an existing zomniverse-project Git checkout directly.",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
            InitialDirectory = Directory.Exists(userProfile) ? userProfile : string.Empty
        };

        if (picker.ShowDialog(owner) != DialogResult.OK || string.IsNullOrWhiteSpace(picker.SelectedPath))
            return null;

        var selected = Path.GetFullPath(picker.SelectedPath);
        if (Directory.Exists(Path.Combine(selected, ".git")))
            return selected;

        return Path.Combine(selected, TargetRepository);
    }

    private static void OpenPublicationWorkspace(GuardianForm guardian)
    {
        if (_config is null || _operationRunning) return;
        var link = StandaloneProjectPublishing.GetLink(_config);
        if (link is null || !IsTargetPublicationRemote(link.RemoteUrl)) return;

        var workspace = GetSavedWorkspacePath(link.Branch);
        if (string.IsNullOrWhiteSpace(workspace) || !Directory.Exists(Path.Combine(workspace, ".git")))
        {
            using var missing = new GuardianConfirmDialog(
                "Public publication workspace",
                "CHOOSE PUBLIC WORKSPACE FIRST",
                "No explicit local publication-workspace location is configured yet. Use Public Get and choose where the public checkout should live.",
                "OK",
                showCancel: false);
            missing.ShowDialog(guardian);
            return;
        }

        OpenFolder(workspace);
    }

    private static async Task<(bool Success, string Message)> EnsurePublicationWorkspaceAsync(
        string remoteUrl,
        string branch,
        string workspace)
    {
        workspace = Path.GetFullPath(workspace);
        var parent = Path.GetDirectoryName(workspace);
        if (string.IsNullOrWhiteSpace(parent))
            return (false, "The selected publication-workspace path is not valid.");

        Directory.CreateDirectory(parent);

        if (!Directory.Exists(Path.Combine(workspace, ".git")))
        {
            if (Directory.Exists(workspace) && Directory.EnumerateFileSystemEntries(workspace).Any())
            {
                return (false,
                    "The selected publication-workspace folder already contains files but is not a Git checkout. GitPet left it untouched:\r\n\r\n" +
                    workspace +
                    "\r\n\r\nChoose a different parent folder, an empty folder, or the existing zomniverse-project Git checkout.");
            }

            var clone = await RunGitAsync(
                parent,
                ["clone", "--branch", branch, "--single-branch", remoteUrl, workspace],
                TimeSpan.FromMinutes(5));
            if (!clone.Success)
                return (false, "GitPet could not clone the public publication repository. Nothing in the private repository was changed.\r\n\r\n" + clone.Output);
        }

        var dirty = await RunGitAsync(workspace, ["status", "--porcelain"], TimeSpan.FromSeconds(30));
        if (!dirty.Success)
            return (false, "GitPet could not inspect the public publication checkout.\r\n\r\n" + dirty.Output);
        if (!string.IsNullOrWhiteSpace(dirty.Output))
        {
            return (false,
                "The public publication workspace has local changes. GitPet will not overwrite or merge them automatically.\r\n\r\n" +
                dirty.Output.Trim() +
                "\r\n\r\nReview/commit/discard those public-workspace changes first, then use Public Get again.");
        }

        var origin = await RunGitAsync(workspace, ["remote", "get-url", "origin"], TimeSpan.FromSeconds(20));
        if (!origin.Success)
            return (false, "The public publication checkout has no readable origin remote.\r\n\r\n" + origin.Output);

        if (!RemoteEquals(origin.Output.Trim(), remoteUrl))
        {
            var setOrigin = await RunGitAsync(workspace, ["remote", "set-url", "origin", remoteUrl], TimeSpan.FromSeconds(20));
            if (!setOrigin.Success)
                return (false, "GitPet could not align the publication checkout origin.\r\n\r\n" + setOrigin.Output);
        }

        var fetch = await RunGitAsync(workspace, ["fetch", "--prune", "origin", branch], TimeSpan.FromMinutes(3));
        if (!fetch.Success)
            return (false, "GitPet could not fetch the public publication branch.\r\n\r\n" + fetch.Output);

        var remoteRef = "refs/remotes/origin/" + branch;
        var verify = await RunGitAsync(workspace, ["rev-parse", "--verify", remoteRef], TimeSpan.FromSeconds(20));
        if (!verify.Success)
            return (false, $"The public repository does not expose a readable '{branch}' branch.\r\n\r\n" + verify.Output);

        var checkout = await RunGitAsync(workspace, ["checkout", "-B", branch, remoteRef], TimeSpan.FromMinutes(1));
        if (!checkout.Success)
            return (false, "GitPet could not materialize the public branch in the independent workspace.\r\n\r\n" + checkout.Output);

        var reset = await RunGitAsync(workspace, ["reset", "--hard", remoteRef], TimeSpan.FromMinutes(1));
        if (!reset.Success)
            return (false, "GitPet could not synchronize the clean public workspace to the fetched branch.\r\n\r\n" + reset.Output);

        return (true, "Public publication workspace synchronized.");
    }

    private static void SetLegacyStandaloneTimerSuppressed(bool suppress)
    {
        if (suppress)
        {
            if (_legacyTimerSuppressed) return;
            var timer = GetLegacyStandaloneTimer();
            if (timer is null) return;
            timer.Stop();
            _legacyTimerSuppressed = true;
            return;
        }

        ResumeLegacyStandaloneTimer();
    }

    private static void ResumeLegacyStandaloneTimer()
    {
        if (!_legacyTimerSuppressed) return;
        var timer = GetLegacyStandaloneTimer();
        timer?.Start();
        _legacyTimerSuppressed = false;
    }

    private static System.Windows.Forms.Timer? GetLegacyStandaloneTimer() =>
        typeof(StandaloneProjectPublishingUiRuntime)
            .GetField("_timer", StaticPrivate)?
            .GetValue(null) as System.Windows.Forms.Timer;

    private static string? GetSavedWorkspacePath(string branch)
    {
        try
        {
            var file = GetWorkspacePreferenceFile(branch);
            if (!File.Exists(file)) return null;
            var value = File.ReadAllText(file, Encoding.UTF8).Trim();
            return string.IsNullOrWhiteSpace(value) ? null : Path.GetFullPath(value);
        }
        catch
        {
            return null;
        }
    }

    private static void SaveWorkspacePath(string branch, string workspace)
    {
        var file = GetWorkspacePreferenceFile(branch);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, Path.GetFullPath(workspace), Encoding.UTF8);
    }

    private static string GetWorkspacePreferenceFile(string branch)
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ZomniverseGitPet",
            "PublicationWorkspaces");
        var branchKey = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(branch.Trim().ToLowerInvariant())))
            .ToLowerInvariant()[..12];
        return Path.Combine(root, $"{TargetRepository}-{branchKey}.path.txt");
    }

    internal static bool IsTargetPublicationRemote(string? remoteUrl)
    {
        if (string.IsNullOrWhiteSpace(remoteUrl)) return false;
        var value = remoteUrl.Trim().TrimEnd('/');
        if (value.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) value = value[..^4];
        value = value.Replace('\\', '/');
        return value.EndsWith($"/{TargetOwner}/{TargetRepository}", StringComparison.OrdinalIgnoreCase) ||
               value.EndsWith($":{TargetOwner}/{TargetRepository}", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool RemoteEquals(string left, string right)
    {
        static string Normalize(string value)
        {
            var normalized = (value ?? "").Trim().TrimEnd('/');
            if (normalized.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) normalized = normalized[..^4];
            return normalized.Replace('\\', '/');
        }

        return Normalize(left).Equals(Normalize(right), StringComparison.OrdinalIgnoreCase);
    }

    private static Control? FindControl(Control root, string name)
    {
        if (root.Name.Equals(name, StringComparison.Ordinal)) return root;
        foreach (Control child in root.Controls)
        {
            var found = FindControl(child, name);
            if (found is not null) return found;
        }
        return null;
    }

    private static void OpenFolder(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{path}\"",
                UseShellExecute = true
            });
        }
        catch
        {
            // Opening Explorer is convenience only. It must never affect repository state.
        }
    }

    private static async Task<GitRunResult> RunGitAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        TimeSpan timeout)
    {
        try
        {
            var start = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var argument in arguments) start.ArgumentList.Add(argument);

            using var process = new Process { StartInfo = start };
            if (!process.Start()) return new(false, -1, "Git could not be started.");

            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeoutCts = new CancellationTokenSource(timeout);

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                try { if (!process.HasExited) process.Kill(true); } catch { }
                var timedOutOut = await stdout;
                var timedOutErr = await stderr;
                return new(false, -1, (timedOutOut + "\r\n" + timedOutErr).Trim() + "\r\nGit operation timed out.");
            }

            var output = ((await stdout) + "\r\n" + (await stderr)).Trim();
            return new(process.ExitCode == 0, process.ExitCode, output);
        }
        catch (Exception ex)
        {
            return new(false, -1, ex.Message);
        }
    }
}
