using System.Text.Json;

namespace ZomniverseGitPet;

public sealed class AppConfig
{
    public int SchemaVersion { get; set; } = 1;
    public string? RepositoryPath { get; set; }
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
            return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(AppPaths.ConfigFile), JsonOptions)
                ?? new AppConfig();
        }
        catch (Exception ex)
        {
            throw new InvalidDataException($"Configuration could not be read: {ex.Message}", ex);
        }
    }

    public void Save(AppConfig config)
    {
        Directory.CreateDirectory(AppPaths.Root);
        var temporary = AppPaths.ConfigFile + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(config, JsonOptions));
        File.Move(temporary, AppPaths.ConfigFile, true);
    }
}

