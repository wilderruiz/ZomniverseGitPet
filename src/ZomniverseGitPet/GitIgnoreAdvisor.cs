namespace ZomniverseGitPet;

internal enum GitIgnoreConfidence
{
    High,
    Security,
    Review
}

internal sealed record GitIgnoreSuggestion(
    string Rule,
    string Description,
    GitIgnoreConfidence Confidence,
    bool DefaultSelected);

internal static class GitIgnoreAdvisor
{
    private static readonly Dictionary<string, GitIgnoreSuggestion> DirectorySuggestions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["bin"] = new("bin/", ".NET/build output", GitIgnoreConfidence.High, true),
            ["obj"] = new("obj/", ".NET intermediate build files", GitIgnoreConfidence.High, true),
            [".vs"] = new(".vs/", "Visual Studio workspace state", GitIgnoreConfidence.High, true),
            [".idea"] = new(".idea/", "JetBrains IDE workspace state", GitIgnoreConfidence.High, true),
            ["node_modules"] = new("node_modules/", "Node package dependencies", GitIgnoreConfidence.High, true),
            ["__pycache__"] = new("__pycache__/", "Python bytecode cache", GitIgnoreConfidence.High, true),
            [".pytest_cache"] = new(".pytest_cache/", "pytest cache", GitIgnoreConfidence.High, true),
            [".mypy_cache"] = new(".mypy_cache/", "mypy cache", GitIgnoreConfidence.High, true),
            [".ruff_cache"] = new(".ruff_cache/", "Ruff cache", GitIgnoreConfidence.High, true),
            [".venv"] = new(".venv/", "Python virtual environment", GitIgnoreConfidence.High, true),
            ["venv"] = new("venv/", "Python virtual environment", GitIgnoreConfidence.High, true),
            ["coverage"] = new("coverage/", "Generated coverage report", GitIgnoreConfidence.Review, false),
            ["dist"] = new("dist/", "Generated distribution/build output; confirm this project does not intentionally commit it", GitIgnoreConfidence.Review, false),
            [".cache"] = new(".cache/", "Tool cache; confirm it is generated", GitIgnoreConfidence.Review, false),
            ["tmp"] = new("tmp/", "Temporary files; confirm the folder is generated", GitIgnoreConfidence.Review, false),
            ["temp"] = new("temp/", "Temporary files; confirm the folder is generated", GitIgnoreConfidence.Review, false)
        };

    public static IReadOnlyList<GitIgnoreSuggestion> Suggest(string root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return [];

        var existing = ReadExistingRules(root);
        var found = new Dictionary<string, GitIgnoreSuggestion>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<(string Path, int Depth)>();
        queue.Enqueue((root, 0));
        var sampledEntries = 0;

        while (queue.Count > 0 && sampledEntries < 10_000)
        {
            var (directory, depth) = queue.Dequeue();
            IEnumerable<string> entries;
            try { entries = Directory.EnumerateFileSystemEntries(directory); }
            catch { continue; }

            foreach (var entry in entries)
            {
                if (++sampledEntries > 10_000) break;
                var name = Path.GetFileName(entry);
                bool isDirectory;
                try { isDirectory = Directory.Exists(entry); }
                catch { continue; }

                if (isDirectory)
                {
                    if (name.Equals(".git", StringComparison.OrdinalIgnoreCase)) continue;
                    if (DirectorySuggestions.TryGetValue(name, out var suggestion))
                        AddIfMissing(found, existing, suggestion);

                    if (depth < 4 && !ShouldPruneDirectory(name)) queue.Enqueue((entry, depth + 1));
                    continue;
                }

                AddFileSuggestion(name, found, existing);
            }
        }

        return found.Values
            .OrderBy(item => item.Confidence == GitIgnoreConfidence.Security ? 0 : item.Confidence == GitIgnoreConfidence.High ? 1 : 2)
            .ThenBy(item => item.Rule, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string ReadCurrentContent(string root)
    {
        var ignorePath = Path.Combine(root, ".gitignore");
        return File.Exists(ignorePath) ? File.ReadAllText(ignorePath) : "";
    }

    public static string BuildPreviewContent(string root, IEnumerable<string> rules)
    {
        ProjectGitIgnoreComposer.SplitCombinedRules(rules, out var scope, out var suggestions);
        return ProjectGitIgnoreComposer.BuildPreview(root, scope, suggestions);
    }

    public static int AppendAcceptedRules(string root, IEnumerable<string> rules)
    {
        ProjectGitIgnoreComposer.SplitCombinedRules(rules, out var scope, out var suggestions);
        return ProjectGitIgnoreComposer.Apply(root, scope, suggestions);
    }

    private static HashSet<string> ReadExistingRules(string root)
    {
        try { return ParseRules(ReadCurrentContent(root)); }
        catch { return new HashSet<string>(StringComparer.OrdinalIgnoreCase); }
    }

    private static HashSet<string> ParseRules(string text) =>
        text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static void AddFileSuggestion(
        string fileName,
        Dictionary<string, GitIgnoreSuggestion> found,
        HashSet<string> existing)
    {
        if (fileName.Equals(".DS_Store", StringComparison.OrdinalIgnoreCase))
            AddIfMissing(found, existing, new(".DS_Store", "macOS Finder metadata", GitIgnoreConfidence.High, true));
        else if (fileName.Equals("Thumbs.db", StringComparison.OrdinalIgnoreCase))
            AddIfMissing(found, existing, new("Thumbs.db", "Windows thumbnail cache", GitIgnoreConfidence.High, true));
        else if (fileName.Equals(".env", StringComparison.OrdinalIgnoreCase))
            AddIfMissing(found, existing, new(".env", "Environment configuration may contain secrets", GitIgnoreConfidence.Security, true));
        else if (fileName.StartsWith(".env.", StringComparison.OrdinalIgnoreCase) && !IsEnvironmentTemplate(fileName) && !existing.Contains(".env.*"))
            AddIfMissing(found, existing, new(fileName, "Environment-specific configuration may contain secrets", GitIgnoreConfidence.Security, true));
        else if (fileName.EndsWith(".pem", StringComparison.OrdinalIgnoreCase))
            AddIfMissing(found, existing, new("*.pem", "Private/certificate key material should be reviewed before tracking", GitIgnoreConfidence.Security, true));
        else if (fileName.EndsWith(".key", StringComparison.OrdinalIgnoreCase))
            AddIfMissing(found, existing, new("*.key", "Private key material should not normally be tracked", GitIgnoreConfidence.Security, true));
        else if (fileName.EndsWith(".user", StringComparison.OrdinalIgnoreCase))
            AddIfMissing(found, existing, new("*.user", "Developer-specific Visual Studio settings", GitIgnoreConfidence.High, true));
        else if (fileName.EndsWith(".suo", StringComparison.OrdinalIgnoreCase))
            AddIfMissing(found, existing, new("*.suo", "Visual Studio user options", GitIgnoreConfidence.High, true));
        else if (fileName.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
            AddIfMissing(found, existing, new("*.log", "Generated log files", GitIgnoreConfidence.High, true));
    }

    private static bool IsEnvironmentTemplate(string fileName) =>
        fileName.Equals(".env.example", StringComparison.OrdinalIgnoreCase) ||
        fileName.Equals(".env.sample", StringComparison.OrdinalIgnoreCase) ||
        fileName.Equals(".env.template", StringComparison.OrdinalIgnoreCase) ||
        fileName.Equals(".env.dist", StringComparison.OrdinalIgnoreCase);

    private static void AddIfMissing(
        Dictionary<string, GitIgnoreSuggestion> found,
        HashSet<string> existing,
        GitIgnoreSuggestion suggestion)
    {
        if (!existing.Contains(suggestion.Rule)) found.TryAdd(suggestion.Rule, suggestion);
    }

    private static bool ShouldPruneDirectory(string name) =>
        name.Equals("node_modules", StringComparison.OrdinalIgnoreCase) ||
        name.Equals(".vs", StringComparison.OrdinalIgnoreCase) ||
        name.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
        name.Equals(".venv", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("venv", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("obj", StringComparison.OrdinalIgnoreCase);
}
