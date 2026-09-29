namespace ZomniverseGitPet;

internal static class SuspiciousPathReview
{
    public static bool Confirm(Form? owner, IReadOnlyList<SuspiciousPathMatch> matches, string saveContext)
    {
        if (matches.Count == 0) return true;

        var details = matches
            .GroupBy(match => match.Path, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
                group.Key + "\r\n" +
                string.Join("\r\n", group.Select(match => "  matched rule: " + match.Pattern)))
            .ToArray();

        using var review = new GuardianConfirmDialog(
            "Review before saving",
            "POTENTIAL SENSITIVE PATHS",
            "GitPet found file paths that resemble credential or secret files.\r\n\r\n" +
            string.Join("\r\n\r\n", details) +
            "\r\n\r\n" +
            "This is a warning, not a verdict. Review the files and continue only if they are intentional and safe to keep in Git history.\r\n\r\n" +
            $"Action: {saveContext}",
            confirmText: "Save anyway",
            cancelText: "Cancel",
            dialogSize: new Size(820, 540),
            resizable: true,
            scrollable: true,
            confirmWidth: 150);

        return review.ShowDialog(owner) == DialogResult.Yes;
    }
}
