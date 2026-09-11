namespace ZomniverseGitPet;

/*
PATCH: THEMED CONFIRMATION DIALOG
DATE: 2026-09-09
Replace Windows prompt with GitPet-styled confirmation.
*/
internal sealed class GuardianConfirmDialog : Form
{
    public GuardianConfirmDialog(
        string title,
        string heading,
        string message,
        /*
        PATCH: OPTIONAL CANCEL BUTTON
        DATE: 2026-09-09
        Allow confirmations to display one action button.
        */
        string confirmText = "Yes",
        string cancelText = "No",
        bool showCancel = true,
        Size? dialogSize = null)
    {
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        Size = dialogSize ?? new Size(720, 430);

        BackColor = GuardianTheme.Window;
        ForeColor = GuardianTheme.Ink;
        Font = new Font("Segoe UI", 9.5f);

        /*
        PATCH: GUARDIAN DIALOG CHROME
        DATE: 2026-09-09
        Match dialog title bar with Guardian styling.
        */
        WindowChrome.ApplyGuardianChrome(this);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            Padding = new Padding(24),
            BackColor = GuardianTheme.Window
        };

        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));

        var headingLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = heading,
            /*
            PATCH: UNIFIED DIALOG SURFACE
            DATE: 2026-09-09
            Use one continuous background across dialog.
            */
            BackColor = GuardianTheme.Window,            
            ForeColor = GuardianTheme.HotPinkSoft,
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };

        /*
        PATCH: STATIC CONFIRMATION BODY
        DATE: 2026-09-09
        Show message directly without scrolling text panel.
        */
        var body = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            BackColor = GuardianTheme.Window,
            ForeColor = GuardianTheme.Ink,
            Font = new Font("Segoe UI", 10f),
            Text = message,
            TextAlign = ContentAlignment.TopLeft,
            Padding = new Padding(6, 8, 6, 8)
        };
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 12, 0, 0),
            BackColor = GuardianTheme.Window
        };

        /*
        PATCH: DIALOG SAVE STYLE ONLY
        DATE: 2026-09-09
        Use Save styling without repository state.
        */
        var confirm = new GuardianActionButton
        {
            Text = confirmText,
            Width = 120,
            Height = 38,
            Kind = GuardianActionKind.Primary,
            SyncStateAware = false,
            /*
            PATCH: ALIGN DIALOG ACTION BUTTONS
            DATE: 2026-09-09
            Remove default margin so both buttons align.
            */
            DialogResult = DialogResult.Yes,
            Margin = Padding.Empty            
        };

        /*
        PATCH: GITPET CANCEL BUTTON
        DATE: 2026-09-09
        Match secondary button with Guardian controls.
        */
        var cancel = new GuardianActionButton
        {
            Text = cancelText,
            Width = 120,
            Height = 38,
            Kind = GuardianActionKind.Standard,
            DialogResult = DialogResult.No,
            Margin = new Padding(10, 0, 0, 0)
        };

        /*
        PATCH: CONDITIONAL CANCEL BUTTON
        DATE: 2026-09-09
        Hide Cancel for informational success dialogs.
        */
        buttons.Controls.Add(confirm);

        if (showCancel)
            buttons.Controls.Add(cancel);

        root.Controls.Add(headingLabel, 0, 0);
        root.Controls.Add(body, 0, 1);
        root.Controls.Add(buttons, 0, 2);

        Controls.Add(root);

        /*
        PATCH: CONDITIONAL CANCEL BEHAVIOUR
        DATE: 2026-09-09
        Only register Cancel when button is visible.
        */
        AcceptButton = confirm;

        if (showCancel)
            CancelButton = cancel;
    }
}
