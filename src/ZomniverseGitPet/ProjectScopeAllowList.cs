namespace ZomniverseGitPet;

internal sealed record ProjectScopeAllowListIssue(
    int LineNumber,
    string Input,
    string Message);

internal sealed record ProjectScopeAllowListResult(
    IReadOnlyList<ProjectScopeEntry> Entries,
    IReadOnlyList<ProjectScopeAllowListIssue> Issues)
{
    public bool Success => Issues.Count == 0;
    public int FileCount => Entries.Count(entry => !entry.IsDirectory);
    public int DirectoryCount => Entries.Count(entry => entry.IsDirectory);
}

/* ==========================================================================
   PATCH: ADVANCED PROJECT ALLOW LIST
   DATE.TIME: 2026-09-11 14:35 +03:00
   REASON:
   Resolve pasted paths into safe deterministic project scope entries.
   ========================================================================== */
internal static class ProjectScopeAllowList
{
    public static ProjectScopeAllowListResult Resolve(string rootPath, string? text)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        var issues = new List<ProjectScopeAllowListIssue>();
        var entries = new Dictionary<string, ProjectScopeEntry>(StringComparer.OrdinalIgnoreCase);
        var lines = (text ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            var original = lines[index];
            var candidate = original.Trim();
            if (candidate.Length == 0 || candidate.StartsWith('#')) continue;

            candidate = TrimMatchingQuotes(candidate);
            if (candidate.Length == 0) continue;

            string fullPath;
            try
            {
                var normalizedInput = candidate.Replace('/', Path.DirectorySeparatorChar);
                fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(
                    Path.IsPathRooted(normalizedInput)
                        ? normalizedInput
                        : Path.Combine(root, normalizedInput)));
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                issues.Add(new ProjectScopeAllowListIssue(index + 1, original, "Path is not valid."));
                continue;
            }

            var relative = Path.GetRelativePath(root, fullPath).Replace('\\', '/');
            if (relative == "." || IsOutsideRoot(relative))
            {
                issues.Add(new ProjectScopeAllowListIssue(
                    index + 1,
                    original,
                    relative == "."
                        ? "List a file or folder inside the project root, not the root itself."
                        : "Path is outside the project root."));
                continue;
            }

            if (ContainsGitMetadataSegment(relative))
            {
                issues.Add(new ProjectScopeAllowListIssue(index + 1, original, ".git metadata cannot be selected."));
                continue;
            }

            var isDirectory = Directory.Exists(fullPath);
            var isFile = File.Exists(fullPath);
            if (!isDirectory && !isFile)
            {
                issues.Add(new ProjectScopeAllowListIssue(index + 1, original, "Path was not found on this computer."));
                continue;
            }

            var nestedRepository = FindNestedRepository(root, fullPath, isDirectory);
            if (nestedRepository is not null)
            {
                var nestedRelative = Path.GetRelativePath(root, nestedRepository).Replace('\\', '/');
                issues.Add(new ProjectScopeAllowListIssue(
                    index + 1,
                    original,
                    $"Path belongs to nested Git repository '{nestedRelative}'."));
                continue;
            }

            AddCanonicalEntry(entries, new ProjectScopeEntry(relative, isDirectory));
        }

        return new ProjectScopeAllowListResult(
            entries.Values
                .OrderBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            issues);
    }

    public static string Format(IEnumerable<ProjectScopeEntry> entries) =>
        string.Join(
            Environment.NewLine,
            entries
                .OrderBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase)
                .Select(entry => entry.IsDirectory
                    ? entry.RelativePath.TrimEnd('/') + "/"
                    : entry.RelativePath));

    public static string FormatIssueSummary(ProjectScopeAllowListResult result, int maxIssues = 3)
    {
        if (result.Success)
            return $"{result.FileCount} file{Plural(result.FileCount)} and {result.DirectoryCount} folder{Plural(result.DirectoryCount)} ready to apply.";

        var shown = result.Issues
            .Take(Math.Max(1, maxIssues))
            .Select(issue => $"Line {issue.LineNumber}: {issue.Message}")
            .ToArray();
        var remaining = result.Issues.Count - shown.Length;
        return string.Join("  ", shown) + (remaining > 0 ? $"  +{remaining} more issue{Plural(remaining)}." : string.Empty);
    }

    private static void AddCanonicalEntry(
        Dictionary<string, ProjectScopeEntry> entries,
        ProjectScopeEntry candidate)
    {
        var normalized = Normalize(candidate.RelativePath);

        if (entries.Values.Any(entry =>
                entry.IsDirectory &&
                IsDescendant(normalized, Normalize(entry.RelativePath))))
        {
            return;
        }

        if (candidate.IsDirectory)
        {
            foreach (var key in entries.Keys
                         .Where(existing => IsDescendant(Normalize(existing), normalized))
                         .ToArray())
            {
                entries.Remove(key);
            }
        }

        entries[normalized] = new ProjectScopeEntry(normalized, candidate.IsDirectory);
    }

    private static string? FindNestedRepository(string root, string fullPath, bool isDirectory)
    {
        var current = isDirectory ? fullPath : Path.GetDirectoryName(fullPath);
        while (!string.IsNullOrWhiteSpace(current) && !PathsEqual(current, root))
        {
            var metadata = Path.Combine(current, ".git");
            if (Directory.Exists(metadata) || File.Exists(metadata)) return current;

            var parent = Directory.GetParent(current)?.FullName;
            if (string.IsNullOrWhiteSpace(parent) || PathsEqual(parent, current)) break;
            current = parent;
        }

        return null;
    }

    private static bool ContainsGitMetadataSegment(string relativePath) =>
        relativePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment.Equals(".git", StringComparison.OrdinalIgnoreCase));

    private static bool IsOutsideRoot(string relativePath) =>
        Path.IsPathRooted(relativePath) ||
        relativePath.Equals("..", StringComparison.Ordinal) ||
        relativePath.StartsWith("../", StringComparison.Ordinal);

    private static bool IsDescendant(string candidate, string ancestor) =>
        ancestor.Length > 0 &&
        candidate.StartsWith(ancestor + "/", StringComparison.OrdinalIgnoreCase);

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string path) =>
        (path ?? string.Empty).Replace('\\', '/').Trim().Trim('/');

    private static string TrimMatchingQuotes(string value)
    {
        if (value.Length < 2) return value;
        var first = value[0];
        var last = value[^1];
        return (first == '"' && last == '"') || (first == '\'' && last == '\'')
            ? value[1..^1].Trim()
            : value;
    }

    private static string Plural(int count) => count == 1 ? string.Empty : "s";
}
