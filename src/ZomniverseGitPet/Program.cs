using System.IO.Pipes;
using System.Security.Principal;

namespace ZomniverseGitPet;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        SplitContainerSafety.InstallForApplication();

        var userId = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        var instanceName = "ZomniverseGitPet-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(userId)))[..16];
        using var mutex = new Mutex(true, instanceName, out var ownsMutex);
        if (!ownsMutex)
        {
            TryActivateExisting(instanceName);
            return;
        }

        try
        {
            /* ==========================================================================
               PATCH: IMMEDIATE STARTUP FEEDBACK
               FUNCTION:
               Display the happy GitPet splash before slower startup services initialize.

               DATE.TIME ADDED: 2026-09-11 07:41 +03:00

               REASON (20 words max):
               Replace the silent startup pause with visible progress using GitPet's existing embedded artwork.
               ========================================================================== */
            using var startupSplash = new StartupSplashForm();
            startupSplash.ShowImmediately();

            var configStore = new ConfigStore();
            var config = configStore.Load();
            var audit = new AuditLog();
            var git = new GitService(audit);
            startupSplash.SetStage("Preparing your Git guardian");

            /* ==========================================================================
               PATCH: CONNECTION UI COORDINATOR
               DATE.TIME: 2026-09-11 11:42 +03:00
               Keep onboarding and Guardian connection state visibly synchronized.
               ========================================================================== */
            ConnectionUiRuntime.Initialize(config, configStore, audit);
            startupSplash.SetStage("Checking connection settings");

            /* ========================================================================== 
               PATCH: GUIDED FIRST-RUN ONBOARDING
               DATE.TIME: 2026-09-10 21:22 +03:00
               Let GitPet guide connection mode before Guardian starts.
               ========================================================================== */
            if (!config.OnboardingCompleted)
            {
                startupSplash.CloseForLaunch();

                using var guidePet = new PetForm(
                    showGuardian: () => { },
                    chooseRepository: () => Task.CompletedTask,
                    exit: Application.Exit);
                guidePet.Show();
                guidePet.ShowGuidance("👋 HI! I'M GITPET\nLet's set things up");

                using var onboarding = new FirstRunSetupForm(
                    config,
                    configStore,
                    git,
                    audit,
                    guidePet.ShowGuidance);
                var result = onboarding.ShowDialog();

                guidePet.AllowClose = true;
                guidePet.Close();
                if (result != DialogResult.OK || !config.OnboardingCompleted)
                    return;
            }

            startupSplash.SetStage("Checking repository state");
            using var syncWatcher = new GuardianRemoteWatcher(config, git);

            /* ==========================================================================
               PATCH: LIVE GUARDIAN WORKBOARD
               DATE.TIME: 2026-09-11 12:51 +03:00
               Surface Save, Get, Send, Reconcile projections continuously.
               ========================================================================== */
            GuardianWorkboardRuntime.Initialize(config, git, audit);

            /* ==========================================================================
               PATCH: PROJECT-ONLY SEND ROUTER
               DATE.TIME: 2026-09-11 20:29 +03:00
               Keep logical-project publishing separate from parent Git history.
               ========================================================================== */
            StandaloneProjectPublishingUiRuntime.Initialize(config, git, audit);

            startupSplash.SetStage("Starting GitPet");
            using var updater = new ApplicationUpdateCoordinator(audit);
            using var context = new ZomniverseGitPetContext(instanceName, config, configStore, git, audit);

            /*
            PATCH: INSTALLED RELEASE UPDATE WATCHER
            DATE: 2026-09-10
            Check installed builds for verified GitHub releases.
            */
            updater.Start();

            startupSplash.CloseForLaunch();
            Application.Run(context);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "ZomniverseGitPet could not start", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void TryActivateExisting(string pipeName)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.Out);
            pipe.Connect(800);
            pipe.WriteByte(1);
        }
        catch { }
    }
}
