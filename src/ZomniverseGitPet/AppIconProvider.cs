namespace ZomniverseGitPet;

internal static class AppIconProvider
{
    private static readonly Lazy<Icon> SharedIcon = new(LoadIcon);

    public static Icon Icon => SharedIcon.Value;

    private static Icon LoadIcon()
    {
        try
        {
            var extracted = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (extracted is not null) return extracted;
        }
        catch { }

        return SystemIcons.Application;
    }
}
