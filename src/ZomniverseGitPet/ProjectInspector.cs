namespace ZomniverseGitPet;

internal enum ProjectSuitability
{
    Ready,
    NestedRepository,
    CanPrepare,
    InvalidRepository,
    GitUnavailable,
    Unavailable
}

internal sealed record ProjectInspection(
    string SelectedPath,
    ProjectSuitability Suitability,
    string? RepositoryRoot,
    string Message,
    IReadOnlyList<GitIgnoreSuggestion> IgnoreSuggestions);

internal sealed class ProjectInspector(GitService git)
{
    public async Task<ProjectInspection> InspectAsync(string selectedPath, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(selectedPath) || !Directory.Exists(selectedPath))
            return new(selectedPath, ProjectSuitability.Unavailable, null,
                "The selected folder is unavailable or no longer exists.", []);

        var normalized = Normalize(selectedPath);
        try { _ = Directory.EnumerateFileSystemEntries(normalized).Take(1).ToArray(); }
        catch (Exception ex)
        {
            return new(normalized, ProjectSuitability.Unavailable, null,
                "The selected folder cannot be read: " + ex.Message, []);
        }

        var gitVersion = await git.GetGitVersionAsync(normalized, token);
        if (!gitVersion.Success)
            return new(normalized, ProjectSuitability.GitUnavailable, null,
                "Git for Windows is unavailable. ZomniverseGitPet requires git.exe to inspect or prepare a project.\r\n\r\n" + gitVersion.Output,
                []);

        var rootResult = await git.GetRepositoryRootAsync(normalized, token);
        if (rootResult.Success && !string.IsNullOrWhiteSpace(rootResult.Output))
        {
            var root = Normalize(rootResult.Output.Trim());
            var suggestions = GitIgnoreAdvisor.Suggest(root);
            if (PathEquals(root, normalized))
                return new(normalized, ProjectSuitability.Ready, root,
                    "This folder is already a readable Git repository.", suggestions);

            return new(normalized, ProjectSuitability.NestedRepository, root,
                $"The selected folder is inside an existing Git repository.\r\n\r\nRepository root:\r\n{root}", suggestions);
        }

        var gitMetadata = Path.Combine(normalized, ".git");
        if (Directory.Exists(gitMetadata) || File.Exists(gitMetadata))
            return new(normalized, ProjectSuitability.InvalidRepository, null,
                "This folder contains Git metadata, but Git could not read it as a valid repository. ZomniverseGitPet will not overwrite or reinitialize existing Git metadata.",
                GitIgnoreAdvisor.Suggest(normalized));

        return new(normalized, ProjectSuitability.CanPrepare, null,
            "This is a normal readable folder that can be prepared as a local Git repository.",
            GitIgnoreAdvisor.Suggest(normalized));
    }

    private static bool PathEquals(string left, string right) =>
        string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}
