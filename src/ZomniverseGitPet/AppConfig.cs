using System.Text.Json;

namespace ZomniverseGitPet;

public sealed class RecentRepositoryEntry
{
    public string Path { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public DateTimeOffset LastOpenedUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AppConfig
{
    public const int RecentRepositoryLimit = 20;

    public int SchemaVersion { get; set; } = 2;
    public string? RepositoryPath { get; set; }
    public List<RecentRepositoryEntry> RecentRepositories { get; set; } = [];
    public int PollSeconds { get; set; } = 20;
    public bool AutomaticCheckpointsEnabled { get; set; }
    public int QuietMinutes { get; set; } = 10;
    public bool RequireTestsForAutomaticCheckpoint { get; set; } = true;
    public List<string> TestCommands { get; set; } = [];
    public List<string> SuspiciousPathPatterns { get; set; } =
    [
        @"(^|/)\.env($|\.)", @"\.pem$", @"\.key$", "id_rsa",
        "credentials", @"secrets?\.", "password", "token"
    ];

    public void RememberRepository(string path, DateTimeOffset? openedAt = null)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var normalized = NormalizePath(path);
        RepositoryPath = normalized;

        RecentRepositories.RemoveAll(item =>
            string.Equals(NormalizePath(item.Path), normalized, StringComparison.OrdinalIgnoreCase));
        RecentRepositories.Insert(0, new RecentRepositoryEntry
        {
            Path = normalized,
            DisplayName = GetDisplayName(normalized),
            LastOpenedUtc = openedAt ?? DateTimeOffset.UtcNow
        });

        if (RecentRepositories.Count > RecentRepositoryLimit)
            RecentRepositories.RemoveRange(RecentRepositoryLimit, RecentRepositories.Count - RecentRepositoryLimit);
    }

    public void ForgetUnavailableRepositories()
    {
        var activeWasUnavailable = !string.IsNullOrWhiteSpace(RepositoryPath) && !Directory.Exists(RepositoryPath);
        RecentRepositories.RemoveAll(item => string.IsNullOrWhiteSpace(item.Path) || !Directory.Exists(item.Path));
        if (activeWasUnavailable) RepositoryPath = null;
    }

    internal void Normalize()
    {
        SchemaVersion = 2;
        var normalized = new List<RecentRepositoryEntry>();
        foreach (var item in RecentRepositories
                     .Where(item => !string.IsNullOrWhiteSpace(item.Path))
                     .OrderByDescending(item => item.LastOpenedUtc))
        {
            var path = NormalizePath(item.Path);
            if (normalized.Any(existing => string.Equals(existing.Path, path, StringComparison.OrdinalIgnoreCase))) continue;
            normalized.Add(new RecentRepositoryEntry
            {
                Path = path,
                DisplayName = string.IsNullOrWhiteSpace(item.DisplayName) ? GetDisplayName(path) : item.DisplayName,
                LastOpenedUtc = item.LastOpenedUtc
            });
            if (normalized.Count == RecentRepositoryLimit) break;
        }
        RecentRepositories = normalized;

        if (!string.IsNullOrWhiteSpace(RepositoryPath))
        {
            var active = NormalizePath(RepositoryPath);
            RepositoryPath = active;
            if (Directory.Exists(active) && !RecentRepositories.Any(item => string.Equals(item.Path, active, StringComparison.OrdinalIgnoreCase)))
            {
                RecentRepositories.Insert(0, new RecentRepositoryEntry
                {
                    Path = active,
                    DisplayName = GetDisplayName(active),
                    LastOpenedUtc = DateTimeOffset.UtcNow
                });
                if (RecentRepositories.Count > RecentRepositoryLimit)
                    RecentRepositories.RemoveRange(RecentRepositoryLimit, RecentRepositories.Count - RecentRepositoryLimit);
            }
        }
    }

    private static string NormalizePath(string path)
    {
        try { return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)); }
        catch { return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
    }

    private static string GetDisplayName(string path)
    {
        var name = Path.GetFileName(path);
        return string.IsNullOrWhiteSpace(name) ? path : name;
    }
}

public static class AppPaths
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZomniverseGitPet");
    public static string ConfigFile => Path.Combine(Root, "config.json");
    public static string AuditFile => Path.Combine(Root, "audit.jsonl");
}

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public AppConfig Load()
    {
        Directory.CreateDirectory(AppPaths.Root);
        if (!File.Exists(AppPaths.ConfigFile))
        {
            var created = new AppConfig();
            Save(created);
            return created;
        }

        try
        {
            var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(AppPaths.ConfigFile), JsonOptions)
                         ?? new AppConfig();
            config.Normalize();
            Save(config);
            return config;
        }
        catch (Exception ex)
        {
            throw new InvalidDataException($"Configuration could not be read: {ex.Message}", ex);
        }
    }

    public void Save(AppConfig config)
    {
        config.Normalize();
        Directory.CreateDirectory(AppPaths.Root);
        var temporary = AppPaths.ConfigFile + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(config, JsonOptions));
        File.Move(temporary, AppPaths.ConfigFile, true);
    }
}
