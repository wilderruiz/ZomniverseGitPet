using System.Text;

namespace ZomniverseGitPet;

internal enum ReconcileCopyRole
{
    Base,
    Local,
    Remote,
    Merged
}

internal static class ReconcileCopyExport
{
    public static string BuildChangedBlock(
        string rawSource,
        IReadOnlySet<int> changedLines)
    {
        if (string.IsNullOrEmpty(rawSource) || changedLines.Count == 0)
            return "";

        var lines = NormalizeLf(rawSource).Split('\n');
        var selected = new List<string>();

        for (var i = 0; i < lines.Length; i++)
        {
            if (changedLines.Contains(i + 1))
                selected.Add(lines[i]);
        }

        return string.Join(Environment.NewLine, selected);
    }

    public static string BuildComparison(
        ReconcileInspectorSourceModel model,
        ReconcileInspectorView view)
    {
        var (leftLabel, left, rightLabel, right) = view switch
        {
            ReconcileInspectorView.BaseLocal =>
                ("BASE", model.Base, "LOCAL", model.Local),
            ReconcileInspectorView.LocalRemote =>
                ("LOCAL", model.Local, "REMOTE", model.Remote),
            ReconcileInspectorView.BaseRemote =>
                ("BASE", model.Base, "REMOTE", model.Remote),
            ReconcileInspectorView.MergedPreview =>
                ("LOCAL BEFORE MERGE", model.Local, "MERGED CANDIDATE",
                    new ReconcileSourceSnapshot(
                        "MERGED",
                        "preview",
                        "",
                        "",
                        "generated-preview",
                        model.MergePreview.Available && model.MergePreview.Exists,
                        model.MergePreview.Text)),
            _ => ("BASE", model.Base, "LOCAL", model.Local)
        };

        var builder = new StringBuilder();
        AppendPlainSource(builder, leftLabel, left);
        builder.AppendLine();
        builder.AppendLine(new string('=', 72));
        builder.AppendLine();
        AppendPlainSource(builder, rightLabel, right);
        return builder.ToString().TrimEnd();
    }

    public static string BuildCodeOnlyMarkdown(ReconcileInspectorSourceModel model)
    {
        var fence = MarkdownFenceLanguage(model.RelativePath);
        var builder = new StringBuilder();

        AppendMarkdownCode(builder, "BASE", model.Base.Text, model.Base.Exists, fence);
        AppendMarkdownCode(builder, "LOCAL", model.Local.Text, model.Local.Exists, fence);
        AppendMarkdownCode(builder, "REMOTE", model.Remote.Text, model.Remote.Exists, fence);

        if (model.MergePreview.Available)
            AppendMarkdownCode(
                builder,
                "MERGED CANDIDATE",
                model.MergePreview.Text,
                model.MergePreview.Exists,
                fence);

        return builder.ToString().TrimEnd();
    }

    public static string BuildEverythingMarkdown(
        ReconcileInspectorSourceModel model,
        string workboardDetail)
    {
        var summary = ReconcileSummaryPresentation.Create(
            model.Analysis,
            model.MergePreview);
        var fence = MarkdownFenceLanguage(model.RelativePath);
        var builder = new StringBuilder();

        builder.AppendLine("# ZGit Pet Reconcile Inspector");
        builder.AppendLine();
        builder.AppendLine("## Assessment");
        builder.AppendLine();
        builder.AppendLine($"- **Status:** {summary.Assessment}");
        builder.AppendLine($"- **Change relationship:** {summary.ChangeRelationship}");
        builder.AppendLine($"- **Overlap:** {summary.Overlap}");
        builder.AppendLine($"- **Merged preview:** {summary.MergedPreview}");
        builder.AppendLine();
        builder.AppendLine(summary.Interpretation);
        builder.AppendLine();

        builder.AppendLine("## File");
        builder.AppendLine();
        builder.AppendLine($"- **Path:** `{EscapeInline(model.RelativePath)}`");
        builder.AppendLine($"- **State:** {model.State}");
        builder.AppendLine($"- **Branch:** `{EscapeInline(model.Branch)}`");
        builder.AppendLine($"- **LOCAL:** {model.Analysis.LocalLabel} ({HunkText(model.Analysis.LocalHunkCount)})");
        builder.AppendLine($"- **REMOTE:** {model.Analysis.RemoteLabel} ({HunkText(model.Analysis.RemoteHunkCount)})");
        if (!string.IsNullOrWhiteSpace(workboardDetail))
            builder.AppendLine($"- **Workboard detail:** {workboardDetail}");
        builder.AppendLine();

        builder.AppendLine("## Pinned revisions");
        builder.AppendLine();
        AppendRevision(builder, "BASE", model.Base);
        AppendRevision(builder, "LOCAL", model.Local);
        AppendRevision(builder, "REMOTE", model.Remote);
        builder.AppendLine();

        builder.AppendLine("## Source");
        builder.AppendLine();
        AppendMarkdownCode(builder, "BASE", model.Base.Text, model.Base.Exists, fence);
        AppendMarkdownCode(builder, "LOCAL", model.Local.Text, model.Local.Exists, fence);
        AppendMarkdownCode(builder, "REMOTE", model.Remote.Text, model.Remote.Exists, fence);

        builder.AppendLine("## Merged preview");
        builder.AppendLine();
        builder.AppendLine($"**Status:** {model.MergePreview.Status}");
        builder.AppendLine();
        if (model.MergePreview.Available)
        {
            AppendMarkdownCode(
                builder,
                "MERGED CANDIDATE",
                model.MergePreview.Text,
                model.MergePreview.Exists,
                fence);
        }
        else
        {
            builder.AppendLine("_No merged candidate is available._");
            builder.AppendLine();
        }

        builder.AppendLine("> Clipboard source reminder: code blocks above use the raw pinned Git source / raw generated candidate, not Pretty-view display text.");

        return builder.ToString().TrimEnd();
    }

    public static string BuildEverythingPlainText(
        ReconcileInspectorSourceModel model,
        string workboardDetail)
    {
        var summary = ReconcileSummaryPresentation.Create(
            model.Analysis,
            model.MergePreview);
        var builder = new StringBuilder();

        builder.AppendLine("ZGIT PET RECONCILE INSPECTOR");
        builder.AppendLine(new string('=', 72));
        builder.AppendLine($"ASSESSMENT: {summary.Assessment}");
        builder.AppendLine($"CHANGE RELATIONSHIP: {summary.ChangeRelationship}");
        builder.AppendLine($"OVERLAP: {summary.Overlap}");
        builder.AppendLine($"MERGED PREVIEW: {summary.MergedPreview}");
        builder.AppendLine();
        builder.AppendLine(summary.Interpretation);
        builder.AppendLine();

        builder.AppendLine("FILE");
        builder.AppendLine($"Path: {model.RelativePath}");
        builder.AppendLine($"State: {model.State}");
        builder.AppendLine($"Branch: {model.Branch}");
        builder.AppendLine($"LOCAL: {model.Analysis.LocalLabel} ({HunkText(model.Analysis.LocalHunkCount)})");
        builder.AppendLine($"REMOTE: {model.Analysis.RemoteLabel} ({HunkText(model.Analysis.RemoteHunkCount)})");
        if (!string.IsNullOrWhiteSpace(workboardDetail))
            builder.AppendLine($"Workboard detail: {workboardDetail}");
        builder.AppendLine();

        builder.AppendLine("PINNED REVISIONS");
        AppendPlainRevision(builder, "BASE", model.Base);
        AppendPlainRevision(builder, "LOCAL", model.Local);
        AppendPlainRevision(builder, "REMOTE", model.Remote);
        builder.AppendLine();

        AppendPlainSource(builder, "BASE", model.Base);
        builder.AppendLine();
        AppendPlainSource(builder, "LOCAL", model.Local);
        builder.AppendLine();
        AppendPlainSource(builder, "REMOTE", model.Remote);
        builder.AppendLine();

        builder.AppendLine("MERGED PREVIEW");
        builder.AppendLine($"Status: {model.MergePreview.Status}");
        if (model.MergePreview.Available)
        {
            builder.AppendLine("--- MERGED CANDIDATE ---");
            builder.AppendLine(model.MergePreview.Exists ? model.MergePreview.Text : "[NOT PRESENT]");
        }
        else
        {
            builder.AppendLine("[NO CANDIDATE AVAILABLE]");
        }

        builder.AppendLine();
        builder.AppendLine("Clipboard source reminder: source above is raw backing Git text / raw generated candidate, not Pretty-view display text.");

        return builder.ToString().TrimEnd();
    }

    public static string MarkdownFenceLanguage(string path) =>
        SharedCodeReviewRenderer.LanguageFor(path) switch
        {
            "csharp" => "csharp",
            "js" => "javascript",
            "ts" => "typescript",
            "python" => "python",
            "json" => "json",
            "css" => "css",
            "html" => "html",
            "xml" => "xml",
            "sql" => "sql",
            "powershell" => "powershell",
            "php" => "php",
            _ => "text"
        };

    private static void AppendRevision(
        StringBuilder builder,
        string label,
        ReconcileSourceSnapshot source)
    {
        builder.AppendLine($"- **{label}:** `{source.CommitSha}`");
        builder.AppendLine($"  - Locator: `{EscapeInline(source.Locator)}`");
        builder.AppendLine($"  - Exists: {(source.Exists ? "yes" : "no")}");
    }

    private static void AppendPlainRevision(
        StringBuilder builder,
        string label,
        ReconcileSourceSnapshot source)
    {
        builder.AppendLine($"{label}: {source.CommitSha}");
        builder.AppendLine($"  Locator: {source.Locator}");
        builder.AppendLine($"  Exists: {(source.Exists ? "yes" : "no")}");
    }

    private static void AppendMarkdownCode(
        StringBuilder builder,
        string label,
        string text,
        bool exists,
        string fenceLanguage)
    {
        builder.AppendLine($"### {label}");
        builder.AppendLine();
        if (!exists)
        {
            builder.AppendLine("_Not present at this revision._");
            builder.AppendLine();
            return;
        }

        var fence = SafeFence(text);
        builder.AppendLine(fence + fenceLanguage);
        builder.AppendLine(text ?? "");
        builder.AppendLine(fence);
        builder.AppendLine();
    }

    private static void AppendPlainSource(
        StringBuilder builder,
        string label,
        ReconcileSourceSnapshot source)
    {
        builder.AppendLine($"--- {label} ---");
        builder.AppendLine(source.Exists ? source.Text : "[NOT PRESENT]");
    }

    private static string SafeFence(string? text)
    {
        var value = text ?? "";
        var longest = 0;
        var current = 0;
        foreach (var ch in value)
        {
            if (ch == '`')
            {
                current++;
                longest = Math.Max(longest, current);
            }
            else
            {
                current = 0;
            }
        }

        return new string('`', Math.Max(3, longest + 1));
    }

    private static string EscapeInline(string value) =>
        (value ?? "").Replace("`", "'");

    private static string HunkText(int count) =>
        count == 1 ? "1 hunk" : $"{count} hunks";

    private static string NormalizeLf(string value) =>
        (value ?? "").Replace("\r\n", "\n").Replace("\r", "\n");
}
