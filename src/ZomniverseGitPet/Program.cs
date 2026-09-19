using System.IO.Pipes;
using System.Security.Principal;
using System.Text;

namespace ZomniverseGitPet;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        SplitContainerSafety.InstallForApplication();

        var identity = ApplicationIdentity.Current;
        ApplicationIdentity.ApplyWindowsAppUserModelId(identity);

        var userId = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        var instanceName = ApplicationIdentity.BuildGlobalInstanceName(userId);
        using var mutex = new Mutex(true, instanceName, out var ownsMutex);
        var mutexOwned = ownsMutex;

        if (!mutexOwned)
        {
            var response = RequestExistingInstance(instanceName, identity.Channel);
            if (response != ExistingInstanceResponse.SwitchApproved)
                return;

            try
            {
                mutexOwned = mutex.WaitOne(TimeSpan.FromSeconds(15));
            }
            catch (AbandonedMutexException)
            {
                // The previous GitPet process ended before explicitly releasing the
                // mutex. Windows still grants ownership to this waiting process.
                mutexOwned = true;
            }

            if (!mutexOwned)
            {
                MessageBox.Show(
                    $"The running GitPet copy did not close in time. {identity.DisplayName} was not started.\r\n\r\n" +
                    "Close the existing GitPet normally, then try again.",
                    "GitPet switch did not complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }
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
            LogicalProjectScopeRuntime.Initialize(config);
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
            using var context = new ZomniverseGitPetContext(
                instanceName,
                identity,
                config,
                configStore,
                git,
                audit);

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
            MessageBox.Show(
                ex.Message,
                $"{identity.DisplayName} could not start",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            if (mutexOwned)
            {
                try { mutex.ReleaseMutex(); }
                catch (ApplicationException) { }
            }
        }
    }

    private static ExistingInstanceResponse RequestExistingInstance(
        string pipeName,
        ApplicationChannel requestedChannel)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.InOut,
                PipeOptions.None);
            pipe.Connect(1500);

            using var writer = new StreamWriter(
                pipe,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                1024,
                leaveOpen: true)
            {
                AutoFlush = true
            };
            using var reader = new StreamReader(
                pipe,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                1024,
                leaveOpen: true);

            writer.WriteLine(ApplicationInstanceProtocol.BuildActivationRequest(requestedChannel));
            var response = reader.ReadLine();
            return ApplicationInstanceProtocol.ParseResponse(response);
        }
        catch
        {
            // Compatibility with an older running GitPet build whose pipe was
            // one-way only. It can still be activated, but cannot perform a
            // channel handoff until that installed/DEV copy is rebuilt.
            TryActivateLegacyInstance(pipeName);
            return ExistingInstanceResponse.LegacyActivated;
        }
    }

    private static void TryActivateLegacyInstance(string pipeName)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.Out);
            pipe.Connect(800);
            pipe.WriteByte(1);
        }
        catch
        {
        }
    }
}
