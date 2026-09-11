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
    private IReadOnlyList<string> _acceptedRules = [];
    private bool _wizardStarted;

    public ProjectPreparationForm(
        string folderPath,
        IReadOnlyList<GitIgnoreSuggestion> suggestions,
        bool initializeGit,
        bool chooseScope = false,
        IReadOnlyList<ProjectScopeEntry>? initialScope = null)
    {
        _folderPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folderPath));
        _initializeGit = initializeGit;
        _chooseScope = initializeGit || chooseScope;
        _initialScope = initialScope;

        // This form is only the modal result carrier now. The visible UX is provided by
        // ProjectScopeSelectionForm followed by ProjectPreparationReviewForm.
        Text = initializeGit ? "Prepare project for Git" : _chooseScope ? "Reconfigure project" : "Repository hygiene";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        Opacity = 0;
        Size = new Size(1, 1);
    }

    public IReadOnlyList<string> AcceptedRules => _acceptedRules;
    public ProjectScopePlan? ScopePlan { get; private set; }

    // Replacing the legacy managed scope removes old GitPet scope rules from .gitignore.
    // The replacement scope itself is persisted in config and enforced with Git pathspecs.
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
            if (_chooseScope)
            {
                /* ==========================================================================
                   PATCH: LOCAL LOGICAL PROJECT SCOPE
                   DATE.TIME: 2026-09-11 14:05 +03:00
                   Store scope in GitPet instead of repository-wide ignore rules.
                   ========================================================================== */
                var existingScope = _initialScope ??
                    (_initializeGit ? null : ProjectGitIgnoreComposer.ReadManagedScope(_folderPath));

                using var scope = new ProjectScopeSelectionForm(_folderPath, existingScope);
                if (scope.ShowDialog(Owner) != DialogResult.OK)
                {
                    Finish(DialogResult.Cancel);
                    return;
                }
                plan = scope.ScopePlan;
            }
            else
            {
                plan = ProjectScopePlanner.Create(_folderPath, [], trackEverything: true);
            }

            ScopePlan = plan;
            var scopedSuggestions = ScopedGitIgnoreAdvisor.Suggest(plan);
            var documents = ScopedGitIgnoreAdvisor.FindIgnoreDocuments(plan);

            // Project scope is no longer encoded into .gitignore. The hygiene review remains
            // repository-aware and may still append explicitly approved ignore suggestions.
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
