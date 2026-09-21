using System.Runtime.CompilerServices;
using ZomniverseGitPet;

internal static class ApplicationUpdateRegression
{
    [ModuleInitializer]
    public static void Run()
    {
        if (!ApplicationUpdateService.IsNewerVersion("0.4.2", "0.4.1"))
            throw new InvalidOperationException("Updater regression: 0.4.2 should be newer than 0.4.1.");

        if (!ApplicationUpdateService.IsNewerVersion("v0.4.2", "0.4.1"))
            throw new InvalidOperationException("Updater regression: a v-prefixed release tag should compare correctly.");

        if (ApplicationUpdateService.IsNewerVersion("0.4.2", "0.4.2"))
            throw new InvalidOperationException("Updater regression: the same version must not trigger an update.");

        if (ApplicationUpdateService.IsNewerVersion("0.4.1", "0.4.2"))
            throw new InvalidOperationException("Updater regression: an older release must not trigger an update.");

        var directory = Path.Combine(Path.GetTempPath(), "zgitpet-updater-regression-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "payload.bin");
            File.WriteAllText(path, "abc");

            const string expected = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";
            if (!ApplicationUpdateService.VerifySha256(path, expected))
                throw new InvalidOperationException("Updater regression: the known SHA-256 payload should verify.");

            if (ApplicationUpdateService.VerifySha256(path, new string('0', 64)))
                throw new InvalidOperationException("Updater regression: a wrong SHA-256 value must be rejected.");
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); }
            catch { }
        }

        Console.WriteLine("Application updater regression passed (version + SHA-256 rules).");
    }
}
