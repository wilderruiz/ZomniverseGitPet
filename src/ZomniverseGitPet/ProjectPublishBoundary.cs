namespace ZomniverseGitPet;

internal sealed record ProjectPublishBoundarySetComparison(
    IReadOnlyList<string> Matched,
    IReadOnlyList<string> AllowListOnly,
    IReadOnlyList<string> SendOnly)
{
    public bool ExactMatch => AllowListOnly.Count == 0 && SendOnly.Count == 0;
}

internal sealed record ProjectPublishBoundaryResult(
    string ProjectName,
    string RepositoryRoot,
    IReadOnlyList<string> Matched,
    IReadOnlyList<string> AllowListOnly,
    IReadOnlyList<string> SendOnly,
    IReadOnlyList<ProjectScopeAllowListIssue> Issues,
    string CommandLog,
    string FailureMessage = "")
{
    public bool ExactMatch =>
        string.IsNullOrWhiteSpace(FailureMessage) &&
        Issues.Count == 0 &&
        AllowListOnly.Count == 0 &&
        SendOnly.Count == 0;

    public string BuildReport()
    {
        var lines = new List<string>
        {
            $"{ProjectName} — PUBLISH BOUNDARY CHECK",
            "",
            $"Repository: {RepositoryRoot}",
            $"Expected from allow list: {Matched.Count + AllowListOnly.Count}",
            $"Prepared to send:        {Matched.Count + SendOnly.Count}",
            "",
            $"MATCHED:         {Matched.Count}",
            $"ALLOW-LIST ONLY: {AllowListOnly.Count}",
            $"SEND ONLY:       {SendOnly.Count}",
            $"ALLOW-LIST ISSUES: {Issues.Count}",
            ""
        };

        if (!string.IsNullOrWhiteSpace(FailureMessage))
        {
            lines.Add("CHECK COULD NOT COMPLETE");
            lines.Add(FailureMessage.Trim());
            lines.Add("");
        }
        else if (ExactMatch)
        {
            lines.Add("✓ EXACT MATCH — SAFE TO PUBLISH");
            lines.Add("Everything prepared for Send belongs to the saved project allow list.");
            lines.Add("");
        }
        else
        {
            lines.Add("✕ PUBLISH BOUNDARY MISMATCH");
            lines.Add("GitPet will block Send until this project boundary is resolved.");
            lines.Add("");
        }

        AppendSection(lines, "ALLOW-LIST ONLY", AllowListOnly);
        AppendSection(lines, "SEND ONLY", SendOnly);

        if (Issues.Count > 0)
        {
            lines.Add("ALLOW-LIST ISSUES");
            foreach (var issue in Issues)
                lines.Add($"Line {issue.LineNumber}: {issue.Input} — {issue.Message}");
            lines.Add("");
        }

        if (Matched.Count > 0)
            AppendSection(lines, "MATCHED", Matched);

        return string.Join(Environment.NewLine, lines);
    }

    private static void AppendSection(List<string> lines, string title, IReadOnlyList<string> values)
    {
        if (values.Count == 0) return;
        lines.Add(title);
        lines.AddRange(values);
        lines.Add("");
    }
}

/* ==========================================================================
   PATCH: PROJECT PUBLISH BOUNDARY CHECK
   DATE.TIME: 2026-09-11 21:12 +03:00
   Compare saved allow-list files with the standalone Send snapshot.
   ========================================================================== */
internal static class ProjectPublishBoundary
{
    public static ProjectPublishBoundarySetComparison CompareSets(
        IEnumerable<string> expectedFiles,
        IEnumerable<string> sendFiles)
    {
        var expected = NormalizeSet(expectedFiles);
        var send = NormalizeSet(sendFiles);

        return new ProjectPublishBoundarySetComparison(
            expected.Intersect(send, StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            expected.Except(send, StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            send.Except(expected, StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    public static async Task<ProjectPublishBoundaryResult> CompareAsync(
        AppConfig config,
        GitService git,
        string repositoryRoot,
        string allowListText,
        CancellationToken token = default)
    {
        var project = config.GetActiveProject();
        if (project is null)
            return Failure("Project", repositoryRoot, "Choose a GitPet project before comparing its publishing boundary.");

        if (!StandaloneProjectPublishing.IsLogicalProject(config, repositoryRoot))
            return Failure(project.DisplayName, repositoryRoot, "This project uses the whole Git repository, so standalone scope comparison is not required.");

        var resolved = ProjectScopeAllowList.Resolve(repositoryRoot, allowListText);
        if (!resolved.Success)
        {
            return new ProjectPublishBoundaryResult(
                project.DisplayName,
                repositoryRoot,
                [],
                [],
                [],
                resolved.Issues,
                BuildCommandLog(repositoryRoot, [], []),
                "The saved allow list contains paths that GitPet cannot resolve safely.");
        }

        if (resolved.Entries.Count == 0)
            return Failure(project.DisplayName, repositoryRoot, "The saved project allow list is empty.");

        var expectedPathspecs = resolved.Entries
            .Select(entry => Normalize(entry.RelativePath))
            .Where(path => path.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var expectedArguments = new List<string>
        {
            "ls-tree", "-r", "--full-tree", "--name-only", "HEAD", "--"
        };
        expectedArguments.AddRange(expectedPathspecs);

        var expectedResult = await git.RunGitAsync(
            repositoryRoot,
            expectedArguments,
            TimeSpan.FromSeconds(30),
            token);
        if (!expectedResult.Success)
        {
            return Failure(
                project.DisplayName,
                repositoryRoot,
                "GitPet could not resolve the saved allow list against the latest saved Git version.\r\n\r\n" + expectedResult.Output,
                BuildCommandLog(repositoryRoot, expectedPathspecs, StandaloneProjectPublishing.GetPublishingPathspecs(config, repositoryRoot)));
        }

        var expectedFiles = ParseFiles(expectedResult.Output);
        var sendSnapshot = await StandaloneProjectPublishing.BuildSnapshotAsync(
            config,
            git,
            repositoryRoot,
            token);
        if (sendSnapshot is null)
        {
            return Failure(
                project.DisplayName,
                repositoryRoot,
                "GitPet could not build the current standalone Send snapshot.",
                BuildCommandLog(repositoryRoot, expectedPathspecs, StandaloneProjectPublishing.GetPublishingPathspecs(config, repositoryRoot)));
        }

        var comparison = CompareSets(expectedFiles, sendSnapshot.Files);
        return new ProjectPublishBoundaryResult(
            project.DisplayName,
            repositoryRoot,
            comparison.Matched,
            comparison.AllowListOnly,
            comparison.SendOnly,
            [],
            BuildCommandLog(repositoryRoot, expectedPathspecs, sendSnapshot.Pathspecs));
    }

    public static async Task<ProjectPublishBoundaryResult> CompareAsync(
        string repositoryRoot,
        string projectPath,
        string projectName,
        string? projectId,
        string allowListText,
        CancellationToken token = default)
    {
        var config = new ConfigStore().Load();
        var project = !string.IsNullOrWhiteSpace(projectId)
            ? config.FindProject(projectId)
            : null;

        project ??= config.RecentRepositories
            .OrderByDescending(entry => entry.LastOpenedUtc)
            .FirstOrDefault(entry =>
                PathEquals(entry.RepositoryRoot, repositoryRoot) &&
                PathEquals(entry.Path, projectPath) &&
                string.Equals(entry.DisplayName, projectName, StringComparison.OrdinalIgnoreCase));

        if (project is null)
        {
            return Failure(
                projectName,
                repositoryRoot,
                "Save this GitPet project first. There is no stored project registration to compare with Send yet.");
        }

        config.ActivateProject(project.Id);
        var git = new GitService(new AuditLog());
        return await CompareAsync(config, git, repositoryRoot, allowListText, token);
    }

    public static string BuildCommandLog(
        string repositoryRoot,
        IEnumerable<string> expectedPathspecs,
        IEnumerable<string> sendPathspecs)
    {
        var lines = new List<string>
        {
            "READ-ONLY GITPET PUBLISH BOUNDARY CHECK",
            "",
            "Repository:",
            repositoryRoot,
            "",
            "Allow-list resolution:",
            FormatLsTreeCommand(expectedPathspecs),
            "",
            "Current standalone Send snapshot:",
            FormatLsTreeCommand(sendPathspecs),
            "",
            "These commands only read the saved HEAD tree. They do not stage, commit, pull, push, or modify files."
        };
        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatLsTreeCommand(IEnumerable<string> pathspecs)
    {
        var paths = pathspecs
            .Select(Normalize)
            .Where(path => path.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var suffix = paths.Length == 0
            ? string.Empty
            : " -- " + string.Join(" ", paths.Select(Quote));
        return "git ls-tree -r --full-tree --name-only HEAD" + suffix;
    }

    private static string Quote(string value) =>
        "\"" + value.Replace("\"", "\\\"") + "\"";

    private static string[] ParseFiles(string output) =>
        (output ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Normalize)
            .Where(path => path.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string[] NormalizeSet(IEnumerable<string> paths) =>
        (paths ?? Array.Empty<string>())
            .Select(Normalize)
            .Where(path => path.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string Normalize(string path) =>
        (path ?? string.Empty).Replace('\\', '/').Trim().TrimStart('/').TrimEnd('/');

    private static bool PathEquals(string left, string right)
    {
        try
        {
            return string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static ProjectPublishBoundaryResult Failure(
        string projectName,
        string repositoryRoot,
        string message,
        string? commandLog = null) =>
        new(
            projectName,
            repositoryRoot,
            [],
            [],
            [],
            [],
            commandLog ?? BuildCommandLog(repositoryRoot, [], []),
            message);
}
