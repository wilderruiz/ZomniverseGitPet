using System.Runtime.CompilerServices;

namespace ZomniverseGitPet;

internal static class MajorUpdateFeature
{
    private static readonly Dictionary<IntPtr, ToolStripMenuItem> Menus = new();
    private static readonly SemaphoreSlim AnalysisGate = new(1, 1);
    private static System.Windows.Forms.Timer? _timer;
    private static bool _started;

    [ModuleInitializer]
    internal static void Initialize()
    {
        Application.Idle += Bootstrap;
    }

    private static void Bootstrap(object? sender, EventArgs e)
    {
        if (_started) return;
        _started = true;
        Application.Idle -= Bootstrap;

        EnsureMenus();
        _timer = new System.Windows.Forms.Timer { Interval = 45_000 };
        _timer.Tick += async (_, _) =>
        {
            EnsureMenus();
            await RefreshNudgesAsync();
        };
        _timer.Start();
        _ = RefreshNudgesAsync();
    }

    private static void EnsureMenus()
    {
        var guardians = Application.OpenForms.Cast<Form>()
            .OfType<GuardianForm>()
            .Where(form => !form.IsDisposed)
            .ToArray();

        foreach (var guardian in guardians)
        {
            if (guardian.MainMenuStrip is null || guardian.Handle == IntPtr.Zero) continue;
            if (guardian.MainMenuStrip.Items.Cast<ToolStripItem>().Any(item => item.Name == "MajorUpdateMenu"))
                continue;

            var menu = new ToolStripMenuItem("Milestones")
            {
                Name = "MajorUpdateMenu",
                ToolTipText = "GitPet can recognize unusually large/structural changes and help preserve the old generation before a redesign."
            };

            var review = new ToolStripMenuItem("Review major update / release plan…")
            {
                ToolTipText = "Compare the current generation with the previous/published baseline and create safe local legacy + redesign refs."
            };
            review.Click += async (_, _) => await OpenReleaseCenterAsync(guardian);
            menu.DropDownItems.Add(review);

            var releases = new ToolStripMenuItem("Open project releases on GitHub ↗")
            {
                ToolTipText = "Open the GitHub Releases archive for this project's origin remote."
            };
            releases.Click += async (_, _) => await OpenProjectReleasesAsync(guardian);
            menu.DropDownItems.Add(releases);

            menu.DropDownItems.Add(new ToolStripSeparator());
            menu.DropDownItems.Add(new ToolStripMenuItem(
                "GitPet never force-pushes main or publishes a release automatically.") { Enabled = false });

            var helpIndex = guardian.MainMenuStrip.Items.Cast<ToolStripItem>()
                .Select((item, index) => new { item, index })
                .FirstOrDefault(value => value.item.Text.Equals("Help", StringComparison.OrdinalIgnoreCase))?.index
                ?? guardian.MainMenuStrip.Items.Count;
            guardian.MainMenuStrip.Items.Insert(Math.Max(0, helpIndex), menu);

            var handle = guardian.Handle;
            Menus[handle] = menu;
            guardian.FormClosed += (_, _) => Menus.Remove(handle);
        }
    }

    private static async Task RefreshNudgesAsync()
    {
        if (!await AnalysisGate.WaitAsync(0)) return;
        try
        {
            var config = new ConfigStore().Load();
            var repository = config.RepositoryPath;
            if (string.IsNullOrWhiteSpace(repository) || !Directory.Exists(repository))
            {
                SetAllMenus("Milestones", GuardianTheme.Ink,
                    "Create or inspect legacy milestones and major-release branches.");
                return;
            }

            var visibleGuardian = Application.OpenForms.Cast<Form>()
                .OfType<GuardianForm>()
                .FirstOrDefault(form => form.Visible && !form.IsDisposed);
            if (visibleGuardian is null) return;

            var audit = new AuditLog();
            var git = new GitService(audit);
            var coordinator = new MajorUpdateCoordinator(git, audit);
            var assessment = await coordinator.AnalyzeAsync(repository, refreshRemote: false);

            if (assessment.IsMajorCandidate)
            {
                SetAllMenus("Major update? ✦", GuardianTheme.Changes,
                    assessment.Prompt + " " +
                    (assessment.Reasons.Count == 0
                        ? "Open this menu to review it."
                        : string.Join("; ", assessment.Reasons.Take(3))));
            }
            else
            {
                SetAllMenus("Milestones", GuardianTheme.Ink,
                    "Create or inspect legacy milestones and major-release branches.");
            }
        }
        catch
        {
            // Detection is advisory. A failed background analysis must never disturb normal Guardian use.
        }
        finally
        {
            AnalysisGate.Release();
        }
    }

    private static async Task OpenReleaseCenterAsync(Form owner)
    {
        var config = new ConfigStore().Load();
        var repository = config.RepositoryPath;
        if (string.IsNullOrWhiteSpace(repository) || !Directory.Exists(repository))
        {
            MessageBox.Show(owner, "Choose a project first.", "Milestones", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var wait = new MajorUpdateAnalysisForm();
        wait.Show(owner);
        wait.Refresh();

        try
        {
            var audit = new AuditLog();
            var git = new GitService(audit);
            var coordinator = new MajorUpdateCoordinator(git, audit);
            var assessment = await coordinator.AnalyzeAsync(repository, refreshRemote: true);
            wait.Close();

            using var form = new MajorUpdateForm(assessment, coordinator);
            form.ShowDialog(owner);
        }
        catch (Exception ex)
        {
            wait.Close();
            MessageBox.Show(owner,
                "GitPet could not inspect the project history for a milestone plan. Nothing was changed.\r\n\r\n" + ex.Message,
                "Milestones", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static async Task OpenProjectReleasesAsync(Form owner)
    {
        var config = new ConfigStore().Load();
        var repository = config.RepositoryPath;
        if (string.IsNullOrWhiteSpace(repository) || !Directory.Exists(repository))
        {
            MessageBox.Show(owner, "Choose a project first.", "Project releases", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var audit = new AuditLog();
        var git = new GitService(audit);
        var origin = await git.RunGitAsync(repository, ["remote", "get-url", "origin"]);
        var github = origin.Success ? MajorUpdateCoordinator.TryGetGitHubWebUrl(origin.Output.Trim()) : null;
        if (github is null)
        {
            MessageBox.Show(owner,
                "GitPet could not derive a GitHub Releases page from this project's origin.\r\n\r\n" +
                "Milestone branches and tags still work with any normal Git remote; the one-click Releases page is GitHub-specific.",
                "Project releases", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        OpenUrl(github.TrimEnd('/') + "/releases", owner);
    }

    private static void SetAllMenus(string text, Color color, string tooltip)
    {
        foreach (var entry in Menus.ToArray())
        {
            if (entry.Value.Owner is null)
            {
                Menus.Remove(entry.Key);
                continue;
            }
            entry.Value.Text = text;
            entry.Value.ForeColor = color;
            entry.Value.ToolTipText = tooltip;
        }
    }

    private static void OpenUrl(string url, IWin32Window owner)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            MessageBox.Show(owner, "GitPet could not open the browser.", "Project releases", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}

internal sealed class MajorUpdateAnalysisForm : Form
{
    public MajorUpdateAnalysisForm()
    {
        Text = "GitPet is comparing generations…";
        Icon = AppIconProvider.Icon;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ControlBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(520, 155);
        BackColor = GuardianTheme.Surface;
        ForeColor = GuardianTheme.Ink;
        Font = new Font("Segoe UI", 9);

        Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24),
            Text = "◇  CHECKING PROJECT GENERATIONS\r\n\r\nGitPet is fetching remote history read-only and measuring the change. Nothing is being merged, committed, tagged, or pushed.",
            ForeColor = GuardianTheme.Ink,
            Font = new Font("Segoe UI", 10),
            TextAlign = ContentAlignment.MiddleLeft
        });
    }
}
