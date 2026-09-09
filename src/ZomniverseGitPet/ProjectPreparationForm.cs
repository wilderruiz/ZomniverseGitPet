namespace ZomniverseGitPet;

/// <summary>
/// Compatibility wrapper for the existing preparation call sites. New-project preparation
/// is now a two-step wizard: choose the tracking scope first, then review the exact root
/// .gitignore proposal with nested ignore files visible. Existing repository hygiene skips
/// the scope step and opens the review directly.
/// </summary>
internal sealed class ProjectPreparationForm : Form
{
    private readonly string _folderPath;
    private readonly bool _initializeGit;
    private IReadOnlyList<string> _acceptedRules = [];
    private bool _wizardStarted;

    public ProjectPreparationForm(
        string folderPath,
        IReadOnlyList<GitIgnoreSuggestion> suggestions,
        bool initializeGit)
    {
        _folderPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folderPath));
        _initializeGit = initializeGit;

        // This form is only the modal result carrier now. The visible UX is provided by
        // ProjectScopeSelectionForm followed by ProjectPreparationReviewForm.
        Text = initializeGit ? "Prepare project for Git" : "Repository hygiene";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        Opacity = 0;
        Size = new Size(1, 1);
    }

    public IReadOnlyList<string> AcceptedRules => _acceptedRules;

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
            if (_initializeGit)
            {
                using var scope = new ProjectScopeSelectionForm(_folderPath);
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
            var scopeRules = _initializeGit ? ProjectScopePlanner.BuildIgnoreRules(plan) : [];

            using var review = new ProjectPreparationReviewForm(
                _folderPath,
                scopedSuggestions,
                _initializeGit,
                scopeRules,
                documents,
                plan.Summary);

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
                "Prepare project for Git",
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
