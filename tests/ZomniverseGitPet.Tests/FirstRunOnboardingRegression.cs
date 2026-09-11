using System.Runtime.CompilerServices;
using ZomniverseGitPet;

internal static class FirstRunOnboardingRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        Require(
            GitRepositoryAddressParser.TryParse("openai/openai", out var slug) &&
            slug.IsGitHub &&
            slug.RepositoryName == "openai" &&
            slug.CloneSource == "https://github.com/openai/openai.git",
            "GitHub owner/repository shorthand should normalize to HTTPS clone URL.");

        Require(
            GitRepositoryAddressParser.TryParse("https://github.com/wilderruiz/ZomniverseGitPet.git", out var web) &&
            web.IsGitHub &&
            web.RepositoryName == "ZomniverseGitPet",
            "GitHub HTTPS clone URL should preserve repository identity.");

        Require(
            GitRepositoryAddressParser.TryParse("git@github.com:wilderruiz/ZomniverseGitPet.git", out var ssh) &&
            ssh.IsGitHub &&
            ssh.RepositoryName == "ZomniverseGitPet",
            "GitHub SSH clone URL should be recognized.");

        Require(
            !GitRepositoryAddressParser.TryParse("not a repository", out _),
            "Invalid repository text should be rejected.");

        var local = new AppConfig
        {
            OnboardingCompleted = true,
            ConnectionMode = GitPetConnectionModes.LocalGitOnly
        };
        local.Normalize();
        Require(
            local.SchemaVersion == 5 &&
            local.OnboardingCompleted &&
            local.ConnectionMode == GitPetConnectionModes.LocalGitOnly,
            "Local Git Only mode should survive config normalization.");

        var incomplete = new AppConfig
        {
            OnboardingCompleted = false,
            ConnectionMode = GitPetConnectionModes.GitHub
        };
        incomplete.Normalize();
        Require(
            incomplete.ConnectionMode == GitPetConnectionModes.Unconfigured,
            "Incomplete onboarding must not retain an active connection mode.");

        Console.WriteLine("First-run onboarding regression passed (modes + clone addresses).");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
