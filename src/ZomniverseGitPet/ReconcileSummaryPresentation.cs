namespace ZomniverseGitPet;

internal enum ReconcileSummaryTone
{
    Positive,
    Caution,
    Conflict,
    Neutral
}

internal sealed record ReconcileSummaryPresentation(
    string Assessment,
    ReconcileSummaryTone AssessmentTone,
    string ChangeRelationship,
    ReconcileSummaryTone ChangeTone,
    string Overlap,
    ReconcileSummaryTone OverlapTone,
    string MergedPreview,
    ReconcileSummaryTone PreviewTone,
    string Interpretation)
{
    public static ReconcileSummaryPresentation Create(
        ReconcileChangeAnalysis analysis,
        ReconcileMergePreview preview)
    {
        var changeTone = analysis.HasOverlap
            ? ReconcileSummaryTone.Conflict
            : analysis.OverallLabel is "INDEPENDENT CHANGES" or "SAME RESULT" or "LOCAL ONLY" or "REMOTE ONLY" or "NO CONTENT CHANGE"
                ? ReconcileSummaryTone.Positive
                : ReconcileSummaryTone.Caution;

        var overlap = analysis.HasOverlap
            ? "OVERLAP DETECTED"
            : "NO OVERLAP DETECTED";
        var overlapTone = analysis.HasOverlap
            ? ReconcileSummaryTone.Conflict
            : ReconcileSummaryTone.Positive;

        var previewTone = preview.HasConflicts
            ? ReconcileSummaryTone.Conflict
            : preview.Available
                ? ReconcileSummaryTone.Positive
                : ReconcileSummaryTone.Caution;

        var reviewReady =
            !analysis.HasOverlap &&
            preview.Available &&
            !preview.HasConflicts;

        var assessment = reviewReady
            ? "REVIEW READY"
            : "REVIEW REQUIRED";
        var assessmentTone = reviewReady
            ? ReconcileSummaryTone.Positive
            : analysis.HasOverlap || preview.HasConflicts
                ? ReconcileSummaryTone.Conflict
                : ReconcileSummaryTone.Caution;

        return new ReconcileSummaryPresentation(
            assessment,
            assessmentTone,
            analysis.OverallLabel,
            changeTone,
            overlap,
            overlapTone,
            preview.Status,
            previewTone,
            BuildInterpretation(analysis, preview));
    }

    private static string BuildInterpretation(
        ReconcileChangeAnalysis analysis,
        ReconcileMergePreview preview)
    {
        if (analysis.HasOverlap)
        {
            return preview.HasConflicts
                ? "Both histories changed overlapping BASE locations, and Git produced a conflicted merged preview. Review the overlapping edits before reconciling."
                : "Both histories changed overlapping BASE locations. Review those edits carefully before reconciling, even if a candidate is available.";
        }

        if (preview.HasConflicts)
        {
            return "No BASE hunk overlap was detected by the structural analysis, but Git still produced a conflicted textual merge. Review the merged candidate before reconciling.";
        }

        if (!preview.Available)
        {
            return "The change relationship is known, but GitPet could not produce a merged candidate. Review LOCAL and REMOTE before reconciling.";
        }

        if (analysis.OverallLabel == "INDEPENDENT CHANGES")
        {
            return "Both histories changed different BASE locations. No overlapping edit regions were detected, and Git produced a clean merged preview. Review the candidate before reconciling.";
        }

        if (analysis.OverallLabel == "SAME RESULT")
            return "Both histories resolve to the same file content. Review the pinned revisions, then reconcile when ready.";

        return $"{analysis.Detail} Git produced a clean merged preview. Review the candidate before reconciling.";
    }
}
