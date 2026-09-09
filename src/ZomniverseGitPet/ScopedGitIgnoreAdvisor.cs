namespace ZomniverseGitPet;

internal sealed record GitIgnoreDocument(string RelativePath, string Content);

internal static class ScopedGitIgnoreAdvisor
{
    private const int IgnoreDocumentScanLimit = 10_000;

    public static IReadOnlyList<GitIgnoreSuggestion> Suggest(ProjectScopePlan plan)
    {
        var found = new Dictionary<string, GitIgnoreSuggestion>(StringComparer.OrdinalIgnoreCase);

        if (plan.TrackEverything)
        {
            MergeSuggestions(found, GitIgnoreAdvisor.Suggest(plan.RootPath));

            IEnumerable<string> children;
            try { children = Directory.EnumerateDirectories(plan.RootPath).Take(250).ToArray(); }
            catch { children = []; }
            foreach (var child in children)
            {
                if (ProjectScopePlanner.IsDirectNestedRepository(child)) continue;
                MergeSuggestions(found, GitIgnoreAdvisor.Suggest(child));
            }
        }
        else
        {
            foreach (var entry in plan.Entries)
            {
                var full = Path.GetFullPath(Path.Combine(plan.RootPath, entry.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
                if (entry.IsDirectory && Directory.Exists(full))
                    MergeSuggestions(found, GitIgnoreAdvisor.Suggest(full));
                else if (!entry.IsDirectory && File.Exists(full))
                    AddFileOnlySuggestion(found, Path.GetFileName(full));
            }
        }

        return found.Values
            .Where(item => !IsEnvironmentTemplateRule(item.Rule))
            .OrderBy(item => item.Confidence == GitIgnoreConfidence.Security ? 0 : item.Confidence == GitIgnoreConfidence.High ? 1 : 2)
            .ThenBy(item => item.Rule, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<GitIgnoreDocument> FindIgnoreDocuments(ProjectScopePlan plan)
    {
        var found = new Dictionary<string, GitIgnoreDocument>(StringComparer.OrdinalIgnoreCase);
        AddDocument(plan.RootPath, plan.RootPath, found);

        var roots = GetScanRoots(plan);
        var visited = 0;
        foreach (var scanRoot in roots)
        {
            var queue = new Queue<(string Path, int Depth)>();
            queue.Enqueue((scanRoot, 0));

            while (queue.Count > 0 && visited < IgnoreDocumentScanLimit)
            {
                var (directory, depth) = queue.Dequeue();
                if (++visited > IgnoreDocumentScanLimit) break;

                if (!PathEquals(directory, plan.RootPath))
                    AddDocument(plan.RootPath, directory, found);

                if (depth >= 6) continue;
                IEnumerable<string> children;
                try { children = Directory.EnumerateDirectories(directory); }
                catch { continue; }

                foreach (var child in children)
                {
                    var name = Path.GetFileName(child);
                    if (ShouldPrune(name) || ProjectScopePlanner.IsDirectNestedRepository(child)) continue;
                    queue.Enqueue((child, depth + 1));
                }
            }
        }

        return found.Values
            .OrderBy(doc => doc.RelativePath.Equals(".gitignore", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(doc => doc.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string BuildCurrentOverview(string root, IReadOnlyList<GitIgnoreDocument> documents)
    {
        var rootContent = GitIgnoreAdvisor.ReadCurrentContent(root);
        var builder = new System.Text.StringBuilder();

        builder.AppendLine("ROOT .gitignore");
        builder.AppendLine(Path.Combine(root, ".gitignore"));
        builder.AppendLine();
        builder.AppendLine(string.IsNullOrEmpty(rootContent) ? "(no root .gitignore yet / empty)" : rootContent.TrimEnd());

        var nested = documents.Where(doc => !doc.RelativePath.Equals(".gitignore", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (nested.Length > 0)
        {
            builder.AppendLine();
            builder.AppendLine("────────────────────────────────────────");
            builder.AppendLine($"NESTED .gitignore FILES ALREADY IN EFFECT ({nested.Length})");
            builder.AppendLine("GitPet leaves these files unchanged; they continue to control their own subfolders.");

            foreach (var document in nested)
            {
                builder.AppendLine();
                builder.AppendLine("▶ " + document.RelativePath);
                builder.AppendLine(document.Content.TrimEnd());
            }
        }

        return builder.ToString();
    }

    private static IEnumerable<string> GetScanRoots(ProjectScopePlan plan)
    {
        if (plan.TrackEverything) return [plan.RootPath];

        return plan.Entries
            .Where(entry => entry.IsDirectory)
            .Select(entry => Path.GetFullPath(Path.Combine(plan.RootPath, entry.RelativePath.Replace('/', Path.DirectorySeparatorChar))))
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AddDocument(string projectRoot, string directory, Dictionary<string, GitIgnoreDocument> found)
    {
        try
        {
            var path = Path.Combine(directory, ".gitignore");
            if (!File.Exists(path)) return;
            var relative = Path.GetRelativePath(projectRoot, path).Replace('\\', '/');
            found.TryAdd(relative, new GitIgnoreDocument(relative, File.ReadAllText(path)));
        }
        catch { }
    }

    private static void MergeSuggestions(
        Dictionary<string, GitIgnoreSuggestion> target,
        IEnumerable<GitIgnoreSuggestion> suggestions)
    {
        foreach (var suggestion in suggestions)
        {
            if (IsEnvironmentTemplateRule(suggestion.Rule)) continue;
            target.TryAdd(suggestion.Rule, suggestion);
        }
    }

    private static void AddFileOnlySuggestion(Dictionary<string, GitIgnoreSuggestion> found, string fileName)
    {
        if (fileName.Equals(".env", StringComparison.OrdinalIgnoreCase))
            found.TryAdd(".env", new(".env", "Environment configuration may contain secrets", GitIgnoreConfidence.Security, true));
        else if (fileName.StartsWith(".env.", StringComparison.OrdinalIgnoreCase) && !IsEnvironmentTemplateRule(fileName))
            found.TryAdd(fileName, new(fileName, "Environment-specific configuration may contain secrets", GitIgnoreConfidence.Security, true));
        else if (fileName.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
            found.TryAdd("*.log", new("*.log", "Generated log files", GitIgnoreConfidence.High, true));
        else if (fileName.EndsWith(".pem", StringComparison.OrdinalIgnoreCase))
            found.TryAdd("*.pem", new("*.pem", "Private/certificate key material should be reviewed before tracking", GitIgnoreConfidence.Security, true));
        else if (fileName.EndsWith(".key", StringComparison.OrdinalIgnoreCase))
            found.TryAdd("*.key", new("*.key", "Private key material should not normally be tracked", GitIgnoreConfidence.Security, true));
    }

    private static bool IsEnvironmentTemplateRule(string rule)
    {
        var name = Path.GetFileName(rule);
        if (!name.StartsWith(".env", StringComparison.OrdinalIgnoreCase)) return false;
        return name.EndsWith(".example", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith(".sample", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith(".template", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith(".dist", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldPrune(string name) =>
        name.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("node_modules", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("vendor", StringComparison.OrdinalIgnoreCase) ||
        name.Equals(".venv", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("venv", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("obj", StringComparison.OrdinalIgnoreCase);

    private static bool PathEquals(string left, string right)
    {
        try
        {
            return string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
                StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }
}

internal static class ProjectGitIgnoreComposer
{
    private const string ScopeHeader = "# ZomniverseGitPet selected project scope";
    private const string ScopeBegin = "# >>> ZomniverseGitPet managed project scope >>>";
    private const string ScopeEnd = "# <<< ZomniverseGitPet managed project scope <<<";
    private const string SuggestedHeader = "# Suggested by ZomniverseGitPet — files and folders Git should leave alone";
    private const string Divider = "# =============================================================================";

    public static string BuildPreview(
        string root,
        IEnumerable<string> scopeRules,
        IEnumerable<string> suggestionRules)
    {
        var current = GitIgnoreAdvisor.ReadCurrentContent(root);
        return BuildUpdated(current, scopeRules, suggestionRules, out _);
    }

    public static string BuildPreviewReplacingScope(
        string root,
        IEnumerable<string> scopeRules,
        IEnumerable<string> suggestionRules)
    {
        var current = GitIgnoreAdvisor.ReadCurrentContent(root);
        var withoutManagedScope = RemoveManagedScopeSection(current);
        return BuildUpdated(withoutManagedScope, scopeRules, suggestionRules, out _);
    }

    public static int Apply(
        string root,
        IEnumerable<string> scopeRules,
        IEnumerable<string> suggestionRules)
    {
        var path = Path.Combine(root, ".gitignore");
        var current = GitIgnoreAdvisor.ReadCurrentContent(root);
        var updated = BuildUpdated(current, scopeRules, suggestionRules, out var added);
        if (added > 0) File.WriteAllText(path, updated);
        return added;
    }

    public static bool ApplyReplacingScope(
        string root,
        IEnumerable<string> scopeRules,
        IEnumerable<string> suggestionRules)
    {
        var path = Path.Combine(root, ".gitignore");
        var current = GitIgnoreAdvisor.ReadCurrentContent(root);
        var withoutManagedScope = RemoveManagedScopeSection(current);
        var updated = BuildUpdated(withoutManagedScope, scopeRules, suggestionRules, out _);
        if (string.Equals(current, updated, StringComparison.Ordinal)) return false;
        File.WriteAllText(path, updated);
        return true;
    }

    public static void SplitCombinedRules(
        IEnumerable<string> rules,
        out IReadOnlyList<string> scopeRules,
        out IReadOnlyList<string> suggestionRules)
    {
        var scope = new List<string>();
        var suggestions = new List<string>();
        foreach (var rule in Normalize(rules))
        {
            if (LooksLikeScopeRule(rule)) scope.Add(rule);
            else suggestions.Add(rule);
        }
        scopeRules = scope;
        suggestionRules = suggestions;
    }

    private static string BuildUpdated(
        string current,
        IEnumerable<string> scopeRules,
        IEnumerable<string> suggestionRules,
        out int added)
    {
        var existing = ParseRules(current);
        var scope = Normalize(scopeRules).Where(rule => !existing.Contains(rule)).ToArray();
        foreach (var rule in scope) existing.Add(rule);
        var suggestions = Normalize(suggestionRules).Where(rule => !existing.Contains(rule)).ToArray();
        added = scope.Length + suggestions.Length;
        if (added == 0) return current;

        var builder = new System.Text.StringBuilder(current);
        EnsureLineBoundary(builder);

        if (scope.Length > 0)
        {
            StartSection(builder);
            builder.AppendLine(ScopeBegin);
            builder.AppendLine(ScopeHeader);
            builder.AppendLine("# GitPet uses this block to make the chosen parent folder a selective project.");
            builder.AppendLine("# '/*' hides root content first; the following ! rules re-include only what you selected.");
            builder.AppendLine("# Existing nested Git repositories are explicitly kept outside this parent repository.");
            builder.AppendLine("# Reconfigure Project Setup to replace this managed scope safely later.");
            builder.AppendLine(Divider);
            foreach (var rule in scope) builder.AppendLine(rule);
            builder.AppendLine(ScopeEnd);
        }

        if (suggestions.Length > 0)
        {
            StartSection(builder);
            if (!ContainsHeader(current, SuggestedHeader)) builder.AppendLine(SuggestedHeader);
            builder.AppendLine("# These ignore patterns were explicitly selected in GitPet.");
            builder.AppendLine("# A pattern without a leading / applies by name throughout the project tree.");
            builder.AppendLine("# Lines beginning with ! are safe exceptions that keep matching templates visible to Git.");
            builder.AppendLine(Divider);
            foreach (var rule in suggestions)
            {
                builder.AppendLine(DescribeRule(rule));
                builder.AppendLine(rule);
            }
        }

        return builder.ToString();
    }

    private static string RemoveManagedScopeSection(string current)
    {
        if (string.IsNullOrEmpty(current)) return current;

        var newline = current.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var normalized = current.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var lines = normalized.Split('\n').ToList();

        var markerStart = lines.FindIndex(line => line.Trim().Equals(ScopeBegin, StringComparison.OrdinalIgnoreCase));
        if (markerStart >= 0)
        {
            var markerEnd = lines.FindIndex(markerStart + 1,
                line => line.Trim().Equals(ScopeEnd, StringComparison.OrdinalIgnoreCase));
            if (markerEnd < markerStart) markerEnd = markerStart;
            var start = ExpandStartToSectionBoundary(lines, markerStart);
            lines.RemoveRange(start, markerEnd - start + 1);
            return string.Join(newline, lines);
        }

        // Compatibility with 0.3.6-0.3.9 scope blocks, which predate explicit begin/end markers.
        var header = lines.FindIndex(line => line.Trim().Equals(ScopeHeader, StringComparison.OrdinalIgnoreCase));
        if (header < 0) return current;

        var legacyStart = ExpandStartToSectionBoundary(lines, header);
        var cursor = header + 1;
        while (cursor < lines.Count && !lines[cursor].Trim().Equals(Divider, StringComparison.Ordinal)) cursor++;
        if (cursor < lines.Count) cursor++;

        while (cursor < lines.Count)
        {
            var value = lines[cursor].Trim();
            if (value.Length == 0 || LooksLikeScopeRule(value))
            {
                cursor++;
                continue;
            }
            break;
        }

        lines.RemoveRange(legacyStart, Math.Max(0, cursor - legacyStart));
        return string.Join(newline, lines);
    }

    private static int ExpandStartToSectionBoundary(IReadOnlyList<string> lines, int start)
    {
        if (start > 0 && lines[start - 1].Trim().Equals(Divider, StringComparison.Ordinal)) start--;
        if (start > 0 && string.IsNullOrWhiteSpace(lines[start - 1])) start--;
        return start;
    }

    private static string DescribeRule(string rule)
    {
        if (rule.Equals(".env", StringComparison.OrdinalIgnoreCase))
            return "# Ignore real .env secret/configuration files anywhere in the project.";
        if (rule.Equals(".env.*", StringComparison.OrdinalIgnoreCase))
            return "# Ignore environment variants such as .env.local or .env.production anywhere.";
        if (rule.StartsWith("!.env.", StringComparison.OrdinalIgnoreCase))
            return "# Keep this common environment template visible so it can be committed safely.";
        if (rule.Contains("LEGACY", StringComparison.Ordinal) || rule.Contains("legacy", StringComparison.Ordinal))
            return "# Ignore files or folders whose name contains LEGACY/legacy anywhere in the project.";
        if (rule.StartsWith("*.", StringComparison.Ordinal))
            return $"# Ignore files ending in {rule[1..]} anywhere in the project.";
        if (rule.Length > 0 && rule[^1] == '/' && rule[0] != '/')
            return $"# Ignore folders named '{rule.TrimEnd('/')}' anywhere in the project.";
        if (rule.StartsWith("**/*", StringComparison.Ordinal))
            return "# Ignore names matching this text pattern anywhere in the project.";
        if (!rule.Contains('/'))
            return $"# Ignore items named '{rule}' anywhere in the project.";
        return "# Ignore items matching this explicitly selected Git pattern.";
    }

    private static bool LooksLikeScopeRule(string rule) =>
        rule.StartsWith("/", StringComparison.Ordinal) ||
        rule.StartsWith("!/", StringComparison.Ordinal);

    private static void StartSection(System.Text.StringBuilder builder)
    {
        EnsureLineBoundary(builder);
        if (builder.Length > 0) builder.AppendLine();
        builder.AppendLine(Divider);
    }

    private static string[] Normalize(IEnumerable<string> rules) =>
        rules.Where(rule => !string.IsNullOrWhiteSpace(rule))
            .Select(rule => rule.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static HashSet<string> ParseRules(string text) =>
        text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static bool ContainsHeader(string text, string header) =>
        text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Any(line => line.Trim().Equals(header, StringComparison.OrdinalIgnoreCase));

    private static void EnsureLineBoundary(System.Text.StringBuilder builder)
    {
        if (builder.Length > 0 && builder[^1] != '\n' && builder[^1] != '\r') builder.AppendLine();
    }
}
