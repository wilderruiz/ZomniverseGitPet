using System.Diagnostics;

namespace ZomniverseGitPet;

/* ========================================================================== 
   PATCH: IGNORED PROJECT FILE REVIEW
   DATE: 2026-09-11

   Require explicit selection before force-tracking ignored files.
   ========================================================================== */
internal sealed class IgnoredProjectFilesDialog : Form
{
    private readonly string _repositoryRoot;
    private readonly IReadOnlyList<IgnoredProjectFile> _files;
    private readonly DataGridView _grid;

    /* ==========================================================================
       PATCH: IGNORED FILE DIALOG RESOURCES
       FUNCTION:
       Provides the standard GitPet header artwork and explanatory button tooltips.

       DATE.TIME ADDED: 2026-09-11 17:16 +03:00

       REASON:
       Match established GitPet dialog presentation and clarify each available action.
       ========================================================================== */
    private readonly PetAssets _petAssets = new();
    private readonly ToolTip _toolTips = new()
    {
        InitialDelay = 350,
        ReshowDelay = 100,
        AutoPopDelay = 15000,
        ShowAlways = true
    };

    public IReadOnlyList<string> SelectedPaths => _grid.Rows
        .Cast<DataGridViewRow>()
        .Where(row => Convert.ToBoolean(row.Cells[0].Value ?? false))
        .Select(row => ((IgnoredProjectFile)row.Tag!).Path)
        .ToArray();

    public IgnoredProjectFilesDialog(string repositoryRoot, IReadOnlyList<IgnoredProjectFile> files)
    {
        _repositoryRoot = repositoryRoot;
        _files = files;
        Text = "Ignored project files";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(820, 520);
        Size = new Size(1040, 680);
        BackColor = GuardianTheme.Window;
        ForeColor = GuardianTheme.Ink;
        Font = new Font("Segoe UI", 9.5f);
        ShowInTaskbar = false;
        WindowChrome.ApplyGuardianChrome(this);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(24),
            BackColor = GuardianTheme.Window
        };
        /* ==========================================================================
           PATCH: TALLER LOGO HEADER
           FUNCTION:
           Adds sufficient header height for the GitPet artwork and dialog title.

           DATE.TIME ADDED: 2026-09-11 17:16 +03:00

           REASON:
           Display the GitPet logo without compressing the title.
           ========================================================================== */
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));

        /* ==========================================================================
           PATCH: GITPET DIALOG HEADER ARTWORK
           FUNCTION:
           Displays the existing GitPet artwork beside the ignored-files heading.

           DATE.TIME ADDED: 2026-09-11 17:16 +03:00

           REASON:
           Make the dialog header consistent with other guided GitPet windows.
           ========================================================================== */
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = GuardianTheme.Window,
            Margin = Padding.Empty
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var logo = new PictureBox
        {
            Dock = DockStyle.Fill,
            Image = _petAssets.Happy,
            SizeMode = PictureBoxSizeMode.Zoom,
            Margin = new Padding(0, 5, 14, 5),
            BackColor = Color.Transparent,
            TabStop = false
        };

        var heading = new Label
        {
            Dock = DockStyle.Fill,
            Text = "IGNORED PROJECT FILES",
            ForeColor = GuardianTheme.Changes,
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = Padding.Empty
        };

        header.Controls.Add(logo, 0, 0);
        header.Controls.Add(heading, 1, 0);
        root.Controls.Add(header, 0, 0);
        root.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Some files selected for the current GitPet project are ignored by Git.\r\n" +
                   "GitPet must not force-track anything unless you explicitly approve it.",
            ForeColor = GuardianTheme.MutedInk,
            Font = new Font("Segoe UI", 10f),
            TextAlign = ContentAlignment.TopLeft,
            Padding = new Padding(0, 4, 0, 8)
        }, 0, 1);

        _grid = BuildGrid(files);
        root.Controls.Add(_grid, 0, 2);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 12, 0, 0)
        };
        var cancel = MakeButton("Cancel", 112, GuardianActionKind.Standard, DialogResult.Cancel);
        var proceed = MakeButton("Continue", 128, GuardianActionKind.Primary, DialogResult.Yes);
        /* ==========================================================================
           PATCH: WIDER OPEN IGNORE BUTTON
           FUNCTION:
           Provides enough horizontal space for the complete Open ignore file label.

           DATE.TIME ADDED: 2026-09-11 17:16 +03:00

           REASON:
           Prevent the Open ignore file button text from being truncated.
           ========================================================================== */
        var open = MakeButton(
            "Open ignore file",
            240,
            GuardianActionKind.Standard,
            DialogResult.None);
        open.Click += (_, _) => OpenSelectedIgnoreSource();

        /* ==========================================================================
           PATCH: IGNORED FILE ACTION TOOLTIPS
           FUNCTION:
           Explains the outcome of every action available in the dialog footer.

           DATE.TIME ADDED: 2026-09-11 17:16 +03:00

           REASON:
           Help users understand each action before changing their selection.
           ========================================================================== */
        _toolTips.SetToolTip(
            open,
            "Open the ignore file responsible for the currently selected row.");

        _toolTips.SetToolTip(
            proceed,
            "Continue saving normal files and review any ignored files you selected.");

        _toolTips.SetToolTip(
            cancel,
            "Cancel this Save operation. Nothing will be staged.");

        footer.Controls.Add(cancel);
        footer.Controls.Add(proceed);
        footer.Controls.Add(open);
        root.Controls.Add(footer, 0, 3);

        Controls.Add(root);
        AcceptButton = proceed;
        CancelButton = cancel;
    }

    private static DataGridView BuildGrid(IReadOnlyList<IgnoredProjectFile> files)
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AutoGenerateColumns = false,
            BackgroundColor = GuardianTheme.Console,
            BorderStyle = BorderStyle.FixedSingle,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            GridColor = GuardianTheme.BorderSoft,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            EnableHeadersVisualStyles = false
        };
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = GuardianTheme.SurfaceSoft,
            ForeColor = GuardianTheme.MutedInk,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            SelectionBackColor = GuardianTheme.SurfaceSoft
        };
        grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = GuardianTheme.Console,
            ForeColor = GuardianTheme.Ink,
            SelectionBackColor = GuardianTheme.SurfaceSoft,
            SelectionForeColor = GuardianTheme.Ink,
            Padding = new Padding(6, 4, 6, 4)
        };
        /* ==========================================================================
           PATCH: TALLER IGNORED FILE ROWS
           FUNCTION:
           Gives every ignored-file row enough vertical space to display its text clearly.

           DATE.TIME ADDED: 2026-09-11 17:16 +03:00

           REASON:
           Prevent file paths and ignore details from appearing vertically cropped.
           ========================================================================== */
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        grid.RowTemplate.Height = 52;
        grid.ColumnHeadersHeight = 42;
        grid.ColumnHeadersHeightSizeMode =
            DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

        grid.DefaultCellStyle.Alignment =
            DataGridViewContentAlignment.MiddleLeft;
        grid.DefaultCellStyle.WrapMode =
            DataGridViewTriState.False;
        grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "TRACK", Width = 72, FalseValue = false, TrueValue = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "FILE", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 52 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "IGNORED BY", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 28 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "RULE", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 20 });

        foreach (var file in files)
        {
            var row = grid.Rows[grid.Rows.Add(false, file.Path,
                file.IgnoreSource + (file.IgnoreLine is int line ? $" : {line}" : ""), file.Rule)];
            row.Tag = file;
        }
        return grid;
    }

    private static GuardianActionButton MakeButton(
        string text, int width, GuardianActionKind kind, DialogResult result) => new()
    {
        Text = text,
        Width = width,
        Height = 38,
        Kind = kind,
        SyncStateAware = false,
        DialogResult = result,
        Margin = new Padding(10, 0, 0, 0)
    };

    private void OpenSelectedIgnoreSource()
    {
        if (_grid.CurrentRow?.Tag is not IgnoredProjectFile selected) return;
        var source = ResolveSourcePath(selected.IgnoreSource);
        if (source is null || !File.Exists(source))
        {
            ShowNotice("IGNORE SOURCE UNAVAILABLE",
                $"Git reported this ignore source, but it is not an accessible local file:\r\n\r\n{selected.IgnoreSource}");
            return;
        }

        try
        {
            if (selected.IgnoreLine is int line)
            {
                try
                {
                    Process.Start(new ProcessStartInfo("code")
                    {
                        UseShellExecute = true,
                        ArgumentList = { "-g", $"{source}:{line}" }
                    });
                    return;
                }
                catch { }
            }
            Process.Start(new ProcessStartInfo(source) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowNotice("IGNORE SOURCE UNAVAILABLE", ex.Message);
        }
    }

    private string? ResolveSourcePath(string source)
    {
        if (Path.IsPathFullyQualified(source)) return Path.GetFullPath(source);
        if (source.StartsWith(".git/", StringComparison.OrdinalIgnoreCase) ||
            source.StartsWith(".git\\", StringComparison.OrdinalIgnoreCase))
            return Path.GetFullPath(Path.Combine(_repositoryRoot, source));
        return Path.GetFullPath(Path.Combine(_repositoryRoot, source));
    }

    private void ShowNotice(string heading, string message)
    {
        using var dialog = new GuardianConfirmDialog(
            "Ignored project files", heading, message, "OK", "", showCancel: false);
        dialog.ShowDialog(this);
    }

    /* ==========================================================================
       HELPER: Dispose
       FUNCTION:
       Releases the dialog tooltip and embedded GitPet artwork resources.

       DATE.TIME ADDED: 2026-09-11 17:16 +03:00

       REASON:
       Prevent retained image and tooltip resources after closing the dialog.
       ========================================================================== */
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTips.Dispose();
            _petAssets.Dispose();
        }

        base.Dispose(disposing);
    }
}
