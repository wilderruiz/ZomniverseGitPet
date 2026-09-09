namespace ZomniverseGitPet;

internal sealed record ProjectScopeEntry(string RelativePath, bool IsDirectory);

internal sealed record ProjectScopePlan(
    string RootPath,
    bool TrackEverything,
    IReadOnlyList<ProjectScopeEntry> Entries,
    IReadOnlyList<string> NestedRepositories)
{
    public string Summary => TrackEverything
        ? NestedRepositories.Count == 0
            ? "Everything in the chosen project root will be visible to Git."
            : $"Everything is selected, with {NestedRepositories.Count} nested Git repositor{(NestedRepositories.Count == 1 ? "y" : "ies")} kept outside this project."
        : $"Selective scope: {Entries.Count} chosen item{(Entries.Count == 1 ? "" : "s")}. Unselected root content stays outside this Git project.";
}

internal static class ProjectScopePlanner
{
    private const int NestedRepositoryScanLimit = 20_000;

    public static ProjectScopePlan Create(
        string root,
        IEnumerable<ProjectScopeEntry> entries,
        bool trackEverything)
    {
        var normalizedRoot = NormalizeRoot(root);
        var normalizedEntries = entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.RelativePath))
            .Select(entry => new ProjectScopeEntry(NormalizeRelative(entry.RelativePath), entry.IsDirectory))
            .Where(entry => entry.RelativePath.Length > 0 && !entry.RelativePath.Equals(".git", StringComparison.OrdinalIgnoreCase))
            .DistinctBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase)
            .OrderBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var nested = FindNestedRepositories(normalizedRoot);
        return new ProjectScopePlan(normalizedRoot, trackEverything, normalizedEntries, nested);
    }

    public static IReadOnlyList<string> BuildIgnoreRules(ProjectScopePlan plan)
    {
        var rules = new List<string>();

        if (!plan.TrackEverything)
        {
            rules.Add("/*");
            rules.Add("!/.gitignore");

            var root = new ScopeNode("");
            foreach (var entry in plan.Entries)
                AddToTree(root, entry);

            foreach (var child in root.Children.Values.OrderBy(node => node.Name, StringComparer.OrdinalIgnoreCase))
                EmitNode(child, "", rules);
        }

        // A parent repository must never silently absorb an existing child repository.
        // These rules come last so they override any broader include rule above.
        foreach (var nested in plan.NestedRepositories.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            AddUnique(rules, "/" + NormalizeRelative(nested).TrimEnd('/') + "/");

        return rules;
    }

    public static IReadOnlyList<string> FindNestedRepositories(string root)
    {
        var normalizedRoot = NormalizeRoot(root);
        var found = new List<string>();
        var queue = new Queue<string>();
        queue.Enqueue(normalizedRoot);
        var visited = 0;

        while (queue.Count > 0 && visited < NestedRepositoryScanLimit)
        {
            var directory = queue.Dequeue();
            IEnumerable<string> children;
            try { children = Directory.EnumerateDirectories(directory); }
            catch { continue; }

            foreach (var child in children)
            {
                if (++visited > NestedRepositoryScanLimit) break;
                var name = Path.GetFileName(child);
                if (ShouldPrune(name)) continue;

                var gitPath = Path.Combine(child, ".git");
                if (Directory.Exists(gitPath) || File.Exists(gitPath))
                {
                    found.Add(Path.GetRelativePath(normalizedRoot, child).Replace('\\', '/'));
                    continue;
                }

                queue.Enqueue(child);
            }
        }

        return found.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static bool IsDirectNestedRepository(string directory)
    {
        try
        {
            var git = Path.Combine(directory, ".git");
            return Directory.Exists(git) || File.Exists(git);
        }
        catch { return false; }
    }

    private static void AddToTree(ScopeNode root, ProjectScopeEntry entry)
    {
        var segments = entry.RelativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) return;

        var current = root;
        for (var i = 0; i < segments.Length; i++)
        {
            if (!current.Children.TryGetValue(segments[i], out var child))
            {
                child = new ScopeNode(segments[i]);
                current.Children.Add(segments[i], child);
            }
            current = child;
        }

        current.Selected = true;
        current.IsDirectory = entry.IsDirectory;
        if (entry.IsDirectory) current.Children.Clear(); // selecting a folder means its whole subtree
    }

    private static void EmitNode(ScopeNode node, string parentPath, List<string> rules)
    {
        var path = parentPath.Length == 0 ? node.Name : parentPath + "/" + node.Name;

        if (node.Selected && !node.IsDirectory)
        {
            AddUnique(rules, "!/" + path);
            return;
        }

        // Directory or an intermediate parent needed to reach a selected descendant.
        AddUnique(rules, "!/" + path + "/");

        if (node.Selected && node.IsDirectory)
        {
            AddUnique(rules, "!/" + path + "/**");
            return;
        }

        // Re-open the directory, then ignore its children until selected descendants are
        // explicitly re-included. This is what makes deep selections such as CV/expertise
        // safe without accidentally including the rest of CV.
        AddUnique(rules, "/" + path + "/*");
        foreach (var child in node.Children.Values.OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase))
            EmitNode(child, path, rules);
    }

    private static void AddUnique(List<string> rules, string rule)
    {
        if (!rules.Contains(rule, StringComparer.OrdinalIgnoreCase)) rules.Add(rule);
    }

    private static string NormalizeRoot(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static string NormalizeRelative(string path) =>
        path.Replace('\\', '/').Trim().TrimStart('/').TrimEnd('/');

    private static bool ShouldPrune(string name) =>
        name.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("node_modules", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("vendor", StringComparison.OrdinalIgnoreCase) ||
        name.Equals(".venv", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("venv", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("obj", StringComparison.OrdinalIgnoreCase);

    private sealed class ScopeNode(string name)
    {
        public string Name { get; } = name;
        public bool Selected { get; set; }
        public bool IsDirectory { get; set; } = true;
        public Dictionary<string, ScopeNode> Children { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
