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
            var configStore = new ConfigStore();
            var config = configStore.Load();
            var audit = new AuditLog();
            var git = new GitService(audit);

            /* ========================================================================== 
               PATCH: GUIDED FIRST-RUN ONBOARDING
               DATE.TIME: 2026-09-10 21:22 +03:00
               Let GitPet guide connection mode before Guardian starts.
               ========================================================================== */
            if (!config.OnboardingCompleted)
            {
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

            using var syncWatcher = new GuardianRemoteWatcher(config, git);
            using var updater = new ApplicationUpdateCoordinator(audit);
            using var context = new ZomniverseGitPetContext(instanceName, config, configStore, git, audit);

            /*
            PATCH: INSTALLED RELEASE UPDATE WATCHER
            DATE: 2026-09-10
            Check installed builds for verified GitHub releases.
            */
            updater.Start();

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
