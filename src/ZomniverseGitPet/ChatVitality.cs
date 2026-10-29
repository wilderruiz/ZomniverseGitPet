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
    public string RepositoryIdentity { get; set; } = "";
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
    string CurrentAnchorCommit,
    ChatLifespanAssessment Lifespan);

public sealed class ChatLifespanObservation
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string? SessionId { get; set; }
    public string RepoKey { get; set; } = "";
    public string RepositoryIdentity { get; set; } = "";
    public double Hours { get; set; }
    public string Source { get; set; } = "observed_birth_interval";
}

public sealed record ChatLifespanStatistics(
    int CompletedChats,
    double? ShortestHours,
    double? LongestHours,
    double? MeanHours,
    double? MedianHours);

public sealed record ChatActivityPressure(
    string Level,
    bool Warning,
    IReadOnlyList<string> ObservedSignals,
    IReadOnlyList<string> UnobservedSignals);

public sealed record ChatLifespanAssessment(
    DateTimeOffset BirthTimestamp,
    double CurrentAgeHours,
    string EmpiricalBand,
    string GuidanceBasis,
    ChatLifespanStatistics RepositoryStatistics,
    ChatLifespanStatistics GlobalStatistics,
    ChatActivityPressure ActivityPressure,
    string NondeterministicStatement);

public static class ChatLifespanModel
{
    public const string GuidanceBasis =
        "Provisional bands seeded from completed wilderruiz/millenova observations; evolve with cross-repository evidence.";
    public const string NondeterministicStatement =
        "Chat death is nondeterministic. Age and observable activity indicate handoff risk, not remaining context or a death deadline.";
    public static readonly IReadOnlyList<(string RepositoryIdentity, string Source, double Hours)> InitialSeedObservations =
        new[] { 32.3, 58.0, 61.1, 61.4, 17.2, 23.1, 10.6, 45.3 }
            .Select(hours => (
                RepositoryIdentity: "wilderruiz/millenova",
                Source: "millenova_initial_empirical_dataset_2026_10_29",
                Hours: hours))
            .ToArray();

    public static string Band(double hours) => hours switch
    {
        < 10 => "GREEN",
        < 24 => "YELLOW",
        < 40 => "ORANGE",
        < 55 => "RED",
        _ => "CRITICAL"
    };

    public static ChatLifespanStatistics Statistics(IEnumerable<double> values)
    {
        var ordered = values.Where(x => x >= 0 && double.IsFinite(x)).Order().ToArray();
        if (ordered.Length == 0) return new(0, null, null, null, null);
        var middle = ordered.Length / 2;
        var median = ordered.Length % 2 == 0
            ? (ordered[middle - 1] + ordered[middle]) / 2
            : ordered[middle];
        return new(
            ordered.Length,
            Math.Round(ordered[0], 1),
            Math.Round(ordered[^1], 1),
            Math.Round(ordered.Average(), 1),
            Math.Round(median, 1));
    }

    public static ChatActivityPressure ActivityPressure(ChatVitalitySession session)
    {
        var observed = new List<string>();
        if (session.TotalTurns > 0) observed.Add($"conversation_turns={session.TotalTurns}");
        if (session.Gets > 0) observed.Add($"remote_gets={session.Gets}");
        if (session.FilesChanged > 0) observed.Add($"incoming_files={session.FilesChanged}");
        var churn = Math.Max(0L, session.Insertions + session.Deletions);
        if (churn > 0) observed.Add($"incoming_line_churn={churn}");

        var score = Math.Min(4, session.TotalTurns / 20) +
                    Math.Min(4, session.Gets) +
                    Math.Min(4, session.FilesChanged / 25) +
                    Math.Min(4, (int)(churn / 1000));
        var level = score >= 9 ? "HIGH" : score >= 4 ? "ELEVATED" : "NORMAL";
        return new(
            level,
            level != "NORMAL",
            observed,
            ["screenshots", "large_message_dumps", "debugging_loops", "architecture_pivots"]);
    }
}

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
    public static int Calculate(ChatVitalitySession session, int remoteInteractions, DateTimeOffset now)
    {
        var minutes = Math.Max(0, (int)(now - session.StartedAt).TotalMinutes);
        var churn = Math.Max(0L, session.Insertions + session.Deletions);
        var turns = Math.Max(0, session.TotalTurns);

        /*
         * Owner rule: local GitPet mechanics are not remote-chat interactions.
         * Save, Send and Reconcile remain useful historical counters, but they
         * do not increase chat load. Only remote work arriving through Get,
         * plus conversation age/turns and the size of that incoming work,
         * contributes to the vitality heuristic.
         */
        var score =
            Math.Min(20, minutes / 30) +
            Math.Min(20, turns / 6) +
            Math.Min(20, remoteInteractions * 2) +
            Math.Min(18, session.Gets * 3) +
            Math.Min(10, session.FilesChanged / 10) +
            Math.Min(15, (int)(churn / 500));
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
    public int SchemaVersion { get; set; } = 3;
    public List<ChatVitalitySession> Sessions { get; set; } = [];
    public List<ChatVitalityEvent> Events { get; set; } = [];
    public List<ChatLifespanObservation> LifespanObservations { get; set; } = [];
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
                RepositoryIdentity = Path.GetFileName(NormalizePath(repositoryPath)),
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
                var repositoryIdentity = NormalizeRepositoryIdentity(envelope.Repository, repositoryPath);
                if (!string.Equals(existing.RepositoryIdentity, repositoryIdentity, StringComparison.OrdinalIgnoreCase))
                {
                    existing.RepositoryIdentity = repositoryIdentity;
                    changed = true;
                }
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
                UpdateDerived(existing, CountRemoteInteractions(document, existing.Id), timestamp);
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
                RepositoryIdentity = NormalizeRepositoryIdentity(envelope.Repository, repositoryPath),
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
                case ChatVitalityEventType.Get:
                    session.Gets++;
                    session.SyncCycles++;
                    session.FilesChanged += Math.Max(0, filesChanged);
                    session.Insertions += Math.Max(0, insertions);
                    session.Deletions += Math.Max(0, deletions);
                    break;
                case ChatVitalityEventType.Save:
                    session.Saves++;
                    session.CommitsSeen++;
                    break;
                case ChatVitalityEventType.Send:
                    session.Sends++;
                    session.SyncCycles++;
                    break;
                case ChatVitalityEventType.Reconcile:
                    session.Reconciles++;
                    session.SyncCycles++;
                    break;
            }

            document.Events.Add(new ChatVitalityEvent
            {
                SessionId = session.Id,
                Timestamp = timestamp,
                Type = type,
                CommitSha = CleanSha(commitSha) ?? "unknown",
                MetadataJson = string.IsNullOrWhiteSpace(metadataJson) ? "{}" : metadataJson
            });
            var remoteInteractions = CountRemoteInteractions(document, session.Id);
            UpdateDerived(session, remoteInteractions, timestamp);
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
            UpdateDerived(session, CountRemoteInteractions(document, session.Id), timestamp);
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
                    UpdateDerived(x, CountRemoteInteractions(document, x.Id), x.EndedAt ?? timestamp);
                    return Snapshot(document, x, timestamp);
                })
                .ToArray();
        }
    }

    public ChatLifespanStatistics GetRepositoryStatistics(string repositoryPath)
    {
        lock (_gate)
        {
            var document = Load();
            var repoKey = CreateRepoKey(repositoryPath);
            var identity = document.Sessions
                .Where(x => x.RepoKey == repoKey && !string.IsNullOrWhiteSpace(x.RepositoryIdentity))
                .OrderByDescending(x => x.Generation)
                .Select(x => x.RepositoryIdentity)
                .FirstOrDefault() ?? Path.GetFileName(NormalizePath(repositoryPath));
            return ChatLifespanModel.Statistics(RepositoryObservations(document, repoKey, identity).Select(x => x.Hours));
        }
    }

    public ChatLifespanStatistics GetGlobalStatistics()
    {
        lock (_gate)
        {
            var document = Load();
            return ChatLifespanModel.Statistics(document.LifespanObservations.Select(x => x.Hours));
        }
    }

    public static string CreateRepoKey(string repositoryPath)
    {
        var normalized = NormalizePath(repositoryPath).ToUpperInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
    }

    private static string NormalizePath(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Trim()));

    private static string NormalizeRepositoryIdentity(string? identity, string repositoryPath)
    {
        var value = string.IsNullOrWhiteSpace(identity)
            ? Path.GetFileName(NormalizePath(repositoryPath))
            : identity.Trim().Replace('\\', '/').Trim('/');
        return value.ToLowerInvariant();
    }

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
            UpdateDerived(active, CountRemoteInteractions(document, active.Id), timestamp);
            if (!document.LifespanObservations.Any(x => x.SessionId == active.Id))
            {
                document.LifespanObservations.Add(new ChatLifespanObservation
                {
                    SessionId = active.Id,
                    RepoKey = active.RepoKey,
                    RepositoryIdentity = active.RepositoryIdentity,
                    Hours = Math.Round(Math.Max(0, (timestamp - active.StartedAt).TotalHours), 3)
                });
            }
        }
    }

    private static int CountRemoteInteractions(ChatVitalityDocument document, string sessionId) =>
        document.Events.Count(x =>
            x.SessionId == sessionId && x.Type == ChatVitalityEventType.Get);

    private static void UpdateDerived(ChatVitalitySession session, int remoteInteractions, DateTimeOffset now)
    {
        session.ElapsedMinutes = Math.Max(0, (int)(now - session.StartedAt).TotalMinutes);
        session.RiskScore = ChatVitalityRisk.Calculate(session, remoteInteractions, now);
    }

    private static ChatVitalitySnapshot Snapshot(
        ChatVitalityDocument document,
        ChatVitalitySession session,
        DateTimeOffset now)
    {
        var remoteInteractions = CountRemoteInteractions(document, session.Id);
        var lifecycle = session.Outcome != ChatSessionOutcome.Active
            ? "CLOSED"
            : remoteInteractions == 0 ? "NEWBORN" : "ACTIVE";
        var currentAnchor = document.Events
            .Where(x => x.SessionId == session.Id)
            .OrderByDescending(x => x.Timestamp)
            .Select(x => x.CommitSha)
            .FirstOrDefault() ?? session.BirthCommit;
        var identity = session.RepositoryIdentity;
        var repositoryStatistics = ChatLifespanModel.Statistics(
            RepositoryObservations(document, session.RepoKey, identity).Select(x => x.Hours));
        var globalStatistics = ChatLifespanModel.Statistics(
            document.LifespanObservations.Select(x => x.Hours));
        var ageHours = Math.Round(Math.Max(0, (now - session.StartedAt).TotalHours), 1);
        return new ChatVitalitySnapshot(
            session,
            remoteInteractions,
            lifecycle,
            ChatVitalityRisk.Classify(session.RiskScore),
            ChatVitalityRisk.Recommendation(session.RiskScore),
            currentAnchor,
            new ChatLifespanAssessment(
                session.FirstTurnAt,
                ageHours,
                ChatLifespanModel.Band(ageHours),
                ChatLifespanModel.GuidanceBasis,
                repositoryStatistics,
                globalStatistics,
                ChatLifespanModel.ActivityPressure(session),
                ChatLifespanModel.NondeterministicStatement));
    }

    private static IEnumerable<ChatLifespanObservation> RepositoryObservations(
        ChatVitalityDocument document,
        string repoKey,
        string repositoryIdentity)
    {
        var normalized = repositoryIdentity.Trim().Replace('\\', '/').Trim('/').ToLowerInvariant();
        var leaf = normalized.Split('/').LastOrDefault() ?? normalized;
        return document.LifespanObservations.Where(x =>
            x.RepoKey == repoKey ||
            string.Equals(x.RepositoryIdentity, normalized, StringComparison.OrdinalIgnoreCase) ||
            (normalized == leaf && x.RepositoryIdentity.EndsWith('/' + leaf, StringComparison.OrdinalIgnoreCase)));
    }

    private ChatVitalityDocument Load()
    {
        if (!File.Exists(_storePath))
        {
            var fresh = new ChatVitalityDocument();
            EnsureSeedObservations(fresh);
            return fresh;
        }
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
            if (document.SchemaVersion < 3)
            {
                foreach (var session in document.Sessions)
                {
                    if (string.IsNullOrWhiteSpace(session.RepositoryIdentity))
                        session.RepositoryIdentity = Path.GetFileName(session.RepositoryPath).ToLowerInvariant();
                }
                document.SchemaVersion = 3;
            }
            EnsureSeedObservations(document);
            return document;
        }
        catch
        {
            var fresh = new ChatVitalityDocument();
            EnsureSeedObservations(fresh);
            return fresh;
        }
    }

    private static void EnsureSeedObservations(ChatVitalityDocument document)
    {
        foreach (var seed in ChatLifespanModel.InitialSeedObservations)
        {
            if (document.LifespanObservations.Any(x =>
                    x.Source == seed.Source &&
                    string.Equals(x.RepositoryIdentity, seed.RepositoryIdentity, StringComparison.OrdinalIgnoreCase) &&
                    x.Hours == seed.Hours)) continue;
            document.LifespanObservations.Add(new ChatLifespanObservation
            {
                RepositoryIdentity = seed.RepositoryIdentity,
                Hours = seed.Hours,
                Source = seed.Source
            });
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
