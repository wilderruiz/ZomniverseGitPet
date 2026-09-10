namespace ZomniverseGitPet;

internal static class GitPetConnectionModes
{
    public const string Unconfigured = "unconfigured";
    public const string GitHub = "github";
    public const string LocalGitOnly = "local-git-only";

    public static string Normalize(string? value) => value switch
    {
        GitHub => GitHub,
        LocalGitOnly => LocalGitOnly,
        _ => Unconfigured
    };
}
