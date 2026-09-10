namespace ZomniverseGitPet;

internal sealed record GuardianWorkboardRow(
    string State,
    string Path,
    string Detail = "",
    bool IsCommit = false);

internal sealed record GuardianWorkboardSnapshot(
    bool HasRepository,
    string Branch,
    IReadOnlyList<GuardianWorkboardRow> SaveRows,
    IReadOnlyList<GuardianWorkboardRow> GetRows,
    IReadOnlyList<GuardianWorkboardRow> SendRows,
    IReadOnlyList<GuardianWorkboardRow> ReconcileRows,
    int SendCommitCount,
    int SendFileCount,
    bool Diverged,
    bool ReconciliationPending,
    string SaveEmptyText,
    string GetEmptyText,
    string SendEmptyText,
    string ReconcileEmptyText)
{
    public static GuardianWorkboardSnapshot NoProject { get; } = new(
        false,
        "?",
        [],
        [],
        [],
        [],
        0,
        0,
        false,
        false,
        "Open a project to see changes waiting to be saved.",
        "Open a project to see updates waiting to come in.",
        "Open a project to see saved updates waiting to be sent.",
        "Open a project to see whether histories need reconciliation.");
}

/* ==========================================================================
   PATCH: GUARDIAN SYNC WORKBOARD PROJECTION
   DATE.TIME: 2026-09-11 12:35 +03:00
   Project Git state into Save, Get, Send, Reconcile panels.
   ========================================================================== */
internal sealed class GuardianWorkboardService(GitService git)
{
    private const int MaxFileRows = 120;
    private const int MaxCommitRows = 20;

    public async Task<GuardianWorkboardSnapshot> BuildAsync(
        AppConfig config,
        GuardianSyncSnapshot remoteState,
        CancellationToken token = default)
    {
        var repositoryPath = config.RepositoryPath;
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
            return GuardianWorkboardSnapshot.NoProject;

        var status = await git.GetStatusAsync(repositoryPath, token);
        if (!status.Healthy)
        {
            return new GuardianWorkboardSnapshot(
                true,
                status.Branch,
                [],
                [],
                [],
                [],
                0,
                0,
                false,
                false,
                "GitPet could not read local changes yet.",
                "Incoming state is unavailable until Git status recovers.",
                "Outgoing state is unavailable until Git status recovers.",
                "Reconciliation state is unavailable until Git status recovers.");
        }

        var saveRows = status.Files
            .Select(file => new GuardianWorkboardRow(
                HumanizeLocalStatus(file.Status),
                file.Path,
                DescribeLocalStatus(file.Status)))
            .ToArray();

        var branch = status.Branch;
        var validBranch = IsUsableBranch(branch);
        var localOnly = config.ConnectionMode == GitPetConnectionModes.LocalGitOnly;

        var hasRemote = !localOnly && remoteState.HasRemote && validBranch;
        var remoteBranchExists = hasRemote && remoteState.RemoteBranchExists;
        var onlineReachable = hasRemote && remoteState.OnlineReachable;

        var ahead = status.HasTrackingInformation
            ? status.Ahead
            : SameBranch(remoteState.Branch, branch) ? remoteState.Ahead : 0;
        var behind = status.HasTrackingInformation
            ? status.Behind
            : SameBranch(remoteState.Branch, branch) ? remoteState.Behind : 0;

        var divergence = ahead > 0 && behind > 0;
        var reconciliationPending = remoteState.ReconciliationPending ||
            status.Files.Any(file => file.Status.Contains('U'));

        IReadOnlyList<GuardianWorkboardRow> getRows = [];
        IReadOnlyList<GuardianWorkboardRow> sendRows = [];
        IReadOnlyList<GuardianWorkboardRow> reconcileRows = [];
        var sendCommitCount = 0;
        var sendFileCount = 0;

        var saveEmpty = "Nothing waiting to be saved.\nLocal working files match the latest saved version.";
        var getEmpty = localOnly
            ? "Local Git mode is active.\nNothing will be fetched from an online repository."
            : !hasRemote
                ? "No online remote is connected for this project."
                : !onlineReachable
                    ? "Remote state is temporarily unavailable.\nGitPet will check again automatically."
                    : !remoteBranchExists
                        ? "The online branch does not exist yet.\nThere is nothing to get."
                        : "No updates waiting from the remote.";
        var sendEmpty = localOnly
            ? "Local Git mode is active.\nSaved versions stay on this computer."
            : !hasRemote
                ? "No online destination is connected.\nSaved versions remain local."
                : "Everything saved here has already been sent.";
        var reconcileEmpty = localOnly
            ? "Local Git mode has no remote history to reconcile."
            : !hasRemote
                ? "No remote history is connected to this project."
                : "Nothing needs reconciliation.\nLocal and remote history currently agree.";

        if (hasRemote && remoteBranchExists)
        {
            if (behind > 0)
            {
                getRows = await GetDiffRowsAsync(
                    repositoryPath,
                    $"HEAD..origin/{branch}",
                    "incoming",
                    token);
            }

            if (ahead > 0)
            {
                var commits = await GetCommitRowsAsync(
                    repositoryPath,
                    $"origin/{branch}..HEAD",
                    token);
                var files = await GetDiffRowsAsync(
                    repositoryPath,
                    $"origin/{branch}..HEAD",
                    "outgoing",
                    token);

                sendCommitCount = commits.Count;
                sendFileCount = files.Count(row => row.State != "MORE");
                sendRows = commits.Concat(files).ToArray();
            }

            if (divergence || reconciliationPending)
            {
                reconcileRows = await GetReconciliationRowsAsync(
                    repositoryPath,
                    branch,
                    reconciliationPending,
                    status,
                    token);

                if (reconcileRows.Count == 0 && divergence)
                    reconcileEmpty = "Local and remote histories diverged.\nNo overlapping file paths were detected yet.";
            }
        }
        else if (hasRemote && !remoteBranchExists && ahead > 0)
        {
            var commits = await GetCommitRowsAsync(repositoryPath, "HEAD", token);
            var files = await GetFirstPushRowsAsync(repositoryPath, token);
            sendCommitCount = commits.Count;
            sendFileCount = files.Count(row => row.State != "MORE");
            sendRows = commits.Concat(files).ToArray();
            sendEmpty = "No saved commits are waiting to be sent.";
        }

        if (reconciliationPending && reconcileRows.Count == 0)
        {
            reconcileRows = status.Files
                .Where(file => file.Status.Contains('U'))
                .Select(file => new GuardianWorkboardRow(
                    "CONFLICT",
                    file.Path,
                    "Git reports this path as unresolved during reconciliation."))
                .ToArray();
        }

        return new GuardianWorkboardSnapshot(
            true,
            branch,
            saveRows,
            getRows,
            sendRows,
            reconcileRows,
            sendCommitCount,
            sendFileCount,
            divergence,
            reconciliationPending,
            saveEmpty,
            getEmpty,
            sendEmpty,
            reconcileEmpty);
    }

    private async Task<IReadOnlyList<GuardianWorkboardRow>> GetDiffRowsAsync(
        string repositoryPath,
        string range,
        string direction,
        CancellationToken token)
    {
        var result = await git.RunGitAsync(
            repositoryPath,
            ["diff", "--name-status", "--find-renames", range],
            TimeSpan.FromSeconds(20),
            token);

        if (!result.Success) return [];
        return LimitRows(ParseNameStatus(result.Output, direction), MaxFileRows);
    }

    private async Task<IReadOnlyList<GuardianWorkboardRow>> GetCommitRowsAsync(
        string repositoryPath,
        string range,
        CancellationToken token)
    {
        var result = await git.RunGitAsync(
            repositoryPath,
            ["log", $"-{MaxCommitRows}", "--format=%h%x09%s", range],
            TimeSpan.FromSeconds(20),
            token);

        return result.Success ? ParseCommitRows(result.Output) : [];
    }

    private async Task<IReadOnlyList<GuardianWorkboardRow>> GetFirstPushRowsAsync(
        string repositoryPath,
        CancellationToken token)
    {
        var result = await git.RunGitAsync(
            repositoryPath,
            ["ls-tree", "-r", "--name-only", "HEAD"],
            TimeSpan.FromSeconds(20),
            token);
        if (!result.Success) return [];

        var rows = result.Output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(path => new GuardianWorkboardRow(
                "FILE",
                path,
                "This tracked file belongs to the first online branch publication."))
            .ToArray();
        return LimitRows(rows, MaxFileRows);
    }

    private async Task<IReadOnlyList<GuardianWorkboardRow>> GetReconciliationRowsAsync(
        string repositoryPath,
        string branch,
        bool reconciliationPending,
        RepositoryStatus status,
        CancellationToken token)
    {
        if (reconciliationPending)
        {
            var conflictResult = await git.RunGitAsync(
                repositoryPath,
                ["diff", "--name-only", "--diff-filter=U"],
                TimeSpan.FromSeconds(12),
                token);

            if (conflictResult.Success)
            {
                var conflicts = conflictResult.Output
                    .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(path => new GuardianWorkboardRow(
                        "CONFLICT",
                        path,
                        "Git reports an unresolved conflict in this path."))
                    .ToArray();
                if (conflicts.Length > 0) return LimitRows(conflicts, MaxFileRows);
            }

            var statusConflicts = status.Files
                .Where(file => file.Status.Contains('U'))
                .Select(file => new GuardianWorkboardRow(
                    "CONFLICT",
                    file.Path,
                    "Git reports this path as unresolved during reconciliation."))
                .ToArray();
            if (statusConflicts.Length > 0) return LimitRows(statusConflicts, MaxFileRows);
        }

        var mergeBase = await git.RunGitAsync(
            repositoryPath,
            ["merge-base", "HEAD", $"origin/{branch}"],
            TimeSpan.FromSeconds(12),
            token);
        if (!mergeBase.Success || string.IsNullOrWhiteSpace(mergeBase.Output)) return [];

        var baseHash = mergeBase.Output.Trim();
        var localResult = await git.RunGitAsync(
            repositoryPath,
            ["diff", "--name-status", "--find-renames", $"{baseHash}..HEAD"],
            TimeSpan.FromSeconds(20),
            token);
        var remoteResult = await git.RunGitAsync(
            repositoryPath,
            ["diff", "--name-status", "--find-renames", $"{baseHash}..origin/{branch}"],
            TimeSpan.FromSeconds(20),
            token);

        if (!localResult.Success || !remoteResult.Success) return [];

        var localRows = ParseNameStatus(localResult.Output, "local");
        var remoteRows = ParseNameStatus(remoteResult.Output, "remote");
        return LimitRows(CombineReconciliation(localRows, remoteRows), MaxFileRows);
    }

    internal static IReadOnlyList<GuardianWorkboardRow> ParseNameStatus(string output, string direction)
    {
        var rows = new List<GuardianWorkboardRow>();
        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.Split('\t');
            if (fields.Length < 2) continue;

            var code = fields[0].Trim();
            var renamed = (code.StartsWith('R') || code.StartsWith('C')) && fields.Length >= 3;
            var path = renamed ? fields[2].Trim() : fields[1].Trim();
            if (string.IsNullOrWhiteSpace(path)) continue;

            var detail = renamed
                ? $"{direction}: {FriendlyNameStatus(code)} from {fields[1].Trim()}"
                : $"{direction}: {FriendlyNameStatus(code)}";
            rows.Add(new GuardianWorkboardRow(FriendlyNameStatus(code), path, detail));
        }

        return rows;
    }

    internal static IReadOnlyList<GuardianWorkboardRow> ParseCommitRows(string output)
    {
        var rows = new List<GuardianWorkboardRow>();
        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var tab = line.IndexOf('\t');
            if (tab <= 0) continue;
            var hash = line[..tab].Trim();
            var subject = line[(tab + 1)..].Trim();
            rows.Add(new GuardianWorkboardRow(
                "COMMIT",
                $"{hash}  {subject}",
                "Saved local commit waiting to be sent.",
                IsCommit: true));
        }
        return rows;
    }

    internal static IReadOnlyList<GuardianWorkboardRow> CombineReconciliation(
        IReadOnlyList<GuardianWorkboardRow> localRows,
        IReadOnlyList<GuardianWorkboardRow> remoteRows)
    {
        var local = localRows
            .GroupBy(row => row.Path, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var remote = remoteRows
            .GroupBy(row => row.Path, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var paths = local.Keys
            .Concat(remote.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

        var rows = new List<GuardianWorkboardRow>();
        foreach (var path in paths)
        {
            var onLocal = local.TryGetValue(path, out var localRow);
            var onRemote = remote.TryGetValue(path, out var remoteRow);
            if (onLocal && onRemote)
            {
                rows.Add(new GuardianWorkboardRow(
                    "BOTH SIDES",
                    path,
                    $"Local: {localRow!.State}. Remote: {remoteRow!.State}. Review if reconciliation is required."));
            }
            else if (onLocal)
            {
                rows.Add(new GuardianWorkboardRow(
                    "LOCAL",
                    path,
                    $"Only the local history changed this path ({localRow!.State})."));
            }
            else if (onRemote)
            {
                rows.Add(new GuardianWorkboardRow(
                    "REMOTE",
                    path,
                    $"Only the remote history changed this path ({remoteRow!.State})."));
            }
        }
        return rows;
    }

    private static IReadOnlyList<GuardianWorkboardRow> LimitRows(
        IReadOnlyList<GuardianWorkboardRow> rows,
        int max)
    {
        if (rows.Count <= max) return rows;
        return rows.Take(max)
            .Append(new GuardianWorkboardRow(
                "MORE",
                $"… {rows.Count - max} additional items",
                "The workboard limits very large projections to keep Guardian responsive."))
            .ToArray();
    }

    private static bool SameBranch(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static bool IsUsableBranch(string branch) =>
        !string.IsNullOrWhiteSpace(branch) &&
        branch != "?" &&
        !branch.StartsWith("(", StringComparison.Ordinal);

    private static string HumanizeLocalStatus(string status) => status switch
    {
        "??" => "NEW",
        ".M" => "MODIFIED",
        "M." => "STAGED",
        "A." => "ADDED",
        ".D" => "DELETED",
        "D." => "DELETE STAGED",
        "MM" => "STAGED + EDITED",
        _ when status.Contains('U') => "CONFLICT",
        _ => status
    };

    private static string DescribeLocalStatus(string status) => status switch
    {
        "??" => "New local item waiting to be saved.",
        ".M" => "Tracked file modified locally and not yet saved.",
        "M." => "Tracked change already staged and waiting to be saved.",
        "A." => "New staged item waiting to be saved.",
        ".D" or "D." => "Tracked item deleted locally and waiting to be saved.",
        "MM" => "File has staged changes plus additional local edits.",
        _ when status.Contains('U') => "Git reports this path as unresolved.",
        _ => $"Git status: {status}"
    };

    private static string FriendlyNameStatus(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return "CHANGED";
        return code[0] switch
        {
            'A' => "ADDED",
            'M' => "MODIFIED",
            'D' => "DELETED",
            'R' => "RENAMED",
            'C' => "COPIED",
            'T' => "TYPE CHANGED",
            'U' => "CONFLICT",
            _ => code
        };
    }
}
