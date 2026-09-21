namespace ZomniverseGitPet;

internal static class GitServiceCloneExtensions
{
    public static async Task<CommandResult> CloneRepositoryAsync(
        this GitService git,
        string cloneSource,
        string destinationPath,
        CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(cloneSource) || cloneSource.Contains('\r') || cloneSource.Contains('\n'))
            return new(-1, "A valid repository address is required.");
        if (string.IsNullOrWhiteSpace(destinationPath))
            return new(-1, "Choose where GitPet should copy the repository.");

        string destination;
        string parent;
        try
        {
            destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destinationPath));
            parent = Directory.GetParent(destination)?.FullName ?? string.Empty;
        }
        catch (Exception ex)
        {
            return new(-1, "GitPet could not resolve the clone destination.\r\n\r\n" + ex.Message);
        }

        if (string.IsNullOrWhiteSpace(parent))
            return new(-1, "The clone destination needs a parent folder.");

        if (Directory.Exists(destination) && Directory.EnumerateFileSystemEntries(destination).Any())
            return new(-1, "The destination folder already contains files. GitPet will not clone over existing content.");

        Directory.CreateDirectory(parent);

        var result = await git.RunGitAsync(
            parent,
            ["clone", "--origin", "origin", "--", cloneSource, destination],
            TimeSpan.FromMinutes(15),
            token);

        if (!result.Success) return result;

        var root = await git.GetRepositoryRootAsync(destination, token);
        if (!root.Success || string.IsNullOrWhiteSpace(root.Output))
        {
            return new(-1,
                "Git reported that cloning completed, but GitPet could not verify the copied repository.\r\n\r\n" + root.Output);
        }

        return new(0, Path.TrimEndingDirectorySeparator(Path.GetFullPath(root.Output.Trim())));
    }
}
