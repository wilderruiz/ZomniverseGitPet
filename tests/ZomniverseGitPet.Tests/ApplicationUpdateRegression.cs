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

        var vetSummary = ApplicationUpdateService.ResolveVetVisitSummary(
            "  Vet visit summary\n• parasite removed  ",
            "legacy GitHub release notes");
        if (vetSummary != "Vet visit summary\n• parasite removed")
            throw new InvalidOperationException(
                "Updater regression: versioned vet summary should win over legacy release notes and be trimmed.");

        var legacySummary = ApplicationUpdateService.ResolveVetVisitSummary(
            "",
            "  legacy GitHub release notes  ");
        if (legacySummary != "legacy GitHub release notes")
            throw new InvalidOperationException(
                "Updater regression: older releases should fall back to GitHub release notes.");

        var genericSummary = ApplicationUpdateService.ResolveVetVisitSummary(null, null);
        if (string.IsNullOrWhiteSpace(genericSummary) ||
            !genericSummary.Contains("release checks", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Updater regression: missing vet/release notes should produce a stable fallback message.");

        var reportUrl = ApplicationUpdateService.BuildDewormingReportUrl("v0.6.0");
        var passportUrl = ApplicationUpdateService.BuildVetPassportUrl("v0.6.0");
        if (!reportUrl.EndsWith(
                "/blob/v0.6.0/release/v0.6.0/DEWORMING_REPORT.md",
                StringComparison.Ordinal) ||
            !passportUrl.EndsWith(
                "/blob/v0.6.0/release/VET_PASSPORT.md",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Updater regression: veterinary record URLs should be pinned to the offered release tag.");
        }

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

        Console.WriteLine(
            "Application updater regression passed (version + vet-summary fallback + SHA-256 rules).");
    }
}
