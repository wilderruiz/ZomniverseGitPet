using System.Runtime.CompilerServices;
using System.Text.Json;
using ZomniverseGitPet;

internal static class AuditLogConcurrencyRegression
{
    /* ==========================================================================
       PATCH: AUDIT LOG CONCURRENCY REGRESSION
       DATE.TIME: 2026-09-10 14:02 +03:00
       REASON:
       Hammer concurrent audit writers and verify complete JSONL output.
       ========================================================================== */
    [ModuleInitializer]
    internal static void Run()
    {
        const int writerCount = 16;
        const int writesPerWriter = 40;
        const int expectedWrites = writerCount * writesPerWriter;

        var root = Path.Combine(
            Path.GetTempPath(),
            "ZomniverseGitPet.Tests",
            "audit-concurrency-" + Guid.NewGuid().ToString("N"));
        var auditFile = Path.Combine(root, "audit.jsonl");

        Directory.CreateDirectory(root);
        try
        {
            var logs = Enumerable.Range(0, writerCount)
                .Select(_ => new AuditLog(auditFile))
                .ToArray();

            var writes = Enumerable.Range(0, expectedWrites)
                .Select(index => logs[index % logs.Length].WriteAsync(
                    "audit_concurrency_regression",
                    new { index }))
                .ToArray();

            Task.WhenAll(writes).GetAwaiter().GetResult();

            var lines = File.ReadAllLines(auditFile);
            if (lines.Length != expectedWrites)
            {
                throw new InvalidOperationException(
                    $"FAIL: audit log concurrency regression expected {expectedWrites} lines but found {lines.Length}.");
            }

            var seen = new HashSet<int>();
            foreach (var line in lines)
            {
                using var document = JsonDocument.Parse(line);
                var rootElement = document.RootElement;

                if (!rootElement.TryGetProperty("event", out var eventName) ||
                    eventName.GetString() != "audit_concurrency_regression")
                {
                    throw new InvalidOperationException(
                        "FAIL: audit log concurrency regression found a malformed event.");
                }

                var index = rootElement
                    .GetProperty("data")
                    .GetProperty("index")
                    .GetInt32();
                seen.Add(index);
            }

            if (seen.Count != expectedWrites ||
                !Enumerable.Range(0, expectedWrites).All(seen.Contains))
            {
                throw new InvalidOperationException(
                    "FAIL: audit log concurrency regression lost or duplicated writes.");
            }

            Console.WriteLine(
                $"AuditLog concurrency regression passed ({expectedWrites} concurrent writes)." );
        }
        finally
        {
            try
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
            catch
            {
            }
        }
    }
}
