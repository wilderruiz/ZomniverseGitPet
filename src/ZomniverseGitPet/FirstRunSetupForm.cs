namespace ZomniverseGitPet;

internal sealed class FirstRunSetupForm : Form
{
    private static readonly Color GitOrange = Color.FromArgb(240, 80, 50);
    private static readonly Color GitOrangeDark = Color.FromArgb(89, 42, 31);

    private readonly AppConfig _config;
    private readonly ConfigStore _configStore;
    private readonly GitService _git;
    private readonly AuditLog _audit;
    private readonly GitHubAccountService _github;
    private readonly Action<string> _guidePet;

    private readonly Label _connectionStatus = new();
    private readonly Label _selectedMode = new();
    private readonly TextBox _repository = new();
    private readonly TextBox _destination = new();
    private readonly Button _githubButton = new();
    private readonly Button _localButton = new();
    private readonly Button _refreshGitHub = new();
    private readonly Button _cloneButton = new();
    private readonly Button _openLocalButton = new();
    private readonly Button _finishButton = new();
    private readonly ToolTip _tips = new() { InitialDelay = 300, AutoPopDelay = 12000 };

    private GitHubAccountStatus? _githubStatus;
    private string _mode = GitPetConnectionModes.Unconfigured;
    private bool _busy;

    public FirstRunSetupForm(
        AppConfig config,
        ConfigStore configStore,
        GitService git,
        AuditLog audit,
        Action<string> guidePet)
    {
        _config = config;
        _configStore = configStore;
        _git = git;
        _audit = audit;
        _github = new GitHubAccountService(audit);
        _guidePet = guidePet;

        Text = "Welcome to ZomniverseGitPet";
        Icon = AppIconProvider.Icon;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(900, 690);
        Size = new Size(1040, 760);
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = GuardianTheme.Window;
        ForeColor = GuardianTheme.Ink;
        Font = new Font("Segoe UI", 9);
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
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 238));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildModePanel(), 0, 1);
        root.Controls.Add(BuildProjectPanel(), 0, 2);
        root.Controls.Add(BuildFooter(), 0, 3);
        Controls.Add(root);

        _repository.TextChanged += (_, _) => UpdateCloneDestination();
        Shown += async (_, _) =>
        {
            _guidePet("👋 HI! I'M GITPET\nChoose how we'll work");
            await RefreshGitHubAsync();
        };
    }

    private Control BuildHeader()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(30, 18, 30, 14),
            BackColor = GuardianTheme.SurfaceRaised
        };
        panel.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Connect GitHub for online repositories, or keep everything local. You can change your mind later.",
            ForeColor = GuardianTheme.MutedInk,
            Font = new Font("Segoe UI", 10.5f),
            TextAlign = ContentAlignment.BottomLeft
        });
        panel.Controls.Add(new Label
        {
            Dock = DockStyle.Top,
            Height = 48,
            Text = "🦊  WELCOME TO ZOMNIVERSE GITPET",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 17, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        });
        return panel;
    }

    private Control BuildModePanel()
    {
        var outer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(26, 20, 26, 10),
            BackColor = GuardianTheme.Window
        };
        outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        var githubCard = BuildModeCard(
            "🌐  GITHUB CONNECTED",
            "Best for repositories you want to Get ↓ and Send ↑ online. GitPet uses GitHub's browser login and never stores your password or a personal-access token.",
            _githubButton,
            "Connect GitHub",
            GuardianTheme.Violet,
            GuardianTheme.HotPink);

        _connectionStatus.Dock = DockStyle.Bottom;
        _connectionStatus.Height = 34;
        _connectionStatus.ForeColor = GuardianTheme.MutedInk;
        _connectionStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        _connectionStatus.TextAlign = ContentAlignment.MiddleLeft;
        githubCard.Controls.Add(_connectionStatus);
        _connectionStatus.BringToFront();

        _refreshGitHub.Text = "Check again";
        StyleButton(_refreshGitHub, 112, GuardianTheme.SurfaceSoft, GuardianTheme.Border);
        _refreshGitHub.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        _refreshGitHub.Location = new Point(githubCard.ClientSize.Width - 132, githubCard.ClientSize.Height - 48);
        githubCard.Resize += (_, _) =>
            _refreshGitHub.Location = new Point(githubCard.ClientSize.Width - _refreshGitHub.Width - 16, githubCard.ClientSize.Height - 47);
        _refreshGitHub.Click += async (_, _) => await RefreshGitHubAsync();
        githubCard.Controls.Add(_refreshGitHub);
        _refreshGitHub.BringToFront();

        var localCard = BuildModeCard(
            "🧡  LOCAL GIT ONLY",
            "Create restore points, review changes, run tests and track project history without connecting GitHub. Nothing is sent online unless you connect a remote later.",
            _localButton,
            "Use local Git only",
            GitOrangeDark,
            GitOrange);

        outer.Controls.Add(githubCard, 0, 0);
        outer.Controls.Add(localCard, 1, 0);

        _githubButton.Click += async (_, _) => await HandleGitHubButtonAsync();
        _localButton.Click += async (_, _) => await SelectModeAsync(GitPetConnectionModes.LocalGitOnly);
        _tips.SetToolTip(_githubButton, "Connect your own GitHub account through GitHub's browser authentication.");
        _tips.SetToolTip(_localButton, "Keep GitPet offline-first. Save creates local Git restore points; nothing is pushed automatically.");
        return outer;
    }

    private Panel BuildModeCard(
        string title,
        string body,
        Button action,
        string actionText,
        Color fill,
        Color border)
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(8),
            Padding = new Padding(20, 16, 20, 14),
            BackColor = GuardianTheme.Surface
        };

        action.Text = actionText;
        StyleButton(action, 165, fill, border);
        action.Dock = DockStyle.Bottom;

        var bodyLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = body,
            ForeColor = GuardianTheme.MutedInk,
            Font = new Font("Segoe UI", 9.5f),
            TextAlign = ContentAlignment.TopLeft,
            Padding = new Padding(0, 10, 0, 8)
        };
        var titleLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 34,
            Text = title,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };

        card.Controls.Add(bodyLabel);
        card.Controls.Add(titleLabel);
        card.Controls.Add(action);
        return card;
    }

    private Control BuildProjectPanel()
    {
        var outer = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(34, 8, 34, 16),
            BackColor = GuardianTheme.Window
        };

        var card = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 5,
            Padding = new Padding(22, 16, 22, 16),
            BackColor = GuardianTheme.Surface
        };
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 174));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var heading = new Label
        {
            Dock = DockStyle.Fill,
            Text = "📦  COPY A REPOSITORY TO THIS COMPUTER",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        card.SetColumnSpan(heading, 3);
        card.Controls.Add(heading, 0, 0);

        ConfigureTextBox(_repository);
        ConfigureTextBox(_destination);
        _repository.PlaceholderText = "owner/repository or https://github.com/owner/repository.git";
        _destination.PlaceholderText = "Local destination folder";

        card.Controls.Add(FieldLabel("REPOSITORY"), 0, 1);
        card.Controls.Add(_repository, 1, 1);
        card.SetColumnSpan(_repository, 2);
        card.Controls.Add(FieldLabel("SAVE COPY IN"), 0, 2);
        card.Controls.Add(_destination, 1, 2);

        var browse = new Button { Text = "Browse…" };
        StyleButton(browse, 140, GuardianTheme.SurfaceSoft, GuardianTheme.Border);
        browse.Click += (_, _) => BrowseDestination();
        card.Controls.Add(browse, 2, 2);

        _cloneButton.Text = "Clone to this computer";
        StyleButton(_cloneButton, 158, GuardianTheme.Violet, GuardianTheme.HotPink);
        _cloneButton.Enabled = false;
        _cloneButton.Click += async (_, _) => await CloneRepositoryAsync();
        card.Controls.Add(new Label(), 0, 3);
        card.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "GitPet preserves the repository's Git history, then opens the normal Project Scope + Repository Hygiene review before guarding it.",
            ForeColor = GuardianTheme.FaintInk,
            Font = new Font("Segoe UI", 8.5f),
            TextAlign = ContentAlignment.MiddleLeft
        }, 1, 3);
        card.Controls.Add(_cloneButton, 2, 3);
        _tips.SetToolTip(_cloneButton, "Copy this repository to your computer, preserve its Git history, then add it to GitPet Projects.");

        _selectedMode.Dock = DockStyle.Fill;
        _selectedMode.Text = "Choose GitHub Connected or Local Git Only above. You can also finish setup without choosing a project yet.";
        _selectedMode.ForeColor = GuardianTheme.MutedInk;
        _selectedMode.Font = new Font("Segoe UI", 9);
        _selectedMode.TextAlign = ContentAlignment.MiddleLeft;
        card.SetColumnSpan(_selectedMode, 3);
        card.Controls.Add(_selectedMode, 0, 4);

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
            Padding = new Padding(18, 19, 24, 15),
            BackColor = GuardianTheme.SurfaceRaised
        };

        _finishButton.Text = "Start GitPet";
        StyleButton(_finishButton, 132, GuardianTheme.Violet, GuardianTheme.HotPink);
        _finishButton.Enabled = false;
        _finishButton.Click += async (_, _) => await FinishAsync();

        _openLocalButton.Text = "Open existing project…";
        StyleButton(_openLocalButton, 178, GuardianTheme.SurfaceSoft, GuardianTheme.Border);
        _openLocalButton.Enabled = false;
        _openLocalButton.Click += async (_, _) => await OpenExistingProjectAsync();
        _tips.SetToolTip(_openLocalButton, "Open an existing local Git repository. Normal project setup remains available later from Projects.");

        footer.Controls.Add(_finishButton);
        footer.Controls.Add(_openLocalButton);
        return footer;
    }

    private async Task RefreshGitHubAsync()
    {
        if (_busy) return;
        SetBusy(true);
        try
        {
            _githubStatus = await _github.GetStatusAsync();
            _connectionStatus.Text = _githubStatus.Message;
            _connectionStatus.ForeColor = _githubStatus.Authenticated
                ? GuardianTheme.Healthy
                : GuardianTheme.Warning;

            _githubButton.Text = !_githubStatus.CliAvailable
                ? "Install GitHub CLI"
                : !_githubStatus.Authenticated
                    ? "Sign in to GitHub"
                    : "Use connected GitHub";

            if (_githubStatus.Authenticated && _mode == GitPetConnectionModes.GitHub)
                ApplyModeVisuals();
        }
        catch (Exception ex)
        {
            _connectionStatus.Text = ex.Message;
            _connectionStatus.ForeColor = GuardianTheme.Warning;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task HandleGitHubButtonAsync()
    {
        _githubStatus ??= await _github.GetStatusAsync();
        if (!_githubStatus.CliAvailable)
        {
            _guidePet("🧰 ONE-TIME SETUP\nInstall GitHub CLI first");
            if (_github.LaunchInstall(this))
            {
                _connectionStatus.Text = "Installation opened. Finish it in PowerShell, then click Check again.";
                _connectionStatus.ForeColor = GuardianTheme.Changes;
            }
            return;
        }

        if (!_githubStatus.Authenticated)
        {
            _guidePet("🔐 YOUR TURN\nSign in with GitHub");
            if (_github.LaunchSignIn(this))
            {
                _connectionStatus.Text = "GitHub sign-in opened. Complete the browser flow, then click Check again.";
                _connectionStatus.ForeColor = GuardianTheme.Changes;
            }
            return;
        }

        await SelectModeAsync(GitPetConnectionModes.GitHub);
    }

    private async Task SelectModeAsync(string mode)
    {
        _mode = GitPetConnectionModes.Normalize(mode);
        ApplyModeVisuals();
        _cloneButton.Enabled = true;
        _openLocalButton.Enabled = true;
        _finishButton.Enabled = true;

        if (_mode == GitPetConnectionModes.GitHub)
        {
            var who = string.IsNullOrWhiteSpace(_githubStatus?.Login) ? "your account" : _githubStatus!.Login;
            _selectedMode.Text = $"GitHub Connected selected · {who}. Paste owner/repository above to copy a project, or start GitPet now.";
            _guidePet("✅ GITHUB CONNECTED\nNow choose a project");
        }
        else
        {
            _selectedMode.Text = "Local Git Only selected. Restore points stay on this PC unless you deliberately connect a remote later.";
            _guidePet("🧡 LOCAL GIT MODE\nNothing leaves this PC");
        }

        await _github.RecordModeAsync(_mode);
    }

    private void ApplyModeVisuals()
    {
        var githubSelected = _mode == GitPetConnectionModes.GitHub;
        var localSelected = _mode == GitPetConnectionModes.LocalGitOnly;

        _githubButton.FlatAppearance.BorderSize = githubSelected ? 2 : 1;
        _githubButton.FlatAppearance.BorderColor = githubSelected ? GuardianTheme.HotPinkSoft : GuardianTheme.HotPink;
        _localButton.FlatAppearance.BorderSize = localSelected ? 2 : 1;
        _localButton.FlatAppearance.BorderColor = localSelected ? Color.FromArgb(255, 178, 132) : GitOrange;
    }

    private async Task CloneRepositoryAsync()
    {
        if (_busy) return;
        if (!GitRepositoryAddressParser.TryParse(_repository.Text, out var address))
        {
            ShowInline("Enter owner/repository, a Git URL, or another cloneable repository address.", GuardianTheme.Warning);
            return;
        }

        var destination = _destination.Text.Trim();
        if (string.IsNullOrWhiteSpace(destination))
        {
            destination = Path.Combine(GitRepositoryAddressParser.DefaultCloneRoot, address.RepositoryName);
            _destination.Text = destination;
        }

        SetBusy(true);
        _guidePet("📥 COPYING REPOSITORY\nThis can take a moment");
        ShowInline($"Copying {address.RepositoryName} to this computer…", GuardianTheme.Changes);
        try
        {
            var clone = await _git.CloneRepositoryAsync(address.CloneSource, destination);
            if (!clone.Success)
            {
                ShowInline("Clone stopped. " + clone.Output, GuardianTheme.Warning);
                _guidePet("⚠️ CLONE NEEDS HELP\nRead the setup window");
                return;
            }

            await ReviewClonedProjectAsync(destination);
            _config.RememberRepository(destination);
            CompleteConfiguration();
            await _audit.WriteAsync("repository_cloned_from_onboarding", new
            {
                sourceKind = address.IsGitHub ? "github" : "git",
                destination
            });

            _guidePet("🎉 PROJECT READY\nLet's get to work!");
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            ShowInline("Clone setup stopped. " + ex.Message, GuardianTheme.Warning);
            _guidePet("⚠️ I NEED A HAND\nCheck the setup window");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task ReviewClonedProjectAsync(string repositoryPath)
    {
        var suggestions = GitIgnoreAdvisor.Suggest(repositoryPath);
        using var preparation = new ProjectPreparationForm(
            repositoryPath,
            suggestions,
            initializeGit: false,
            chooseScope: true);
        if (preparation.ShowDialog(this) != DialogResult.OK) return;

        ProjectGitIgnoreComposer.SplitCombinedRules(
            preparation.AcceptedRules,
            out var scopeRules,
            out var suggestionRules);
        var changed = ProjectGitIgnoreComposer.ApplyReplacingScope(repositoryPath, scopeRules, suggestionRules);
        await _audit.WriteAsync("cloned_repository_scope_reviewed", new
        {
            repository = repositoryPath,
            gitignoreChanged = changed,
            scopeRules,
            suggestionRules
        });
    }

    private async Task OpenExistingProjectAsync()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose an existing Git repository. You can prepare ordinary folders later from Projects.",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
            InitialDirectory = _config.RepositoryPath ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        SetBusy(true);
        _guidePet("🔎 CHECKING PROJECT\nI'll verify the Git folder");
        try
        {
            var root = await _git.GetRepositoryRootAsync(dialog.SelectedPath);
            if (!root.Success || string.IsNullOrWhiteSpace(root.Output))
            {
                ShowInline("That folder is not ready as a Git repository yet. Finish setup, then use Projects → Prepare / reconfigure folder.", GuardianTheme.Warning);
                return;
            }

            var path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root.Output.Trim()));
            _config.RememberRepository(path);
            CompleteConfiguration();
            _guidePet("✅ PROJECT FOUND\nGuardian is ready");
            DialogResult = DialogResult.OK;
            Close();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task FinishAsync()
    {
        CompleteConfiguration();
        await _audit.WriteAsync("first_run_completed", new { mode = _mode, hasProject = !string.IsNullOrWhiteSpace(_config.RepositoryPath) });
        _guidePet("🦊 SETUP COMPLETE\nOpen Projects anytime");
        DialogResult = DialogResult.OK;
        Close();
    }

    private void CompleteConfiguration()
    {
        _config.OnboardingCompleted = true;
        _config.ConnectionMode = _mode;
        _configStore.Save(_config);
    }

    private void UpdateCloneDestination()
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
            Description = "Choose where GitPet should create the local repository folder.",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
            InitialDirectory = Directory.Exists(GitRepositoryAddressParser.DefaultCloneRoot)
                ? GitRepositoryAddressParser.DefaultCloneRoot
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        if (GitRepositoryAddressParser.TryParse(_repository.Text, out var address))
            _destination.Text = Path.Combine(dialog.SelectedPath, address.RepositoryName);
        else
            _destination.Text = dialog.SelectedPath;
    }

    private void ShowInline(string message, Color color)
    {
        _selectedMode.Text = message;
        _selectedMode.ForeColor = color;
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        UseWaitCursor = busy;
        _githubButton.Enabled = !busy;
        _localButton.Enabled = !busy;
        _refreshGitHub.Enabled = !busy;
        _repository.Enabled = !busy;
        _destination.Enabled = !busy;
        _cloneButton.Enabled = !busy && _mode != GitPetConnectionModes.Unconfigured;
        _openLocalButton.Enabled = !busy && _mode != GitPetConnectionModes.Unconfigured;
        _finishButton.Enabled = !busy && _mode != GitPetConnectionModes.Unconfigured;
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
        box.Margin = new Padding(5, 8, 8, 8);
        box.BackColor = GuardianTheme.Window;
        box.ForeColor = GuardianTheme.Ink;
        box.BorderStyle = BorderStyle.FixedSingle;
        box.Font = new Font("Cascadia Mono", 9.5f);
    }

    private static void StyleButton(Button button, int width, Color fill, Color border)
    {
        button.Width = width;
        button.Height = 38;
        button.Margin = new Padding(7, 2, 7, 2);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = border;
        button.BackColor = fill;
        button.ForeColor = Color.White;
        button.Font = new Font("Segoe UI", 9, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
        button.UseVisualStyleBackColor = false;
    }
}
