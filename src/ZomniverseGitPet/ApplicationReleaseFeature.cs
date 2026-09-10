using System.Runtime.CompilerServices;

namespace ZomniverseGitPet;

internal static class ApplicationReleaseFeature
{
    private const string MenuItemName = "ApplicationReleasePublisherItem";
    private const string SetupMenuItemName = "ApplicationReleaseGitHubSetupItem";
    private static readonly HashSet<IntPtr> AttachedGuardians = new();
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
        _timer = new System.Windows.Forms.Timer { Interval = 5_000 };
        _timer.Tick += (_, _) => EnsureMenus();
        _timer.Start();
    }

    private static void EnsureMenus()
    {
        foreach (var guardian in Application.OpenForms.Cast<Form>()
                     .OfType<GuardianForm>()
                     .Where(form => !form.IsDisposed && form.Handle != IntPtr.Zero))
        {
            if (AttachedGuardians.Contains(guardian.Handle)) continue;

            var mainMenu = guardian.MainMenuStrip;
            if (mainMenu is null) continue;

            var milestones = mainMenu.Items
                .OfType<ToolStripMenuItem>()
                .FirstOrDefault(item => item.Name == "MajorUpdateMenu");
            if (milestones is null) continue;

            var setup = new ToolStripMenuItem("Set up GitHub publishing…")
            {
                Name = SetupMenuItemName,
                Visible = false,
                ToolTipText = "Install GitHub CLI if needed, sign in securely, and verify release-publishing authentication."
            };
            setup.Click += async (_, _) => await OpenGitHubSetupAsync(guardian);

            var publish = new ToolStripMenuItem("Publish ZomniverseGitPet application release…")
            {
                Name = MenuItemName,
                Visible = false,
                ToolTipText = "Validate the prepared Windows package and publish it as a GitHub Release using your authenticated GitHub CLI session."
            };
            publish.Click += async (_, _) => await OpenPublisherAsync(guardian);

            var insertionIndex = milestones.DropDownItems.Count;
            var existingSeparatorWillFollow = false;
            var policy = milestones.DropDownItems
                .OfType<ToolStripMenuItem>()
                .FirstOrDefault(item => !item.Enabled && item.Text.Contains("never force-pushes", StringComparison.OrdinalIgnoreCase));
            if (policy is not null)
            {
                insertionIndex = milestones.DropDownItems.IndexOf(policy);
                if (insertionIndex > 0 && milestones.DropDownItems[insertionIndex - 1] is ToolStripSeparator)
                {
                    insertionIndex--;
                    existingSeparatorWillFollow = true;
                }
            }

            milestones.DropDownItems.Insert(insertionIndex, setup);
            milestones.DropDownItems.Insert(insertionIndex + 1, publish);
            if (!existingSeparatorWillFollow)
                milestones.DropDownItems.Insert(insertionIndex + 2, new ToolStripSeparator());

            milestones.DropDownOpening += (_, _) =>
            {
                var config = new ConfigStore().Load();
                var visible = GitHubReleasePublisher.LooksLikeGitPetSource(config.RepositoryPath);
                setup.Visible = visible;
                publish.Visible = visible;
            };

            var handle = guardian.Handle;
            AttachedGuardians.Add(handle);
            guardian.FormClosed += (_, _) => AttachedGuardians.Remove(handle);
        }
    }

    private static async Task OpenGitHubSetupAsync(Form owner)
    {
        var config = new ConfigStore().Load();
        var repository = config.RepositoryPath;
        if (!GitHubReleasePublisher.LooksLikeGitPetSource(repository)) return;

        var audit = new AuditLog();
        var publisher = new GitHubReleasePublisher(audit);
        using var form = new GitHubPublishingSetupForm(publisher);
        form.ShowDialog(owner);
        await Task.CompletedTask;
    }

    private static async Task OpenPublisherAsync(Form owner)
    {
        var config = new ConfigStore().Load();
        var repository = config.RepositoryPath;
        if (!GitHubReleasePublisher.LooksLikeGitPetSource(repository)) return;

        using var wait = new MajorUpdateAnalysisForm();
        wait.Text = "GitPet is checking release readiness…";
        wait.Show(owner);
        wait.Refresh();

        try
        {
            var audit = new AuditLog();
            var git = new GitService(audit);
            var coordinator = new MajorUpdateCoordinator(git, audit);
            var assessment = await coordinator.AnalyzeAsync(repository!, refreshRemote: true);
            wait.Close();

            var publisher = new GitHubReleasePublisher(audit);
            using var form = new ApplicationReleaseForm(assessment, publisher);
            form.ShowDialog(owner);
        }
        catch (Exception ex)
        {
            wait.Close();
            MessageBox.Show(
                owner,
                "GitPet could not inspect application-release readiness. Nothing was published.\r\n\r\n" + ex.Message,
                "Application release",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }
}