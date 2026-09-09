namespace ZomniverseGitPet;

internal enum SendReadiness
{
    Unknown,
    SaveFirst,
    AlreadyUpToDate,
    Ready
}

internal static class FriendlyGitState
{
    public static string FormatSyncSummary(RepositoryStatus status)
    {
        var parts = new List<string>
        {
            Count(status.Files.Count, "unsaved change")
        };

        if (status.HasTrackingInformation)
        {
            parts.Add(Count(status.Ahead, "saved update") + " ready to send");
            if (status.Behind > 0)
                parts.Add(Count(status.Behind, "update") + " ready to get");
        }
        else
        {
            parts.Add("online update counts unavailable");
        }

        return string.Join(" · ", parts);
    }

    public static SendReadiness GetSendReadiness(RepositoryStatus status)
    {
        if (!status.HasTrackingInformation) return SendReadiness.Unknown;
        if (status.Ahead > 0) return SendReadiness.Ready;
        return status.Files.Count > 0
            ? SendReadiness.SaveFirst
            : SendReadiness.AlreadyUpToDate;
    }

    public static string Count(int count, string singular) =>
        $"{count} {singular}{(count == 1 ? "" : "s")}";
}
