using System.Text.Json;

namespace ZomniverseGitPet;

public sealed class AuditLog
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task WriteAsync(string eventName, object? data = null)
    {
        var entry = JsonSerializer.Serialize(new
        {
            timestamp = DateTimeOffset.UtcNow,
            @event = eventName,
            data = data ?? new { }
        });

        await _gate.WaitAsync();
        try
        {
            Directory.CreateDirectory(AppPaths.Root);
            await File.AppendAllTextAsync(AppPaths.AuditFile, entry + Environment.NewLine);
        }
        finally
        {
            _gate.Release();
        }
    }
}

