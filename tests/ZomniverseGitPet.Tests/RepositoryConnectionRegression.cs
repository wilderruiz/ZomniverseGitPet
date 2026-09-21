using System.Runtime.CompilerServices;
using ZomniverseGitPet;

internal static class RepositoryConnectionRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        var suggested = GitHubAccountService.SuggestRepositoryName("ZAR AI V2");
        if (suggested != "zar-ai-v2")
            throw new InvalidOperationException("Repository name suggestion was not GitHub friendly.");

        var exact = GitHubAccountService.ScoreRepositoryCandidate(
            "zar-ai-v2",
            ["zaraiv2", "zomniverse"]);
        var partial = GitHubAccountService.ScoreRepositoryCandidate(
            "zomniverse-zar-ai-v2",
            ["zaraiv2", "zomniverse"]);
        var unrelated = GitHubAccountService.ScoreRepositoryCandidate(
            "other-project",
            ["zaraiv2", "zomniverse"]);

        if (exact <= partial || partial <= unrelated)
            throw new InvalidOperationException("Repository candidate ranking did not prefer the closest project match.");

        if (!GitRepositoryAddressParser.TryParse("wilderruiz/zar-ai-v2", out var address) ||
            !address.CloneSource.Contains("wilderruiz/zar-ai-v2", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Friendly owner/repository connection input stopped parsing.");

        Console.WriteLine("Repository connection regression passed (naming + candidate ranking + address parsing).");
    }
}
