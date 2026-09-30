using System.Text.RegularExpressions;

namespace ZomniverseGitPet;

internal sealed record ReconcileDiffHunk(
    int OldStart,
    int OldCount,
    int NewStart,
    int NewCount)
{
    public bool IsAddition => OldCount == 0 && NewCount > 0;
    public bool IsDeletion => OldCount > 0 && NewCount == 0;

    public static IReadOnlyList<ReconcileDiffHunk> Parse(string? diff)
    {
        if (string.IsNullOrWhiteSpace(diff)) return [];

        var result = new List<ReconcileDiffHunk>();
        var header = new Regex(
            @"^@@\s+-(?<oldStart>\d+)(?:,(?<oldCount>\d+))?\s+\+(?<newStart>\d+)(?:,(?<newCount>\d+))?\s+@@",
            RegexOptions.Multiline,
            TimeSpan.FromMilliseconds(250));

        foreach (Match match in header.Matches(diff))
        {
            result.Add(new ReconcileDiffHunk(
                int.Parse(match.Groups["oldStart"].Value),
                match.Groups["oldCount"].Success
                    ? int.Parse(match.Groups["oldCount"].Value)
                    : 1,
                int.Parse(match.Groups["newStart"].Value),
                match.Groups["newCount"].Success
                    ? int.Parse(match.Groups["newCount"].Value)
                    : 1));
        }

        return result;
    }
}

internal sealed record ReconcileChangeAnalysis(
    string OverallLabel,
    string LocalLabel,
    string RemoteLabel,
    bool HasOverlap,
    int LocalHunkCount,
    int RemoteHunkCount,
    string Detail);

internal static class ReconcileChangeAnalyzer
{
    public static ReconcileChangeAnalysis Analyze(
        ReconcileSourceSnapshot baseSource,
        ReconcileSourceSnapshot localSource,
        ReconcileSourceSnapshot remoteSource,
        IReadOnlyList<ReconcileDiffHunk> localHunks,
        IReadOnlyList<ReconcileDiffHunk> remoteHunks)
    {
        if (localSource.Exists == remoteSource.Exists &&
            string.Equals(localSource.Text, remoteSource.Text, StringComparison.Ordinal))
        {
            return new(
                "SAME RESULT",
                Shape("LOCAL", localHunks),
                Shape("REMOTE", remoteHunks),
                false,
                localHunks.Count,
                remoteHunks.Count,
                "Both histories resolve to the same file content.");
        }

        if (baseSource.Exists && localSource.Exists != remoteSource.Exists)
        {
            var surviving = localSource.Exists ? localSource : remoteSource;
            var survivingChanged = !string.Equals(
                surviving.Text,
                baseSource.Text,
                StringComparison.Ordinal);

            return new(
                survivingChanged ? "DELETE vs MODIFY" : "DELETE vs UNCHANGED",
                localSource.Exists ? Shape("LOCAL", localHunks) : "DELETE — LOCAL",
                remoteSource.Exists ? Shape("REMOTE", remoteHunks) : "DELETE — REMOTE",
                survivingChanged,
                localHunks.Count,
                remoteHunks.Count,
                survivingChanged
                    ? "One history deleted the file while the other modified it."
                    : "One history deleted the file while the other kept the BASE content.");
        }

        var overlap = HasOverlap(localHunks, remoteHunks);
        var bothChanged = localHunks.Count > 0 && remoteHunks.Count > 0;

        return new(
            overlap
                ? "BOTH MODIFIED SAME AREA"
                : bothChanged
                    ? "INDEPENDENT CHANGES"
                    : localHunks.Count > 0
                        ? "LOCAL ONLY"
                        : remoteHunks.Count > 0
                            ? "REMOTE ONLY"
                            : "NO CONTENT CHANGE",
            Shape("LOCAL", localHunks),
            Shape("REMOTE", remoteHunks),
            overlap,
            localHunks.Count,
            remoteHunks.Count,
            overlap
                ? "Both histories changed overlapping BASE locations. Review the merged candidate carefully."
                : bothChanged
                    ? "Both histories changed the same file in non-overlapping BASE locations."
                    : "Only one side contains content hunks for this path.");
    }

    internal static bool HasOverlap(
        IReadOnlyList<ReconcileDiffHunk> localHunks,
        IReadOnlyList<ReconcileDiffHunk> remoteHunks) =>
        localHunks.Any(local => remoteHunks.Any(remote => Overlaps(local, remote)));

    private static bool Overlaps(ReconcileDiffHunk left, ReconcileDiffHunk right)
    {
        if (left.OldCount == 0 && right.OldCount == 0)
            return left.OldStart == right.OldStart;

        if (left.OldCount == 0)
            return InOldSpan(left.OldStart, right);

        if (right.OldCount == 0)
            return InOldSpan(right.OldStart, left);

        var leftEnd = left.OldStart + left.OldCount;
        var rightEnd = right.OldStart + right.OldCount;
        return left.OldStart < rightEnd && right.OldStart < leftEnd;
    }

    private static bool InOldSpan(int insertionAnchor, ReconcileDiffHunk changed) =>
        changed.OldCount > 0 &&
        insertionAnchor >= changed.OldStart &&
        insertionAnchor < changed.OldStart + changed.OldCount;

    private static string Shape(string side, IReadOnlyList<ReconcileDiffHunk> hunks)
    {
        if (hunks.Count == 0) return $"UNCHANGED — {side}";
        if (hunks.All(hunk => hunk.IsAddition)) return $"NEW BLOCK — {side}";
        if (hunks.All(hunk => hunk.IsDeletion)) return $"DELETE — {side}";
        if (hunks.All(hunk => hunk.OldCount > 0 && hunk.NewCount > 0))
            return $"MODIFIED EXISTING BLOCK — {side}";
        return $"MIXED STRUCTURAL EDIT — {side}";
    }
}

internal sealed record ReconcileMergePreview(
    bool Available,
    bool Exists,
    bool HasConflicts,
    string Text,
    string Status);

internal sealed class ReconcileMergePreviewService(GitService git)
{
    public async Task<ReconcileMergePreview> CreateAsync(
        ReconcileSourceSnapshot baseSource,
        ReconcileSourceSnapshot localSource,
        ReconcileSourceSnapshot remoteSource,
        CancellationToken token)
    {
        if (localSource.Exists == remoteSource.Exists &&
            string.Equals(localSource.Text, remoteSource.Text, StringComparison.Ordinal))
        {
            return new(
                true,
                localSource.Exists,
                false,
                localSource.Text,
                localSource.Exists ? "CLEAN — SAME RESULT" : "CLEAN — BOTH DELETED");
        }

        if (baseSource.Exists && !localSource.Exists && !remoteSource.Exists)
            return new(true, false, false, "", "CLEAN — DELETE");

        if (baseSource.Exists && localSource.Exists != remoteSource.Exists)
        {
            var surviving = localSource.Exists ? localSource : remoteSource;
            if (string.Equals(surviving.Text, baseSource.Text, StringComparison.Ordinal))
                return new(true, false, false, "", "CLEAN — DELETE");

            return new(
                false,
                false,
                true,
                "",
                "DELETE/MODIFY CONFLICT — no automatic candidate");
        }

        if (!baseSource.Exists && localSource.Exists != remoteSource.Exists)
        {
            var added = localSource.Exists ? localSource : remoteSource;
            return new(true, true, false, added.Text, "CLEAN — ONE-SIDED ADD");
        }

        var merge = await git.CreateReconcileMergePreviewAsync(
            baseSource.Exists ? baseSource.Text : "",
            localSource.Exists ? localSource.Text : "",
            remoteSource.Exists ? remoteSource.Text : "",
            token);

        if (merge.ExitCode == 0)
            return new(true, true, false, merge.Output, "CLEAN THREE-WAY MERGE");

        if (merge.ExitCode == 1 && !merge.TimedOut)
            return new(true, true, true, merge.Output, "OVERLAPPING CHANGE — CONFLICT MARKERS PRESENT");

        return new(
            false,
            false,
            false,
            "",
            "MERGED PREVIEW FAILED — " +
            (string.IsNullOrWhiteSpace(merge.Output) ? $"exit {merge.ExitCode}" : merge.Output));
    }
}
