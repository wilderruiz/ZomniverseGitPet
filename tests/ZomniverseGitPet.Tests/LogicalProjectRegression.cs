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

        var sameRootProject = config.RememberProject(
            root,
            root,
            "Millenova",
            trackEverything: false,
            [new ProjectScopeEntry("millenova", true), new ProjectScopeEntry("millenova_config.php", false)]);
        var secondSameRootProject = config.RememberProject(
            root,
            root,
            "Another subdomain",
            trackEverything: false,
            [new ProjectScopeEntry("another-subdomain", true), new ProjectScopeEntry("another-subdomain_config.php", false)]);

        var samePathProjects = config.FindProjectsByPath(root);
        if (samePathProjects.Count < 3 ||
            sameRootProject.Id == secondSameRootProject.Id ||
            !samePathProjects.Any(item => item.Id == sameRootProject.Id) ||
            !samePathProjects.Any(item => item.Id == secondSameRootProject.Id))
            throw new InvalidOperationException("Multiple logical projects could not share the exact same repository root/path.");

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

        wildverseProject.TestCommands.Add("dotnet test");
        config.ActivateProject(rootProject.Id);
        var reassignedPath = Path.Combine(root, "wildverse-moved");
        var reassigned = config.ReassignProjectFolder(
            wildverseProject.Id,
            reassignedPath,
            root,
            trackEverything: false,
            scopeEntries: [new ProjectScopeEntry("wildverse-moved", true)]);
        var movedProject = config.FindProject(wildverseProject.Id);
        if (!reassigned ||
            movedProject is null ||
            !string.Equals(movedProject.Path, reassignedPath, StringComparison.OrdinalIgnoreCase) ||
            movedProject.ScopeEntries.Count != 1 ||
            movedProject.TestCommands.Count != 1 ||
            config.GetActiveProject()?.Id != rootProject.Id)
            throw new InvalidOperationException("Project folder reassignment did not preserve project identity/settings without changing the active project.");

        var sharedReassign = config.ReassignProjectFolder(
            secondSameRootProject.Id,
            root,
            root,
            trackEverything: false,
            scopeEntries: [new ProjectScopeEntry("another-subdomain", true)]);
        if (!sharedReassign ||
            config.FindProjectsByPath(root).Count < 3)
            throw new InvalidOperationException("Same-path logical projects could not be reassigned without false duplicate ownership.");

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
