namespace LynxLab;

internal static class LabCrashLog
{
    private static readonly object Gate = new();

    public static string LogPath { get; } =
        Path.Combine(Path.GetTempPath(), "ZGitPet-LynxLab-crash.log");

    public static void Write(string source, Exception exception)
    {
        try
        {
            lock (Gate)
            {
                File.AppendAllText(
                    LogPath,
                    $"[{DateTimeOffset.Now:O}] {source}{Environment.NewLine}" +
                    exception + Environment.NewLine +
                    new string('-', 80) + Environment.NewLine);
            }
        }
        catch
        {
            // Diagnostics must never become another crash source.
        }
    }

    public static void ShowFatal(string source, Exception exception)
    {
        Write(source, exception);

        try
        {
            MessageBox.Show(
                $"{source} failed.\r\n\r\n{exception.GetType().Name}: {exception.Message}\r\n\r\n" +
                $"Full details were written to:\r\n{LogPath}",
                "Lynx Lab error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        catch
        {
        }
    }
}
