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
            Require(first.LifecycleState == "NEWBORN", "A birth without events should be NEWBORN.");
            Require(first.Session.BirthCommit == "aaaaaaaaaaaaaaaa", "Birth anchor was not preserved.");

            var active = store.RecordEvent(repoA, ChatVitalityEventType.Save, "bbbbbbbbbbbbbbbb",
                filesChanged: 4, insertions: 120, deletions: 30, now: bornAt.AddMinutes(35));
            Require(active is not null && active.LifecycleState == "ACTIVE", "First event should activate the newborn session.");
            Require(active!.InteractionCount == 1 && active.Session.Saves == 1 && active.Session.CommitsSeen == 1,
                "Save event counters were not accumulated.");
            Require(active.CurrentAnchorCommit == "bbbbbbbbbbbbbbbb",
                "Current anchor should advance to the latest observable event commit.");
            Require(active.Session.FilesChanged == 4 && active.Session.Insertions == 120 && active.Session.Deletions == 30,
                "Observable change metrics were not accumulated.");

            store.RecordEvent(repoA, ChatVitalityEventType.Get, "cccccccccccccccc", now: bornAt.AddMinutes(40));
            store.RecordEvent(repoA, ChatVitalityEventType.Send, "cccccccccccccccc", now: bornAt.AddMinutes(41));
            store.RecordEvent(repoA, ChatVitalityEventType.Reconcile, "dddddddddddddddd", now: bornAt.AddMinutes(42));
            var beforeHandoff = store.GetActive(repoA, bornAt.AddMinutes(45));
            Require(beforeHandoff is not null && beforeHandoff.InteractionCount == 4,
                "Get, Send, and Reconcile events were not accumulated.");
            Require(beforeHandoff!.Session.SyncCycles == 3, "Sync-cycle counter should include Get, Send, and Reconcile.");

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

            var independent = store.RegisterBirth(repoB, "1111111111111111", now: bornAt);
            Require(independent.Session.Generation == 1, "Generations must be independent per repository.");

            var reloaded = new ChatVitalityStore(storePath);
            Require(reloaded.GetActive(repoA, bornAt.AddHours(2))?.Session.Generation == 2,
                "Active session did not survive store reload.");
            Require(reloaded.GetHistory(repoA).Count == 2, "Session history did not survive store reload.");

            var abandoned = reloaded.RegisterBirth(repoA, "ffffffffffffffff", now: bornAt.AddHours(3));
            var abandonedPrevious = reloaded.GetHistory(repoA).Single(x => x.Session.Generation == 2);
            Require(abandoned.Session.Generation == 3 && abandonedPrevious.Session.Outcome == ChatSessionOutcome.Abandoned,
                "An omitted previous outcome must conservatively close ACTIVE as ABANDONED.");

            var heavy = new ChatVitalitySession
            {
                StartedAt = bornAt,
                Saves = 8,
                Gets = 6,
                Sends = 6,
                Reconciles = 4,
                CommitsSeen = 8,
                FilesChanged = 200,
                Insertions = 10000,
                Deletions = 5000,
                SyncCycles = 16
            };
            var score = ChatVitalityRisk.Calculate(heavy, 30, bornAt.AddHours(12));
            Require(score == 100 && ChatVitalityRisk.Classify(score) == "HANDOFF RECOMMENDED",
                "Heuristic scoring/classification should be deterministic and clamped.");

            var parsed = GuardianForm.ParseChatVitalityNumStat("12\t3\ta.cs\n-\t-\timage.bin\n8\t0\tb.cs\n");
            Require(parsed == (3, 20, 3), "Numstat accumulation did not preserve files and numeric churn.");

            Console.WriteLine("Chat Vitality storage, lifecycle, scoring, and event regressions passed.");
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
