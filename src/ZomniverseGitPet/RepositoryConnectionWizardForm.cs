namespace ZomniverseGitPet;

/* ==========================================================================
   PATCH: GUIDED REPOSITORY CONNECTION
   DATE.TIME: 2026-09-11 18:12 +03:00
   Guide authenticated users through repository connection or creation.
   ========================================================================== */
internal sealed class RepositoryConnectionWizardForm : Form
{
    private readonly string _projectName;
    private readonly string _projectPath;
    private readonly GitHubAccountStatus _account;
    private readonly GitHubAccountService _github;
    private readonly CancellationTokenSource _lifetime = new();

    private readonly Panel _body = new();
    private readonly Label _status = new();
    private readonly OnboardingButton _back = new();
    private readonly OnboardingButton _primary = new();
    private readonly OnboardingButton _cancel = new();

    private TextBox? _remoteUrl;
    private TextBox? _repositoryName;
    private TextBox? _description;
    private RadioButton? _privateVisibility;
    private DataGridView? _candidates;
    private IReadOnlyList<GitHubRepositoryCandidate> _candidateModels = [];
    private bool _busy;

    public RepositoryConnectionWizardForm(
        string projectName,
        string projectPath,
        GitHubAccountStatus account,
        GitHubAccountService github)
    {
        _projectName = string.IsNullOrWhiteSpace(projectName) ? "Project" : projectName.Trim();
        _projectPath = projectPath;
        _account = account;
        _github = github;

        Text = "Connect project online";
        Icon = AppIconProvider.Icon;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(860, 650);
        Size = new Size(1040, 760);
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        BackColor = GuardianTheme.Window;
        ForeColor = GuardianTheme.Ink;
        Font = new Font("Segoe UI", 9.5f);
        WindowChrome.ApplyGuardianChrome(this);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = GuardianTheme.Window
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 108));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 168));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildProjectSummary(), 0, 1);

        _body.Dock = DockStyle.Fill;
        _body.BackColor = GuardianTheme.Window;
        _body.Padding = new Padding(30, 18, 30, 18);
        root.Controls.Add(_body, 0, 2);
        root.Controls.Add(BuildFooter(), 0, 3);
        Controls.Add(root);

        Shown += (_, _) => ShowQuestion();
        FormClosed += (_, _) =>
        {
            _lifetime.Cancel();
            _lifetime.Dispose();
        };
    }

    public string RemoteUrl { get; private set; } = string.Empty;

    private Control BuildHeader()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(34, 18, 34, 14),
            BackColor = GuardianTheme.SurfaceRaised
        };

        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 44,
            Text = "◇  CONNECT THIS PROJECT ONLINE",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        var subtitle = new Label
        {
            Dock = DockStyle.Fill,
            Text = "GitPet knows your GitHub account. Now choose where this project lives online.",
            ForeColor = GuardianTheme.MutedInk,
            Font = new Font("Segoe UI", 10),
            TextAlign = ContentAlignment.TopLeft
        };

        panel.Controls.Add(subtitle);
        panel.Controls.Add(title);
        return panel;
    }

    private Control BuildProjectSummary()
    {
        var outer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = GuardianTheme.Window,
            Padding = new Padding(30, 16, 30, 10)
        };
        var card = new OnboardingSurfacePanel
        {
            Dock = DockStyle.Fill,
            FillColor = GuardianTheme.SurfaceRaised,
            BackColor = GuardianTheme.SurfaceRaised,
            BorderColor = GuardianTheme.Border,
            CornerRadius = 10,
            Padding = new Padding(20, 12, 20, 12)
        };
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Margin = Padding.Empty,
            BackColor = GuardianTheme.SurfaceRaised
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < 3; i++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 33.333f));

        AddSummaryRow(grid, 0, "PROJECT", _projectName, Color.White);
        AddSummaryRow(grid, 1, "LOCAL FOLDER", _projectPath, GuardianTheme.MutedInk);
        AddSummaryRow(grid, 2, "GITHUB ACCOUNT", _account.Login + "  ✓", GuardianTheme.Healthy);

        card.Controls.Add(grid);
        outer.Controls.Add(card);
        return outer;
    }

    private static void AddSummaryRow(TableLayoutPanel grid, int row, string caption, string value, Color color)
    {
        grid.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = caption,
            ForeColor = GuardianTheme.FaintInk,
            Font = new Font("Segoe UI", 8.2f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, row);
        grid.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = value,
            ForeColor = color,
            Font = new Font(row == 1 ? "Cascadia Mono" : "Segoe UI", row == 1 ? 8.5f : 9.5f, row == 0 ? FontStyle.Bold : FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        }, 1, row);
    }

    private Control BuildFooter()
    {
        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(18, 18, 28, 14),
            BackColor = GuardianTheme.SurfaceRaised
        };

        ConfigureButton(_cancel, "Cancel", GuardianTheme.SurfaceSoft, GuardianTheme.Border, 112);
        ConfigureButton(_primary, "Continue", GuardianTheme.Violet, GuardianTheme.HotPink, 190);
        ConfigureButton(_back, "Back", GuardianTheme.SurfaceSoft, GuardianTheme.Border, 112);
        _cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        _back.Click += (_, _) => ShowQuestion();

        _status.AutoSize = false;
        _status.Width = 410;
        _status.Height = 42;
        _status.Margin = new Padding(10, 5, 10, 0);
        _status.ForeColor = GuardianTheme.MutedInk;
        _status.Font = new Font("Segoe UI", 8.8f);
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _status.AutoEllipsis = true;

        footer.Controls.Add(_cancel);
        footer.Controls.Add(_primary);
        footer.Controls.Add(_back);
        footer.Controls.Add(_status);
        return footer;
    }

    private void ShowQuestion()
    {
        SetBusy(false);
        _body.Controls.Clear();
        _back.Visible = false;
        _primary.Visible = false;
        _status.Text = "Nothing will be sent online until you explicitly use Send ↑.";
        _status.ForeColor = GuardianTheme.MutedInk;

        var stack = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 300,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = GuardianTheme.Window,
            Margin = Padding.Empty
        };
        stack.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        stack.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        stack.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        stack.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));

        stack.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Have you already created this project on GitHub?",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 13, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(4, 0, 0, 8)
        }, 0, 0);

        var yes = ChoiceButton("YES — IT ALREADY EXISTS", "Connect an existing GitHub repository.");
        var no = ChoiceButton("NO — CREATE IT FOR ME", "Create an empty GitHub repository, then connect it locally.");
        var unsure = ChoiceButton("I'M NOT SURE", "Let GitPet look for likely matches in your GitHub account.");
        yes.Click += (_, _) => ShowExistingRepository();
        no.Click += (_, _) => ShowCreateRepository();
        unsure.Click += async (_, _) => await ShowRepositoryFinderAsync();
        stack.Controls.Add(yes, 0, 1);
        stack.Controls.Add(no, 0, 2);
        stack.Controls.Add(unsure, 0, 3);
        _body.Controls.Add(stack);
    }

    private void ShowExistingRepository()
    {
        _body.Controls.Clear();
        _back.Visible = true;
        _primary.Visible = true;
        _primary.Text = "Connect project";
        _status.Text = "Connecting adds origin only. GitPet will not pull, push, stage, or commit.";
        _status.ForeColor = GuardianTheme.MutedInk;

        var card = CreateBodyCard();
        var title = BodyTitle("WHERE DOES THIS PROJECT LIVE?");
        _remoteUrl = new TextBox
        {
            Dock = DockStyle.Top,
            Height = 36,
            BackColor = Color.FromArgb(13, 11, 20),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Cascadia Mono", 10),
            PlaceholderText = "owner/repository or https://github.com/owner/repository.git"
        };
        var help = BodyText("Paste the repository address shown by GitHub. GitPet accepts owner/repository, HTTPS, or SSH addresses.");

        card.Controls.Add(help);
        card.Controls.Add(_remoteUrl);
        card.Controls.Add(title);
        _body.Controls.Add(card);

        _primary.Click -= PrimaryCreateClick;
        _primary.Click -= PrimaryCandidateClick;
        _primary.Click -= PrimaryExistingClick;
        _primary.Click += PrimaryExistingClick;
        _remoteUrl.Focus();
    }

    private async void PrimaryExistingClick(object? sender, EventArgs e)
    {
        var value = _remoteUrl?.Text.Trim() ?? string.Empty;
        if (!GitRepositoryAddressParser.TryParse(value, out var address))
        {
            SetStatus("That repository address does not look valid yet.", GuardianTheme.Warning);
            _remoteUrl?.Focus();
            return;
        }

        RemoteUrl = address.CloneSource;
        DialogResult = DialogResult.OK;
        Close();
        await Task.CompletedTask;
    }

    private void ShowCreateRepository()
    {
        _body.Controls.Clear();
        _back.Visible = true;
        _primary.Visible = true;
        _primary.Text = "Create repository";
        _status.Text = "GitPet will create the repository and connect origin. It will not push anything.";
        _status.ForeColor = GuardianTheme.MutedInk;

        var card = CreateBodyCard();
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5,
            Padding = new Padding(0),
            Margin = Padding.Empty,
            BackColor = GuardianTheme.SurfaceRaised
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        grid.Controls.Add(BodyTitle("CREATE A GITHUB REPOSITORY"), 0, 0);
        grid.SetColumnSpan(grid.GetControlFromPosition(0, 0)!, 2);
        grid.Controls.Add(FieldCaption("OWNER"), 0, 1);
        grid.Controls.Add(FieldValue(_account.Login), 1, 1);
        grid.Controls.Add(FieldCaption("REPOSITORY NAME"), 0, 2);
        _repositoryName = CreateInput(GitHubAccountService.SuggestRepositoryName(_projectName));
        grid.Controls.Add(_repositoryName, 1, 2);
        grid.Controls.Add(FieldCaption("VISIBILITY"), 0, 3);

        var visibility = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = GuardianTheme.SurfaceRaised,
            Padding = new Padding(0, 10, 0, 0)
        };
        _privateVisibility = new RadioButton
        {
            Text = "Private",
            Checked = true,
            AutoSize = true,
            ForeColor = GuardianTheme.Ink,
            Margin = new Padding(0, 4, 28, 0)
        };
        visibility.Controls.Add(_privateVisibility);
        visibility.Controls.Add(new RadioButton
        {
            Text = "Public",
            AutoSize = true,
            ForeColor = GuardianTheme.Ink,
            Margin = new Padding(0, 4, 0, 0)
        });
        grid.Controls.Add(visibility, 1, 3);
        grid.Controls.Add(FieldCaption("DESCRIPTION"), 0, 4);
        _description = CreateInput(string.Empty);
        _description.Multiline = true;
        _description.Height = 64;
        _description.PlaceholderText = "Optional";
        grid.Controls.Add(_description, 1, 4);

        card.Controls.Add(grid);
        _body.Controls.Add(card);

        _primary.Click -= PrimaryExistingClick;
        _primary.Click -= PrimaryCandidateClick;
        _primary.Click -= PrimaryCreateClick;
        _primary.Click += PrimaryCreateClick;
        _repositoryName.Focus();
    }

    private async void PrimaryCreateClick(object? sender, EventArgs e)
    {
        if (_busy) return;
        var requested = _repositoryName?.Text.Trim() ?? string.Empty;
        var name = GitHubAccountService.SuggestRepositoryName(requested);
        if (string.IsNullOrWhiteSpace(name))
        {
            SetStatus("Enter a repository name first.", GuardianTheme.Warning);
            return;
        }

        if (!string.Equals(requested, name, StringComparison.Ordinal))
            _repositoryName!.Text = name;

        SetBusy(true);
        SetStatus("Creating the GitHub repository…", GuardianTheme.Changes);
        try
        {
            var result = await _github.CreateRepositoryAsync(
                _account.Login,
                name,
                _privateVisibility?.Checked != false,
                _description?.Text.Trim() ?? string.Empty,
                _lifetime.Token);
            if (!result.Success)
            {
                SetStatus(result.Message, GuardianTheme.Warning);
                return;
            }

            RemoteUrl = result.RepositoryUrl;
            using var ready = new GuardianConfirmDialog(
                "Repository created",
                "ONLINE HOME READY  ✓",
                $"GitHub repository created:\r\n{_account.Login}/{name}\r\n\r\n" +
                "GitPet will connect this local project to it as origin.\r\n\r\n" +
                "Nothing has been sent online yet.",
                "Continue",
                showCancel: false);
            ready.ShowDialog(this);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (OperationCanceledException)
        {
            SetStatus("Repository creation was cancelled.", GuardianTheme.Warning);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task ShowRepositoryFinderAsync()
    {
        _body.Controls.Clear();
        _back.Visible = true;
        _primary.Visible = true;
        _primary.Text = "Use selected";
        _primary.Enabled = false;
        SetStatus("Checking your GitHub repositories…", GuardianTheme.Changes);

        var card = CreateBodyCard();
        var title = BodyTitle("POSSIBLE MATCHES");
        _candidates = CreateCandidateGrid();
        card.Controls.Add(_candidates);
        card.Controls.Add(title);
        _body.Controls.Add(card);

        _primary.Click -= PrimaryExistingClick;
        _primary.Click -= PrimaryCreateClick;
        _primary.Click -= PrimaryCandidateClick;
        _primary.Click += PrimaryCandidateClick;

        try
        {
            var folderName = Path.GetFileName(Path.TrimEndingDirectorySeparator(_projectPath));
            _candidateModels = await _github.FindRepositoriesAsync(
                _account.Login,
                [_projectName, folderName],
                _lifetime.Token);

            _candidates.Rows.Clear();
            foreach (var item in _candidateModels)
            {
                _candidates.Rows.Add(
                    item.NameWithOwner,
                    item.UpdatedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "—");
            }

            if (_candidateModels.Count == 0)
            {
                SetStatus("No likely GitHub repositories were found. You can create a new one instead.", GuardianTheme.Warning);
                _primary.Text = "Create new instead";
                _primary.Enabled = true;
                return;
            }

            _candidates.Rows[0].Selected = true;
            _primary.Enabled = true;
            SetStatus("Choose the repository that belongs to this local project. Nothing is changed until you continue.", GuardianTheme.MutedInk);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            SetStatus("GitPet could not check GitHub repositories. " + ex.Message, GuardianTheme.Warning);
            _primary.Text = "Create new instead";
            _primary.Enabled = true;
        }
    }

    private void PrimaryCandidateClick(object? sender, EventArgs e)
    {
        if (_candidateModels.Count == 0)
        {
            ShowCreateRepository();
            return;
        }

        var index = _candidates?.SelectedRows.Count > 0 ? _candidates.SelectedRows[0].Index : -1;
        if (index < 0 || index >= _candidateModels.Count)
        {
            SetStatus("Select one repository first.", GuardianTheme.Warning);
            return;
        }

        RemoteUrl = _candidateModels[index].Url;
        DialogResult = DialogResult.OK;
        Close();
    }

    private static OnboardingButton ChoiceButton(string title, string subtitle)
    {
        var button = new OnboardingButton
        {
            Dock = DockStyle.Fill,
            Text = title + "    ·    " + subtitle,
            Margin = new Padding(4, 6, 4, 6),
            FillColor = GuardianTheme.SurfaceRaised,
            BorderColor = GuardianTheme.Border,
            HoverColor = GuardianTheme.SurfaceSoft,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            CornerRadius = 9
        };
        return button;
    }

    private static OnboardingSurfacePanel CreateBodyCard() => new()
    {
        Dock = DockStyle.Fill,
        FillColor = GuardianTheme.SurfaceRaised,
        BackColor = GuardianTheme.SurfaceRaised,
        BorderColor = GuardianTheme.Border,
        CornerRadius = 10,
        Padding = new Padding(22, 18, 22, 18)
    };

    private static Label BodyTitle(string text) => new()
    {
        Dock = DockStyle.Top,
        Height = 44,
        Text = text,
        ForeColor = GuardianTheme.HotPinkSoft,
        Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
        TextAlign = ContentAlignment.MiddleLeft
    };

    private static Label BodyText(string text) => new()
    {
        Dock = DockStyle.Top,
        Height = 72,
        Text = text,
        ForeColor = GuardianTheme.MutedInk,
        Font = new Font("Segoe UI", 9.5f),
        TextAlign = ContentAlignment.TopLeft,
        Padding = new Padding(0, 14, 0, 0)
    };

    private static Label FieldCaption(string text) => new()
    {
        Dock = DockStyle.Fill,
        Text = text,
        ForeColor = GuardianTheme.FaintInk,
        Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
        TextAlign = ContentAlignment.MiddleLeft
    };

    private static Label FieldValue(string text) => new()
    {
        Dock = DockStyle.Fill,
        Text = text,
        ForeColor = GuardianTheme.Healthy,
        Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
        TextAlign = ContentAlignment.MiddleLeft
    };

    private static TextBox CreateInput(string text) => new()
    {
        Dock = DockStyle.Fill,
        Text = text,
        BackColor = Color.FromArgb(13, 11, 20),
        ForeColor = Color.White,
        BorderStyle = BorderStyle.FixedSingle,
        Font = new Font("Segoe UI", 9.5f),
        Margin = new Padding(0, 8, 0, 8)
    };

    private static DataGridView CreateCandidateGrid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = GuardianTheme.Surface,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            GridColor = GuardianTheme.BorderSoft,
            RowHeadersVisible = false,
            EnableHeadersVisualStyles = false,
            ColumnHeadersHeight = 30
        };
        grid.ColumnHeadersDefaultCellStyle.BackColor = GuardianTheme.SurfaceSoft;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = GuardianTheme.MutedInk;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
        grid.DefaultCellStyle.BackColor = GuardianTheme.Surface;
        grid.DefaultCellStyle.ForeColor = GuardianTheme.Ink;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(57, 42, 77);
        grid.DefaultCellStyle.SelectionForeColor = Color.White;
        grid.DefaultCellStyle.Font = new Font("Segoe UI", 9f);
        grid.RowTemplate.Height = 30;
        grid.Columns.Add("Repository", "REPOSITORY");
        grid.Columns.Add("Updated", "UPDATED");
        grid.Columns[0].FillWeight = 72;
        grid.Columns[1].FillWeight = 28;
        return grid;
    }

    private static void ConfigureButton(OnboardingButton button, string text, Color fill, Color border, int width)
    {
        button.Text = text;
        button.Width = width;
        button.Height = 42;
        button.Margin = new Padding(8, 0, 0, 0);
        button.FillColor = fill;
        button.BorderColor = border;
        button.HoverColor = ControlPaint.Light(fill, 0.08f);
        button.CornerRadius = 8;
        button.ForeColor = Color.White;
        button.Font = new Font("Segoe UI", 9, FontStyle.Bold);
    }

    private void SetStatus(string text, Color color)
    {
        _status.Text = text;
        _status.ForeColor = color;
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        UseWaitCursor = busy;
        _back.Enabled = !busy;
        _primary.Enabled = !busy;
        _cancel.Enabled = !busy;
        if (!busy && _candidates is not null && _candidateModels.Count == 0 && _primary.Text == "Use selected")
            _primary.Enabled = false;
    }
}
