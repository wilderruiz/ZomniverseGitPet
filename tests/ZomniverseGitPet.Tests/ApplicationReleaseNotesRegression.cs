using System.Runtime.CompilerServices;
using ZomniverseGitPet;

internal static class ApplicationReleaseNotesRegression
{
    [ModuleInitializer]
    public static void Run()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "zgitpet-release-notes-regression-" + Guid.NewGuid().ToString("N"));
        var tag = "v9.8.7";
        var notesDirectory = Path.Combine(root, "release", tag);
        Directory.CreateDirectory(notesDirectory);

        try
        {
            const string marker = "GITPET VET VISIT TEST MARKER";
            File.WriteAllText(
                Path.Combine(notesDirectory, "GITHUB_RELEASE_NOTES.md"),
                "# Veterinary release notes\n\n" + marker + "\n");

            var package = new ApplicationReleasePackage(
                true,
                root,
                "wilderruiz/ZomniverseGitPet",
                "9.8.7",
                tag,
                Path.Combine(root, "package"),
                Path.Combine(root, "installer.exe"),
                Path.Combine(root, "portable.exe"),
                Path.Combine(root, "release-manifest.json"),
                Path.Combine(root, "SHA256SUMS.txt"),
                "main",
                new string('a', 40),
                "ready");

            var veterinary = ApplicationReleaseForm.BuildDefaultReleaseNotes(package);
            if (!veterinary.Contains(marker, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Release notes regression: versioned veterinary notes were not selected.");

            File.Delete(Path.Combine(notesDirectory, "GITHUB_RELEASE_NOTES.md"));
            var fallback = ApplicationReleaseForm.BuildDefaultReleaseNotes(package);
            if (!fallback.Contains("Windows release package", StringComparison.Ordinal) ||
                !fallback.Contains("9.8.7", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Release notes regression: missing veterinary notes did not fall back safely.");
            }

            var notReady = package with { Ready = false };
            if (ApplicationReleaseForm.BuildDefaultReleaseNotes(notReady).Length != 0)
                throw new InvalidOperationException(
                    "Release notes regression: an unprepared package should not produce release notes.");
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch { }
        }

        Console.WriteLine(
            "Application release notes regression passed (vet record + generic fallback).");
    }
}
