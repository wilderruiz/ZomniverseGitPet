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
            using var syncWatcher = new GuardianRemoteWatcher(config, git);
            using var context = new ZomniverseGitPetContext(instanceName, config, configStore, git, audit);
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
