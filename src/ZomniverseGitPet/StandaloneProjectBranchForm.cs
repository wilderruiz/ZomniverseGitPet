namespace ZomniverseGitPet;

internal sealed class StandaloneProjectBranchForm : Form
{
    private readonly ComboBox _branches = new();

    public string SelectedBranch =>
        _branches.SelectedItem?.ToString() ?? "";

    public StandaloneProjectBranchForm(
        string repositoryLabel,
        string currentBranch,
        IReadOnlyList<string> branches)
    {
        Text = "Standalone project branch";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(620, 270);
        BackColor = GuardianTheme.Surface;
        ForeColor = GuardianTheme.Ink;
        Font = new Font("Segoe UI", 9.5f);

        var title = new Label
        {
            AutoSize = true,
            Text = "PROJECT REMOTE BRANCH",
            Font = new Font("Segoe UI", 15f, FontStyle.Bold),
            ForeColor = GuardianTheme.Ink,
            Location = new Point(24, 22)
        };

        var description = new Label
        {
            AutoSize = false,
            Width = 570,
            Height = 58,
            Location = new Point(24, 58),
            Text =
                $"{repositoryLabel}\r\n" +
                "Choose which existing standalone remote branch this logical project should Get from and Send to. " +
                "The parent repository branch is not changed.",
            ForeColor = GuardianTheme.MutedInk
        };

        _branches.DropDownStyle = ComboBoxStyle.DropDownList;
        _branches.Location = new Point(24, 128);
        _branches.Width = 570;
        _branches.BackColor = GuardianTheme.SurfaceRaised;
        _branches.ForeColor = GuardianTheme.Ink;
        foreach (var branch in branches) _branches.Items.Add(branch);

        var currentIndex = branches
            .Select((value, index) => (value, index))
            .FirstOrDefault(item =>
                item.value.Equals(currentBranch, StringComparison.OrdinalIgnoreCase))
            .index;
        if (_branches.Items.Count > 0)
            _branches.SelectedIndex =
                branches.Any(value => value.Equals(currentBranch, StringComparison.OrdinalIgnoreCase))
                    ? currentIndex
                    : 0;

        var cancel = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Width = 100,
            Height = 34,
            Location = new Point(376, 205),
            BackColor = GuardianTheme.SurfaceRaised,
            ForeColor = GuardianTheme.Ink,
            FlatStyle = FlatStyle.Flat
        };
        cancel.FlatAppearance.BorderColor = GuardianTheme.Border;

        var use = new Button
        {
            Text = "Use branch",
            DialogResult = DialogResult.OK,
            Width = 118,
            Height = 34,
            Location = new Point(484, 205),
            BackColor = GuardianTheme.Violet,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        use.FlatAppearance.BorderColor = GuardianTheme.HotPinkSoft;

        Controls.AddRange([title, description, _branches, cancel, use]);
        AcceptButton = use;
        CancelButton = cancel;
    }
}
