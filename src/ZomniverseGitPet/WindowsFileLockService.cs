using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ZomniverseGitPet;

internal sealed record LockingProcessInfo(int ProcessId, string Name);

internal sealed record ProcessTerminationResult(
    IReadOnlyList<LockingProcessInfo> Closed,
    IReadOnlyList<(LockingProcessInfo Process, string Error)> Failed);

internal static class WindowsFileLockService
{
    private const int ErrorMoreData = 234;
    private const int MaxAppName = 255;
    private const int MaxServiceName = 63;

    public static IReadOnlyList<LockingProcessInfo> FindLockingProcesses(
        string repositoryPath,
        IReadOnlyList<string> relativePaths)
    {
        if (!OperatingSystem.IsWindows() || relativePaths.Count == 0) return [];

        var resources = relativePaths
            .Select(path => Path.GetFullPath(Path.Combine(repositoryPath, path)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var key = Guid.NewGuid().ToString("N");
        var start = RmStartSession(out var session, 0, key);
        if (start != 0) return [];

        try
        {
            var register = RmRegisterResources(
                session,
                (uint)resources.Length,
                resources,
                0,
                null,
                0,
                null);
            if (register != 0) return [];

            uint required = 0;
            uint count = 0;
            uint rebootReasons = 0;
            var query = RmGetList(session, out required, ref count, null, ref rebootReasons);
            if (query != ErrorMoreData || required == 0) return [];

            var native = new RestartManagerProcessInfo[required];
            count = required;
            query = RmGetList(session, out required, ref count, native, ref rebootReasons);
            if (query != 0) return [];

            var currentProcessId = Environment.ProcessId;
            return native
                .Take((int)count)
                .Where(item => item.Process.ProcessId > 0 && item.Process.ProcessId != currentProcessId)
                .Select(item => new LockingProcessInfo(
                    item.Process.ProcessId,
                    ResolveProcessName(item.Process.ProcessId, item.ApplicationName)))
                .DistinctBy(item => item.ProcessId)
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.ProcessId)
                .ToArray();
        }
        catch
        {
            return [];
        }
        finally
        {
            _ = RmEndSession(session);
        }
    }

    public static IReadOnlyList<LockingProcessInfo> FindRepositoryBackgroundProcesses(string repositoryPath)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(repositoryPath)) return [];

        const string script =
            "$root=$env:ZGITPET_REPOSITORY_PATH; " +
            "$allowed=@('node.exe','wscript.exe','cscript.exe','powershell.exe','pwsh.exe','cmd.exe'); " +
            "Get-CimInstance Win32_Process | " +
            "Where-Object { $_.ProcessId -ne $PID -and $allowed -contains $_.Name -and " +
            "$_.CommandLine -and $_.CommandLine.IndexOf($root,[StringComparison]::OrdinalIgnoreCase) -ge 0 } | " +
            "ForEach-Object { Write-Output ((\"{0}`t{1}\" -f $_.ProcessId,$_.Name)) }";

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            process.StartInfo.ArgumentList.Add("-NoProfile");
            process.StartInfo.ArgumentList.Add("-NonInteractive");
            process.StartInfo.ArgumentList.Add("-Command");
            process.StartInfo.ArgumentList.Add(script);
            process.StartInfo.Environment["ZGITPET_REPOSITORY_PATH"] =
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(repositoryPath));

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(5000))
            {
                process.Kill(entireProcessTree: true);
                return [];
            }
            if (process.ExitCode != 0) return [];

            return ParseProcessList(output)
                .Where(item => item.ProcessId != Environment.ProcessId)
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    internal static IReadOnlyList<LockingProcessInfo> ParseProcessList(string? output)
    {
        if (string.IsNullOrWhiteSpace(output)) return [];

        var results = new List<LockingProcessInfo>();
        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = line.IndexOf('\t');
            if (separator <= 0 ||
                !int.TryParse(line[..separator], out var processId) ||
                processId <= 0)
                continue;

            var name = line[(separator + 1)..].Trim();
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                name = name[..^4];
            if (name.Length == 0) name = "Unknown process";
            results.Add(new LockingProcessInfo(processId, name));
        }

        return results
            .DistinctBy(item => item.ProcessId)
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ProcessId)
            .ToArray();
    }

    public static ProcessTerminationResult Terminate(IReadOnlyList<LockingProcessInfo> processes)
    {
        var closed = new List<LockingProcessInfo>();
        var failed = new List<(LockingProcessInfo Process, string Error)>();

        foreach (var item in processes.DistinctBy(process => process.ProcessId))
        {
            if (item.ProcessId == Environment.ProcessId) continue;

            try
            {
                using var process = Process.GetProcessById(item.ProcessId);
                process.Kill(entireProcessTree: true);
                if (!process.WaitForExit(5000))
                {
                    failed.Add((item, "The process did not close within five seconds."));
                    continue;
                }

                closed.Add(item);
            }
            catch (ArgumentException)
            {
                // It exited after discovery, which means it is no longer a blocker.
                closed.Add(item);
            }
            catch (Exception ex)
            {
                failed.Add((item, ex.Message));
            }
        }

        return new ProcessTerminationResult(closed, failed);
    }

    private static string ResolveProcessName(int processId, string restartManagerName)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return string.IsNullOrWhiteSpace(process.ProcessName)
                ? restartManagerName
                : process.ProcessName;
        }
        catch
        {
            return string.IsNullOrWhiteSpace(restartManagerName)
                ? "Unknown process"
                : restartManagerName;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RestartManagerUniqueProcess
    {
        public int ProcessId;
        public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
    }

    private enum RestartManagerApplicationType
    {
        Unknown = 0,
        MainWindow = 1,
        OtherWindow = 2,
        Service = 3,
        Explorer = 4,
        Console = 5,
        Critical = 1000
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RestartManagerProcessInfo
    {
        public RestartManagerUniqueProcess Process;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxAppName + 1)]
        public string ApplicationName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxServiceName + 1)]
        public string ServiceShortName;

        public RestartManagerApplicationType ApplicationType;
        public uint ApplicationStatus;
        public uint TerminalSessionId;

        [MarshalAs(UnmanagedType.Bool)]
        public bool Restartable;
    }

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmStartSession(
        out uint sessionHandle,
        int sessionFlags,
        StringBuilder sessionKey);

    private static int RmStartSession(out uint sessionHandle, int sessionFlags, string sessionKey)
    {
        var key = new StringBuilder(sessionKey, 33);
        return RmStartSession(out sessionHandle, sessionFlags, key);
    }

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmRegisterResources(
        uint sessionHandle,
        uint fileCount,
        string[] fileNames,
        uint applicationCount,
        RestartManagerUniqueProcess[]? applications,
        uint serviceCount,
        string[]? serviceNames);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmGetList(
        uint sessionHandle,
        out uint processInfoNeeded,
        ref uint processInfoCount,
        [In, Out] RestartManagerProcessInfo[]? affectedApplications,
        ref uint rebootReasons);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmEndSession(uint sessionHandle);
}
