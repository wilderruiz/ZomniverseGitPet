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
    private readonly PetAssets _petAssets = new();

    private readonly Label _connectionStatus = new();
    private readonly Label _selectedMode = new();
    private readonly TextBox _repository = new();
    private readonly TextBox _destination = new();
    private readonly OnboardingButton _githubButton = new();
    private readonly OnboardingButton _localButton = new();
    private readonly OnboardingButton _refreshGitHub = new();
    private readonly OnboardingButton _showCloneButton = new();
    private readonly OnboardingButton _cloneButton = new();
    private readonly OnboardingButton _openLocalButton = new();
    private readonly OnboardingButton _finishButton = new();
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
        /* ==========================================================================
           PATCH: COMPACT FIRST-RUN WINDOW SIZE
           FUNCTION:
           Constrains the one-time setup window to its intended presentation
           dimensions while allowing limited user resizing.

           DATE.TIME ADDED: 2026-09-10 23:10 +03:00

           REASON:
           Prevent the first-run setup window from expanding across the entire screen.
           ========================================================================== */

        /* ==========================================================================
           PATCH: LARGER FIRST-RUN PRESENTATION
           FUNCTION:
           Gives the onboarding window additional width and height while
           retaining sensible minimum dimensions.

           DATE.TIME ADDED: 2026-09-10 23:14 +03:00

           REASON:
           Improve onboarding readability without allowing the window to fill the entire screen.
           ========================================================================== */

        /* ==========================================================================
           PATCH: RESIZABLE FIRST-RUN WINDOW
           FUNCTION:
           Establishes a larger initial onboarding size while restoring unrestricted
           user resizing and the standard maximize command.

           DATE.TIME ADDED: 2026-09-11 00:04 +03:00

           REASON:
           Keep expanded clone controls visible and allow users to resize the onboarding window.
           ========================================================================== */
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(1040, 780);
        MaximumSize = Size.Empty;
        Size = new Size(1320, 960);
        MaximizeBox = true;
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
        /* ==========================================================================
           PATCH: TALLER CONNECTION MODE CARDS
           FUNCTION:
           Adds vertical room for connection descriptions, status information,
           and two evenly aligned GitHub action buttons.

           DATE.TIME ADDED: 2026-09-10 23:05 +03:00

           REASON:
           Prevent GitHub status and action controls from overlapping inside the connection card.
           ========================================================================== */

        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 124));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildModePanel(), 0, 1);
        root.Controls.Add(BuildProjectPanel(), 0, 2);
        root.Controls.Add(BuildFooter(), 0, 3);
        Controls.Add(root);

        _repository.TextChanged += (_, _) => UpdateCloneDestination();

        /* ==========================================================================
           PATCH: POSITION ONBOARDING NEAR THE PET
           FUNCTION:
           Places the initial onboarding window above and left of the visible
           pet while keeping it inside the working area.

           DATE.TIME ADDED: 2026-09-10 23:14 +03:00

           REASON:
           Visually connect the first-run window with the pet guiding the setup.
           ========================================================================== */

        Shown += async (_, _) =>
        {
            if (!_config.OnboardingCompleted)
            {
                var guidePetWindow = Application.OpenForms
                    .OfType<PetForm>()
                    .FirstOrDefault(form => form.Visible);

                if (guidePetWindow is not null)
                {
                    /* ==========================================================================
                       PATCH: MONITOR-AWARE ONBOARDING SIZE
                       FUNCTION:
                       Applies the preferred initial dimensions while keeping the
                       onboarding window within the pet's current monitor.

                       DATE.TIME ADDED: 2026-09-11 00:04 +03:00

                       REASON:
                       Prevent the larger initial window from extending beyond smaller monitor working areas.
                       ========================================================================== */
                    var workingArea = Screen
                        .FromControl(guidePetWindow)
                        .WorkingArea;

                    Size = new Size(
                        Math.Min(1320, workingArea.Width - 48),
                        Math.Min(960, workingArea.Height - 48));

                    var desiredLeft =
                        guidePetWindow.Left - Width - 24;

                    var desiredTop =
                        guidePetWindow.Top - Height + 80;

                    var maximumLeft = Math.Max(
                        workingArea.Left,
                        workingArea.Right - Width);

                    var maximumTop = Math.Max(
                        workingArea.Top,
                        workingArea.Bottom - Height);

                    Location = new Point(
                        Math.Clamp(
                            desiredLeft,
                            workingArea.Left,
                            maximumLeft),
                        Math.Clamp(
                            desiredTop,
                            workingArea.Top,
                            maximumTop));
                }
            }

            _guidePet("👋 HI! I'M GITPET\nChoose how we'll work");
            await RefreshGitHubAsync();
        };
    }

    private Control BuildHeader()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(36, 18, 36, 16),
            BackColor = GuardianTheme.SurfaceRaised
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var logo = new PictureBox
        {
            Dock = DockStyle.Fill,
            Image = _petAssets.Happy,
            SizeMode = PictureBoxSizeMode.Zoom,
            Margin = new Padding(0, 4, 16, 4),
            BackColor = Color.Transparent,
            TabStop = false
        };

        var copy = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            BackColor = GuardianTheme.SurfaceRaised
        };
        copy.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        copy.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
        copy.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "WELCOME TO ZOMNIVERSE GITPET",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 17, FontStyle.Bold),
            TextAlign = ContentAlignment.BottomLeft
        }, 0, 0);
        copy.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Let’s choose how you want to begin. You can change these settings later.",
            ForeColor = GuardianTheme.MutedInk,
            Font = new Font("Segoe UI", 10.5f),
            TextAlign = ContentAlignment.TopLeft
        }, 0, 1);

        panel.Controls.Add(logo, 0, 0);
        panel.Controls.Add(copy, 1, 0);
        return panel;
    }

    private Control BuildModePanel()
    {
        var outer = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(34, 16, 34, 8),
            BackColor = GuardianTheme.Window
        };

        var connectionCard = new OnboardingSurfacePanel
        {
            Dock = DockStyle.Fill,
            FillColor = GuardianTheme.HealthyFill,
            BackColor = GuardianTheme.HealthyFill,
            BorderColor = Color.FromArgb(40, 104, 72),
            CornerRadius = 12,
            Padding = new Padding(2)
        };
        var connectionBar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = new Padding(16, 10, 12, 10),
            BackColor = GuardianTheme.HealthyFill
        };
        connectionBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        connectionBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
        connectionBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
        connectionBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));

        _connectionStatus.Dock = DockStyle.Fill;
        _connectionStatus.ForeColor = GuardianTheme.MutedInk;
        _connectionStatus.Font = new Font("Segoe UI", 9.2f, FontStyle.Bold);
        _connectionStatus.TextAlign = ContentAlignment.MiddleLeft;
        _connectionStatus.AutoEllipsis = true;
        _connectionStatus.Margin = new Padding(0, 0, 12, 0);

        _githubButton.Text = "Connect";
        StyleButton(_githubButton, 102, GuardianTheme.Violet, GuardianTheme.HotPink);
        _githubButton.Dock = DockStyle.Fill;
        _githubButton.Margin = new Padding(5, 3, 5, 3);

        _refreshGitHub.Text = "↻";
        StyleButton(_refreshGitHub, 50, GuardianTheme.SurfaceSoft, GuardianTheme.Border);
        _refreshGitHub.Font = new Font("Segoe UI Symbol", 14, FontStyle.Bold);
        _refreshGitHub.Dock = DockStyle.Fill;
        _refreshGitHub.Margin = new Padding(5, 3, 5, 3);

        _localButton.Text = "Local";
        StyleButton(_localButton, 78, GitOrangeDark, GitOrange);
        _localButton.Dock = DockStyle.Fill;
        _localButton.Margin = new Padding(5, 3, 0, 3);

        connectionBar.Controls.Add(_connectionStatus, 0, 0);
        connectionBar.Controls.Add(_githubButton, 1, 0);
        connectionBar.Controls.Add(_refreshGitHub, 2, 0);
        connectionBar.Controls.Add(_localButton, 3, 0);
        connectionCard.Controls.Add(connectionBar);
        outer.Controls.Add(connectionCard);

        _githubButton.Click += async (_, _) => await HandleGitHubButtonAsync();
        _refreshGitHub.Click += async (_, _) => await RefreshGitHubAsync();
        _localButton.Click += async (_, _) => await SelectModeAsync(GitPetConnectionModes.LocalGitOnly);
        _tips.SetToolTip(_githubButton, "Use the connected GitHub account, sign in through the browser, or install GitHub CLI when needed.");
        _tips.SetToolTip(_refreshGitHub, "Check GitHub CLI installation and browser-authentication status again.");
        _tips.SetToolTip(_localButton, "Use GitPet locally without connecting GitHub. Nothing is sent online automatically.");
        return outer;
    }

    private Control BuildProjectPanel()
    {
        var outer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(34, 10, 34, 12),
            AutoScroll = true,
            BackColor = GuardianTheme.Window
        };
        /* ==========================================================================
           PATCH: TALLER PROJECT INTRODUCTION
           FUNCTION:
           Gives the onboarding introduction enough vertical space for its heading
           and explanatory text without crowding the project cards.

           DATE.TIME ADDED: 2026-09-11 00:04 +03:00

           REASON:
           Prevent the project-selection introduction from appearing vertically compressed.
           ========================================================================== */
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 244));
        outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var introduction = new Label
        {
            Dock = DockStyle.Fill,
            Text = "CHOOSE ONE WAY TO START\r\nAlready have a project on this computer? Open it directly, that is the recommended path for most users.",
            ForeColor = GuardianTheme.MutedInk,
            Font = new Font("Segoe UI", 9.5f),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 4, 8, 8)
        };

        var choices = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = GuardianTheme.Window
        };
        choices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
        choices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));

        _openLocalButton.Text = "Open existing folder";
        StyleButton(_openLocalButton, 220, GuardianTheme.Violet, GuardianTheme.HotPink);
        _openLocalButton.Enabled = false;
        _openLocalButton.Click += async (_, _) => await OpenExistingProjectAsync();

        var openCard = BuildProjectChoiceCard(
            "RECOMMENDED",
            "📂  OPEN AN EXISTING PROJECT",
            "Select a Git repository or project folder that is already on this computer. GitPet will review it before watching it.",
            _openLocalButton,
            primary: true);

        /* ==========================================================================
           PATCH: CLONE PROJECT GUIDANCE
           FUNCTION:
           Explains that the clone action reveals fields for copying an online
           Git repository onto the current computer.

           DATE.TIME ADDED: 2026-09-11 00:04 +03:00

           REASON:
           Clarify what happens before users open the optional clone configuration.
           ========================================================================== */
        _showCloneButton.Text = "Clone an online project";
        StyleButton(_showCloneButton, 210, GuardianTheme.SurfaceSoft, GuardianTheme.Border);
        _showCloneButton.Click += (_, _) => ToggleCloneOptions();
        _tips.SetToolTip(
            _showCloneButton,
            "Show the repository address and destination fields needed to copy an online Git project to this computer.");

        var cloneCard = BuildProjectChoiceCard(
            "ONLINE PROJECT",
            "📦  CLONE A REPOSITORY",
            "Copy a repository from GitHub or another Git server to this computer while preserving its complete history.",
            _showCloneButton,
            primary: false);

        choices.Controls.Add(openCard, 0, 0);
        choices.Controls.Add(cloneCard, 1, 0);

        ConfigureTextBox(_repository);
        ConfigureTextBox(_destination);
        _repository.PlaceholderText = "owner/repository or https://github.com/owner/repository.git";
        _destination.PlaceholderText = "Local destination folder";

        var browse = new OnboardingButton { Text = "Browse…" };
        StyleButton(browse, 140, GuardianTheme.SurfaceSoft, GuardianTheme.Border);
        browse.Click += (_, _) => BrowseDestination();

        /* ==========================================================================
           PATCH: FULL-WIDTH CLONE ACTION BUTTON
           FUNCTION:
           Gives the clone action additional width for its complete bold
           label and the custom button's internal drawing space.

           DATE.TIME ADDED: 2026-09-11 00:12 +03:00

           REASON:
           Provide sufficient space for the complete clone action label.
           ========================================================================== */
        _cloneButton.Text = "Clone to this computer";
        StyleButton(_cloneButton, 300, GuardianTheme.Violet, GuardianTheme.HotPink);
        _cloneButton.Enabled = false;
        _cloneButton.Click += async (_, _) => await CloneRepositoryAsync();

        var cloneOptionsCard = new OnboardingSurfacePanel
        {
            Dock = DockStyle.Fill,
            Height = 190,
            Margin = new Padding(8, 14, 8, 8),
            Padding = new Padding(2),
            FillColor = GuardianTheme.SurfaceRaised,
            BackColor = GuardianTheme.SurfaceRaised,
            BorderColor = GuardianTheme.Border,
            CornerRadius = 12,
            Visible = false,
            Tag = "clone-options"
        };
        var cloneOptions = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 4,
            Margin = Padding.Empty,
            Padding = new Padding(20, 14, 20, 14),
            BackColor = GuardianTheme.SurfaceRaised
        };
        /* ==========================================================================
           PATCH: FULL-WIDTH CLONE ACTION COLUMN
           FUNCTION:
           Expands the clone-options action column to contain the larger clone
           button and preserve its horizontal margins.

           DATE.TIME ADDED: 2026-09-11 00:12 +03:00

           REASON:
           Stop the table layout from clipping the enlarged clone action button.
           ========================================================================== */
        cloneOptions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        cloneOptions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        cloneOptions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 316));
        cloneOptions.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        cloneOptions.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        cloneOptions.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        cloneOptions.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        var cloneHeading = new Label
        {
            Dock = DockStyle.Fill,
            Text = "CLONE AN ONLINE PROJECT",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        cloneOptions.SetColumnSpan(cloneHeading, 3);
        cloneOptions.Controls.Add(cloneHeading, 0, 0);
        cloneOptions.Controls.Add(FieldLabel("REPOSITORY"), 0, 1);
        cloneOptions.Controls.Add(_repository, 1, 1);
        cloneOptions.SetColumnSpan(_repository, 2);
        cloneOptions.Controls.Add(FieldLabel("SAVE COPY IN"), 0, 2);
        cloneOptions.Controls.Add(_destination, 1, 2);
        cloneOptions.Controls.Add(browse, 2, 2);
        cloneOptions.Controls.Add(new Label(), 0, 3);
        cloneOptions.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "GitPet preserves the repository's history and opens Project Scope before watching it.",
            ForeColor = GuardianTheme.FaintInk,
            Font = new Font("Segoe UI", 8.5f),
            TextAlign = ContentAlignment.MiddleLeft
        }, 1, 3);
        cloneOptions.Controls.Add(_cloneButton, 2, 3);
        _tips.SetToolTip(_cloneButton, "Copy this repository to your computer, preserve its Git history, then add it to GitPet Projects.");
        cloneOptionsCard.Controls.Add(cloneOptions);

        _selectedMode.Dock = DockStyle.Fill;
        _selectedMode.Text = "Choose how GitPet connects above, then open an existing project, clone one, or start without a project.";
        _selectedMode.ForeColor = GuardianTheme.MutedInk;
        _selectedMode.Font = new Font("Segoe UI", 9);
        _selectedMode.TextAlign = ContentAlignment.MiddleLeft;
        _selectedMode.Padding = new Padding(8, 10, 8, 0);

        outer.Controls.Add(introduction, 0, 0);
        outer.Controls.Add(choices, 0, 1);
        outer.Controls.Add(cloneOptionsCard, 0, 2);
        outer.Controls.Add(_selectedMode, 0, 3);
        return outer;
    }

    private OnboardingSurfacePanel BuildProjectChoiceCard(
        string badge,
        string title,
        string body,
        Button action,
        bool primary)
    {
        var fill = primary ? GuardianTheme.SurfaceRaised : GuardianTheme.Surface;
        var card = new OnboardingSurfacePanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(8),
            Padding = new Padding(22, 18, 22, 16),
            FillColor = fill,
            BackColor = fill,
            BorderColor = primary ? GuardianTheme.HotPink : GuardianTheme.Border,
            CornerRadius = 12
        };

        action.Dock = DockStyle.Bottom;
        action.Margin = Padding.Empty;

        var bodyLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = body,
            ForeColor = GuardianTheme.MutedInk,
            Font = new Font("Segoe UI", 9.5f),
            TextAlign = ContentAlignment.TopLeft,
            Padding = new Padding(0, 8, 0, 8)
        };
        var titleLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 42,
            Text = title,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        var badgeLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 24,
            Text = badge,
            ForeColor = primary ? GuardianTheme.HotPinkSoft : GuardianTheme.MutedInk,
            Font = new Font("Segoe UI", 7.8f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };

        card.Controls.Add(bodyLabel);
        card.Controls.Add(titleLabel);
        card.Controls.Add(badgeLabel);
        card.Controls.Add(action);
        return card;
    }

    private void ToggleCloneOptions()
    {
        var cloneOptions = FindControlByTag(this, "clone-options");
        if (cloneOptions is null) return;

        cloneOptions.Visible = !cloneOptions.Visible;
        _showCloneButton.Text = cloneOptions.Visible
            ? "Hide clone options"
            : "Clone an online project";

        if (cloneOptions.Visible)
        {
            cloneOptions.Parent?.PerformLayout();
            _repository.Focus();
        }
    }

    private static Control? FindControlByTag(Control root, string tag)
    {
        foreach (Control control in root.Controls)
        {
            if (string.Equals(control.Tag as string, tag, StringComparison.Ordinal))
                return control;

            var nested = FindControlByTag(control, tag);
            if (nested is not null) return nested;
        }

        return null;
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

        /* ==========================================================================
           PATCH: WIDER START WITHOUT PROJECT BUTTON
           FUNCTION:
           Provides enough horizontal room for the complete fallback startup
           action label without text truncation.

           DATE.TIME ADDED: 2026-09-11 00:04 +03:00

           REASON:
           Ensure the complete button label remains visible at the configured font size.
           ========================================================================== */
        _finishButton.Text = "Start without a project";
        StyleButton(_finishButton, 270, GuardianTheme.Violet, GuardianTheme.HotPink);
        _finishButton.Enabled = false;
        _finishButton.Click += async (_, _) => await FinishAsync();

        _tips.SetToolTip(_openLocalButton, "Open an existing local Git repository. Normal project setup remains available later from Projects.");

        footer.Controls.Add(_finishButton);
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

            _githubButton.Text = "Connect";

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
            _selectedMode.Text = $"GitHub Connected selected · {who}. Open an existing project, clone one, or start without a project.";
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
        _showCloneButton.Enabled = !busy;
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

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing) return;
        _tips.Dispose();
        _petAssets.Dispose();
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

        if (button is OnboardingButton onboardingButton)
        {
            onboardingButton.FillColor = fill;
            onboardingButton.BorderColor = border;
            onboardingButton.HoverColor = ControlPaint.Light(fill, 0.08f);
            onboardingButton.CornerRadius = 8;
        }
    }
}
