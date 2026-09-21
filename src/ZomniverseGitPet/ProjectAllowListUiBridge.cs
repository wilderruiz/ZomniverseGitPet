namespace ZomniverseGitPet;

/* ==========================================================================
   PATCH: PROJECT ALLOW-LIST UI BRIDGE
   DATE.TIME: 2026-09-11 21:16 +03:00
   Restore project lists and compare them with Send.
   ========================================================================== */
internal static class ProjectAllowListUiBridge
{
    public static void Attach(
        ProjectScopeSelectionForm scope,
        string repositoryRoot,
        string projectPath,
        string projectName)
    {
        var editor = FindEditor(scope);
        if (editor is null) return;

        var status = EnumerateControls(scope)
            .OfType<Label>()
            .FirstOrDefault(label =>
                label.Text.StartsWith("Paste exact paths", StringComparison.OrdinalIgnoreCase));

        var config = new ConfigStore().Load();
        var project = ResolveProject(config, repositoryRoot, projectPath, projectName);
        var projectId = project?.Id;
        var store = new ProjectAllowListStore();
        var saved = store.Load(projectId, repositoryRoot, projectPath, projectName);
        if (!string.IsNullOrWhiteSpace(saved))
        {
            editor.Text = saved;
            if (status is not null)
            {
                status.ForeColor = GuardianTheme.Healthy;
                status.Text = "Restored the saved allow list for this GitPet project.";
            }
        }

        var actions = EnumerateControls(scope)
            .OfType<FlowLayoutPanel>()
            .FirstOrDefault(panel => panel.Controls.OfType<Button>()
                .Any(button => button.Text.StartsWith("Apply to tree", StringComparison.OrdinalIgnoreCase)));
        if (actions is not null &&
            !actions.Controls.OfType<Button>().Any(button => button.Name == "CompareProjectWithSendButton"))
        {
            /* ==========================================================================
               PATCH: AUTO-SIZE COMPARE WITH SEND BUTTON
               FUNCTION:
               Makes the comparison button expand for its complete caption while
               retaining a comfortable minimum width at different display scales.

               DATE.TIME ADDED: 2026-09-11 22:43 +03:00

               REASON:
               Fixed sizing clips the comparison caption under some Windows display scales.
               ========================================================================== */
            var compare = MakeButton("Compare with Send ↑", 220);
            compare.AutoSize = true;
            compare.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            compare.MinimumSize = new Size(220, 38);
            compare.Padding = new Padding(12, 0, 12, 0);
            compare.AutoEllipsis = false;
            compare.Name = "CompareProjectWithSendButton";
            compare.Click += async (_, _) =>
            {
                compare.Enabled = false;
                compare.Text = "Comparing…";
                try
                {
                    var result = await ProjectPublishBoundary.CompareAsync(
                        repositoryRoot,
                        projectPath,
                        scope.ProjectName,
                        projectId,
                        editor.Text);
                    using var dialog = new ProjectPublishBoundaryDialog(result);
                    dialog.ShowDialog(scope);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        scope,
                        "GitPet could not compare this project with Send. Nothing was changed.\r\n\r\n" + ex.Message,
                        "Compare project with Send",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                finally
                {
                    compare.Text = "Compare with Send ↑";
                    compare.Enabled = true;
                }
            };
            actions.Controls.Add(compare);
        }
    }

    public static string ReadText(ProjectScopeSelectionForm scope) =>
        FindEditor(scope)?.Text ?? string.Empty;

    public static void Persist(
        string repositoryRoot,
        string projectPath,
        string projectName,
        string? text)
    {
        try
        {
            var config = new ConfigStore().Load();
            var project = ResolveProject(config, repositoryRoot, projectPath, projectName);
            new ProjectAllowListStore().Save(
                project?.Id,
                repositoryRoot,
                projectPath,
                projectName,
                text);
        }
        catch
        {
            // Allow-list source persistence must never destabilize project setup.
        }
    }

    private static TextBox? FindEditor(Control scope) =>
        EnumerateControls(scope)
            .OfType<TextBox>()
            .FirstOrDefault(textBox => textBox.Multiline);

    private static RecentRepositoryEntry? ResolveProject(
        AppConfig config,
        string repositoryRoot,
        string projectPath,
        string projectName)
    {
        var active = config.GetActiveProject();
        if (active is not null &&
            PathEquals(active.RepositoryRoot, repositoryRoot) &&
            PathEquals(active.Path, projectPath) &&
            string.Equals(active.DisplayName, projectName, StringComparison.OrdinalIgnoreCase))
            return active;

        return config.RecentRepositories
            .OrderByDescending(entry => entry.LastOpenedUtc)
            .FirstOrDefault(entry =>
                PathEquals(entry.RepositoryRoot, repositoryRoot) &&
                PathEquals(entry.Path, projectPath) &&
                string.Equals(entry.DisplayName, projectName, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<Control> EnumerateControls(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var descendant in EnumerateControls(child)) yield return descendant;
        }
    }

    private static Button MakeButton(string text, int width)
    {
        var button = new Button
        {
            Text = text,
            Width = width,
            Height = 38,
            Margin = new Padding(6, 0, 0, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = GuardianTheme.SurfaceSoft,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = GuardianTheme.Border;
        button.FlatAppearance.MouseOverBackColor = GuardianTheme.SurfaceRaised;
        return button;
    }

    private static bool PathEquals(string left, string right)
    {
        try
        {
            return string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }
}
