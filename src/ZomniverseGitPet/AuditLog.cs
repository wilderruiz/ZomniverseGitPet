using System.Text.Json;

namespace ZomniverseGitPet;

public sealed class AuditLog
{
    /* ==========================================================================
       PATCH: PROCESS-WIDE AUDIT WRITE SAFETY
       DATE.TIME: 2026-09-10 14:02 +03:00
       REASON:
       Serialize audit writes and tolerate transient file locks.
       ========================================================================== */
    private const int MaxWriteAttempts = 4;
    private static readonly SemaphoreSlim ProcessGate = new(1, 1);
    private readonly string _auditFile;

    public AuditLog()
        : this(AppPaths.AuditFile)
    {
    }

    internal AuditLog(string auditFile)
    {
        _auditFile = Path.GetFullPath(auditFile);
    }

    public async Task WriteAsync(string eventName, object? data = null)
    {
        string entry;
        try
        {
            entry = JsonSerializer.Serialize(new
            {
                timestamp = DateTimeOffset.UtcNow,
                @event = eventName,
                data = data ?? new { }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Audit serialization skipped: {ex.Message}");
            return;
        }

        await ProcessGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(_auditFile);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            for (var attempt = 1; attempt <= MaxWriteAttempts; attempt++)
            {
                try
                {
                    await File.AppendAllTextAsync(
                            _auditFile,
                            entry + Environment.NewLine)
                        .ConfigureAwait(false);
                    return;
                }
                catch (IOException) when (attempt < MaxWriteAttempts)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt))
                        .ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            // Audit telemetry is best-effort and must never destabilize GitPet.
            System.Diagnostics.Debug.WriteLine($"Audit write skipped: {ex.Message}");
        }
        finally
        {
            ProcessGate.Release();
        }
    }
}
