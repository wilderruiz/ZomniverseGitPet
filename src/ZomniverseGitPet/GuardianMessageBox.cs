namespace ZomniverseGitPet;

/*
PATCH: THEMED GET WINDOWS
DATE: 2026-09-09
Style Get confirmation and result consistently.
*/
internal static class MessageBox
{
    public static DialogResult Show(string text) =>
        System.Windows.Forms.MessageBox.Show(text);

    public static DialogResult Show(string text, string caption) =>
        System.Windows.Forms.MessageBox.Show(text, caption);

    public static DialogResult Show(
        string text,
        string caption,
        MessageBoxButtons buttons) =>
        System.Windows.Forms.MessageBox.Show(text, caption, buttons);

    public static DialogResult Show(
        string text,
        string caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon) =>
        ShowCore(null, text, caption, buttons, icon);

    public static DialogResult Show(IWin32Window? owner, string text) =>
        System.Windows.Forms.MessageBox.Show(owner, text);

    public static DialogResult Show(
        IWin32Window? owner,
        string text,
        string caption) =>
        System.Windows.Forms.MessageBox.Show(owner, text, caption);

    public static DialogResult Show(
        IWin32Window? owner,
        string text,
        string caption,
        MessageBoxButtons buttons) =>
        System.Windows.Forms.MessageBox.Show(owner, text, caption, buttons);

    public static DialogResult Show(
        IWin32Window? owner,
        string text,
        string caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon) =>
        ShowCore(owner, text, caption, buttons, icon);

    private static DialogResult ShowCore(
        IWin32Window? owner,
        string text,
        string caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon)
    {
        if (caption.Equals("Get updates?", StringComparison.OrdinalIgnoreCase) &&
            buttons == MessageBoxButtons.YesNo)
        {
            using var dialog = new GuardianConfirmDialog(
                "Get updates",
                "GET ONLINE",
                text,
                "Get",
                "Cancel");

            return owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
        }

        if (caption.Equals("Get updates", StringComparison.OrdinalIgnoreCase) &&
            buttons == MessageBoxButtons.OK)
        {
            var success = icon == MessageBoxIcon.Information;
            using var dialog = new GuardianConfirmDialog(
                "Get updates",
                success ? "UPDATES RECEIVED  ✓" : "GET NEEDS ATTENTION",
                text,
                "OK",
                "",
                showCancel: false);

            return owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
        }

        if (caption.Equals("Project setup", StringComparison.OrdinalIgnoreCase) &&
            buttons == MessageBoxButtons.OK &&
            icon == MessageBoxIcon.Information)
        {
            using var dialog = new GuardianConfirmDialog(
                "Project setup",
                "PROJECT SAVED  ✓",
                text,
                "OK",
                "",
                showCancel: false,
                dialogSize: new Size(820, 560));

            return owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
        }

        return owner is null
            ? System.Windows.Forms.MessageBox.Show(text, caption, buttons, icon)
            : System.Windows.Forms.MessageBox.Show(owner, text, caption, buttons, icon);
    }
}
