namespace ZomniverseGitPet;

/* ========================================================================== 
   PATCH: PREFLIGHT IGNORED PROJECT FILES
   DATE: 2026-09-11

   Parse Git provenance and create exact-file stage plans.
   ========================================================================== */
public static class IgnoredFileSavePolicy
{
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
