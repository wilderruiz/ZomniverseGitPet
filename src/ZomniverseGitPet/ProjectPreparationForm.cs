namespace ZomniverseGitPet;

/// <summary>
/// Compatibility wrapper for project preparation/reconfiguration. Creating Git metadata and
/// choosing a tracking scope are intentionally separate decisions: an existing repository can
/// reopen the full scope tree without running git init again.
/// </summary>
internal sealed class ProjectPreparationForm : Form
{
    private readonly string _folderPath;
    private readonly bool _initializeGit;
    private readonly bool _chooseScope;
    private IReadOnlyList<string> _acceptedRules = [];
    private bool _wizardStarted;

    public ProjectPreparationForm(
        string folderPath,
        IReadOnlyList<GitIgnoreSuggestion> suggestions,
        bool initializeGit,
        bool chooseScope = false)
    {
        _folderPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folderPath));
        _initializeGit = initializeGit;
        _chooseScope = initializeGit || chooseScope;

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
                PATCH: REOPEN PREVIOUS PROJECT SCOPE
                DATE.TIME: 2026-09-10 09:52 +03:00
                REASON: Initialize reconfiguration from the existing 
                managed scope instead of selecting everything.
                ========================================================================== */

                var existingScope = _initializeGit
                    ? null
                    : ProjectGitIgnoreComposer.ReadManagedScope(_folderPath);

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

            var scopedSuggestions = ScopedGitIgnoreAdvisor.Suggest(plan);
            var documents = ScopedGitIgnoreAdvisor.FindIgnoreDocuments(plan);
            var scopeRules = _chooseScope ? ProjectScopePlanner.BuildIgnoreRules(plan) : [];

            using var review = new ProjectPreparationReviewForm(
                _folderPath,
                scopedSuggestions,
                _initializeGit,
                scopeRules,
                documents,
                plan.Summary,
                replaceScope: ReplacesTrackingScope);

            if (review.ShowDialog(Owner) != DialogResult.OK)
            {
                Finish(DialogResult.Cancel);
                return;
            }

            _acceptedRules = scopeRules
                .Concat(review.AcceptedRules)
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
