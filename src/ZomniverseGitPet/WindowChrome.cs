using System.Runtime.InteropServices;

namespace ZomniverseGitPet;

internal static class WindowChrome
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;

    public static void ApplyGuardianChrome(Form form)
    {
        WindowPlacementManager.Attach(form);

        if (!OperatingSystem.IsWindowsVersionAtLeast(10)) return;

        void Apply()
        {
            try
            {
                var enabled = 1;
                DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int));

                var caption = ToColorRef(GuardianTheme.Window);
                var border = ToColorRef(GuardianTheme.Border);
                var text = ToColorRef(GuardianTheme.Ink);
                DwmSetWindowAttribute(form.Handle, DwmwaCaptionColor, ref caption, sizeof(int));
                DwmSetWindowAttribute(form.Handle, DwmwaBorderColor, ref border, sizeof(int));
                DwmSetWindowAttribute(form.Handle, DwmwaTextColor, ref text, sizeof(int));
            }
            catch
            {
                // Older supported Windows builds may ignore newer DWM attributes.
            }
        }

        if (form.IsHandleCreated) Apply();
        form.HandleCreated += (_, _) => Apply();
    }

    private static int ToColorRef(Color color) =>
        color.R | (color.G << 8) | (color.B << 16);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int dwAttribute,
        ref int pvAttribute,
        int cbAttribute);
}
