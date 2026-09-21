using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace ZomniverseGitPet;

internal sealed record ApplicationUpdateInfo(
    string CurrentVersion,
    string AvailableVersion,
    string ReleaseTag,
    string ReleasePage,
    string InstallerFileName,
    string InstallerDownloadUrl,
    string InstallerSha256,
    string ReleaseNotes);

internal sealed class ApplicationUpdateService
{
    private const string LatestManifestUrl =
        "https://github.com/wilderruiz/ZomniverseGitPet/releases/latest/download/release-manifest.json";

    private static readonly HttpClient Client = CreateClient();

    public bool IsInstalledBuild =>
        File.Exists(Path.Combine(AppContext.BaseDirectory, "unins000.exe"));

    public string CurrentVersion => GetCurrentVersion().ToString(3);

    public async Task<ApplicationUpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken)
    {
        using var response = await Client.GetAsync(LatestManifestUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var manifest = await JsonSerializer.DeserializeAsync<ReleaseManifest>(
            stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken);

        if (manifest is null ||
            !string.Equals(manifest.Channel, "stable", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(manifest.Version) ||
            string.IsNullOrWhiteSpace(manifest.ReleaseTag) ||
            string.IsNullOrWhiteSpace(manifest.ReleasePage) ||
            manifest.Installer is null ||
            string.IsNullOrWhiteSpace(manifest.Installer.FileName) ||
            string.IsNullOrWhiteSpace(manifest.Installer.DownloadUrl) ||
            string.IsNullOrWhiteSpace(manifest.Installer.Sha256))
        {
            throw new InvalidDataException("The GitPet release manifest is incomplete.");
        }

        if (!TryParseVersion(manifest.Version, out var available))
            throw new InvalidDataException($"GitPet release version '{manifest.Version}' is invalid.");

        var current = GetCurrentVersion();
        if (Normalize(available).CompareTo(Normalize(current)) <= 0) return null;

        if (!IsSha256(manifest.Installer.Sha256))
            throw new InvalidDataException("The GitPet installer checksum is invalid.");

        var releaseNotes = await TryGetReleaseNotesAsync(manifest.ReleaseTag, cancellationToken);

        return new ApplicationUpdateInfo(
            current.ToString(3),
            available.ToString(3),
            manifest.ReleaseTag,
            manifest.ReleasePage,
            manifest.Installer.FileName,
            manifest.Installer.DownloadUrl,
            manifest.Installer.Sha256.ToLowerInvariant(),
            releaseNotes);
    }

    public async Task<string> DownloadAndVerifyInstallerAsync(
        ApplicationUpdateInfo update,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        var updateRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ZomniverseGitPet",
            "updates",
            update.AvailableVersion);
        Directory.CreateDirectory(updateRoot);

        var finalPath = Path.Combine(updateRoot, update.InstallerFileName);
        var partialPath = finalPath + ".download";

        if (File.Exists(finalPath) && VerifySha256(finalPath, update.InstallerSha256))
        {
            progress?.Report(100);
            return finalPath;
        }

        if (File.Exists(partialPath)) File.Delete(partialPath);

        using var response = await Client.GetAsync(
            update.InstallerDownloadUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = new FileStream(
            partialPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            1024 * 64,
            useAsync: true);

        var buffer = new byte[1024 * 64];
        long received = 0;
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            received += read;

            if (totalBytes is > 0)
            {
                var percent = (int)Math.Clamp(received * 100L / totalBytes.Value, 0, 100);
                progress?.Report(percent);
            }
        }

        await output.FlushAsync(cancellationToken);
        output.Close();

        if (!VerifySha256(partialPath, update.InstallerSha256))
        {
            File.Delete(partialPath);
            throw new InvalidDataException(
                "The downloaded GitPet installer did not match the published SHA-256 checksum.");
        }

        File.Move(partialPath, finalPath, overwrite: true);
        progress?.Report(100);
        return finalPath;
    }

    public static void LaunchInstaller(string installerPath)
    {
        if (!File.Exists(installerPath))
            throw new FileNotFoundException("The verified GitPet installer could not be found.", installerPath);

        Process.Start(new ProcessStartInfo
        {
            FileName = installerPath,
            UseShellExecute = true
        });
    }

    internal static bool IsNewerVersion(string available, string current)
    {
        return TryParseVersion(available, out var availableVersion) &&
               TryParseVersion(current, out var currentVersion) &&
               Normalize(availableVersion).CompareTo(Normalize(currentVersion)) > 0;
    }

    internal static bool VerifySha256(string path, string expectedSha256)
    {
        using var stream = File.OpenRead(path);
        var hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        return string.Equals(hash, expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> TryGetReleaseNotesAsync(string releaseTag, CancellationToken cancellationToken)
    {
        try
        {
            var url =
                "https://api.github.com/repos/wilderruiz/ZomniverseGitPet/releases/tags/" +
                Uri.EscapeDataString(releaseTag);
            using var response = await Client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode) return string.Empty;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return document.RootElement.TryGetProperty("body", out var body)
                ? body.GetString()?.Trim() ?? string.Empty
                : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ZomniverseGitPet-Updater/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    private static Version GetCurrentVersion()
    {
        var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
        return assemblyVersion ?? new Version(0, 0, 0, 0);
    }

    private static bool TryParseVersion(string text, out Version version)
    {
        var candidate = text.Trim();
        if (candidate.StartsWith('v') || candidate.StartsWith('V')) candidate = candidate[1..];
        return Version.TryParse(candidate, out version!);
    }

    private static Version Normalize(Version version)
    {
        return new Version(
            Math.Max(0, version.Major),
            Math.Max(0, version.Minor),
            Math.Max(0, version.Build),
            Math.Max(0, version.Revision));
    }

    private static bool IsSha256(string value)
    {
        return value.Length == 64 && value.All(Uri.IsHexDigit);
    }

    private sealed class ReleaseManifest
    {
        public int SchemaVersion { get; set; }
        public string Version { get; set; } = string.Empty;
        public string Channel { get; set; } = string.Empty;
        public string ReleaseTag { get; set; } = string.Empty;
        public string ReleasePage { get; set; } = string.Empty;
        public ReleaseAsset? Installer { get; set; }
    }

    private sealed class ReleaseAsset
    {
        public string FileName { get; set; } = string.Empty;
        public string Sha256 { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
    }
}
