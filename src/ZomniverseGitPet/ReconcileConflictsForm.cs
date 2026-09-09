namespace ZomniverseGitPet;

internal enum ReconcileChoice
{
    Local,
    Online
}

internal sealed class ReconcileConflictsForm : Form
{
    private const string LocalChoice = "Keep my local version";
    private const string OnlineChoice = "Keep online version";
    private readonly DataGridView _grid = new();

    public IReadOnlyDictionary<string, ReconcileChoice> Choices { get; private set; } =
        new Dictionary<string, ReconcileChoice>(StringComparer.OrdinalIgnoreCase);

    public ReconcileConflictsForm(IReadOnlyList<string> conflicts)
    {
        Text = "Reconcile changed files";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(820, 560);
        MinimumSize = new Size(680, 440);
        BackColor = GuardianTheme.Window;
        ForeColor = GuardianTheme.Ink;
        Font = new Font("Segoe UI", 9);
        ShowInTaskbar = false;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            Padding = new Padding(18),
            BackColor = GuardianTheme.Window
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 108));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));

        var intro = new Label
        {
            Dock = DockStyle.Fill,
            Text = "LOCAL + ONLINE CHANGED\r\n\r\n" +
                   "These files were changed differently in both places. Choose which complete file version GitPet should keep. " +
                   "Non-conflicting files are already being combined. Nothing will be sent online.",
            ForeColor = GuardianTheme.MutedInk,
            Font = new Font("Segoe UI", 9.5f),
            TextAlign = ContentAlignment.MiddleLeft
        };

        ConfigureGrid();
        foreach (var conflict in conflicts) _grid.Rows.Add(conflict, null);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 12, 0, 0),
            BackColor = GuardianTheme.Window
        };

        var apply = new Button
        {
            Text = "Use selected versions",
            Width = 170,
            Height = 38,
            BackColor = GuardianTheme.Violet,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        apply.FlatAppearance.BorderColor = GuardianTheme.HotPinkSoft;
        apply.Click += (_, _) => AcceptChoices();

        var cancel = new Button
        {
            Text = "Cancel",
            Width = 100,
            Height = 38,
            BackColor = GuardianTheme.SurfaceRaised,
            ForeColor = GuardianTheme.Ink,
            FlatStyle = FlatStyle.Flat,
            DialogResult = DialogResult.Cancel,
            Margin = new Padding(8, 0, 0, 0)
        };
        cancel.FlatAppearance.BorderColor = GuardianTheme.Border;

        buttons.Controls.Add(apply);
        buttons.Controls.Add(cancel);
        root.Controls.Add(intro, 0, 0);
        root.Controls.Add(_grid, 0, 1);
        root.Controls.Add(buttons, 0, 2);
        Controls.Add(root);

        AcceptButton = apply;
        CancelButton = cancel;
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.MultiSelect = false;
        _grid.RowHeadersVisible = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.BackgroundColor = GuardianTheme.Surface;
        _grid.BorderStyle = BorderStyle.None;
        _grid.GridColor = GuardianTheme.BorderSoft;
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = GuardianTheme.SurfaceSoft;
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = GuardianTheme.MutedInk;
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        _grid.DefaultCellStyle.BackColor = GuardianTheme.Surface;
        _grid.DefaultCellStyle.ForeColor = GuardianTheme.Ink;
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(57, 42, 77);
        _grid.DefaultCellStyle.SelectionForeColor = Color.White;
        _grid.RowTemplate.Height = 36;

        var fileColumn = new DataGridViewTextBoxColumn
        {
            Name = "File",
            HeaderText = "CONFLICTING FILE",
            ReadOnly = true,
            FillWeight = 62
        };
        var choiceColumn = new DataGridViewComboBoxColumn
        {
            Name = "Choice",
            HeaderText = "KEEP",
            FillWeight = 38,
            FlatStyle = FlatStyle.Flat,
            DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton
        };
        choiceColumn.Items.AddRange(LocalChoice, OnlineChoice);

        _grid.Columns.Add(fileColumn);
        _grid.Columns.Add(choiceColumn);
    }

    private void AcceptChoices()
    {
        var result = new Dictionary<string, ReconcileChoice>(StringComparer.OrdinalIgnoreCase);
        foreach (DataGridViewRow row in _grid.Rows)
        {
            var path = Convert.ToString(row.Cells["File"].Value) ?? "";
            var selected = Convert.ToString(row.Cells["Choice"].Value) ?? "";
            if (string.IsNullOrWhiteSpace(selected))
            {
                MessageBox.Show(this,
                    $"Choose which version to keep for:\r\n\r\n{path}",
                    "Choice required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            result[path] = selected == LocalChoice
                ? ReconcileChoice.Local
                : ReconcileChoice.Online;
        }

        Choices = result;
        DialogResult = DialogResult.OK;
        Close();
    }
}
