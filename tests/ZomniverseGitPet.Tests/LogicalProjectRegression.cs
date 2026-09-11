using System.Runtime.CompilerServices;
using ZomniverseGitPet;

internal static class LogicalProjectRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "zgitpet-shared-repo");
        var wildverse = Path.Combine(root, "wildverse");
        var home = Path.Combine(root, "home");

        var config = new AppConfig();
        LogicalProjectScopeRuntime.Initialize(config);

        var rootProject = config.RememberProject(
            root,
            root,
            "Web family",
            trackEverything: false,
            [new ProjectScopeEntry("CV", true), new ProjectScopeEntry("home", true)]);
        var wildverseProject = config.RememberProject(
            wildverse,
            root,
            "Wildverse",
            trackEverything: false,
            [new ProjectScopeEntry("wildverse", true)]);

        if (config.RecentRepositories.Count != 2 ||
            rootProject.RepositoryRoot != wildverseProject.RepositoryRoot ||
            rootProject.Id == wildverseProject.Id ||
            config.GetActiveProject()?.Id != wildverseProject.Id)
            throw new InvalidOperationException("Logical projects did not remain independent inside one repository.");

        var specs = LogicalProjectScopeRuntime.GetPathspecs(root, includeRootGitIgnore: true);
        if (!specs.Contains("wildverse", StringComparer.OrdinalIgnoreCase) ||
            !specs.Contains(".gitignore", StringComparer.OrdinalIgnoreCase) ||
            specs.Contains("home", StringComparer.OrdinalIgnoreCase) ||
            !LogicalProjectScopeRuntime.ContainsPath(root, "wildverse/src/app.cs") ||
            LogicalProjectScopeRuntime.ContainsPath(root, "home/index.php"))
            throw new InvalidOperationException("Active logical project scope did not constrain local Git operations.");

        config.RenameProject(wildverseProject.Id, "Wildverse Site");
        if (config.GetActiveProject()?.DisplayName != "Wildverse Site")
            throw new InvalidOperationException("Logical project rename did not persist independently.");

        var legacy = new AppConfig();
        var legacyRoot = legacy.RememberProject(root, root, "Legacy root", true, []);
        LogicalProjectScopeRuntime.Initialize(legacy);
        LogicalProjectScopeRuntime.MigrateLegacyScope(root,
            [new ProjectScopeEntry("CV", true), new ProjectScopeEntry("home", true)]);
        if (legacyRoot.TrackEverything || legacyRoot.ScopeEntries.Count != 2)
            throw new InvalidOperationException("Legacy managed scope was not preserved before migration.");

        Console.WriteLine("Logical project regression passed (shared repository + scoped save rules).");
    }
}
