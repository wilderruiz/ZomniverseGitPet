namespace ZomniverseGitPet;

internal sealed record GitIgnoreDuplicateRule(string Rule, int Occurrences);

internal static class GitIgnoreCleaner
{
    public static IReadOnlyList<GitIgnoreDuplicateRule> FindDuplicateRules(string content)
    {
        if (string.IsNullOrEmpty(content)) return Array.Empty<GitIgnoreDuplicateRule>();

        return NormalizeLines(content)
            .Select(line => line.Trim())
            .Where(IsRuleLine)
            .GroupBy(line => line, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => new GitIgnoreDuplicateRule(group.Key, group.Count()))
            .OrderBy(item => item.Rule, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string RemoveDuplicateRules(string content, out int removed)
    {
        removed = 0;
        if (string.IsNullOrEmpty(content)) return content;

        var newline = DetectNewline(content);
        var trailingNewline = EndsWithNewline(content);
        var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var lines = normalized.Split('\n').ToList();
        if (trailingNewline && lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var kept = new List<string>(lines.Count);
        foreach (var line in lines)
        {
            var rule = line.Trim();
            if (IsRuleLine(rule) && !seen.Add(rule))
            {
                removed++;
                continue;
            }
            kept.Add(line);
        }

        var result = string.Join(newline, kept);
        if (trailingNewline) result += newline;
        return result;
    }

    private static IEnumerable<string> NormalizeLines(string content) =>
        content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');

    private static bool IsRuleLine(string line) =>
        line.Length > 0 && !line.StartsWith('#');

    private static string DetectNewline(string content)
    {
        if (content.Contains("\r\n", StringComparison.Ordinal)) return "\r\n";
        if (content.Contains('\r') && !content.Contains('\n')) return "\r";
        return "\n";
    }

    private static bool EndsWithNewline(string content) =>
        content.EndsWith("\r\n", StringComparison.Ordinal) ||
        content.EndsWith('\n') ||
        content.EndsWith('\r');
}
