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
    public DateTimeOffset FirstTurnAt { get; set; }
    public DateTimeOffset? FirstRepoContactAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public string BirthCommit { get; set; } = "";
    public string? DeathAnchorCommit { get; set; }
    public string? AgentBirthId { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ChatSessionOutcome Outcome { get; set; } = ChatSessionOutcome.Active;
    public int UserTurns { get; set; }
    public int AssistantTurns { get; set; }
    public int TotalTurns { get; set; }
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

public sealed class ChatVitalityAgentEnvelope
{
    [JsonPropertyName("protocol")]
    public string Protocol { get; set; } = "";

    [JsonPropertyName("event")]
    public string Event { get; set; } = "";

    [JsonPropertyName("birth_id")]
    public string BirthId { get; set; } = "";

    [JsonPropertyName("repository")]
    public string Repository { get; set; } = "";

    [JsonPropertyName("generation")]
    public int? Generation { get; set; }

    [JsonPropertyName("first_turn_at")]
    public DateTimeOffset? FirstTurnAt { get; set; }

    [JsonPropertyName("birth_commit")]
    public string? BirthCommit { get; set; }

    [JsonPropertyName("user_turns")]
    public int UserTurns { get; set; } = 1;

    [JsonPropertyName("assistant_turns")]
    public int AssistantTurns { get; set; }

    [JsonPropertyName("total_turns")]
    public int TotalTurns { get; set; }

    [JsonPropertyName("previous_outcome")]
    public string? PreviousOutcome { get; set; }

    [JsonPropertyName("previous_death_anchor")]
    public string? PreviousDeathAnchor { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

public static class ChatVitalityAgentProtocol
{
    public const string ProtocolName = "zgitpet-chat-vitality-v1";
    public const string BirthEvent = "CHAT_BIRTH";
    public static readonly string RelativeEnvelopePath =
        Path.Combine(".zgitpet", "chat-vitality-agent.json");

    private static readonly JsonSerializerOptions EnvelopeJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static ChatVitalityAgentEnvelope? TryRead(string repositoryPath)
    {
        try
        {
            var path = Path.Combine(repositoryPath, RelativeEnvelopePath);
            if (!File.Exists(path)) return null;
            var envelope = JsonSerializer.Deserialize<ChatVitalityAgentEnvelope>(
                File.ReadAllText(path), EnvelopeJsonOptions);
            if (envelope is null) return null;
            if (!string.Equals(envelope.Protocol, ProtocolName, StringComparison.Ordinal)) return null;
            if (!string.Equals(envelope.Event, BirthEvent, StringComparison.OrdinalIgnoreCase)) return null;
            if (string.IsNullOrWhiteSpace(envelope.BirthId)) return null;
            return envelope;
        }
        catch
        {
            return null;
        }
    }
}

public static class ChatVitalityRisk
{
    public static int Calculate(ChatVitalitySession session, int interactions, DateTimeOffset now)
    {
        var minutes = Math.Max(0, (int)(now - session.StartedAt).TotalMinutes);
        var churn = Math.Max(0L, session.Insertions + session.Deletions);
        var turns = Math.Max(0, session.TotalTurns);
        var score =
            Math.Min(20, minutes / 30) +
            Math.Min(20, turns / 6) +
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
    public int SchemaVersion { get; set; } = 2;
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

            CloseActiveSessions(
                document,
                repoSessions,
                previousOutcome,
                previousLastCommit,
                currentHead,
                timestamp);

            var session = new ChatVitalitySession
            {
                RepoKey = repoKey,
                RepositoryPath = NormalizePath(repositoryPath),
                Generation = generation,
                StartedAt = timestamp,
                FirstTurnAt = timestamp,
                FirstRepoContactAt = timestamp,
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

    public ChatVitalitySnapshot? ImportAgentBirthIfPresent(
        string repositoryPath,
        DateTimeOffset? now = null)
    {
        var envelope = ChatVitalityAgentProtocol.TryRead(repositoryPath);
        if (envelope is null) return null;

        lock (_gate)
        {
            var timestamp = now ?? DateTimeOffset.UtcNow;
            var document = Load();
            var repoKey = CreateRepoKey(repositoryPath);
            var birthId = envelope.BirthId.Trim();
            var existing = document.Sessions
                .Where(x => x.RepoKey == repoKey &&
                    string.Equals(x.AgentBirthId, birthId, StringComparison.Ordinal))
                .OrderByDescending(x => x.Generation)
                .FirstOrDefault();

            if (existing is not null)
            {
                var changed = ApplyAgentTurnCounts(existing, envelope);
                if (existing.FirstRepoContactAt is null)
                {
                    existing.FirstRepoContactAt = timestamp;
                    changed = true;
                }
                var birthCommit = CleanSha(envelope.BirthCommit);
                if (birthCommit is not null &&
                    (string.IsNullOrWhiteSpace(existing.BirthCommit) || existing.BirthCommit == "unknown"))
                {
                    existing.BirthCommit = birthCommit;
                    changed = true;
                }
                UpdateDerived(existing, CountEvents(document, existing.Id), timestamp);
                if (changed) Save(document);
                return Snapshot(document, existing, timestamp);
            }

            var repoSessions = document.Sessions.Where(x => x.RepoKey == repoKey).ToList();
            var highest = repoSessions.Count == 0 ? 0 : repoSessions.Max(x => x.Generation);
            var requestedGeneration = envelope.Generation.GetValueOrDefault();
            var generation = requestedGeneration > highest ? requestedGeneration : highest + 1;
            var startedAt = envelope.FirstTurnAt ?? timestamp;
            var birthCommitAnchor = CleanSha(envelope.BirthCommit) ?? "unknown";
            var previousOutcome = ParseOutcome(envelope.PreviousOutcome);

            CloseActiveSessions(
                document,
                repoSessions,
                previousOutcome,
                envelope.PreviousDeathAnchor,
                birthCommitAnchor,
                startedAt);

            var session = new ChatVitalitySession
            {
                RepoKey = repoKey,
                RepositoryPath = NormalizePath(repositoryPath),
                Generation = generation,
                StartedAt = startedAt,
                FirstTurnAt = startedAt,
                FirstRepoContactAt = timestamp,
                BirthCommit = birthCommitAnchor,
                AgentBirthId = birthId,
                Outcome = ChatSessionOutcome.Active,
                Notes = envelope.Notes?.Trim() ?? ""
            };
            ApplyAgentTurnCounts(session, envelope);
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
        ImportAgentBirthIfPresent(repositoryPath, now);

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
        ImportAgentBirthIfPresent(repositoryPath, now);

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
        ImportAgentBirthIfPresent(repositoryPath, now);

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

    private static ChatSessionOutcome? ParseOutcome(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim().Replace("-", "").Replace("_", "");
        return normalized.ToUpperInvariant() switch
        {
            "CLEANHANDOFF" => ChatSessionOutcome.CleanHandoff,
            "HARDHANG" => ChatSessionOutcome.HardHang,
            "MAXLENGTH" => ChatSessionOutcome.MaxLength,
            "ABANDONED" => ChatSessionOutcome.Abandoned,
            _ => null
        };
    }

    private static bool ApplyAgentTurnCounts(
        ChatVitalitySession session,
        ChatVitalityAgentEnvelope envelope)
    {
        var userTurns = Math.Max(1, envelope.UserTurns);
        var assistantTurns = Math.Max(0, envelope.AssistantTurns);
        var totalTurns = Math.Max(
            Math.Max(1, envelope.TotalTurns),
            userTurns + assistantTurns);
        var changed = false;
        if (userTurns > session.UserTurns) { session.UserTurns = userTurns; changed = true; }
        if (assistantTurns > session.AssistantTurns) { session.AssistantTurns = assistantTurns; changed = true; }
        if (totalTurns > session.TotalTurns) { session.TotalTurns = totalTurns; changed = true; }
        return changed;
    }

    private static void CloseActiveSessions(
        ChatVitalityDocument document,
        IEnumerable<ChatVitalitySession> repoSessions,
        ChatSessionOutcome? previousOutcome,
        string? previousLastCommit,
        string currentHead,
        DateTimeOffset timestamp)
    {
        foreach (var active in repoSessions.Where(x => x.Outcome == ChatSessionOutcome.Active))
        {
            active.Outcome = previousOutcome is null or ChatSessionOutcome.Active
                ? ChatSessionOutcome.Abandoned
                : previousOutcome.Value;
            active.EndedAt = timestamp;
            active.DeathAnchorCommit = CleanSha(previousLastCommit) ?? CleanSha(currentHead);
            UpdateDerived(active, CountEvents(document, active.Id), timestamp);
        }
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
            var document = JsonSerializer.Deserialize<ChatVitalityDocument>(
                File.ReadAllText(_storePath), JsonOptions) ?? new ChatVitalityDocument();
            if (document.SchemaVersion < 2)
            {
                foreach (var session in document.Sessions)
                {
                    if (session.FirstTurnAt == default) session.FirstTurnAt = session.StartedAt;
                    session.TotalTurns = Math.Max(session.TotalTurns, session.UserTurns + session.AssistantTurns);
                }
                document.SchemaVersion = 2;
            }
            return document;
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
