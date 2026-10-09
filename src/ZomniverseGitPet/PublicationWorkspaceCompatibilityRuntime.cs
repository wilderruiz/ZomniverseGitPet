using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace ZomniverseGitPet;

/* ========================================================================== 
   PATCH: REVIEW-FIRST PUBLICATION WORKSPACE COMPATIBILITY
   DATE.TIME: 2026-10-09 13:23 +03:00

   WHY THIS EXISTS
   ---------------
   Standalone logical-project publishing was intentionally built as a scoped mirror:

       private parent scope <-> isolated publishing workspace <-> standalone remote

   Zomniverse now uses a different model for its public project repository:

       private Zomniverse source --review--> public publication repository

   The public repository is an independently curated publication surface. Remote-only
   files such as WHAT_IS_PUBLISHED_HERE.md, publication notes and future public assets
   MUST NOT be copied back into the private parent repository merely because Get was
   pressed.

   This compatibility runtime changes only the ZOMNIVERSE-PROJECT standalone link.
   Existing scoped-mirror projects keep the original Get/Send behavior unchanged.

   OWNER DECISION (Wilder Ruiz, 2026-10-09): publication is review-first; automatic
   mirroring from the private Zomniverse repository is not appropriate.
   ========================================================================== */
internal static class PublicationWorkspaceCompatibilityRuntime
{
    private const string TargetOwner = "wilderruiz";
    private const string TargetRepository = "zomniverse-project";
    private const string PublicBoundaryMarker = "WHAT_IS_PUBLISHED_HERE.md";

    private static readonly BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
    private static System.Windows.Forms.Timer? _timer;
    private static bool _busy;
    private static AppConfig? _config;

    private static readonly Dictionary<GuardianForm, GuardianActionButton> GetButtons = [];
    private static readonly Dictionary<GuardianForm, GuardianActionButton> OpenButtons = [];

    [ModuleInitializer]
    internal static void InitializeModule()
    {
        // Module initialization happens before the application context has finished
        // wiring its runtime services. Application.Idle gives the normal GitPet
        // initialization path time to populate StandaloneProjectPublishingUiRuntime.
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
            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;
            GetButtons.Clear();
            OpenButtons.Clear();
        };
    }

    private static Task TickAsync()
    {
        if (_busy || _config is null) return Task.CompletedTask;
        _busy = true;
        try
        {
            var guardians = Application.OpenForms
                .OfType<GuardianForm>()
                .Where(form => !form.IsDisposed)
                .ToArray();

            foreach (var stale in GetButtons.Keys.Where(form => !guardians.Contains(form)).ToArray())
                GetButtons.Remove(stale);
            foreach (var stale in OpenButtons.Keys.Where(form => !guardians.Contains(form)).ToArray())
                OpenButtons.Remove(stale);

            foreach (var guardian in guardians)
                UpdateGuardian(guardian);
        }
        finally
        {
            _busy = false;
        }

        return Task.CompletedTask;
    }

    private static void UpdateGuardian(GuardianForm guardian)
    {
        if (_config is null) return;

        var project = _config.GetActiveProject();
        var link = StandaloneProjectPublishing.GetLink(_config);
        var publicationMode = project is not null && link is not null && IsTargetPublicationRemote(link.RemoteUrl);

        var oldGet = FindControl(guardian, "StandaloneProjectGetButton") as GuardianActionButton;
        var oldSend = FindControl(guardian, "StandaloneProjectSendButton") as GuardianActionButton;
        var branch = FindControl(guardian, "StandaloneProjectBranchButton") as GuardianActionButton;

        if (!publicationMode)
        {
            if (GetButtons.TryGetValue(guardian, out var get)) get.Visible = false;
            if (OpenButtons.TryGetValue(guardian, out var open)) open.Visible = false;
            return;
        }

        if (oldGet is null || oldSend is null || oldGet.Parent is null) return;
        EnsureButtons(guardian, oldGet);

        // The legacy scoped-mirror actions are intentionally unavailable for this
        // publication repository. In particular, the old Send would rebuild the public
        // repository from the private parent scope, which conflicts with review-first
        // publication and could retire public-only documentation.
        oldGet.Visible = false;
        oldGet.Enabled = false;
        oldSend.Visible = false;
        oldSend.Enabled = false;

        // Branch selection remains useful because the publication checkout itself can
        // follow a deliberate public branch.
        if (branch is not null) branch.Visible = true;

        var workspaceExists = Directory.Exists(Path.Combine(GetWorkspacePath(project!, link!.Branch), ".git"));

        var publicGet = GetButtons[guardian];
        publicGet.Visible = true;
        publicGet.Enabled = !_busy;
        publicGet.Text = "Public Get ↓";
        publicGet.Cursor = publicGet.Enabled ? Cursors.Hand : Cursors.Default;

        var publicOpen = OpenButtons[guardian];
        publicOpen.Visible = true;
        publicOpen.Enabled = workspaceExists && !_busy;
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
        if (_config is null) return;
        var project = _config.GetActiveProject();
        var link = StandaloneProjectPublishing.GetLink(_config);
        if (project is null || link is null || !IsTargetPublicationRemote(link.RemoteUrl)) return;

        var workspace = GetWorkspacePath(project, link.Branch);

        using var confirmation = new GuardianConfirmDialog(
            "Get public publication workspace",
            "REVIEW-FIRST PUBLICATION WORKSPACE",
            $"Update the independent public checkout from:\r\n{link.RepositoryLabel} / {link.Branch}\r\n\r\n" +
            $"Local publication workspace:\r\n{workspace}\r\n\r\n" +
            "This does NOT copy public-only files into the private Zomniverse repository and does NOT publish anything automatically. " +
            "Local/remote agents may work in this checkout, then you can review its normal Git history before publication.",
            "Get public ↓",
            "Cancel",
            confirmWidth: 150,
            dialogSize: new Size(820, 560),
            scrollable: true,
            resizable: true);

        if (confirmation.ShowDialog(guardian) != DialogResult.Yes) return;

        guardian.UseWaitCursor = true;
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

            var marker = Path.Combine(workspace, PublicBoundaryMarker);
            var markerNote = File.Exists(marker)
                ? $"\r\n\r\nPublication boundary marker verified: {PublicBoundaryMarker}"
                : $"\r\n\r\nWARNING: {PublicBoundaryMarker} is missing. Review this checkout before treating it as the curated public surface.";

            using var ready = new GuardianConfirmDialog(
                "Public publication workspace",
                "PUBLIC WORKSPACE READY  ✓",
                $"The public repository is synchronized in its own independent checkout.\r\n\r\n{workspace}" +
                markerNote +
                "\r\n\r\nThe private Zomniverse working tree was not modified.",
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
        }
    }

    private static void OpenPublicationWorkspace(GuardianForm guardian)
    {
        if (_config is null) return;
        var project = _config.GetActiveProject();
        var link = StandaloneProjectPublishing.GetLink(_config);
        if (project is null || link is null || !IsTargetPublicationRemote(link.RemoteUrl)) return;

        var workspace = GetWorkspacePath(project, link.Branch);
        if (!Directory.Exists(Path.Combine(workspace, ".git")))
        {
            using var missing = new GuardianConfirmDialog(
                "Public publication workspace",
                "GET PUBLIC WORKSPACE FIRST",
                "The independent public checkout has not been created on this PC yet. Use Public Get first.",
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
        Directory.CreateDirectory(Path.GetDirectoryName(workspace)!);

        if (!Directory.Exists(Path.Combine(workspace, ".git")))
        {
            if (Directory.Exists(workspace) && Directory.EnumerateFileSystemEntries(workspace).Any())
                return (false, "The publication workspace path already contains files but is not a Git checkout. GitPet left it untouched:\r\n" + workspace);

            var clone = await RunGitAsync(
                Path.GetDirectoryName(workspace)!,
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

    private static string GetWorkspacePath(RecentRepositoryEntry project, string branch)
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrWhiteSpace(documents))
            documents = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var repo = TargetRepository;
        var branchSuffix = branch.Equals("main", StringComparison.OrdinalIgnoreCase)
            ? ""
            : "-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(branch))).ToLowerInvariant()[..8];

        return Path.Combine(documents, "ZomniverseGitPet", "PublicationWorkspaces", repo + branchSuffix);
    }

    private static bool IsTargetPublicationRemote(string? remoteUrl)
    {
        if (string.IsNullOrWhiteSpace(remoteUrl)) return false;
        var value = remoteUrl.Trim().TrimEnd('/');
        if (value.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) value = value[..^4];
        value = value.Replace('\\', '/');
        return value.EndsWith($"/{TargetOwner}/{TargetRepository}", StringComparison.OrdinalIgnoreCase) ||
               value.EndsWith($":{TargetOwner}/{TargetRepository}", StringComparison.OrdinalIgnoreCase);
    }

    private static bool RemoteEquals(string left, string right)
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
            try
            {
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch
            {
                // Opening Explorer is convenience only; the workspace remains usable.
            }
        }
    }

    private static async Task<(bool Success, int ExitCode, string Output)> RunGitAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        TimeSpan timeout)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            return (false, -1, ex.Message);
        }

        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var cts = new CancellationTokenSource(timeout);

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            return (false, -1, $"Git command timed out after {timeout.TotalSeconds:0} seconds.");
        }

        var output = ((await stdout) + Environment.NewLine + (await stderr)).Trim();
        return (process.ExitCode == 0, process.ExitCode, output);
    }
}
