namespace LynxLab;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        Application.ThreadException += (_, args) =>
        {
            LabCrashLog.ShowFatal("Lynx Lab UI thread", args.Exception);
            Application.Exit();
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
                LabCrashLog.Write("Lynx Lab AppDomain", exception);
        };

        try
        {
            Application.Run(new LynxLabForm());
        }
        catch (Exception ex)
        {
            LabCrashLog.ShowFatal("Lynx Lab startup", ex);
        }
    }
}
