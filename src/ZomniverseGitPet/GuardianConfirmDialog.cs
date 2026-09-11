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
        Size? dialogSize = null,
        /* ==========================================================================
           PATCH: CONFIGURABLE CONFIRMATION PRESENTATION
           FUNCTION:
           Allows selected confirmations to support resizing, scrolling, and wider primary actions.

           DATE.TIME ADDED: 2026-09-11 17:48 +03:00

           REASON:
           Support confirmation dialogs containing variable file counts and longer action labels.
           ========================================================================== */
        bool resizable = false,
        bool scrollable = false,
        int confirmWidth = 120)
    {
        /* ==========================================================================
           PATCH: OPTIONAL RESIZABLE CONFIRMATION
           FUNCTION:
           Makes only explicitly configured confirmation dialogs resizable and maximizable.

           DATE.TIME ADDED: 2026-09-11 17:48 +03:00

           REASON:
           Let users enlarge confirmations containing an unpredictable number of files.
           ========================================================================== */
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = resizable
            ? FormBorderStyle.Sizable
            : FormBorderStyle.FixedDialog;
        MaximizeBox = resizable;
        MinimizeBox = false;
        ShowInTaskbar = false;
        Size = dialogSize ?? new Size(720, 430);
        MinimumSize = resizable
            ? new Size(680, 460)
            : Size.Empty;

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

        /* ==========================================================================
           PATCH: OPTIONAL SCROLLABLE CONFIRMATION BODY
           FUNCTION:
           Uses a themed scrollable text surface when confirmation content exceeds the available area.

           DATE.TIME ADDED: 2026-09-11 17:48 +03:00

           REASON:
           Keep large force-track file lists readable at every window size.
           ========================================================================== */
        Control body;

        if (scrollable)
        {
            var scrollingBody = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                DetectUrls = false,
                WordWrap = false,
                ScrollBars = RichTextBoxScrollBars.Both,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = GuardianTheme.Console,
                ForeColor = GuardianTheme.Ink,
                Font = new Font("Segoe UI", 10f),
                Text = message,
                Margin = new Padding(6, 8, 6, 8)
            };

            scrollingBody.HandleCreated += (_, _) =>
                ApplyDarkScrollbarTheme(scrollingBody);

            body = scrollingBody;
        }
        else
        {
            body = new Label
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
        }
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
        /* ==========================================================================
           PATCH: CONFIGURABLE PRIMARY BUTTON WIDTH
           FUNCTION:
           Applies the requested width to confirmation actions containing longer labels.

           DATE.TIME ADDED: 2026-09-11 17:48 +03:00

           REASON:
           Prevent the Track selected files label from being truncated.
           ========================================================================== */
        var confirm = new GuardianActionButton
        {
            Text = confirmText,
            Width = confirmWidth,
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

    /* ==========================================================================
       HELPER: SetWindowTheme
       FUNCTION:
       Requests the native Windows dark theme for scrollable confirmation controls.

       DATE.TIME ADDED: 2026-09-11 17:48 +03:00

       REASON:
       Replace bright native scrollbars with scrollbars matching GitPet's dark surfaces.
       ========================================================================== */
    [System.Runtime.InteropServices.DllImport(
        "uxtheme.dll",
        CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int SetWindowTheme(
        IntPtr windowHandle,
        string? subApplicationName,
        string? subIdentifierList);

    /* ==========================================================================
       HELPER: ApplyDarkScrollbarTheme
       FUNCTION:
       Applies GitPet-compatible native dark styling to a scrollable text control.

       DATE.TIME ADDED: 2026-09-11 17:48 +03:00

       REASON:
       Style both vertical and horizontal scrollbars consistently with existing GitPet editors.
       ========================================================================== */
    private static void ApplyDarkScrollbarTheme(Control control)
    {
        if (!control.IsHandleCreated) return;

        _ = SetWindowTheme(
            control.Handle,
            "DarkMode_Explorer",
            null);
    }
}
