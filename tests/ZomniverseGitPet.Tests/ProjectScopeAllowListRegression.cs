using System.Runtime.CompilerServices;
using ZomniverseGitPet;

internal static class ProjectScopeAllowListRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        var basePath = Path.Combine(
            Path.GetTempPath(),
            "ZomniverseGitPet.Tests",
            "allow-list-" + Guid.NewGuid().ToString("N"));
        var root = Path.Combine(basePath, "project");
        var outside = Path.Combine(basePath, "outside.txt");

        try
        {
            Directory.CreateDirectory(Path.Combine(root, "CV"));
            Directory.CreateDirectory(Path.Combine(root, "home", "assets"));
            Directory.CreateDirectory(Path.Combine(root, ".git"));
            File.WriteAllText(Path.Combine(root, "CV", "Profile.pdf"), "profile");
            File.WriteAllText(Path.Combine(root, "home", "assets", "logo.png"), "logo");
            File.WriteAllText(Path.Combine(root, ".git", "config"), "git");
            File.WriteAllText(outside, "outside");

            var absoluteFolder = Path.Combine(root, "home", "assets");
            var resolved = ProjectScopeAllowList.Resolve(
                root,
                "# exact project scope\nCV/Profile.pdf\n\"" + absoluteFolder + "\"\n");

            Require(resolved.Success, "valid relative and absolute paths should resolve");
            Require(resolved.Entries.Count == 2, "expected one file and one folder");
            Require(resolved.FileCount == 1 && resolved.DirectoryCount == 1, "file/folder counts should be exact");
            Require(resolved.Entries.Any(entry => !entry.IsDirectory && entry.RelativePath == "CV/Profile.pdf"), "relative file missing");
            Require(resolved.Entries.Any(entry => entry.IsDirectory && entry.RelativePath == "home/assets"), "absolute folder missing");

            var formatted = ProjectScopeAllowList.Format(resolved.Entries);
            Require(formatted.Contains("CV/Profile.pdf", StringComparison.Ordinal), "formatted file path missing");
            Require(formatted.Contains("home/assets/", StringComparison.Ordinal), "formatted folder path should end with slash");

            var canonical = ProjectScopeAllowList.Resolve(
                root,
                "home/assets/\nhome/assets/logo.png\n");
            Require(canonical.Success && canonical.Entries.Count == 1, "selected parent folder should absorb child entries");
            Require(canonical.Entries[0].IsDirectory && canonical.Entries[0].RelativePath == "home/assets", "canonical folder selection incorrect");

            var rejected = ProjectScopeAllowList.Resolve(
                root,
                outside + "\n.git/config\nmissing/file.txt\n");
            Require(!rejected.Success && rejected.Issues.Count == 3, "unsafe or missing paths should be reported atomically");
            Require(rejected.Issues.Any(issue => issue.Message.Contains("outside", StringComparison.OrdinalIgnoreCase)), "outside-root issue missing");
            Require(rejected.Issues.Any(issue => issue.Message.Contains(".git", StringComparison.OrdinalIgnoreCase)), ".git issue missing");
            Require(rejected.Issues.Any(issue => issue.Message.Contains("not found", StringComparison.OrdinalIgnoreCase)), "missing-path issue missing");

            /* ==========================================================================
               PATCH: ALLOW-LIST PERSISTENCE AND BOUNDARY REGRESSION
               DATE.TIME: 2026-09-11 21:20 +03:00
               Verify project list restore and exact Send set comparison.
               ========================================================================== */
            var storePath = Path.Combine(basePath, "project-allow-lists.json");
            var store = new ProjectAllowListStore(storePath);
            var rawList = "# ZAR-like project contract\nCV/Profile.pdf\nhome/assets/\n";

            store.Save(null, root, root, "Allow list test", rawList);
            var restored = store.Load(null, root, root, "Allow list test");
            Require(restored.Contains("CV/Profile.pdf", StringComparison.Ordinal), "saved allow-list file should restore");
            Require(restored.Contains("home/assets/", StringComparison.Ordinal), "saved allow-list folder should restore");

            store.Save("project-123", root, root, "Allow list test", restored);
            var restoredById = store.Load("project-123", root, root, "Allow list test");
            Require(restoredById == restored, "allow-list store should migrate to and restore by project id");

            var exact = ProjectPublishBoundary.CompareSets(
                ["CV/Profile.pdf", "home/assets/logo.png"],
                ["home/assets/logo.png", "CV/Profile.pdf"]);
            Require(exact.ExactMatch && exact.Matched.Count == 2, "same file sets in different order should match exactly");

            var mismatch = ProjectPublishBoundary.CompareSets(
                ["CV/Profile.pdf", "home/assets/logo.png"],
                ["CV/Profile.pdf", "unexpected/never-send.txt"]);
            Require(!mismatch.ExactMatch, "different file sets must not report an exact match");
            Require(mismatch.AllowListOnly.SequenceEqual(["home/assets/logo.png"], StringComparer.OrdinalIgnoreCase), "allow-list-only file missing");
            Require(mismatch.SendOnly.SequenceEqual(["unexpected/never-send.txt"], StringComparer.OrdinalIgnoreCase), "send-only file missing");

            Console.WriteLine("Project scope allow-list regression passed (resolve + persistence + Send boundary)." );
        }
        finally
        {
            try
            {
                if (Directory.Exists(basePath)) Directory.Delete(basePath, recursive: true);
            }
            catch { }
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("Project scope allow-list regression failed: " + message);
    }
}
