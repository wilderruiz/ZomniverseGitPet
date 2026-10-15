using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZomniverseGitPet;

public enum ChatSessionOutcome
{
    Active,
    CleanHandoff,
    HardHang,
    MaxLength,
    Abandoned
}

public enum ChatVitalityEventType
{
    Get,
    Save,
    Send,
    Reconcile
}

public sealed class ChatVitalitySession
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string RepoKey { get; set; } = "";
    public string RepositoryPath { get; set; } = "";
    public int Generation { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public string BirthCommit { get; set; } = "";
    public string? DeathAnchorCommit { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ChatSessionOutcome Outcome { get; set; } = ChatSessionOutcome.Active;
    public int Gets { get; set; }
    public int Saves { get; set; }
    public int Sends { get; set; }
    public int Reconciles { get; set; }
    public int CommitsSeen { get; set; }
    public int FilesChanged { get; set; }
    public long Insertions { get; set; }
    public long Deletions { get; set; }
    public int SyncCycles { get; set; }
    public int ElapsedMinutes { get; set; }
    public int RiskScore { get; set; }
    public string Notes { get; set; } = "";
}

public sealed class ChatVitalityEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string SessionId { get; set; } = "";
    public DateTimeOffset Timestamp { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ChatVitalityEventType Type { get; set; }
    public string CommitSha { get; set; } = "";
    public int Value { get; set; } = 1;
    public string MetadataJson { get; set; } = "{}";
}

public sealed record ChatVitalitySnapshot(
    ChatVitalitySession Session,
    int InteractionCount,
    string LifecycleState,
    string RiskClass,
    string Recommendation,
    string CurrentAnchorCommit);

public static class ChatVitalityRisk
{
    public static int Calculate(ChatVitalitySession session, int interactions, DateTimeOffset now)
    {
        var minutes = Math.Max(0, (int)(now - session.StartedAt).TotalMinutes);
        var churn = Math.Max(0L, session.Insertions + session.Deletions);
        var score =
            Math.Min(20, minutes / 30) +
            Math.Min(20, interactions * 2) +
            Math.Min(20, session.Saves * 4) +
            Math.Min(12, session.Gets * 3) +
            Math.Min(12, session.Sends * 3) +
            Math.Min(18, session.Reconciles * 6) +
            Math.Min(10, session.FilesChanged / 10) +
            Math.Min(15, (int)(churn / 500)) +
            Math.Min(15, session.CommitsSeen * 3) +
            Math.Min(10, session.SyncCycles * 2);
        return Math.Clamp(score, 0, 100);
    }

    public static string Classify(int score) => score switch
    {
        <= 29 => "LOW",
        <= 49 => "MODERATE",
        <= 69 => "ELEVATED",
        <= 84 => "HIGH",
        _ => "HANDOFF RECOMMENDED"
    };

    public static string Recommendation(int score) => score switch
    {
        >= 85 => "Create a clean checkpoint and hand off now",
        >= 70 => "Recommend handoff after next clean checkpoint",
        _ => ""
    };
}

internal sealed class ChatVitalityDocument
{
    public int SchemaVersion { get; set; } = 1;
    public List<ChatVitalitySession> Sessions { get; set; } = [];
    public List<ChatVitalityEvent> Events { get; set; } = [];
}

public sealed class ChatVitalityStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
    private readonly object _gate = new();
    private readonly string _storePath;

    public ChatVitalityStore(string? storePath = null)
    {
        _storePath = storePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ZomniverseGitPet", "chat-vitality.json");
    }

    public ChatVitalitySnapshot RegisterBirth(
        string repositoryPath,
        string currentHead,
        int? requestedGeneration = null,
        ChatSessionOutcome? previousOutcome = null,
        string? previousLastCommit = null,
        string? notes = null,
        DateTimeOffset? now = null)
    {
        lock (_gate)
        {
            var timestamp = now ?? DateTimeOffset.UtcNow;
            var document = Load();
            var repoKey = CreateRepoKey(repositoryPath);
            var repoSessions = document.Sessions.Where(x => x.RepoKey == repoKey).ToList();
            var highest = repoSessions.Count == 0 ? 0 : repoSessions.Max(x => x.Generation);
            var generation = requestedGeneration ?? highest + 1;
            if (generation <= highest)
                throw new InvalidOperationException($"Generation must be greater than {highest} for this repository.");

            foreach (var active in repoSessions.Where(x => x.Outcome == ChatSessionOutcome.Active))
            {
                active.Outcome = previousOutcome is null or ChatSessionOutcome.Active
                    ? ChatSessionOutcome.Abandoned
                    : previousOutcome.Value;
                active.EndedAt = timestamp;
                active.DeathAnchorCommit = CleanSha(previousLastCommit) ?? CleanSha(currentHead);
                UpdateDerived(active, CountEvents(document, active.Id), timestamp);
            }

            var session = new ChatVitalitySession
            {
                RepoKey = repoKey,
                RepositoryPath = NormalizePath(repositoryPath),
                Generation = generation,
                StartedAt = timestamp,
                BirthCommit = CleanSha(currentHead) ?? "unknown",
                Outcome = ChatSessionOutcome.Active,
                Notes = notes?.Trim() ?? ""
            };
            document.Sessions.Add(session);
            UpdateDerived(session, 0, timestamp);
            Save(document);
            return Snapshot(document, session, timestamp);
        }
    }

    public ChatVitalitySnapshot? RecordEvent(
        string repositoryPath,
        ChatVitalityEventType type,
        string commitSha,
        int filesChanged = 0,
        long insertions = 0,
        long deletions = 0,
        string? metadataJson = null,
        DateTimeOffset? now = null)
    {
        lock (_gate)
        {
            var timestamp = now ?? DateTimeOffset.UtcNow;
            var document = Load();
            var repoKey = CreateRepoKey(repositoryPath);
            var session = document.Sessions
                .Where(x => x.RepoKey == repoKey && x.Outcome == ChatSessionOutcome.Active)
                .OrderByDescending(x => x.Generation)
                .FirstOrDefault();
            if (session is null) return null;

            switch (type)
            {
                case ChatVitalityEventType.Get: session.Gets++; session.SyncCycles++; break;
                case ChatVitalityEventType.Save: session.Saves++; session.CommitsSeen++; break;
                case ChatVitalityEventType.Send: session.Sends++; session.SyncCycles++; break;
                case ChatVitalityEventType.Reconcile: session.Reconciles++; session.SyncCycles++; break;
            }
            session.FilesChanged += Math.Max(0, filesChanged);
            session.Insertions += Math.Max(0, insertions);
            session.Deletions += Math.Max(0, deletions);
            document.Events.Add(new ChatVitalityEvent
            {
                SessionId = session.Id,
                Timestamp = timestamp,
                Type = type,
                CommitSha = CleanSha(commitSha) ?? "unknown",
                MetadataJson = string.IsNullOrWhiteSpace(metadataJson) ? "{}" : metadataJson
            });
            var interactions = CountEvents(document, session.Id);
            UpdateDerived(session, interactions, timestamp);
            Save(document);
            return Snapshot(document, session, timestamp);
        }
    }

    public ChatVitalitySnapshot? GetActive(string repositoryPath, DateTimeOffset? now = null)
    {
        lock (_gate)
        {
            var timestamp = now ?? DateTimeOffset.UtcNow;
            var document = Load();
            var repoKey = CreateRepoKey(repositoryPath);
            var session = document.Sessions
                .Where(x => x.RepoKey == repoKey && x.Outcome == ChatSessionOutcome.Active)
                .OrderByDescending(x => x.Generation)
                .FirstOrDefault();
            if (session is null) return null;
            UpdateDerived(session, CountEvents(document, session.Id), timestamp);
            return Snapshot(document, session, timestamp);
        }
    }

    public IReadOnlyList<ChatVitalitySnapshot> GetHistory(string repositoryPath, DateTimeOffset? now = null)
    {
        lock (_gate)
        {
            var timestamp = now ?? DateTimeOffset.UtcNow;
            var document = Load();
            var repoKey = CreateRepoKey(repositoryPath);
            return document.Sessions.Where(x => x.RepoKey == repoKey)
                .OrderByDescending(x => x.Generation)
                .Select(x =>
                {
                    UpdateDerived(x, CountEvents(document, x.Id), x.EndedAt ?? timestamp);
                    return Snapshot(document, x, timestamp);
                })
                .ToArray();
        }
    }

    public static string CreateRepoKey(string repositoryPath)
    {
        var normalized = NormalizePath(repositoryPath).ToUpperInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
    }

    private static string NormalizePath(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Trim()));

    private static string? CleanSha(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static int CountEvents(ChatVitalityDocument document, string sessionId) =>
        document.Events.Count(x => x.SessionId == sessionId);

    private static void UpdateDerived(ChatVitalitySession session, int interactions, DateTimeOffset now)
    {
        session.ElapsedMinutes = Math.Max(0, (int)(now - session.StartedAt).TotalMinutes);
        session.RiskScore = ChatVitalityRisk.Calculate(session, interactions, now);
    }

    private static ChatVitalitySnapshot Snapshot(
        ChatVitalityDocument document,
        ChatVitalitySession session,
        DateTimeOffset now)
    {
        var interactions = CountEvents(document, session.Id);
        var lifecycle = session.Outcome != ChatSessionOutcome.Active
            ? "CLOSED"
            : interactions == 0 ? "NEWBORN" : "ACTIVE";
        var currentAnchor = document.Events
            .Where(x => x.SessionId == session.Id)
            .OrderByDescending(x => x.Timestamp)
            .Select(x => x.CommitSha)
            .FirstOrDefault() ?? session.BirthCommit;
        return new ChatVitalitySnapshot(
            session,
            interactions,
            lifecycle,
            ChatVitalityRisk.Classify(session.RiskScore),
            ChatVitalityRisk.Recommendation(session.RiskScore),
            currentAnchor);
    }

    private ChatVitalityDocument Load()
    {
        if (!File.Exists(_storePath)) return new ChatVitalityDocument();
        try
        {
            return JsonSerializer.Deserialize<ChatVitalityDocument>(File.ReadAllText(_storePath), JsonOptions)
                ?? new ChatVitalityDocument();
        }
        catch
        {
            return new ChatVitalityDocument();
        }
    }

    private void Save(ChatVitalityDocument document)
    {
        var directory = Path.GetDirectoryName(_storePath)!;
        Directory.CreateDirectory(directory);
        var temporary = _storePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(document, JsonOptions));
        File.Move(temporary, _storePath, true);
    }
}
