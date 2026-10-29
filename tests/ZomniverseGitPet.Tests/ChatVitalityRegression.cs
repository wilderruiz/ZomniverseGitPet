using System.Text.Json;
using ZomniverseGitPet;

internal static class ChatVitalityRegression
{
    public static Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "ZomniverseGitPet.ChatVitality", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var storePath = Path.Combine(root, "chat-vitality.json");
        var repoA = Path.Combine(root, "RepoA");
        var repoB = Path.Combine(root, "RepoB");
        Directory.CreateDirectory(repoA);
        Directory.CreateDirectory(repoB);

        try
        {
            var store = new ChatVitalityStore(storePath);
            var bornAt = new DateTimeOffset(2026, 10, 15, 8, 0, 0, TimeSpan.Zero);
            var first = store.RegisterBirth(repoA, "aaaaaaaaaaaaaaaa", now: bornAt);
            Require(first.Session.Generation == 1, "First generation should be 1.");
            Require(first.LifecycleState == "NEWBORN", "A birth without remote Gets should be NEWBORN.");
            Require(first.Session.BirthCommit == "aaaaaaaaaaaaaaaa", "Birth anchor was not preserved.");
            Require(first.Session.FirstTurnAt == bornAt && first.Session.FirstRepoContactAt == bornAt,
                "Manual birth should preserve its explicit birth/contact timestamp.");

            var beforeLocalOpsScore = first.Session.RiskScore;
            var afterSave = store.RecordEvent(repoA, ChatVitalityEventType.Save, "bbbbbbbbbbbbbbbb",
                filesChanged: 4, insertions: 120, deletions: 30, now: bornAt);
            Require(afterSave is not null && afterSave.LifecycleState == "NEWBORN",
                "A local Save must not activate the remote chat generation.");
            Require(afterSave!.InteractionCount == 0 && afterSave.Session.Saves == 1 && afterSave.Session.CommitsSeen == 1,
                "Save should remain a historical local counter, not a remote interaction.");
            Require(afterSave.Session.FilesChanged == 0 && afterSave.Session.Insertions == 0 && afterSave.Session.Deletions == 0,
                "Local Save churn must not be accumulated into remote-chat workload.");
            Require(afterSave.Session.RiskScore == beforeLocalOpsScore,
                "Local Save must not increase chat load.");

            var afterSend = store.RecordEvent(repoA, ChatVitalityEventType.Send, "bbbbbbbbbbbbbbbb", now: bornAt);
            var afterReconcile = store.RecordEvent(repoA, ChatVitalityEventType.Reconcile, "bbbbbbbbbbbbbbbb", now: bornAt);
            Require(afterSend is not null && afterReconcile is not null &&
                    afterSend.InteractionCount == 0 && afterReconcile.InteractionCount == 0,
                "Send/Reconcile must not count as remote chat interactions.");
            Require(afterReconcile!.Session.RiskScore == beforeLocalOpsScore,
                "Send/Reconcile must not increase chat load.");

            var active = store.RecordEvent(repoA, ChatVitalityEventType.Get, "cccccccccccccccc",
                filesChanged: 4, insertions: 120, deletions: 30, now: bornAt.AddMinutes(40));
            Require(active is not null && active.LifecycleState == "ACTIVE",
                "First remote Get should activate the newborn session.");
            Require(active!.InteractionCount == 1 && active.Session.Gets == 1,
                "Get should be the remote interaction counter.");
            Require(active.Session.FilesChanged == 4 && active.Session.Insertions == 120 && active.Session.Deletions == 30,
                "Incoming Get change metrics were not accumulated.");
            Require(active.CurrentAnchorCommit == "cccccccccccccccc",
                "Current anchor should advance to the latest observable event commit.");

            var beforeHandoff = store.GetActive(repoA, bornAt.AddMinutes(45));
            Require(beforeHandoff is not null && beforeHandoff.InteractionCount == 1,
                "Only Get should contribute to remote interaction count.");
            Require(beforeHandoff!.Session.SyncCycles == 3,
                "Sync-cycle history may still include Get, Send, and Reconcile.");

            var second = store.RegisterBirth(
                repoA, "eeeeeeeeeeeeeeee", previousOutcome: ChatSessionOutcome.MaxLength,
                previousLastCommit: "dddddddddddddddd", now: bornAt.AddHours(2));
            Require(second.Session.Generation == 2 && second.LifecycleState == "NEWBORN",
                "Second birth should increment generation and start NEWBORN.");
            var history = store.GetHistory(repoA, bornAt.AddHours(2));
            var closed = history.Single(x => x.Session.Generation == 1);
            Require(closed.Session.Outcome == ChatSessionOutcome.MaxLength,
                "Explicit previous outcome was not preserved.");
            Require(closed.Session.DeathAnchorCommit == "dddddddddddddddd",
                "Explicit death anchor was not preserved.");
            var repoAStats = store.GetRepositoryStatistics(repoA);
            Require(repoAStats.CompletedChats == 1 && repoAStats.MeanHours == 2.0,
                "A new birth must record the prior interval only for that repository.");

            var independent = store.RegisterBirth(repoB, "1111111111111111", now: bornAt);
            Require(independent.Session.Generation == 1, "Generations must be independent per repository.");
            Require(store.GetActive(repoA, bornAt)?.Session.Generation == 2,
                "A birth in repository B must not close repository A's active interval.");
            Require(store.GetRepositoryStatistics(repoB).CompletedChats == 0,
                "Millenova seed evidence must not become repository B evidence.");

            var reloaded = new ChatVitalityStore(storePath);
            Require(reloaded.GetActive(repoA, bornAt.AddHours(2))?.Session.Generation == 2,
                "Active session did not survive store reload.");
            Require(reloaded.GetHistory(repoA).Count == 2, "Session history did not survive store reload.");

            var abandoned = reloaded.RegisterBirth(repoA, "ffffffffffffffff", now: bornAt.AddHours(3));
            var abandonedPrevious = reloaded.GetHistory(repoA).Single(x => x.Session.Generation == 2);
            Require(abandoned.Session.Generation == 3 && abandonedPrevious.Session.Outcome == ChatSessionOutcome.Abandoned,
                "An omitted previous outcome must conservatively close ACTIVE as ABANDONED.");

            var repoC = Path.Combine(root, "RepoC");
            Directory.CreateDirectory(Path.Combine(repoC, ".zgitpet"));
            var agentBirthAt = bornAt.AddHours(4);
            var envelopePath = Path.Combine(repoC, ChatVitalityAgentProtocol.RelativeEnvelopePath);
            File.WriteAllText(envelopePath, JsonSerializer.Serialize(new ChatVitalityAgentEnvelope
            {
                Protocol = ChatVitalityAgentProtocol.ProtocolName,
                Event = ChatVitalityAgentProtocol.BirthEvent,
                BirthId = "repo-c-chat-1",
                Repository = "example/repo-c",
                FirstTurnAt = agentBirthAt,
                BirthCommit = "1212121212121212",
                UserTurns = 1,
                AssistantTurns = 1,
                TotalTurns = 2,
                Notes = "agent announced birth"
            }));

            var imported = reloaded.GetActive(repoC, agentBirthAt.AddMinutes(5));
            Require(imported is not null && imported.Session.Generation == 1,
                "Agent birth envelope should create the first repo-scoped generation automatically.");
            Require(imported!.Session.AgentBirthId == "repo-c-chat-1",
                "Agent birth id was not preserved for idempotency.");
            Require(imported.Session.StartedAt == agentBirthAt && imported.Session.FirstTurnAt == agentBirthAt,
                "Agent birth must use first conversational turn rather than first repository contact.");
            Require(imported.Session.FirstRepoContactAt == agentBirthAt.AddMinutes(5),
                "First repository contact should be stored separately from chat birth.");
            Require(imported.Session.TotalTurns == 2 && imported.Session.UserTurns == 1 && imported.Session.AssistantTurns == 1,
                "Initial conversation turn counters were not imported.");
            Require(imported.Session.BirthCommit == "1212121212121212",
                "Agent-supplied repository birth anchor was not preserved.");

            var importedAgain = reloaded.GetActive(repoC, agentBirthAt.AddMinutes(6));
            Require(reloaded.GetHistory(repoC).Count == 1 && importedAgain?.Session.AgentBirthId == "repo-c-chat-1",
                "Re-reading the same birth envelope must be idempotent.");

            File.WriteAllText(envelopePath, JsonSerializer.Serialize(new ChatVitalityAgentEnvelope
            {
                Protocol = ChatVitalityAgentProtocol.ProtocolName,
                Event = ChatVitalityAgentProtocol.BirthEvent,
                BirthId = "repo-c-chat-1",
                Repository = "example/repo-c",
                FirstTurnAt = agentBirthAt,
                BirthCommit = "1212121212121212",
                UserTurns = 5,
                AssistantTurns = 5,
                TotalTurns = 10
            }));
            var pulsed = reloaded.GetActive(repoC, agentBirthAt.AddMinutes(7));
            Require(pulsed?.Session.TotalTurns == 10 && pulsed.Session.UserTurns == 5 && pulsed.Session.AssistantTurns == 5,
                "Later observation of the same birth id should monotonically raise turn counters.");

            File.WriteAllText(envelopePath, JsonSerializer.Serialize(new ChatVitalityAgentEnvelope
            {
                Protocol = ChatVitalityAgentProtocol.ProtocolName,
                Event = ChatVitalityAgentProtocol.BirthEvent,
                BirthId = "repo-c-chat-2",
                Repository = "example/repo-c",
                FirstTurnAt = agentBirthAt.AddHours(1),
                BirthCommit = "3434343434343434",
                UserTurns = 1,
                AssistantTurns = 1,
                TotalTurns = 2,
                PreviousOutcome = "MAX_LENGTH",
                PreviousDeathAnchor = "2323232323232323"
            }));
            var nextAgentBirth = reloaded.GetActive(repoC, agentBirthAt.AddHours(1).AddMinutes(1));
            Require(nextAgentBirth?.Session.Generation == 2,
                "A new agent birth id should create the next repository generation.");
            var closedAgent = reloaded.GetHistory(repoC).Single(x => x.Session.Generation == 1);
            Require(closedAgent.Session.Outcome == ChatSessionOutcome.MaxLength &&
                    closedAgent.Session.DeathAnchorCommit == "2323232323232323",
                "Agent birth should explicitly close the previous generation with its supplied outcome/anchor.");

            var millenova = Path.Combine(root, "millenova");
            Directory.CreateDirectory(millenova);
            var millenovaStats = reloaded.GetRepositoryStatistics(millenova);
            Require(millenovaStats == new ChatLifespanStatistics(8, 10.6, 61.4, 38.6, 38.8),
                "The initial eight observations must remain associated with Millenova.");
            var globalStats = reloaded.GetGlobalStatistics();
            Require(globalStats.CompletedChats >= 10 && globalStats.LongestHours == 61.4,
                "Pooled statistics must combine seed and newly completed repository intervals.");
            Require(nextAgentBirth!.Lifespan.CurrentAgeHours >= 0 &&
                    nextAgentBirth.Lifespan.BirthTimestamp == agentBirthAt.AddHours(1) &&
                    nextAgentBirth.Lifespan.EmpiricalBand == "GREEN" &&
                    nextAgentBirth.Lifespan.GuidanceBasis.Contains("wilderruiz/millenova", StringComparison.Ordinal),
                "Active snapshots must expose age and provisional empirical guidance provenance.");
            Require(nextAgentBirth.Lifespan.NondeterministicStatement.Contains("nondeterministic", StringComparison.OrdinalIgnoreCase) &&
                    !nextAgentBirth.Lifespan.NondeterministicStatement.Contains("percent", StringComparison.OrdinalIgnoreCase),
                "Lifespan guidance must not fabricate context percentages or deterministic death predictions.");

            var localOnly = new ChatVitalitySession
            {
                StartedAt = bornAt,
                TotalTurns = 2,
                Saves = 50,
                Sends = 50,
                Reconciles = 50,
                CommitsSeen = 50,
                SyncCycles = 100
            };
            var localOnlyScore = ChatVitalityRisk.Calculate(localOnly, 0, bornAt);
            Require(localOnlyScore == 0,
                "Local Save/Send/Reconcile activity must not increase chat load.");

            var heavy = new ChatVitalitySession
            {
                StartedAt = bornAt,
                TotalTurns = 180,
                Gets = 6,
                FilesChanged = 200,
                Insertions = 10000,
                Deletions = 5000
            };
            var score = ChatVitalityRisk.Calculate(heavy, 6, bornAt.AddHours(12));
            Require(score >= 85 && ChatVitalityRisk.Classify(score) == "HANDOFF RECOMMENDED",
                "Remote-chat age, turns, Gets, and incoming churn should drive handoff risk.");

            var parsed = GuardianForm.ParseChatVitalityNumStat("12\t3\ta.cs\n-\t-\timage.bin\n8\t0\tb.cs\n");
            Require(parsed == (3, 20, 3), "Numstat accumulation did not preserve files and numeric churn.");

            Console.WriteLine("Chat Vitality cross-repository history, empirical lifespan, birth, Get-only load, scoring, and event regressions passed.");
            return Task.CompletedTask;
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
