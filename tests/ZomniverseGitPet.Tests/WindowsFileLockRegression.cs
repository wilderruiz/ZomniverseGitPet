using System.Diagnostics;
using ZomniverseGitPet;

internal static class WindowsFileLockRegression
{
    public static async Task RunAsync()
    {
        if (!OperatingSystem.IsWindows()) return;

        var root = Path.Combine(Path.GetTempPath(), "ZomniverseGitPet.LockTest", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "locked.txt");
        File.WriteAllText(path, "locked");
        Process? holder = null;

        try
        {
            var escapedPath = path.Replace("'", "''", StringComparison.Ordinal);
            holder = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -NonInteractive -Command \"$s=[IO.File]::Open('" + escapedPath +
                            "',[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None); " +
                            "Write-Output READY; [Console]::Out.Flush(); Start-Sleep -Seconds 20\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }) ?? throw new InvalidOperationException("Could not start the lock-holder process.");

            var ready = await holder.StandardOutput.ReadLineAsync();
            if (!string.Equals(ready, "READY", StringComparison.Ordinal))
                throw new InvalidOperationException("The lock-holder process did not become ready.");

            var blockers = WindowsFileLockService.FindLockingProcesses(root, ["locked.txt"]);
            if (!blockers.Any(process => process.ProcessId == holder.Id))
                throw new InvalidOperationException("Restart Manager did not identify the process holding the file.");

            Console.WriteLine("Windows file-lock owner regression passed.");
        }
        finally
        {
            try
            {
                if (holder is { HasExited: false }) holder.Kill(entireProcessTree: true);
            }
            catch { }
            holder?.Dispose();
            try { Directory.Delete(root, true); } catch { }
        }
    }
}
