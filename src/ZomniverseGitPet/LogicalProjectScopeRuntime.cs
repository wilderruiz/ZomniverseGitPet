namespace ZomniverseGitPet;

/* ==========================================================================
   PATCH: LOGICAL PROJECT SCOPE RUNTIME
   DATE.TIME: 2026-09-11 13:58 +03:00
   Keep project scope separate from shared repository history.
   ========================================================================== */
internal static class LogicalProjectScopeRuntime
{
    private static AppConfig? _config;

    public static void Initialize(AppConfig config) => _config = config;

    public static RecentRepositoryEntry? ActiveProject => _config?.GetActiveProject();

    public static string DisplayName
    {
        get
        {
            var project = ActiveProject;
            if (project is not null && !string.IsNullOrWhiteSpace(project.DisplayName))
                return project.DisplayName;

            var repository = _config?.RepositoryPath;
            if (string.IsNullOrWhiteSpace(repository)) return "NO PROJECT";
            var normalized = Path.TrimEndingDirectorySeparator(repository);
            var name = Path.GetFileName(normalized);
            return string.IsNullOrWhiteSpace(name) ? normalized : name;
        }
    }

    public static string GetWorkingDirectory(string repositoryRoot)
    {
        var project = ActiveProject;
        if (project is null || !PathEquals(project.RepositoryRoot, repositoryRoot)) return repositoryRoot;
        return Directory.Exists(project.Path) ? project.Path : repositoryRoot;
    }

    public static IReadOnlyList<ProjectScopeEntry> GetScopeEntries(string repositoryRoot)
    {
        var project = ActiveProject;
        if (project is null || !PathEquals(project.RepositoryRoot, repositoryRoot) || project.TrackEverything)
            return [];

        var entries = (project.ScopeEntries ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.RelativePath))
            .Select(item => new ProjectScopeEntry(NormalizeRelative(item.RelativePath), item.IsDirectory))
            .Where(item => item.RelativePath.Length > 0)
            .DistinctBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (entries.Length > 0) return entries;

        var relativeProject = TryGetRelativePath(repositoryRoot, project.Path);
        return string.IsNullOrWhiteSpace(relativeProject)
            ? []
            : [new ProjectScopeEntry(relativeProject, IsDirectory: true)];
    }

    public static IReadOnlyList<string> GetPathspecs(string repositoryRoot, bool includeRootGitIgnore = false)
    {
        var project = ActiveProject;
        if (project is null || !PathEquals(project.RepositoryRoot, repositoryRoot) || project.TrackEverything)
            return [];

        var values = GetScopeEntries(repositoryRoot)
            .Select(item => item.RelativePath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (includeRootGitIgnore && !values.Contains(".gitignore", StringComparer.OrdinalIgnoreCase))
            values.Add(".gitignore");
        return values;
    }

    public static bool ContainsPath(string repositoryRoot, string repositoryRelativePath)
    {
        var project = ActiveProject;
        if (project is null || !PathEquals(project.RepositoryRoot, repositoryRoot) || project.TrackEverything)
            return true;

        var candidate = NormalizeRelative(repositoryRelativePath);
        if (candidate.Equals(".gitignore", StringComparison.OrdinalIgnoreCase)) return true;

        foreach (var entry in GetScopeEntries(repositoryRoot))
        {
            var scope = NormalizeRelative(entry.RelativePath);
            if (entry.IsDirectory)
            {
                if (candidate.Equals(scope, StringComparison.OrdinalIgnoreCase) ||
                    candidate.StartsWith(scope + "/", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            else if (candidate.Equals(scope, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    public static string? TryGetRelativePath(string repositoryRoot, string projectPath)
    {
        try
        {
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repositoryRoot));
            var project = Path.TrimEndingDirectorySeparator(Path.GetFullPath(projectPath));
            if (PathEquals(root, project)) return null;

            var prefix = root + Path.DirectorySeparatorChar;
            if (!project.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
            return NormalizeRelative(Path.GetRelativePath(root, project));
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeRelative(string path) =>
        path.Replace('\\', '/').Trim().TrimStart('/').TrimEnd('/');

    private static bool PathEquals(string left, string right)
    {
        try
        {
            left = Path.TrimEndingDirectorySeparator(Path.GetFullPath(left));
            right = Path.TrimEndingDirectorySeparator(Path.GetFullPath(right));
        }
        catch
        {
        }
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }
}
