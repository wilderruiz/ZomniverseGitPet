using System.Text.Json;

namespace ZomniverseGitPet;

public sealed class RecentRepositoryEntry
{
    public string Path { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public DateTimeOffset LastOpenedUtc { get; set; } = DateTimeOffset.UtcNow;
    public List<string> TestCommands { get; set; } = [];
}

public sealed class AppConfig
{
    public const int RecentRepositoryLimit = 20;

    public int SchemaVersion { get; set; } = 4;
    public string? RepositoryPath { get; set; }
    public List<RecentRepositoryEntry> RecentRepositories { get; set; } = [];
    public int PollSeconds { get; set; } = 20;
    public bool AutomaticCheckpointsEnabled { get; set; }
    public int QuietMinutes { get; set; } = 10;
    public bool RequireTestsForAutomaticCheckpoint { get; set; } = true;

    /* ========================================================================== 
       PATCH: FIRST-RUN CONNECTION MODE
       DATE.TIME: 2026-09-10 21:05 +03:00
       Remember GitHub or local-only onboarding choice.
       ========================================================================== */
    public bool OnboardingCompleted { get; set; }
    public string ConnectionMode { get; set; } = GitPetConnectionModes.Unconfigured;

    // Legacy v1/v2 field. Kept only so existing config.json files can migrate
    // their test commands into the currently active project.
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

        var previous = RecentRepositories.FirstOrDefault(item =>
            string.Equals(NormalizePath(item.Path), normalized, StringComparison.OrdinalIgnoreCase));
        var testCommands = NormalizeCommands(previous?.TestCommands);

        RecentRepositories.RemoveAll(item =>
            string.Equals(NormalizePath(item.Path), normalized, StringComparison.OrdinalIgnoreCase));
        RecentRepositories.Insert(0, new RecentRepositoryEntry
        {
            Path = normalized,
            DisplayName = GetDisplayName(normalized),
            LastOpenedUtc = openedAt ?? DateTimeOffset.UtcNow,
            TestCommands = testCommands
        });

        if (RecentRepositories.Count > RecentRepositoryLimit)
            RecentRepositories.RemoveRange(RecentRepositoryLimit, RecentRepositories.Count - RecentRepositoryLimit);
    }

    public bool ForgetRepository(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var normalized = NormalizePath(path);
        var removed = RecentRepositories.RemoveAll(item =>
            string.Equals(NormalizePath(item.Path), normalized, StringComparison.OrdinalIgnoreCase)) > 0;

        if (!string.IsNullOrWhiteSpace(RepositoryPath) &&
            string.Equals(NormalizePath(RepositoryPath), normalized, StringComparison.OrdinalIgnoreCase))
            RepositoryPath = null;

        return removed;
    }

    public IReadOnlyList<string> GetTestCommandsForRepository(string? path = null)
    {
        var value = string.IsNullOrWhiteSpace(path) ? RepositoryPath : path;
        if (string.IsNullOrWhiteSpace(value)) return Array.Empty<string>();
        var normalized = NormalizePath(value);
        var entry = RecentRepositories.FirstOrDefault(item =>
            string.Equals(NormalizePath(item.Path), normalized, StringComparison.OrdinalIgnoreCase));
        return entry is null ? Array.Empty<string>() : NormalizeCommands(entry.TestCommands);
    }

    public void SetTestCommandsForRepository(string path, IEnumerable<string> commands)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var normalized = NormalizePath(path);
        var entry = RecentRepositories.FirstOrDefault(item =>
            string.Equals(NormalizePath(item.Path), normalized, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            entry = new RecentRepositoryEntry
            {
                Path = normalized,
                DisplayName = GetDisplayName(normalized),
                LastOpenedUtc = DateTimeOffset.UtcNow
            };
            RecentRepositories.Insert(0, entry);
        }

        entry.TestCommands = NormalizeCommands(commands);
    }

    public void ForgetUnavailableRepositories()
    {
        var activeWasUnavailable = !string.IsNullOrWhiteSpace(RepositoryPath) && !Directory.Exists(RepositoryPath);
        RecentRepositories.RemoveAll(item => string.IsNullOrWhiteSpace(item.Path) || !Directory.Exists(item.Path));
        if (activeWasUnavailable) RepositoryPath = null;
    }

    internal void Normalize()
    {
        SchemaVersion = 4;
        ConnectionMode = GitPetConnectionModes.Normalize(ConnectionMode);
        if (!OnboardingCompleted) ConnectionMode = GitPetConnectionModes.Unconfigured;

        RecentRepositories ??= [];
        TestCommands ??= [];
        SuspiciousPathPatterns ??= [];

        var normalized = new List<RecentRepositoryEntry>();
        foreach (var item in RecentRepositories
                     .Where(item => item is not null && !string.IsNullOrWhiteSpace(item.Path))
                     .OrderByDescending(item => item.LastOpenedUtc))
        {
            var path = NormalizePath(item.Path);
            if (normalized.Any(existing => string.Equals(existing.Path, path, StringComparison.OrdinalIgnoreCase))) continue;
            normalized.Add(new RecentRepositoryEntry
            {
                Path = path,
                DisplayName = string.IsNullOrWhiteSpace(item.DisplayName) ? GetDisplayName(path) : item.DisplayName,
                LastOpenedUtc = item.LastOpenedUtc,
                TestCommands = NormalizeCommands(item.TestCommands)
            });
            if (normalized.Count == RecentRepositoryLimit) break;
        }
        RecentRepositories = normalized;

        if (!string.IsNullOrWhiteSpace(RepositoryPath))
        {
            var active = NormalizePath(RepositoryPath);
            RepositoryPath = active;
            var existing = RecentRepositories.FirstOrDefault(item =>
                string.Equals(item.Path, active, StringComparison.OrdinalIgnoreCase));
            if (Directory.Exists(active) && existing is null)
            {
                existing = new RecentRepositoryEntry
                {
                    Path = active,
                    DisplayName = GetDisplayName(active),
                    LastOpenedUtc = DateTimeOffset.UtcNow
                };
                RecentRepositories.Insert(0, existing);
                if (RecentRepositories.Count > RecentRepositoryLimit)
                    RecentRepositories.RemoveRange(RecentRepositoryLimit, RecentRepositories.Count - RecentRepositoryLimit);
            }

            // Migrate the old global test list into the active project once.
            if (existing is not null && (existing.TestCommands?.Count ?? 0) == 0 && TestCommands.Count > 0)
            {
                existing.TestCommands = NormalizeCommands(TestCommands);
                TestCommands.Clear();
            }
        }
    }

    private static List<string> NormalizeCommands(IEnumerable<string>? commands) =>
        (commands ?? Array.Empty<string>())
            .Where(command => command is not null)
            .Select(command => command.Trim())
            .Where(command => command.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

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
