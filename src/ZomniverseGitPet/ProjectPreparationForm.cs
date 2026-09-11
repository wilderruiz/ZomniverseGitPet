namespace ZomniverseGitPet;

/// <summary>
/// Compatibility wrapper for project preparation/reconfiguration. Creating Git metadata,
/// choosing a GitPet project scope, and reviewing repository hygiene are separate decisions.
/// Project scope is stored by GitPet locally; .gitignore remains repository-wide hygiene only.
/// </summary>
internal sealed class ProjectPreparationForm : Form
{
    private readonly string _folderPath;
    private readonly bool _initializeGit;
    private readonly bool _chooseScope;
    private readonly IReadOnlyList<ProjectScopeEntry>? _initialScope;
    private readonly string _projectPath;
    private string _projectName;
    private IReadOnlyList<string> _acceptedRules = [];
    private bool _wizardStarted;

    public ProjectPreparationForm(
        string folderPath,
        IReadOnlyList<GitIgnoreSuggestion> suggestions,
        bool initializeGit,
        bool chooseScope = false,
        IReadOnlyList<ProjectScopeEntry>? initialScope = null,
        string? projectPath = null,
        string? projectName = null)
    {
        _folderPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folderPath));
        _projectPath = string.IsNullOrWhiteSpace(projectPath)
            ? _folderPath
            : Path.TrimEndingDirectorySeparator(Path.GetFullPath(projectPath));
        _projectName = string.IsNullOrWhiteSpace(projectName)
            ? Path.GetFileName(_projectPath)
            : projectName.Trim();
        _initializeGit = initializeGit;
        _chooseScope = initializeGit || chooseScope;
        _initialScope = initialScope;

        Text = initializeGit ? "Prepare project for Git" : _chooseScope ? "Reconfigure project" : "Repository hygiene";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        Opacity = 0;
        Size = new Size(1, 1);
    }

    public IReadOnlyList<string> AcceptedRules => _acceptedRules;
    public ProjectScopePlan? ScopePlan { get; private set; }
    public string ProjectName => _projectName;

    public bool ReplacesTrackingScope => _chooseScope && !_initializeGit;

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (_wizardStarted) return;
        _wizardStarted = true;
        BeginInvoke(new Action(RunWizard));
    }

    private void RunWizard()
    {
        try
        {
            ProjectScopePlan plan;
            var legacyScope = _initializeGit
                ? null
                : ProjectGitIgnoreComposer.ReadManagedScope(_folderPath);

            if (_chooseScope)
            {
                /* ==========================================================================
                   PATCH: LOCAL LOGICAL PROJECT SCOPE
                   DATE.TIME: 2026-09-11 14:05 +03:00
                   Store scope in GitPet instead of repository-wide ignore rules.
                   ========================================================================== */
                var existingScope = _initialScope ?? legacyScope;

                using var scope = new ProjectScopeSelectionForm(
                    _folderPath,
                    existingScope,
                    _projectPath,
                    _projectName);

                /* ==========================================================================
                   PATCH: PERSISTENT ALLOW-LIST PROJECT CONTRACT
                   DATE.TIME: 2026-09-11 21:17 +03:00
                   Restore project lists and expose Send boundary comparison.
                   ========================================================================== */
                ProjectAllowListUiBridge.Attach(
                    scope,
                    _folderPath,
                    _projectPath,
                    _projectName);

                if (scope.ShowDialog(Owner) != DialogResult.OK)
                {
                    Finish(DialogResult.Cancel);
                    return;
                }
                plan = scope.ScopePlan;
                _projectName = scope.ProjectName;
            }
            else
            {
                plan = ProjectScopePlanner.Create(_folderPath, [], trackEverything: true);
            }

            ScopePlan = plan;
            var scopedSuggestions = ScopedGitIgnoreAdvisor.Suggest(plan);
            var documents = ScopedGitIgnoreAdvisor.FindIgnoreDocuments(plan);

            IReadOnlyList<string> scopeRules = [];

            using var review = new ProjectPreparationReviewForm(
                _folderPath,
                scopedSuggestions,
                _initializeGit,
                scopeRules,
                documents,
                plan.Summary + " Project scope is stored locally by GitPet and does not hide sibling projects from Git.",
                replaceScope: ReplacesTrackingScope);

            if (review.ShowDialog(Owner) != DialogResult.OK)
            {
                Finish(DialogResult.Cancel);
                return;
            }

            // Old versions encoded project scope into the root .gitignore. Preserve that
            // scope on the existing root project before the caller removes the managed block.
            if (_chooseScope && legacyScope is { Count: > 0 })
                LogicalProjectScopeRuntime.MigrateLegacyScope(_folderPath, legacyScope);

            _acceptedRules = review.AcceptedRules
                .Where(rule => !string.IsNullOrWhiteSpace(rule))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            Finish(DialogResult.OK);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                Owner,
                "GitPet could not build the project preparation review. Nothing was initialized or changed.\r\n\r\n" + ex.Message,
                _initializeGit ? "Prepare project for Git" : "Reconfigure project",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            Finish(DialogResult.Cancel);
        }
    }

    private void Finish(DialogResult result)
    {
        DialogResult = result;
        Close();
    }
}
