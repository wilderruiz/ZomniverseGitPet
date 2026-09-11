using System.Text.Json;

namespace ZomniverseGitPet;

public sealed class ProjectScopeConfigEntry
{
    public string RelativePath { get; set; } = "";
    public bool IsDirectory { get; set; }
}

public sealed class RecentRepositoryEntry
{
    // "RecentRepositoryEntry" is retained for config compatibility, but one entry now
    // represents a GitPet project. Multiple entries may share the same Git repository.
    public string Id { get; set; } = "";
    public string Path { get; set; } = "";
    public string RepositoryRoot { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool TrackEverything { get; set; } = true;
    public List<ProjectScopeConfigEntry> ScopeEntries { get; set; } = [];
    public DateTimeOffset LastOpenedUtc { get; set; } = DateTimeOffset.UtcNow;
    public List<string> TestCommands { get; set; } = [];
}

public sealed class AppConfig
{
    public const int RecentRepositoryLimit = 20;

    public int SchemaVersion { get; set; } = 5;

    // RepositoryPath remains the authoritative Git working root for compatibility with
    // existing Git operations. ActiveProjectId identifies the logical GitPet project.
    public string? RepositoryPath { get; set; }
    public string? ActiveProjectId { get; set; }
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

    public RecentRepositoryEntry? GetActiveProject()
    {
        if (!string.IsNullOrWhiteSpace(ActiveProjectId))
        {
            var byId = RecentRepositories.FirstOrDefault(item =>
                string.Equals(item.Id, ActiveProjectId, StringComparison.OrdinalIgnoreCase));
            if (byId is not null) return byId;
        }

        if (string.IsNullOrWhiteSpace(RepositoryPath)) return null;
        var repository = NormalizePath(RepositoryPath);
        return RecentRepositories
            .OrderByDescending(item => item.LastOpenedUtc)
            .FirstOrDefault(item =>
                string.Equals(NormalizePath(item.RepositoryRoot), repository, StringComparison.OrdinalIgnoreCase));
    }

    public RecentRepositoryEntry? FindProject(string id) =>
        string.IsNullOrWhiteSpace(id)
            ? null
            : RecentRepositories.FirstOrDefault(item =>
                string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));

    public RecentRepositoryEntry? FindProjectByPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var normalized = NormalizePath(path);
        return RecentRepositories
            .OrderByDescending(item => item.LastOpenedUtc)
            .FirstOrDefault(item =>
                string.Equals(NormalizePath(item.Path), normalized, StringComparison.OrdinalIgnoreCase));
    }

    public void RememberRepository(string path, DateTimeOffset? openedAt = null)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var normalized = NormalizePath(path);
        var existing = RecentRepositories.FirstOrDefault(item =>
            string.Equals(NormalizePath(item.Path), normalized, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(NormalizePath(item.RepositoryRoot), normalized, StringComparison.OrdinalIgnoreCase));

        RememberProject(
            normalized,
            normalized,
            existing?.DisplayName ?? GetDisplayName(normalized),
            trackEverything: true,
            scopeEntries: [],
            existing?.Id,
            openedAt);
    }

    internal RecentRepositoryEntry RememberProject(
        string projectPath,
        string repositoryRoot,
        string? displayName,
        bool trackEverything,
        IEnumerable<ProjectScopeEntry>? scopeEntries,
        string? projectId = null,
        DateTimeOffset? openedAt = null)
    {
        var normalizedProject = NormalizePath(projectPath);
        var normalizedRepository = NormalizePath(repositoryRoot);
        var existing = !string.IsNullOrWhiteSpace(projectId)
            ? FindProject(projectId)
            : null;

        var id = existing?.Id;
        if (string.IsNullOrWhiteSpace(id)) id = Guid.NewGuid().ToString("N");
        var tests = NormalizeCommands(existing?.TestCommands);

        if (existing is not null) RecentRepositories.Remove(existing);

        var entry = new RecentRepositoryEntry
        {
            Id = id,
            Path = normalizedProject,
            RepositoryRoot = normalizedRepository,
            DisplayName = NormalizeDisplayName(displayName, normalizedProject),
            TrackEverything = trackEverything,
            ScopeEntries = NormalizeScopeEntries(scopeEntries),
            LastOpenedUtc = openedAt ?? DateTimeOffset.UtcNow,
            TestCommands = tests
        };

        RecentRepositories.Insert(0, entry);
        ActiveProjectId = entry.Id;
        RepositoryPath = entry.RepositoryRoot;

        if (RecentRepositories.Count > RecentRepositoryLimit)
            RecentRepositories.RemoveRange(RecentRepositoryLimit, RecentRepositories.Count - RecentRepositoryLimit);
        return entry;
    }

    public bool ActivateProject(string id, DateTimeOffset? openedAt = null)
    {
        var entry = FindProject(id);
        if (entry is null) return false;

        entry.LastOpenedUtc = openedAt ?? DateTimeOffset.UtcNow;
        ActiveProjectId = entry.Id;
        RepositoryPath = entry.RepositoryRoot;
        RecentRepositories.Remove(entry);
        RecentRepositories.Insert(0, entry);
        return true;
    }

    public bool RenameProject(string id, string displayName)
    {
        var entry = FindProject(id);
        if (entry is null || string.IsNullOrWhiteSpace(displayName)) return false;
        entry.DisplayName = displayName.Trim();
        return true;
    }

    public bool ForgetProject(string id)
    {
        var entry = FindProject(id);
        if (entry is null) return false;
        var wasActive = string.Equals(entry.Id, ActiveProjectId, StringComparison.OrdinalIgnoreCase);
        RecentRepositories.Remove(entry);

        if (wasActive)
        {
            ActiveProjectId = null;
            RepositoryPath = null;
        }
        return true;
    }

    public bool ForgetRepository(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var normalized = NormalizePath(path);
        var matches = RecentRepositories
            .Where(item => string.Equals(NormalizePath(item.Path), normalized, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (matches.Length == 0)
        {
            matches = RecentRepositories
                .Where(item => string.Equals(NormalizePath(item.RepositoryRoot), normalized, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }
        if (matches.Length == 0) return false;

        var activeRemoved = matches.Any(item =>
            string.Equals(item.Id, ActiveProjectId, StringComparison.OrdinalIgnoreCase));
        foreach (var entry in matches) RecentRepositories.Remove(entry);
        if (activeRemoved)
        {
            ActiveProjectId = null;
            RepositoryPath = null;
        }
        return true;
    }

    public IReadOnlyList<string> GetTestCommandsForRepository(string? path = null)
    {
        var entry = ResolveProjectForPath(path);
        return entry is null ? Array.Empty<string>() : NormalizeCommands(entry.TestCommands);
    }

    public void SetTestCommandsForRepository(string path, IEnumerable<string> commands)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var entry = ResolveProjectForPath(path);
        if (entry is null)
        {
            RememberRepository(path);
            entry = GetActiveProject();
        }
        if (entry is not null) entry.TestCommands = NormalizeCommands(commands);
    }

    public void ForgetUnavailableRepositories()
    {
        var activeId = ActiveProjectId;
        RecentRepositories.RemoveAll(item =>
            string.IsNullOrWhiteSpace(item.Path) ||
            string.IsNullOrWhiteSpace(item.RepositoryRoot) ||
            !Directory.Exists(item.Path) ||
            !Directory.Exists(item.RepositoryRoot));

        if (!string.IsNullOrWhiteSpace(activeId) && FindProject(activeId) is null)
        {
            ActiveProjectId = null;
            RepositoryPath = null;
        }
    }

    internal void Normalize()
    {
        SchemaVersion = 5;
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
            var projectPath = NormalizePath(item.Path);
            var repositoryRoot = string.IsNullOrWhiteSpace(item.RepositoryRoot)
                ? projectPath
                : NormalizePath(item.RepositoryRoot);
            var id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString("N") : item.Id.Trim();
            if (normalized.Any(existing => string.Equals(existing.Id, id, StringComparison.OrdinalIgnoreCase)))
                id = Guid.NewGuid().ToString("N");

            normalized.Add(new RecentRepositoryEntry
            {
                Id = id,
                Path = projectPath,
                RepositoryRoot = repositoryRoot,
                DisplayName = NormalizeDisplayName(item.DisplayName, projectPath),
                TrackEverything = item.TrackEverything || (item.ScopeEntries?.Count ?? 0) == 0 && PathEquals(projectPath, repositoryRoot),
                ScopeEntries = NormalizeScopeConfigEntries(item.ScopeEntries),
                LastOpenedUtc = item.LastOpenedUtc,
                TestCommands = NormalizeCommands(item.TestCommands)
            });
            if (normalized.Count == RecentRepositoryLimit) break;
        }
        RecentRepositories = normalized;

        if (!string.IsNullOrWhiteSpace(ActiveProjectId) && FindProject(ActiveProjectId) is null)
            ActiveProjectId = null;

        if (string.IsNullOrWhiteSpace(ActiveProjectId) && !string.IsNullOrWhiteSpace(RepositoryPath))
        {
            var repository = NormalizePath(RepositoryPath);
            var existing = RecentRepositories.FirstOrDefault(item =>
                string.Equals(NormalizePath(item.RepositoryRoot), repository, StringComparison.OrdinalIgnoreCase));
            if (existing is null && Directory.Exists(repository))
            {
                existing = new RecentRepositoryEntry
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Path = repository,
                    RepositoryRoot = repository,
                    DisplayName = GetDisplayName(repository),
                    TrackEverything = true,
                    LastOpenedUtc = DateTimeOffset.UtcNow
                };
                RecentRepositories.Insert(0, existing);
                if (RecentRepositories.Count > RecentRepositoryLimit)
                    RecentRepositories.RemoveRange(RecentRepositoryLimit, RecentRepositories.Count - RecentRepositoryLimit);
            }
            ActiveProjectId = existing?.Id;
        }

        var active = GetActiveProject();
        if (active is not null)
        {
            RepositoryPath = active.RepositoryRoot;
            if ((active.TestCommands?.Count ?? 0) == 0 && TestCommands.Count > 0)
            {
                active.TestCommands = NormalizeCommands(TestCommands);
                TestCommands.Clear();
            }
        }
        else if (string.IsNullOrWhiteSpace(ActiveProjectId))
        {
            RepositoryPath = null;
        }
    }

    private RecentRepositoryEntry? ResolveProjectForPath(string? path)
    {
        var active = GetActiveProject();
        if (string.IsNullOrWhiteSpace(path)) return active;

        var normalized = NormalizePath(path);
        if (active is not null &&
            (PathEquals(active.Path, normalized) || PathEquals(active.RepositoryRoot, normalized)))
            return active;

        return RecentRepositories.FirstOrDefault(item =>
            PathEquals(item.Path, normalized) || PathEquals(item.RepositoryRoot, normalized));
    }

    private static List<ProjectScopeConfigEntry> NormalizeScopeEntries(IEnumerable<ProjectScopeEntry>? entries) =>
        (entries ?? Array.Empty<ProjectScopeEntry>())
            .Where(entry => entry is not null && !string.IsNullOrWhiteSpace(entry.RelativePath))
            .Select(entry => new ProjectScopeConfigEntry
            {
                RelativePath = NormalizeRelativePath(entry.RelativePath),
                IsDirectory = entry.IsDirectory
            })
            .Where(entry => entry.RelativePath.Length > 0)
            .DistinctBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static List<ProjectScopeConfigEntry> NormalizeScopeConfigEntries(IEnumerable<ProjectScopeConfigEntry>? entries) =>
        (entries ?? Array.Empty<ProjectScopeConfigEntry>())
            .Where(entry => entry is not null && !string.IsNullOrWhiteSpace(entry.RelativePath))
            .Select(entry => new ProjectScopeConfigEntry
            {
                RelativePath = NormalizeRelativePath(entry.RelativePath),
                IsDirectory = entry.IsDirectory
            })
            .Where(entry => entry.RelativePath.Length > 0)
            .DistinctBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static List<string> NormalizeCommands(IEnumerable<string>? commands) =>
        (commands ?? Array.Empty<string>())
            .Where(command => command is not null)
            .Select(command => command.Trim())
            .Where(command => command.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string NormalizeDisplayName(string? value, string path) =>
        string.IsNullOrWhiteSpace(value) ? GetDisplayName(path) : value.Trim();

    private static string NormalizeRelativePath(string path) =>
        path.Replace('\\', '/').Trim().TrimStart('/').TrimEnd('/');

    private static string NormalizePath(string path)
    {
        try { return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)); }
        catch { return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
    }

    private static bool PathEquals(string left, string right) =>
        string.Equals(NormalizePath(left), NormalizePath(right), StringComparison.OrdinalIgnoreCase);

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
