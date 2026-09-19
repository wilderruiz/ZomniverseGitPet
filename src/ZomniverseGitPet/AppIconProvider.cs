namespace ZomniverseGitPet;

internal static class AppIconProvider
{
    private const string DevIconFileName = "DEV-ZGitPet-v2.ico";
    private const string ReleaseIconFileName = "ZGitPet-Release-v3.ico";

    private static readonly Lazy<Icon> SharedIcon = new(LoadIcon);

    public static Icon Icon => SharedIcon.Value;

    /* ==========================================================================
       PATCH: CHANNEL-SPECIFIC EXTERNAL ICON LINEAGE
       DATE.TIME: 2026-09-19 23:08 +03:00
       Prefer a versioned physical icon beside DEV/release executables so Windows
       shortcut/taskbar icon caching does not depend on extracting exe resource 0.
       ========================================================================== */
    private static Icon LoadIcon()
    {
        var external = GetExternalIconPath(ApplicationIdentity.Current, Application.ExecutablePath);
        if (!string.IsNullOrWhiteSpace(external) && File.Exists(external))
        {
            try
            {
                return new Icon(external);
            }
            catch
            {
                // Fall through to the executable resource for portable/repair scenarios.
            }
        }

        try
        {
            var extracted = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (extracted is not null) return extracted;
        }
        catch
        {
        }

        return SystemIcons.Application;
    }

    internal static string? GetExternalIconPath(
        ApplicationIdentityInfo identity,
        string executablePath)
    {
        var directory = Path.GetDirectoryName(executablePath);
        if (string.IsNullOrWhiteSpace(directory)) return null;

        var fileName = identity.Channel switch
        {
            ApplicationChannel.Development => DevIconFileName,
            ApplicationChannel.InstalledRelease => ReleaseIconFileName,
            _ => null
        };

        return fileName is null ? null : Path.Combine(directory, fileName);
    }
}
