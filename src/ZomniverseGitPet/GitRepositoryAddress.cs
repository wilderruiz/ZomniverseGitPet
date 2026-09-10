namespace ZomniverseGitPet;

internal sealed record GitRepositoryAddress(string CloneSource, string RepositoryName, bool IsGitHub);

internal static class GitRepositoryAddressParser
{
    public static bool TryParse(string? value, out GitRepositoryAddress address)
    {
        address = new GitRepositoryAddress(string.Empty, string.Empty, false);
        var input = (value ?? string.Empty).Trim();
        if (input.Length == 0 || input.Contains('\r') || input.Contains('\n')) return false;

        if (TryParseGitHubSlug(input, out var slugName))
        {
            address = new GitRepositoryAddress(
                $"https://github.com/{input.TrimEnd('/')}.git",
                slugName,
                true);
            return true;
        }

        if (Uri.TryCreate(input, UriKind.Absolute, out var uri))
        {
            var name = RepositoryNameFromPath(uri.AbsolutePath);
            if (string.IsNullOrWhiteSpace(name)) return false;
            var isGitHub = uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase);
            address = new GitRepositoryAddress(input, name, isGitHub);
            return true;
        }

        if (input.StartsWith("git@", StringComparison.OrdinalIgnoreCase) && input.Contains(':'))
        {
            var colon = input.IndexOf(':');
            var name = RepositoryNameFromPath(input[(colon + 1)..]);
            if (string.IsNullOrWhiteSpace(name)) return false;
            address = new GitRepositoryAddress(
                input,
                name,
                input.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase));
            return true;
        }

        if (Path.IsPathRooted(input))
        {
            var normalized = Path.TrimEndingDirectorySeparator(input);
            var name = Path.GetFileName(normalized);
            if (string.IsNullOrWhiteSpace(name)) return false;
            address = new GitRepositoryAddress(normalized, name, false);
            return true;
        }

        return false;
    }

    internal static string DefaultCloneRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "GitPet Projects");

    private static bool TryParseGitHubSlug(string input, out string name)
    {
        name = string.Empty;
        var value = input.Trim('/');
        var parts = value.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2) return false;
        if (parts.Any(part => part.Length == 0 || part.Contains(' ') || part.Contains(':') || part.Contains('\\'))) return false;
        name = RepositoryNameFromPath(parts[1]);
        return name.Length > 0;
    }

    private static string RepositoryNameFromPath(string path)
    {
        var value = path.Trim().Trim('/', '\\');
        if (value.Length == 0) return string.Empty;
        var segment = value.Split('/', '\\').LastOrDefault() ?? string.Empty;
        return segment.EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            ? segment[..^4]
            : segment;
    }
}
