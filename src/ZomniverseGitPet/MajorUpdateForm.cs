using System.Diagnostics;

namespace ZomniverseGitPet;

internal sealed class MajorUpdateForm : Form
{
    private readonly MajorUpdateAssessment _assessment;
    private readonly MajorUpdateCoordinator _coordinator;
    private readonly TextBox _legacyBranch = new();
    private readonly TextBox _legacyTag = new();
    private readonly TextBox _newBranch = new();
    private readonly TextBox _releaseTitle = new();
    private readonly RichTextBox _status = new();
    private readonly Button _createPlan = new();
    private readonly Button _publishLegacy = new();
    private readonly Button _openDraft = new();
    private readonly Button _openReleases = new();
    private readonly Button _copyDetails = new();
    private MajorReleasePlanResult? _plan;

    public MajorUpdateForm(MajorUpdateAssessment assessment, MajorUpdateCoordinator coordinator)
    {
        _assessment = assessment;
        _coordinator = coordinator;

        Text = assessment.IsMajorCandidate ? "Major update detected" : "Milestones and releases";
        Icon = AppIconProvider.Icon;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(920, 690);
        Size = new Size(1120, 820);
        BackColor = GuardianTheme.Window;
        ForeColor = GuardianTheme.Ink;
        Font = new Font("Segoe UI", 9);
        WindowChrome.ApplyGuardianChrome(this);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = Padding.Empty,
            BackColor = GuardianTheme.Window
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 185));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 220));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildEvidence(), 0, 1);
        root.Controls.Add(BuildPlan(), 0, 2);
        root.Controls.Add(BuildStatus(), 0, 3);
        root.Controls.Add(BuildFooter(), 0, 4);
        Controls.Add(root);

        var (legacyMajor, newMajor) = MajorUpdateCoordinator.SuggestMajorVersions(assessment.ExistingTags);
        _legacyBranch.Text = $"legacy/v{legacyMajor}";
        _legacyTag.Text = $"v{legacyMajor}.0.0-legacy";
        _newBranch.Text = $"redesign/v{newMajor}";
        _releaseTitle.Text = $"Legacy version {legacyMajor} — before the v{newMajor} redesign";

        var github = MajorUpdateCoordinator.TryGetGitHubWebUrl(assessment.OriginUrl);
        _openReleases.Enabled = github is not null;
        _openDraft.Enabled = false;
        _publishLegacy.Enabled = false;
        _copyDetails.Enabled = false;

        _status.Text = BuildInitialStatus();
    }

    private Control BuildHeader()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = GuardianTheme.SurfaceRaised,
            Padding = new Padding(26, 15, 26, 12)
        };

        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 44,
            Text = _assessment.IsMajorCandidate
                ? "◇  " + _assessment.Prompt.ToUpperInvariant()
                : "◇  RELEASE & MILESTONE CENTER",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 15, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };
        var subtitle = new Label
        {
            Dock = DockStyle.Fill,
            Text = _assessment.IsMajorCandidate
                ? "GitPet found signs of a structural change. You decide whether this is a new generation."
                : "Protect an old generation, start a new development line, and keep publishing deliberate.",
            ForeColor = GuardianTheme.MutedInk,
            Font = new Font("Segoe UI", 10),
            TextAlign = ContentAlignment.MiddleLeft
        };
        panel.Controls.Add(subtitle);
        panel.Controls.Add(title);
        return panel;
    }

    private Control BuildEvidence()
    {
        var outer = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(22, 12, 22, 8),
            BackColor = GuardianTheme.Window
        };
        var card = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = GuardianTheme.Surface,
            Padding = new Padding(18, 12, 18, 12)
        };
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));

        var facts = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = GuardianTheme.Ink,
            TextAlign = ContentAlignment.TopLeft,
            Font = new Font("Segoe UI", 9.5f),
            Text =
                $"PROJECT   {Path.GetFileName(Path.TrimEndingDirectorySeparator(_assessment.RepositoryPath))}\r\n" +
                $"BRANCH    {_assessment.Branch}\r\n" +
                $"FILES     {_assessment.ChangedFiles} changed · {_assessment.AddedFiles} added · {_assessment.DeletedFiles} removed\r\n" +
                $"LINES     +{_assessment.AddedLines:N0} / -{_assessment.DeletedLines:N0}\r\n" +
                $"REMOTE    {_assessment.RelationLabel}"
        };

        var reasons = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            BackColor = GuardianTheme.Surface,
            ForeColor = GuardianTheme.MutedInk,
            Font = new Font("Segoe UI", 9.5f),
            ScrollBars = RichTextBoxScrollBars.Vertical,
            Text = _assessment.Reasons.Count == 0
                ? "GitPet did not find enough structural signals to call this a major-update candidate. You can still create a milestone manually."
                : "WHY GITPET NOTICED\r\n\r\n• " + string.Join("\r\n• ", _assessment.Reasons)
        };
        card.Controls.Add(facts, 0, 0);
        card.Controls.Add(reasons, 1, 0);
        outer.Controls.Add(card);
        return outer;
    }

    private Control BuildPlan()
    {
        var outer = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(22, 4, 22, 10),
            BackColor = GuardianTheme.Window
        };
        var card = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5,
            Padding = new Padding(18, 12, 18, 12),
            BackColor = GuardianTheme.SurfaceSoft
        };
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        AddField(card, 0, "OLD VERSION BRANCH", _legacyBranch,
            "A living branch pointing at the generation you are preserving, for example legacy/v1.");
        AddField(card, 1, "OLD VERSION TAG", _legacyTag,
            "A frozen exact marker for the old generation. GitHub Releases can be created from this tag.");
        AddField(card, 2, "NEW WORK BRANCH", _newBranch,
            "The major redesign moves onto this branch. GitPet never force-rewrites main.");
        AddField(card, 3, "RELEASE TITLE", _releaseTitle,
            "Suggested human-readable title to paste into the GitHub Release page.");

        var explanation = new Label
        {
            Dock = DockStyle.Fill,
            Text = _assessment.WorkingTreeDirty
                ? "Your current uncommitted redesign stays on disk and moves with the new branch. Create your next Checkpoint there. The legacy branch/tag point to the previous baseline."
                : "GitPet will preserve the previous/published baseline as legacy and put the current local generation on the new branch. Nothing is pushed by Create local plan.",
            ForeColor = GuardianTheme.MutedInk,
            Padding = new Padding(0, 7, 0, 0),
            AutoEllipsis = true
        };
        card.Controls.Add(explanation, 0, 4);
        card.SetColumnSpan(explanation, 2);
        outer.Controls.Add(card);
        return outer;
    }

    private Control BuildStatus()
    {
        var outer = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(22, 2, 22, 8),
            BackColor = GuardianTheme.Window
        };
        _status.Dock = DockStyle.Fill;
        _status.ReadOnly = true;
        _status.BorderStyle = BorderStyle.None;
        _status.BackColor = GuardianTheme.Console;
        _status.ForeColor = GuardianTheme.Ink;
        _status.Font = new Font("Cascadia Mono", 9.5f);
        _status.ScrollBars = RichTextBoxScrollBars.Vertical;
        outer.Controls.Add(_status);
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

        var close = MakeButton("Close", 90, GuardianTheme.SurfaceSoft, GuardianTheme.Border);
        close.Click += (_, _) => Close();
        _createPlan.Text = "Create local release plan";
        StyleButton(_createPlan, 190, GuardianTheme.Violet, GuardianTheme.HotPink);
        _createPlan.Click += async (_, _) => await CreatePlanAsync();

        _publishLegacy.Text = "Publish legacy refs ↑";
        StyleButton(_publishLegacy, 165, GuardianTheme.SurfaceSoft, GuardianTheme.HotPink);
        _publishLegacy.Click += async (_, _) => await PublishLegacyAsync();

        _openDraft.Text = "Create GitHub Release ↗";
        StyleButton(_openDraft, 175, GuardianTheme.SurfaceSoft, GuardianTheme.Border);
        _openDraft.Click += (_, _) => OpenDraftRelease();

        _openReleases.Text = "Previous releases ↗";
        StyleButton(_openReleases, 150, GuardianTheme.SurfaceSoft, GuardianTheme.Border);
        _openReleases.Click += (_, _) => OpenAllReleases();

        _copyDetails.Text = "Copy release details";
        StyleButton(_copyDetails, 145, GuardianTheme.SurfaceSoft, GuardianTheme.Border);
        _copyDetails.Click += (_, _) => CopyReleaseDetails();

        footer.Controls.Add(close);
        footer.Controls.Add(_createPlan);
        footer.Controls.Add(_publishLegacy);
        footer.Controls.Add(_openDraft);
        footer.Controls.Add(_openReleases);
        footer.Controls.Add(_copyDetails);
        CancelButton = close;
        return footer;
    }

    private async Task CreatePlanAsync()
    {
        if (string.IsNullOrWhiteSpace(_legacyBranch.Text) ||
            string.IsNullOrWhiteSpace(_legacyTag.Text) ||
            string.IsNullOrWhiteSpace(_newBranch.Text))
        {
            MessageBox.Show(this, "Enter names for the legacy branch, legacy tag, and new work branch.",
                "Major update", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var answer = MessageBox.Show(
            this,
            "Create the major-update plan LOCALLY?\r\n\r\n" +
            $"Old generation branch: {_legacyBranch.Text.Trim()}\r\n" +
            $"Old generation tag: {_legacyTag.Text.Trim()}\r\n" +
            $"New work branch: {_newBranch.Text.Trim()}\r\n\r\n" +
            "GitPet will not push, force-update main, reset files, or delete history.",
            "Confirm local major-update plan",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (answer != DialogResult.Yes) return;

        SetBusy(true, "Creating local branches and tag...");
        try
        {
            _plan = await _coordinator.CreateLocalReleasePlanAsync(
                _assessment,
                _legacyBranch.Text,
                _legacyTag.Text,
                _newBranch.Text);
            _status.Text = _plan.Message;
            if (!_plan.Success)
            {
                _status.ForeColor = GuardianTheme.Warning;
                return;
            }

            _status.ForeColor = GuardianTheme.Healthy;
            _legacyBranch.ReadOnly = true;
            _legacyTag.ReadOnly = true;
            _newBranch.ReadOnly = true;
            _publishLegacy.Enabled = !string.IsNullOrWhiteSpace(_assessment.OriginUrl);
            _copyDetails.Enabled = true;
            var github = MajorUpdateCoordinator.TryGetGitHubWebUrl(_assessment.OriginUrl);
            _openDraft.Enabled = github is not null;
            _openReleases.Enabled = github is not null;
            _createPlan.Enabled = false;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task PublishLegacyAsync()
    {
        if (_plan is not { Success: true }) return;
        var answer = MessageBox.Show(
            this,
            "Publish ONLY the protected old-generation branch and tag to origin?\r\n\r\n" +
            $"Branch: {_plan.LegacyBranch}\r\nTag: {_plan.LegacyTag}\r\n\r\n" +
            $"The new branch '{_plan.NewBranch}' will NOT be pushed by this action. main will not be changed.",
            "Confirm legacy publication",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (answer != DialogResult.Yes) return;

        SetBusy(true, "Publishing protected legacy branch + tag to origin...");
        try
        {
            var result = await _coordinator.PublishLegacyRefsAsync(_assessment, _plan.LegacyBranch, _plan.LegacyTag);
            _status.Text = result.Success
                ? "Legacy branch and tag published ✓\r\n\r\n" +
                  "The old generation is now protected on origin. Use Create GitHub Release to turn the tag into a browsable release page.\r\n\r\n" +
                  "Your new redesign branch was NOT pushed. Use Guardian Push ↑ separately when you decide to publish it.\r\n\r\n" + result.Output
                : "Legacy publication stopped.\r\n\r\n" + result.Output;
            _status.ForeColor = result.Success ? GuardianTheme.Healthy : GuardianTheme.Warning;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void OpenDraftRelease()
    {
        if (_plan is not { Success: true }) return;
        var github = MajorUpdateCoordinator.TryGetGitHubWebUrl(_assessment.OriginUrl);
        if (github is null) return;
        CopyReleaseDetails();
        OpenUrl(MajorUpdateCoordinator.BuildGitHubReleaseDraftUrl(github, _plan.LegacyTag, _plan.LegacyBranch));
    }

    private void OpenAllReleases()
    {
        var github = MajorUpdateCoordinator.TryGetGitHubWebUrl(_assessment.OriginUrl);
        if (github is null) return;
        OpenUrl(github.TrimEnd('/') + "/releases");
    }

    private void CopyReleaseDetails()
    {
        var tag = _plan?.LegacyTag ?? _legacyTag.Text.Trim();
        var title = string.IsNullOrWhiteSpace(_releaseTitle.Text)
            ? $"Legacy release {tag}"
            : _releaseTitle.Text.Trim();
        var notes =
            $"{title}\r\n\r\n" +
            $"Tag: {tag}\r\n" +
            $"Legacy branch: {_plan?.LegacyBranch ?? _legacyBranch.Text.Trim()}\r\n\r\n" +
            "This release preserves the previous project generation before a major architectural/design update. " +
            "The newer generation continues on a separate branch so the old version remains inspectable and recoverable.";
        try
        {
            Clipboard.SetText(notes);
            _status.Text = "Release title and notes copied to the clipboard.\r\n\r\n" + notes;
            _status.ForeColor = GuardianTheme.Ink;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "GitPet could not copy the release details.\r\n\r\n" + ex.Message,
                "Release details", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private string BuildInitialStatus()
    {
        var builder = new System.Text.StringBuilder();
        if (_assessment.IsMajorCandidate)
        {
            builder.AppendLine("GITPET RECOMMENDATION");
            builder.AppendLine();
            builder.AppendLine("Treat this as a possible new generation rather than another tiny checkpoint.");
        }
        else
        {
            builder.AppendLine("MILESTONE MODE");
            builder.AppendLine();
            builder.AppendLine("GitPet is not strongly classifying the current difference as major, but you can deliberately create a generation boundary here.");
        }
        builder.AppendLine();
        if (_assessment.RemoteRelation == RemoteHistoryRelation.Diverged)
        {
            builder.AppendLine("Both local and origin contain unique commits. The local release plan avoids force-pushing main: it protects the online generation as legacy and moves the local generation onto a new branch.");
        }
        else if (_assessment.WorkingTreeDirty)
        {
            builder.AppendLine("Your major changes are still uncommitted. GitPet can mark the current HEAD as the legacy baseline and switch the working changes onto the new branch before your next Checkpoint.");
        }
        else
        {
            builder.AppendLine("GitPet can preserve the previous/published baseline and continue the current generation on a new branch.");
        }
        builder.AppendLine();
        builder.AppendLine("Create local release plan does not contact GitHub. Publishing legacy refs and publishing the redesign remain explicit separate actions.");
        return builder.ToString();
    }

    private static void AddField(TableLayoutPanel card, int row, string label, TextBox input, string tooltip)
    {
        card.Controls.Add(new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = GuardianTheme.MutedInk,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
        }, 0, row);
        input.Dock = DockStyle.Fill;
        input.BackColor = GuardianTheme.Console;
        input.ForeColor = GuardianTheme.Ink;
        input.BorderStyle = BorderStyle.FixedSingle;
        input.Margin = new Padding(0, 3, 0, 3);
        card.Controls.Add(input, 1, row);
        var tips = new ToolTip { ShowAlways = true, AutoPopDelay = 12000 };
        tips.SetToolTip(input, tooltip);
    }

    private static Button MakeButton(string text, int width, Color fill, Color border)
    {
        var button = new Button { Text = text };
        StyleButton(button, width, fill, border);
        return button;
    }

    private static void StyleButton(Button button, int width, Color fill, Color border)
    {
        button.Width = width;
        button.Height = 38;
        button.Margin = new Padding(7, 0, 0, 0);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = border;
        button.BackColor = fill;
        button.ForeColor = Color.White;
        button.Font = new Font("Segoe UI", 9, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
        button.UseVisualStyleBackColor = false;
    }

    private void SetBusy(bool busy, string? message = null)
    {
        UseWaitCursor = busy;
        _legacyBranch.Enabled = !busy;
        _legacyTag.Enabled = !busy;
        _newBranch.Enabled = !busy;
        _releaseTitle.Enabled = !busy;
        if (_createPlan.Enabled) _createPlan.Enabled = !busy;
        if (_plan is { Success: true }) _publishLegacy.Enabled = !busy && !string.IsNullOrWhiteSpace(_assessment.OriginUrl);
        if (!string.IsNullOrWhiteSpace(message))
        {
            _status.Text = message;
            _status.ForeColor = GuardianTheme.Changes;
        }
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            MessageBox.Show("GitPet could not open the browser.", "ZomniverseGitPet", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
