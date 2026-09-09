using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace ZomniverseGitPet;

internal enum RemoteHistoryRelation
{
    Unknown,
    Equal,
    LocalAhead,
    RemoteAhead,
    Diverged
}

internal sealed record MajorUpdateAssessment(
    string RepositoryPath,
    string Branch,
    string HeadCommit,
    string? RemoteCommit,
    string? OriginUrl,
    RemoteHistoryRelation RemoteRelation,
    int LocalAheadCommits,
    int RemoteAheadCommits,
    int ChangedFiles,
    int AddedFiles,
    int DeletedFiles,
    int RenamedFiles,
    long AddedLines,
    long DeletedLines,
    bool WorkingTreeDirty,
    bool IsMajorCandidate,
    string Prompt,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<string> ExistingTags,
    string Fingerprint)
{
    public string RelationLabel => RemoteRelation switch
    {
        RemoteHistoryRelation.Equal => "Local and origin are aligned",
        RemoteHistoryRelation.LocalAhead => $"Local is {LocalAheadCommits} commit(s) ahead",
        RemoteHistoryRelation.RemoteAhead => $"Origin is {RemoteAheadCommits} commit(s) ahead",
        RemoteHistoryRelation.Diverged => $"Both changed: local +{LocalAheadCommits}, origin +{RemoteAheadCommits}",
        _ => "Remote relationship unavailable"
    };
}

internal sealed record MajorReleasePlanResult(
    bool Success,
    string Message,
    string LegacyBranch,
    string LegacyTag,
    string NewBranch,
    string LegacyTarget);

internal sealed class MajorUpdateCoordinator
{
    private readonly GitService _git;
    private readonly AuditLog _audit;

    public MajorUpdateCoordinator(GitService git, AuditLog audit)
    {
        _git = git;
        _audit = audit;
    }

    public async Task<MajorUpdateAssessment> AnalyzeAsync(
        string repositoryPath,
        bool refreshRemote,
        CancellationToken token = default)
    {
        var branchResult = await _git.GetCurrentBranchAsync(repositoryPath, token);
        var branch = branchResult.Success ? branchResult.Output.Trim() : "";
        if (string.IsNullOrWhiteSpace(branch)) branch = "main";

        var headResult = await _git.RunGitAsync(repositoryPath, ["rev-parse", "HEAD"], cancellationToken: token);
        var head = headResult.Success ? headResult.Output.Trim() : "HEAD";

        var originResult = await _git.RunGitAsync(repositoryPath, ["remote", "get-url", "origin"], cancellationToken: token);
        var originUrl = originResult.Success && !string.IsNullOrWhiteSpace(originResult.Output)
            ? originResult.Output.Trim()
            : null;

        string? remoteCommit = null;
        var localAhead = 0;
        var remoteAhead = 0;
        var relation = RemoteHistoryRelation.Unknown;
        string? comparisonBase = null;

        if (!string.IsNullOrWhiteSpace(originUrl))
        {
            string? remoteRef = null;
            if (refreshRemote)
            {
                var fetch = await _git.RunGitAsync(
                    repositoryPath,
                    ["fetch", "--no-tags", "origin", branch],
                    TimeSpan.FromMinutes(3), token);
                if (fetch.Success) remoteRef = "FETCH_HEAD";
            }

            if (remoteRef is null)
            {
                var tracking = $"refs/remotes/origin/{branch}";
                var verifyTracking = await _git.RunGitAsync(repositoryPath, ["rev-parse", "--verify", tracking], cancellationToken: token);
                if (verifyTracking.Success) remoteRef = tracking;
            }

            if (remoteRef is not null)
            {
                var remoteResult = await _git.RunGitAsync(repositoryPath, ["rev-parse", remoteRef], cancellationToken: token);
                if (remoteResult.Success)
                {
                    remoteCommit = remoteResult.Output.Trim();
                    var counts = await _git.RunGitAsync(
                        repositoryPath,
                        ["rev-list", "--left-right", "--count", $"HEAD...{remoteRef}"],
                        cancellationToken: token);
                    if (counts.Success)
                    {
                        var parts = counts.Output.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2)
                        {
                            _ = int.TryParse(parts[0], out localAhead);
                            _ = int.TryParse(parts[1], out remoteAhead);
                            relation = (localAhead, remoteAhead) switch
                            {
                                (0, 0) => RemoteHistoryRelation.Equal,
                                (> 0, 0) => RemoteHistoryRelation.LocalAhead,
                                (0, > 0) => RemoteHistoryRelation.RemoteAhead,
                                (> 0, > 0) => RemoteHistoryRelation.Diverged,
                                _ => RemoteHistoryRelation.Unknown
                            };
                        }
                    }

                    var mergeBase = await _git.RunGitAsync(repositoryPath, ["merge-base", "HEAD", remoteRef], cancellationToken: token);
                    if (mergeBase.Success && !string.IsNullOrWhiteSpace(mergeBase.Output))
                        comparisonBase = mergeBase.Output.Trim();
                }
            }
        }

        var status = await _git.RunGitAsync(
            repositoryPath,
            ["status", "--porcelain=v1", "--untracked-files=normal"],
            cancellationToken: token);
        var statusEntries = ParseStatusEntries(status.Success ? status.Output : "");
        var workingDirty = statusEntries.Count > 0;

        var baseRef = !string.IsNullOrWhiteSpace(comparisonBase) && localAhead > 0
            ? comparisonBase
            : "HEAD";

        var names = await _git.RunGitAsync(
            repositoryPath,
            ["diff", "--name-status", baseRef, "--"],
            cancellationToken: token);
        var diffEntries = ParseNameStatus(names.Success ? names.Output : "");

        foreach (var entry in statusEntries.Where(entry => entry.Code == "??"))
            if (!diffEntries.Any(existing => PathEquals(existing.Path, entry.Path)))
                diffEntries.Add(new ChangeEntry("A", entry.Path));

        var numstat = await _git.RunGitAsync(
            repositoryPath,
            ["diff", "--numstat", baseRef, "--"],
            cancellationToken: token);
        var (addedLines, deletedLines) = ParseNumStat(numstat.Success ? numstat.Output : "");

        var added = diffEntries.Count(entry => entry.Code.StartsWith('A') || entry.Code == "??");
        var deleted = diffEntries.Count(entry => entry.Code.StartsWith('D'));
        var renamed = diffEntries.Count(entry => entry.Code.StartsWith('R'));
        var changedFiles = diffEntries.Select(entry => entry.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count();

        var reasons = new List<string>();
        var score = 0;
        if (changedFiles >= 40)
        {
            score += 4;
            reasons.Add($"{changedFiles} files differ from the baseline");
        }
        else if (changedFiles >= 20)
        {
            score += 2;
            reasons.Add($"{changedFiles} files differ from the baseline");
        }
        else if (changedFiles >= 12)
        {
            score += 1;
            reasons.Add($"{changedFiles} files changed together");
        }

        var lineDelta = addedLines + deletedLines;
        if (lineDelta >= 2500)
        {
            score += 3;
            reasons.Add($"about {lineDelta:N0} source/data lines changed");
        }
        else if (lineDelta >= 1000)
        {
            score += 2;
            reasons.Add($"about {lineDelta:N0} source/data lines changed");
        }

        if (added + deleted >= 12)
        {
            score += 2;
            reasons.Add($"{added} files added and {deleted} removed");
        }

        var structuralPaths = diffEntries
            .Select(entry => entry.Path.Replace('\\', '/'))
            .Where(path => Regex.IsMatch(path,
                @"(^|/)(migrations?|schema|database|db|data-model|package-lock|composer\.lock|pyproject|package\.json)(/|\.|$)",
                RegexOptions.IgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (structuralPaths.Length > 0)
        {
            score += 2;
            reasons.Add("database/schema/dependency structure changed");
        }

        var deletedOldFormats = diffEntries.Any(entry => entry.Code.StartsWith('D') &&
            HasExtension(entry.Path, ".csv", ".md", ".xml", ".yaml", ".yml"));
        var addedNewFormats = diffEntries.Any(entry => entry.Code.StartsWith('A') &&
            HasExtension(entry.Path, ".json", ".sql"));
        var formatMigration = deletedOldFormats && addedNewFormats;
        if (formatMigration)
        {
            score += 3;
            reasons.Add("data appears to be migrating from an older format to JSON/SQL");
        }

        if (relation == RemoteHistoryRelation.Diverged)
        {
            score += 1;
            reasons.Add("local and online histories both contain unique commits");
        }

        var major = score >= 4 || changedFiles >= 50;
        var prompt = formatMigration
            ? "Wait — is this a new concept?"
            : structuralPaths.Length > 0
                ? "A major update may be underway."
                : "That is a big change you did.";

        var tagsResult = await _git.RunGitAsync(repositoryPath, ["tag", "--list"], cancellationToken: token);
        var tags = tagsResult.Success
            ? tagsResult.Output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [];

        var fingerprint = string.Join('|',
            Path.GetFullPath(repositoryPath), head, remoteCommit ?? "-", changedFiles, added, deleted, renamed,
            addedLines, deletedLines, relation, localAhead, remoteAhead);

        return new MajorUpdateAssessment(
            repositoryPath,
            branch,
            head,
            remoteCommit,
            originUrl,
            relation,
            localAhead,
            remoteAhead,
            changedFiles,
            added,
            deleted,
            renamed,
            addedLines,
            deletedLines,
            workingDirty,
            major,
            prompt,
            reasons,
            tags,
            fingerprint);
    }

    public async Task<MajorReleasePlanResult> CreateLocalReleasePlanAsync(
        MajorUpdateAssessment assessment,
        string legacyBranch,
        string legacyTag,
        string newBranch,
        CancellationToken token = default)
    {
        legacyBranch = legacyBranch.Trim();
        legacyTag = legacyTag.Trim();
        newBranch = newBranch.Trim();

        var branchValidation = await ValidateBranchAsync(assessment.RepositoryPath, legacyBranch, token);
        if (!branchValidation.Success) return Failure(branchValidation.Output, legacyBranch, legacyTag, newBranch);
        var newBranchValidation = await ValidateBranchAsync(assessment.RepositoryPath, newBranch, token);
        if (!newBranchValidation.Success) return Failure(newBranchValidation.Output, legacyBranch, legacyTag, newBranch);
        var tagValidation = await _git.RunGitAsync(
            assessment.RepositoryPath,
            ["check-ref-format", $"refs/tags/{legacyTag}"], cancellationToken: token);
        if (!tagValidation.Success) return Failure("The legacy tag name is not valid Git syntax.", legacyBranch, legacyTag, newBranch);

        foreach (var reference in new[] { $"refs/heads/{legacyBranch}", $"refs/heads/{newBranch}", $"refs/tags/{legacyTag}" })
        {
            var existing = await _git.RunGitAsync(assessment.RepositoryPath, ["show-ref", "--verify", "--quiet", reference], cancellationToken: token);
            if (existing.Success)
                return Failure($"Git already has {reference}. Choose another name so GitPet never replaces an existing branch or tag.", legacyBranch, legacyTag, newBranch);
        }

        var legacyTarget = !string.IsNullOrWhiteSpace(assessment.RemoteCommit)
            ? assessment.RemoteCommit!
            : assessment.HeadCommit;

        var makeLegacyBranch = await _git.RunGitAsync(
            assessment.RepositoryPath,
            ["branch", "--no-track", legacyBranch, legacyTarget], cancellationToken: token);
        if (!makeLegacyBranch.Success)
            return Failure("Could not create the local legacy branch.\r\n\r\n" + makeLegacyBranch.Output, legacyBranch, legacyTag, newBranch, legacyTarget);

        var makeTag = await _git.RunGitAsync(
            assessment.RepositoryPath,
            ["tag", "-a", legacyTag, legacyTarget, "-m", $"Legacy generation preserved by ZomniverseGitPet ({legacyTag})"],
            cancellationToken: token);
        if (!makeTag.Success)
            return Failure("The legacy branch was created, but the tag could not be created. Nothing was pushed.\r\n\r\n" + makeTag.Output,
                legacyBranch, legacyTag, newBranch, legacyTarget);

        var makeNewBranch = await _git.RunGitAsync(
            assessment.RepositoryPath,
            ["switch", "-c", newBranch], cancellationToken: token);
        if (!makeNewBranch.Success)
            return Failure("The legacy branch/tag were created locally, but GitPet could not switch the redesign onto its new branch. Nothing was pushed.\r\n\r\n" + makeNewBranch.Output,
                legacyBranch, legacyTag, newBranch, legacyTarget);

        await _audit.WriteAsync("major_release_plan_created", new
        {
            repository = assessment.RepositoryPath,
            legacyBranch,
            legacyTag,
            newBranch,
            legacyTarget,
            remoteRelation = assessment.RemoteRelation.ToString(),
            pushed = false
        });

        var message =
            "Major-update plan created locally ✓\r\n\r\n" +
            $"Legacy branch: {legacyBranch}\r\n" +
            $"Legacy tag: {legacyTag}\r\n" +
            $"New working branch: {newBranch}\r\n\r\n" +
            "Nothing was pushed. No branch was force-updated and main was not rewritten.";
        return new(true, message, legacyBranch, legacyTag, newBranch, legacyTarget);
    }

    public async Task<CommandResult> PublishLegacyRefsAsync(
        MajorUpdateAssessment assessment,
        string legacyBranch,
        string legacyTag,
        CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(assessment.OriginUrl))
            return new(-1, "No readable origin remote is configured.");

        var result = await _git.RunGitAsync(
            assessment.RepositoryPath,
            [
                "push", "--atomic", "origin",
                $"refs/heads/{legacyBranch}:refs/heads/{legacyBranch}",
                $"refs/tags/{legacyTag}:refs/tags/{legacyTag}"
            ],
            TimeSpan.FromMinutes(5), token);

        await _audit.WriteAsync("legacy_release_refs_published", new
        {
            repository = assessment.RepositoryPath,
            legacyBranch,
            legacyTag,
            success = result.Success,
            result.ExitCode
        });
        return result;
    }

    public static (int LegacyMajor, int NewMajor) SuggestMajorVersions(IEnumerable<string> tags)
    {
        var max = 0;
        foreach (var tag in tags)
        {
            var match = Regex.Match(tag, @"^v(?<major>\d+)(?:\.|$)", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups["major"].Value, out var major))
                max = Math.Max(max, major);
        }
        var legacy = max == 0 ? 1 : max;
        return (legacy, legacy + 1);
    }

    public static string? TryGetGitHubWebUrl(string? remoteUrl)
    {
        if (string.IsNullOrWhiteSpace(remoteUrl)) return null;
        var value = remoteUrl.Trim();

        if (value.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase))
        {
            var path = value["git@github.com:".Length..].Trim('/');
            if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) path = path[..^4];
            return path.Contains('/') ? "https://github.com/" + path : null;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
        {
            var path = uri.AbsolutePath.Trim('/');
            if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) path = path[..^4];
            return path.Contains('/') ? "https://github.com/" + path : null;
        }
        return null;
    }

    public static string BuildGitHubReleaseDraftUrl(string repositoryUrl, string legacyTag, string legacyBranch) =>
        $"{repositoryUrl.TrimEnd('/')}/releases/new?tag={Uri.EscapeDataString(legacyTag)}&target={Uri.EscapeDataString(legacyBranch)}";

    private async Task<CommandResult> ValidateBranchAsync(string repositoryPath, string branch, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(branch)) return new(-1, "A branch name is required.");
        var result = await _git.RunGitAsync(repositoryPath, ["check-ref-format", "--branch", branch], cancellationToken: token);
        return result.Success ? result : new(result.ExitCode, $"'{branch}' is not a valid Git branch name.\r\n\r\n{result.Output}", result.TimedOut);
    }

    private static MajorReleasePlanResult Failure(
        string message,
        string legacyBranch,
        string legacyTag,
        string newBranch,
        string legacyTarget = "") =>
        new(false, message, legacyBranch, legacyTag, newBranch, legacyTarget);

    private static List<ChangeEntry> ParseStatusEntries(string output)
    {
        var result = new List<ChangeEntry>();
        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length < 3) continue;
            var code = line[..2].Trim();
            var path = line[3..].Trim();
            var arrow = path.LastIndexOf(" -> ", StringComparison.Ordinal);
            if (arrow >= 0) path = path[(arrow + 4)..];
            result.Add(new ChangeEntry(code.Length == 0 ? "M" : code, path));
        }
        return result;
    }

    private static List<ChangeEntry> ParseNameStatus(string output)
    {
        var result = new List<ChangeEntry>();
        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('\t');
            if (parts.Length < 2) continue;
            var code = parts[0].Trim();
            var path = code.StartsWith('R') && parts.Length >= 3 ? parts[2] : parts[1];
            result.Add(new ChangeEntry(code, path.Trim()));
        }
        return result;
    }

    private static (long Added, long Deleted) ParseNumStat(string output)
    {
        long added = 0;
        long deleted = 0;
        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('\t');
            if (parts.Length < 2) continue;
            if (long.TryParse(parts[0], out var plus)) added += plus;
            if (long.TryParse(parts[1], out var minus)) deleted += minus;
        }
        return (added, deleted);
    }

    private static bool HasExtension(string path, params string[] extensions) =>
        extensions.Any(extension => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase));

    private static bool PathEquals(string left, string right) =>
        string.Equals(left.Replace('\\', '/'), right.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);

    private sealed record ChangeEntry(string Code, string Path);
}
