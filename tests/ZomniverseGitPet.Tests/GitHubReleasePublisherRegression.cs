using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using ZomniverseGitPet;

internal static class GitHubReleasePublisherRegression
{
    [ModuleInitializer]
    public static void Run()
    {
        if (GitHubReleasePublisher.TryGetRepositorySlug("https://github.com/wilderruiz/ZomniverseGitPet.git") !=
            "wilderruiz/ZomniverseGitPet")
            throw new InvalidOperationException("Release publisher regression: HTTPS GitHub origin should resolve owner/repository.");

        if (GitHubReleasePublisher.TryGetRepositorySlug("git@github.com:wilderruiz/ZomniverseGitPet.git") !=
            "wilderruiz/ZomniverseGitPet")
            throw new InvalidOperationException("Release publisher regression: SSH GitHub origin should resolve owner/repository.");

        if (GitHubReleasePublisher.TryGetRepositorySlug("https://gitlab.com/example/project.git") is not null)
            throw new InvalidOperationException("Release publisher regression: non-GitHub origins must not be accepted.");

        var parent = Path.Combine(Path.GetTempPath(), "zgitpet-release-publisher-" + Guid.NewGuid().ToString("N"));
        var repository = Path.Combine(parent, "ZomniverseGitPet");
        const string version = "9.8.7";
        const string sourceBranch = "feature/release-test";
        const string sourceCommit = "0123456789abcdef0123456789abcdef01234567";
        var projectDirectory = Path.Combine(repository, "src", "ZomniverseGitPet");
        var packageRoot = Path.Combine(parent, "ZomniverseGitPet_Releases", "packages", version);
        var installerDirectory = Path.Combine(packageRoot, "installer");
        var portableDirectory = Path.Combine(packageRoot, "portable");

        Directory.CreateDirectory(projectDirectory);
        Directory.CreateDirectory(installerDirectory);
        Directory.CreateDirectory(portableDirectory);

        try
        {
            File.WriteAllText(
                Path.Combine(projectDirectory, "ZomniverseGitPet.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><Version>9.8.7</Version></PropertyGroup></Project>");

            var installerName = $"ZomniverseGitPet-Setup-{version}.exe";
            var portableName = $"ZomniverseGitPet-{version}-win-x64-portable.exe";
            var installerPath = Path.Combine(installerDirectory, installerName);
            var portablePath = Path.Combine(portableDirectory, portableName);
            File.WriteAllText(installerPath, "verified installer payload");
            File.WriteAllText(portablePath, "verified portable payload");

            var installerHash = Sha256(installerPath);
            var portableHash = Sha256(portablePath);
            var manifest = new
            {
                schemaVersion = 1,
                version,
                channel = "stable",
                releaseTag = "v" + version,
                sourceBranch,
                sourceCommit,
                installer = new { fileName = installerName, sha256 = installerHash },
                portable = new { fileName = portableName, sha256 = portableHash }
            };
            File.WriteAllText(
                Path.Combine(packageRoot, "release-manifest.json"),
                JsonSerializer.Serialize(manifest));
            File.WriteAllText(
                Path.Combine(packageRoot, "SHA256SUMS.txt"),
                $"{installerHash}  {installerName}\n{portableHash}  {portableName}\n");

            var publisher = new GitHubReleasePublisher(new AuditLog());
            var package = publisher.InspectPreparedPackage(
                repository,
                "https://github.com/wilderruiz/ZomniverseGitPet.git");
            if (!package.Ready || package.Version != version || package.ReleaseTag != "v" + version ||
                package.AssetPaths.Count != 4 || package.SourceBranch != sourceBranch || package.SourceCommit != sourceCommit)
                throw new InvalidOperationException("Release publisher regression: a complete verified package should be publishable.");

            if (!GitHubReleasePublisher.PackageMatchesSource(package, sourceBranch, sourceCommit))
                throw new InvalidOperationException("Release publisher regression: matching source provenance should be accepted.");

            if (GitHubReleasePublisher.PackageMatchesSource(package, sourceBranch, new string('f', 40)))
                throw new InvalidOperationException("Release publisher regression: stale source commits must be rejected.");

            File.AppendAllText(portablePath, "tampered");
            var tampered = publisher.InspectPreparedPackage(
                repository,
                "https://github.com/wilderruiz/ZomniverseGitPet.git");
            if (tampered.Ready)
                throw new InvalidOperationException("Release publisher regression: a tampered package must be rejected.");
        }
        finally
        {
            try { Directory.Delete(parent, recursive: true); }
            catch { }
        }

        Console.WriteLine("GitHub release publisher regression passed (origin + package + provenance rules).");
    }

    private static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
