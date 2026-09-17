namespace ZomniverseGitPet;

/* ========================================================================== 
   PATCH: PREFLIGHT IGNORED PROJECT FILES
   DATE: 2026-09-11

   Parse Git provenance and create exact-file stage plans.
   ========================================================================== */
public static class IgnoredFileSavePolicy
{
    public static bool TryParseCheckIgnoreBatch(
        string output,
        out IReadOnlyList<IgnoredProjectFile> items,
        out string error)
    {
        items = [];
        error = "";
        if (string.IsNullOrEmpty(output)) return true;

        var fields = output.Split('\0');
        var fieldCount = fields.Length;
        if (fields[^1].Length == 0) fieldCount--;
        if (fieldCount % 4 != 0)
        {
            error = $"Git returned malformed ignored-file provenance ({fieldCount} fields; expected groups of 4).";
            return false;
        }

        var parsed = new List<IgnoredProjectFile>(fieldCount / 4);
        for (var index = 0; index < fieldCount; index += 4)
        {
            if (string.IsNullOrWhiteSpace(fields[index]) ||
                string.IsNullOrWhiteSpace(fields[index + 2]) ||
                string.IsNullOrWhiteSpace(fields[index + 3]))
            {
                error = $"Git returned incomplete ignored-file provenance at record {(index / 4) + 1}.";
                return false;
            }

            parsed.Add(new IgnoredProjectFile(
                Normalize(fields[index + 3]),
                fields[index],
                int.TryParse(fields[index + 1], out var line) && line > 0 ? line : null,
                fields[index + 2]));
        }

        items = parsed;
        return true;
    }

    public static IgnoredProjectFile? ParseCheckIgnore(string output)
    {
        var fields = output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length >= 4)
        {
            return new IgnoredProjectFile(
                Normalize(fields[3]),
                fields[0],
                int.TryParse(fields[1], out var nulLine) && nulLine > 0 ? nulLine : null,
                fields[2]);
        }

        var tab = output.IndexOf('\t');
        if (tab < 0) return null;
        var provenance = output[..tab];
        var path = output[(tab + 1)..].TrimEnd('\r', '\n');
        var lineSeparator = provenance.LastIndexOf(':');
        if (lineSeparator < 0) return null;
        var sourceAndLine = provenance[..lineSeparator];
        var rule = provenance[(lineSeparator + 1)..];
        var sourceSeparator = sourceAndLine.LastIndexOf(':');
        if (sourceSeparator < 0) return null;
        var source = sourceAndLine[..sourceSeparator];
        var lineText = sourceAndLine[(sourceSeparator + 1)..];

        return new IgnoredProjectFile(
            Normalize(path),
            source,
            int.TryParse(lineText, out var textLine) && textLine > 0 ? textLine : null,
            rule);
    }

    public static SaveStagePlan CreateStagePlan(
        IEnumerable<string> normalFiles,
        IEnumerable<IgnoredProjectFile> ignoredFiles,
        IEnumerable<string> approvedPaths)
    {
        var approved = approvedPaths.Select(Normalize)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ignored = ignoredFiles
            .GroupBy(item => Normalize(item.Path), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();

        return new SaveStagePlan(
            ExactFiles(normalFiles),
            ExactFiles(ignored.Where(item => approved.Contains(Normalize(item.Path))).Select(item => item.Path)),
            ExactFiles(ignored.Where(item => !approved.Contains(Normalize(item.Path))).Select(item => item.Path)));
    }

    public static IReadOnlyList<string> BuildStageArguments(
        IEnumerable<string> exactFiles,
        bool force)
    {
        var files = ExactFiles(exactFiles);
        if (files.Count == 0) return [];

        var arguments = new List<string> { "add" };
        if (force) arguments.Add("-f");
        else arguments.Add("-A");
        arguments.Add("--");
        arguments.AddRange(files);
        return arguments;
    }

    private static IReadOnlyList<string> ExactFiles(IEnumerable<string> paths) =>
        paths.Select(Normalize)
            .Where(path => path.Length > 0 && path != ".")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string Normalize(string path) =>
        (path ?? "").Replace('\\', '/').Trim().TrimStart('/');
}
