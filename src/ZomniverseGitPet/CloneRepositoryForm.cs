namespace ZomniverseGitPet;

internal sealed class CloneRepositoryForm : Form
{
    private readonly TextBox _repository = new();
    private readonly TextBox _destination = new();
    private readonly Label _validation = new();
    private readonly ToolTip _tips = new() { InitialDelay = 300, AutoPopDelay = 12000 };

    public CloneRepositoryForm()
    {
        Text = "Copy repository to this computer";
        Icon = AppIconProvider.Icon;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(820, 470);
        BackColor = GuardianTheme.Window;
        ForeColor = GuardianTheme.Ink;
        Font = new Font("Segoe UI", 9);
        WindowChrome.ApplyGuardianChrome(this);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = GuardianTheme.Window
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 105));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildFields(), 0, 1);
        root.Controls.Add(_validation, 0, 2);
        root.Controls.Add(BuildFooter(), 0, 3);
        Controls.Add(root);

        _validation.Dock = DockStyle.Fill;
        _validation.Padding = new Padding(28, 8, 28, 4);
        _validation.ForeColor = GuardianTheme.MutedInk;
        _validation.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        _validation.Text = "Paste owner/repository or a clone URL. GitPet will preserve the repository's Git history.";

        _repository.TextChanged += (_, _) => UpdateDestination();
    }

    public GitRepositoryAddress Address =>
        GitRepositoryAddressParser.TryParse(_repository.Text, out var address)
            ? address
            : new GitRepositoryAddress(string.Empty, string.Empty, false);

    public string DestinationPath => _destination.Text.Trim();

    private Control BuildHeader()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(28, 14, 28, 12),
            BackColor = GuardianTheme.SurfaceRaised
        };
        panel.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Clone means copy a repository and all its version history onto this PC. GitPet then adds the copy to Projects.",
            ForeColor = GuardianTheme.MutedInk,
            Font = new Font("Segoe UI", 9.5f),
            TextAlign = ContentAlignment.BottomLeft
        });
        panel.Controls.Add(new Label
        {
            Dock = DockStyle.Top,
            Height = 42,
            Text = "📥  COPY REPOSITORY TO THIS COMPUTER",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        });
        return panel;
    }

    private Control BuildFields()
    {
        var outer = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(28, 18, 28, 6),
            BackColor = GuardianTheme.Window
        };
        var card = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 3,
            Padding = new Padding(20, 14, 20, 14),
            BackColor = GuardianTheme.Surface
        };
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        ConfigureTextBox(_repository);
        ConfigureTextBox(_destination);
        _repository.PlaceholderText = "owner/repository or https://github.com/owner/repository.git";
        _destination.PlaceholderText = "Local destination folder";

        card.Controls.Add(FieldLabel("REPOSITORY"), 0, 0);
        card.Controls.Add(_repository, 1, 0);
        card.SetColumnSpan(_repository, 2);
        card.Controls.Add(FieldLabel("LOCAL COPY"), 0, 1);
        card.Controls.Add(_destination, 1, 1);

        var browse = MakeButton("Browse…", 110, GuardianTheme.SurfaceSoft, GuardianTheme.Border);
        browse.Click += (_, _) => BrowseDestination();
        card.Controls.Add(browse, 2, 1);

        var note = new Label
        {
            Dock = DockStyle.Fill,
            Text = "After cloning, GitPet opens Project Scope + Repository Hygiene so generated, private, or irrelevant files can stay outside the tracked project view.",
            ForeColor = GuardianTheme.FaintInk,
            Font = new Font("Segoe UI", 8.75f),
            TextAlign = ContentAlignment.TopLeft,
            Padding = new Padding(0, 8, 0, 0)
        };
        card.SetColumnSpan(note, 3);
        card.Controls.Add(note, 0, 2);

        outer.Controls.Add(card);
        return outer;
    }

    private Control BuildFooter()
    {
        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(16, 16, 22, 12),
            BackColor = GuardianTheme.SurfaceRaised
        };

        var clone = MakeButton("Clone", 120, GuardianTheme.Violet, GuardianTheme.HotPink);
        var cancel = MakeButton("Cancel", 100, GuardianTheme.SurfaceSoft, GuardianTheme.Border);
        clone.Click += (_, _) => TryAccept();
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        _tips.SetToolTip(clone, "Copy this repository to your computer and add the new local copy to GitPet Projects.");
        footer.Controls.Add(clone);
        footer.Controls.Add(cancel);
        AcceptButton = clone;
        CancelButton = cancel;
        return footer;
    }

    private void TryAccept()
    {
        if (!GitRepositoryAddressParser.TryParse(_repository.Text, out var address))
        {
            _validation.Text = "Enter owner/repository, a Git URL, or another cloneable repository address.";
            _validation.ForeColor = GuardianTheme.Warning;
            return;
        }

        if (string.IsNullOrWhiteSpace(_destination.Text))
            _destination.Text = Path.Combine(GitRepositoryAddressParser.DefaultCloneRoot, address.RepositoryName);

        if (Directory.Exists(_destination.Text) && Directory.EnumerateFileSystemEntries(_destination.Text).Any())
        {
            _validation.Text = "That destination already contains files. Choose an empty/new folder so GitPet never overwrites existing work.";
            _validation.ForeColor = GuardianTheme.Warning;
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private void UpdateDestination()
    {
        if (!GitRepositoryAddressParser.TryParse(_repository.Text, out var address)) return;
        if (string.IsNullOrWhiteSpace(_destination.Text) ||
            _destination.Text.StartsWith(GitRepositoryAddressParser.DefaultCloneRoot, StringComparison.OrdinalIgnoreCase))
        {
            _destination.Text = Path.Combine(GitRepositoryAddressParser.DefaultCloneRoot, address.RepositoryName);
        }
    }

    private void BrowseDestination()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose the parent folder for the repository copy.",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
            InitialDirectory = Directory.Exists(GitRepositoryAddressParser.DefaultCloneRoot)
                ? GitRepositoryAddressParser.DefaultCloneRoot
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _destination.Text = GitRepositoryAddressParser.TryParse(_repository.Text, out var address)
            ? Path.Combine(dialog.SelectedPath, address.RepositoryName)
            : dialog.SelectedPath;
    }

    private static Label FieldLabel(string text) => new()
    {
        Dock = DockStyle.Fill,
        Text = text,
        ForeColor = GuardianTheme.MutedInk,
        Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
        TextAlign = ContentAlignment.MiddleLeft
    };

    private static void ConfigureTextBox(TextBox box)
    {
        box.Dock = DockStyle.Fill;
        box.Margin = new Padding(6, 11, 8, 11);
        box.BackColor = GuardianTheme.Window;
        box.ForeColor = GuardianTheme.Ink;
        box.BorderStyle = BorderStyle.FixedSingle;
        box.Font = new Font("Cascadia Mono", 9.25f);
    }

    private static Button MakeButton(string text, int width, Color fill, Color border)
    {
        var button = new Button
        {
            Text = text,
            Width = width,
            Height = 38,
            Margin = new Padding(7, 0, 0, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = fill,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = border;
        return button;
    }
}
