namespace ZomniverseGitPet;

/* ==========================================================================
   FEATURE: CREATE A NEW GITHUB REPOSITORY
   FUNCTION:
   Creates a brand-new repository under the authenticated GitHub account and
   pairs it with a new local folder without disturbing the current project.

   SAFETY:
   - Never overwrites a non-empty local folder.
   - Checks the authenticated account for an exact repository-name match first.
   - Never commits or pushes automatically.
   ========================================================================== */
internal sealed class NewGitHubRepositoryForm : Form
{
    private readonly string _login;
    private readonly TextBox _name = new();
    private readonly TextBox _parent = new();
    private readonly TextBox _description = new();
    private readonly RadioButton _private = new();
    private readonly Label _destination = new();
    private readonly Label _validation = new();

    public NewGitHubRepositoryForm(string login, string initialParent)
    {
        _login = login.Trim();

        Text = "Create new GitHub repository";
        Icon = AppIconProvider.Icon;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(820, 600);
        BackColor = GuardianTheme.Window;
        ForeColor = GuardianTheme.Ink;
        Font = new Font("Segoe UI", 9.5f);
        WindowChrome.ApplyGuardianChrome(this);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = GuardianTheme.Window,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildAccountCard(), 0, 1);
        root.Controls.Add(BuildFields(initialParent), 0, 2);
        root.Controls.Add(BuildFooter(), 0, 3);
        Controls.Add(root);

        Shown += (_, _) =>
        {
            _name.Focus();
            _name.SelectAll();
            RefreshDestination();
        };
    }

    public string RepositoryName => GitHubAccountService.SuggestRepositoryName(_name.Text);
    public string ParentFolder => _parent.Text.Trim();
    public string DestinationPath => Path.Combine(ParentFolder, RepositoryName);
    public bool IsPrivate => _private.Checked;
    public string DescriptionText => _description.Text.Trim();

    private Control BuildHeader()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(30, 16, 30, 12),
            BackColor = GuardianTheme.SurfaceRaised
        };

        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 42,
            Text = "◇  CREATE A NEW GITHUB REPOSITORY",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 15.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        var subtitle = new Label
        {
            Dock = DockStyle.Fill,
            Text = "A new online repository and a matching local folder. Your current GitPet project is not replaced.",
            ForeColor = GuardianTheme.MutedInk,
            Font = new Font("Segoe UI", 9.7f),
            TextAlign = ContentAlignment.TopLeft
        };

        panel.Controls.Add(subtitle);
        panel.Controls.Add(title);
        return panel;
    }

    private Control BuildAccountCard()
    {
        var outer = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(30, 12, 30, 10),
            BackColor = GuardianTheme.Window
        };
        var card = new OnboardingSurfacePanel
        {
            Dock = DockStyle.Fill,
            FillColor = GuardianTheme.SurfaceRaised,
            BackColor = GuardianTheme.SurfaceRaised,
            BorderColor = GuardianTheme.Border,
            CornerRadius = 10,
            Padding = new Padding(18, 10, 18, 10)
        };
        var label = new Label
        {
            Dock = DockStyle.Fill,
            Text = $"GITHUB ACCOUNT    {_login}  ✓\r\nGitPet will check this account for the repository name before creating anything online.",
            ForeColor = GuardianTheme.Healthy,
            Font = new Font("Segoe UI", 9.2f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        card.Controls.Add(label);
        outer.Controls.Add(card);
        return outer;
    }

    private Control BuildFields(string initialParent)
    {
        var outer = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(30, 12, 30, 10),
            BackColor = GuardianTheme.Window
        };
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(22, 18, 22, 16),
            BackColor = GuardianTheme.SurfaceSoft
        };

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 6,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = GuardianTheme.SurfaceSoft
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        grid.Controls.Add(Caption("REPOSITORY NAME"), 0, 0);
        ConfigureInput(_name, "new-repository");
        _name.TextChanged += (_, _) => RefreshDestination();
        grid.Controls.Add(_name, 1, 0);
        grid.SetColumnSpan(_name, 2);

        grid.Controls.Add(Caption("LOCAL PARENT"), 0, 1);
        ConfigureInput(_parent, "");
        _parent.Text = NormalizeInitialParent(initialParent);
        _parent.TextChanged += (_, _) => RefreshDestination();
        grid.Controls.Add(_parent, 1, 1);

        var browse = MakeButton("Browse…", GuardianActionKind.Standard, 102);
        browse.Click += (_, _) => BrowseParent();
        grid.Controls.Add(browse, 2, 1);

        grid.Controls.Add(Caption("LOCAL FOLDER"), 0, 2);
        _destination.Dock = DockStyle.Fill;
        _destination.ForeColor = GuardianTheme.MutedInk;
        _destination.Font = new Font("Cascadia Mono", 8.8f);
        _destination.TextAlign = ContentAlignment.MiddleLeft;
        _destination.AutoEllipsis = true;
        grid.Controls.Add(_destination, 1, 2);
        grid.SetColumnSpan(_destination, 2);

        grid.Controls.Add(Caption("VISIBILITY"), 0, 3);
        var visibility = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = GuardianTheme.SurfaceSoft,
            Padding = new Padding(0, 9, 0, 0)
        };
        _private.Text = "Private";
        _private.Checked = true;
        _private.AutoSize = true;
        _private.ForeColor = GuardianTheme.Ink;
        _private.Margin = new Padding(0, 4, 28, 0);
        visibility.Controls.Add(_private);
        visibility.Controls.Add(new RadioButton
        {
            Text = "Public",
            AutoSize = true,
            ForeColor = GuardianTheme.Ink,
            Margin = new Padding(0, 4, 0, 0)
        });
        grid.Controls.Add(visibility, 1, 3);
        grid.SetColumnSpan(visibility, 2);

        grid.Controls.Add(Caption("DESCRIPTION"), 0, 4);
        ConfigureInput(_description, "Optional");
        _description.Multiline = true;
        _description.Dock = DockStyle.Fill;
        grid.Controls.Add(_description, 1, 4);
        grid.SetColumnSpan(_description, 2);

        _validation.Dock = DockStyle.Fill;
        _validation.ForeColor = GuardianTheme.MutedInk;
        _validation.Font = new Font("Segoe UI", 8.8f);
        _validation.TextAlign = ContentAlignment.MiddleLeft;
        grid.Controls.Add(_validation, 0, 5);
        grid.SetColumnSpan(_validation, 3);

        card.Controls.Add(grid);
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
            Padding = new Padding(18, 18, 28, 12),
            BackColor = GuardianTheme.SurfaceRaised
        };

        var cancel = MakeButton("Cancel", GuardianActionKind.Standard, 110);
        cancel.DialogResult = DialogResult.Cancel;

        var create = MakeButton("Create repository", GuardianActionKind.Primary, 170);
        create.Click += (_, _) => Finish();

        footer.Controls.Add(cancel);
        footer.Controls.Add(create);
        AcceptButton = create;
        CancelButton = cancel;
        return footer;
    }

    private void BrowseParent()
    {
        using var browse = new FolderBrowserDialog
        {
            Description = "Choose the parent folder where GitPet should create the new repository folder.",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
            InitialDirectory = Directory.Exists(ParentFolder)
                ? ParentFolder
                : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };
        if (browse.ShowDialog(this) != DialogResult.OK) return;
        _parent.Text = browse.SelectedPath;
    }

    private void Finish()
    {
        var name = RepositoryName;
        if (string.IsNullOrWhiteSpace(name))
        {
            SetValidation("Enter a repository name first.", GuardianTheme.Warning);
            _name.Focus();
            return;
        }

        if (!string.Equals(_name.Text.Trim(), name, StringComparison.Ordinal))
            _name.Text = name;

        if (IsReservedWindowsName(name))
        {
            SetValidation("That repository name is reserved by Windows. Choose another name.", GuardianTheme.Warning);
            _name.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(ParentFolder))
        {
            SetValidation("Choose the local parent folder first.", GuardianTheme.Warning);
            _parent.Focus();
            return;
        }

        try
        {
            _ = Path.GetFullPath(DestinationPath);
        }
        catch (Exception ex)
        {
            SetValidation("The local folder path is not valid: " + ex.Message, GuardianTheme.Warning);
            return;
        }

        if (File.Exists(DestinationPath))
        {
            SetValidation("A file already exists at that local destination.", GuardianTheme.Warning);
            return;
        }

        if (Directory.Exists(DestinationPath) &&
            Directory.EnumerateFileSystemEntries(DestinationPath).Any())
        {
            SetValidation("That local folder already contains files. Choose a new or empty destination.", GuardianTheme.Warning);
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private void RefreshDestination()
    {
        var name = GitHubAccountService.SuggestRepositoryName(_name.Text);
        if (string.IsNullOrWhiteSpace(_parent.Text) || string.IsNullOrWhiteSpace(name))
        {
            _destination.Text = "Choose a parent folder and repository name.";
            return;
        }

        try
        {
            _destination.Text = Path.Combine(_parent.Text.Trim(), name);
        }
        catch
        {
            _destination.Text = "Local destination is not valid yet.";
        }
    }

    private void SetValidation(string message, Color color)
    {
        _validation.Text = message;
        _validation.ForeColor = color;
    }

    private static string NormalizeInitialParent(string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && Directory.Exists(value))
            return Path.GetFullPath(value);

        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrWhiteSpace(documents) && Directory.Exists(documents))
            return documents;

        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    private static bool IsReservedWindowsName(string name)
    {
        var stem = name.Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? name;
        return stem.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
               stem.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
               stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
               stem.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
               (stem.Length == 4 &&
                (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
                 stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
                char.IsDigit(stem[3]) && stem[3] != '0');
    }

    private static Label Caption(string text) => new()
    {
        Dock = DockStyle.Fill,
        Text = text,
        ForeColor = GuardianTheme.FaintInk,
        Font = new Font("Segoe UI", 8.4f, FontStyle.Bold),
        TextAlign = ContentAlignment.MiddleLeft
    };

    private static void ConfigureInput(TextBox input, string placeholder)
    {
        input.Dock = DockStyle.Fill;
        input.BackColor = GuardianTheme.Console;
        input.ForeColor = GuardianTheme.Ink;
        input.BorderStyle = BorderStyle.FixedSingle;
        input.Font = new Font("Segoe UI", 10f);
        input.PlaceholderText = placeholder;
        input.Margin = new Padding(0, 6, 8, 6);
    }

    private static GuardianActionButton MakeButton(string text, GuardianActionKind kind, int width) => new()
    {
        Text = text,
        Kind = kind,
        Width = width,
        Height = 38,
        Margin = new Padding(6, 0, 0, 0)
    };
}
