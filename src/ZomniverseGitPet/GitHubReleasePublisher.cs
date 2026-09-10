using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;

namespace ZomniverseGitPet;

internal sealed record ApplicationReleasePackage(
    bool Ready,
    string RepositoryPath,
    string RepositorySlug,
    string Version,
    string ReleaseTag,
    string PackageRoot,
    string InstallerPath,
    string PortablePath,
    string ManifestPath,
    string ChecksumsPath,
    string StatusMessage)
{
    public IReadOnlyList<string> AssetPaths =>
        [InstallerPath, PortablePath, ManifestPath, ChecksumsPath];
}

internal sealed record GitHubCliStatus(
    bool Available,
    bool Authenticated,
    string Message);

internal sealed record GitHubReleasePublishResult(
    bool Success,
    bool AlreadyExists,
    string Message,
    string? ReleaseUrl = null);

internal sealed class GitHubReleasePublisher
{
    private readonly AuditLog _audit;

    public GitHubReleasePublisher(AuditLog audit)
    {
        _audit = audit;
    }

    public ApplicationReleasePackage InspectPreparedPackage(
        string repositoryPath,
        string? originUrl)
    {
        repositoryPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repositoryPath));
        var repositorySlug = TryGetRepositorySlug(originUrl) ?? string.Empty;
        var projectPath = Path.Combine(
            repositoryPath,
            "src",
            "ZomniverseGitPet",
            "ZomniverseGitPet.csproj");

        if (!File.Exists(projectPath))
            return Failure(repositoryPath, repositorySlug,
                "This project is not the ZomniverseGitPet source tree. Application release publishing stays hidden for ordinary projects.");

        string version;
        try
        {
            var document = XDocument.Load(projectPath);
            version = document.Descendants("Version").Select(node => node.Value.Trim()).FirstOrDefault() ?? string.Empty;
        }
        catch (Exception ex)
        {
            return Failure(repositoryPath, repositorySlug, "GitPet could not read the application version. " + ex.Message);
        }

        if (string.IsNullOrWhiteSpace(version))
            return Failure(repositoryPath, repositorySlug, "The application project does not declare a release version.");

        var parent = Directory.GetParent(repositoryPath)?.FullName;
        if (string.IsNullOrWhiteSpace(parent))
            return Failure(repositoryPath, repositorySlug, "GitPet could not resolve the release-package folder beside this repository.", version);

        var packageRoot = Path.Combine(parent, "ZomniverseGitPet_Releases", "packages", version);
        var installerPath = Path.Combine(packageRoot, "installer", $"ZomniverseGitPet-Setup-{version}.exe");
        var portablePath = Path.Combine(packageRoot, "portable", $"ZomniverseGitPet-{version}-win-x64-portable.exe");
        var manifestPath = Path.Combine(packageRoot, "release-manifest.json");
        var checksumsPath = Path.Combine(packageRoot, "SHA256SUMS.txt");
        var releaseTag = "v" + version;

        if (string.IsNullOrWhiteSpace(repositorySlug))
            return Failure(repositoryPath, repositorySlug,
                "The origin remote is not a readable GitHub repository. GitPet will not publish an application release without a specific GitHub destination.",
                version, releaseTag, packageRoot, installerPath, portablePath, manifestPath, checksumsPath);

        var missing = new[] { installerPath, portablePath, manifestPath, checksumsPath }
            .Where(path => !File.Exists(path))
            .Select(Path.GetFileName)
            .ToArray();
        if (missing.Length > 0)
            return Failure(repositoryPath, repositorySlug,
                "The prepared release package is incomplete. Run scripts\\build-release.ps1 first. Missing: " + string.Join(", ", missing),
                version, releaseTag, packageRoot, installerPath, portablePath, manifestPath, checksumsPath);

        try
        {
            using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = manifest.RootElement;
            var manifestVersion = ReadString(root, "version");
            var manifestTag = ReadString(root, "releaseTag");
            var channel = ReadString(root, "channel");

            if (!string.Equals(manifestVersion, version, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(manifestTag, releaseTag, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(channel, "stable", StringComparison.OrdinalIgnoreCase))
            {
                return Failure(repositoryPath, repositorySlug,
                    "release-manifest.json does not describe this stable application version. Rebuild the package before publishing.",
                    version, releaseTag, packageRoot, installerPath, portablePath, manifestPath, checksumsPath);
            }

            if (!root.TryGetProperty("installer", out var installer) ||
                !root.TryGetProperty("portable", out var portable))
            {
                return Failure(repositoryPath, repositorySlug,
                    "release-manifest.json is missing installer or portable metadata.",
                    version, releaseTag, packageRoot, installerPath, portablePath, manifestPath, checksumsPath);
            }

            var installerFile = ReadString(installer, "fileName");
            var portableFile = ReadString(portable, "fileName");
            var installerHash = ReadString(installer, "sha256");
            var portableHash = ReadString(portable, "sha256");

            if (!string.Equals(installerFile, Path.GetFileName(installerPath), StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(portableFile, Path.GetFileName(portablePath), StringComparison.OrdinalIgnoreCase))
            {
                return Failure(repositoryPath, repositorySlug,
                    "The manifest filenames do not match the prepared package. Rebuild the release package.",
                    version, releaseTag, packageRoot, installerPath, portablePath, manifestPath, checksumsPath);
            }

            if (!ApplicationUpdateService.VerifySha256(installerPath, installerHash) ||
                !ApplicationUpdateService.VerifySha256(portablePath, portableHash))
            {
                return Failure(repositoryPath, repositorySlug,
                    "A prepared release asset no longer matches its published SHA-256 value. GitPet refuses to publish it. Rebuild the package.",
                    version, releaseTag, packageRoot, installerPath, portablePath, manifestPath, checksumsPath);
            }
        }
        catch (Exception ex)
        {
            return Failure(repositoryPath, repositorySlug,
                "GitPet could not validate the prepared release package. " + ex.Message,
                version, releaseTag, packageRoot, installerPath, portablePath, manifestPath, checksumsPath);
        }

        return new ApplicationReleasePackage(
            true,
            repositoryPath,
            repositorySlug,
            version,
            releaseTag,
            packageRoot,
            installerPath,
            portablePath,
            manifestPath,
            checksumsPath,
            "Release package verified: installer, portable build, manifest and SHA-256 metadata are ready.");
    }

    public async Task<GitHubCliStatus> GetGitHubCliStatusAsync(CancellationToken token = default)
    {
        var executable = FindGitHubCliExecutable();
        var version = await RunProcessAsync(executable, ["--version"], null, TimeSpan.FromSeconds(15), token);
        if (!version.Started || !version.Success)
        {
            return new GitHubCliStatus(
                false,
                false,
                "GitHub CLI (gh) was not found. Install it once with: winget install --id GitHub.cli -e");
        }

        var auth = await RunProcessAsync(
            executable,
            ["auth", "status", "--hostname", "github.com"],
            null,
            TimeSpan.FromSeconds(20),
            token);
        if (!auth.Success)
        {
            return new GitHubCliStatus(
                true,
                false,
                "GitHub CLI is installed but not signed in. Run: gh auth login");
        }

        return new GitHubCliStatus(true, true, "GitHub CLI is installed and authenticated ✓");
    }

    public async Task<GitHubReleasePublishResult> PublishAsync(
        ApplicationReleasePackage package,
        string targetBranch,
        string title,
        string notes,
        CancellationToken token = default)
    {
        if (!package.Ready)
            return new(false, false, package.StatusMessage);
        if (string.IsNullOrWhiteSpace(targetBranch))
            return new(false, false, "A named target branch is required.");
        if (string.IsNullOrWhiteSpace(title))
            return new(false, false, "A release title is required.");

        var cli = await GetGitHubCliStatusAsync(token);
        if (!cli.Available || !cli.Authenticated)
            return new(false, false, cli.Message);

        var executable = FindGitHubCliExecutable();
        var existing = await RunProcessAsync(
            executable,
            ["release", "view", package.ReleaseTag, "--repo", package.RepositorySlug, "--json", "url", "--jq", ".url"],
            package.RepositoryPath,
            TimeSpan.FromSeconds(30),
            token);
        if (existing.Success)
        {
            var url = existing.Output.Trim();
            return new(false, true,
                $"GitHub Release {package.ReleaseTag} already exists. GitPet did not replace or upload over an existing release.",
                string.IsNullOrWhiteSpace(url) ? null : url);
        }

        var notesRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ZomniverseGitPet",
            "release-notes");
        Directory.CreateDirectory(notesRoot);
        var notesPath = Path.Combine(notesRoot, $"{package.ReleaseTag}-{Guid.NewGuid():N}.md");

        try
        {
            await File.WriteAllTextAsync(notesPath, notes.Trim() + Environment.NewLine, token);

            var args = new List<string>
            {
                "release", "create", package.ReleaseTag
            };
            args.AddRange(package.AssetPaths);
            args.AddRange([
                "--repo", package.RepositorySlug,
                "--target", targetBranch,
                "--title", title.Trim(),
                "--notes-file", notesPath,
                "--latest"
            ]);

            var publish = await RunProcessAsync(
                executable,
                args,
                package.RepositoryPath,
                TimeSpan.FromMinutes(10),
                token);

            await _audit.WriteAsync("application_release_publish", new
            {
                repository = package.RepositoryPath,
                repositorySlug = package.RepositorySlug,
                package.ReleaseTag,
                branch = targetBranch,
                success = publish.Success,
                publish.ExitCode
            });

            if (!publish.Success)
                return new(false, false,
                    "GitHub Release publication stopped. Nothing was force-pushed or overwritten.\r\n\r\n" + publish.Output);

            var view = await RunProcessAsync(
                executable,
                ["release", "view", package.ReleaseTag, "--repo", package.RepositorySlug, "--json", "url", "--jq", ".url"],
                package.RepositoryPath,
                TimeSpan.FromSeconds(30),
                token);
            var releaseUrl = view.Success ? view.Output.Trim() : null;

            return new(true, false,
                $"GitHub Release {package.ReleaseTag} published successfully ✓\r\n\r\n" +
                "Uploaded the installer, portable build, release manifest and SHA-256 checksums.\r\n" +
                "No existing release was replaced and no branch was force-pushed.",
                string.IsNullOrWhiteSpace(releaseUrl) ? null : releaseUrl);
        }
        finally
        {
            try { File.Delete(notesPath); }
            catch { }
        }
    }

    internal static string? TryGetRepositorySlug(string? originUrl)
    {
        var web = MajorUpdateCoordinator.TryGetGitHubWebUrl(originUrl);
        if (web is null || !Uri.TryCreate(web, UriKind.Absolute, out var uri)) return null;
        var path = uri.AbsolutePath.Trim('/');
        return path.Count(character => character == '/') == 1 ? path : null;
    }

    internal static bool LooksLikeGitPetSource(string? repositoryPath)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath)) return false;
        try
        {
            return File.Exists(Path.Combine(
                Path.GetFullPath(repositoryPath),
                "src",
                "ZomniverseGitPet",
                "ZomniverseGitPet.csproj"));
        }
        catch
        {
            return false;
        }
    }

    private static string ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) ? property.GetString()?.Trim() ?? string.Empty : string.Empty;

    private static ApplicationReleasePackage Failure(
        string repositoryPath,
        string repositorySlug,
        string message,
        string version = "",
        string releaseTag = "",
        string packageRoot = "",
        string installerPath = "",
        string portablePath = "",
        string manifestPath = "",
        string checksumsPath = "") =>
        new(false, repositoryPath, repositorySlug, version, releaseTag, packageRoot,
            installerPath, portablePath, manifestPath, checksumsPath, message);

    private static string FindGitHubCliExecutable()
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GH_EXE")))
            candidates.Add(Environment.GetEnvironmentVariable("GH_EXE")!);

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrWhiteSpace(programFiles))
            candidates.Add(Path.Combine(programFiles, "GitHub CLI", "gh.exe"));

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(local))
        {
            candidates.Add(Path.Combine(local, "Programs", "GitHub CLI", "gh.exe"));
            candidates.Add(Path.Combine(local, "Microsoft", "WinGet", "Links", "gh.exe"));
        }

        return candidates.FirstOrDefault(File.Exists) ?? "gh";
    }

    private static async Task<ProcessResult> RunProcessAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        TimeSpan timeout,
        CancellationToken token)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                    ? Environment.CurrentDirectory
                    : workingDirectory
            };
            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);

            using var process = new Process { StartInfo = startInfo };
            if (!process.Start()) return new(false, -1, "The process could not be started.", false);

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeoutCts.CancelAfter(timeout);

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                catch { }
                if (token.IsCancellationRequested) throw;
                return new(true, -1, $"Command timed out after {timeout.TotalSeconds:0} seconds.", true);
            }

            var stdout = await outputTask;
            var stderr = await errorTask;
            var combined = string.Join(Environment.NewLine,
                new[] { stdout.Trim(), stderr.Trim() }.Where(value => !string.IsNullOrWhiteSpace(value)));
            return new(true, process.ExitCode, combined, false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new(false, -1, ex.Message, false);
        }
    }

    private sealed record ProcessResult(bool Started, int ExitCode, string Output, bool TimedOut)
    {
        public bool Success => Started && !TimedOut && ExitCode == 0;
    }
}
