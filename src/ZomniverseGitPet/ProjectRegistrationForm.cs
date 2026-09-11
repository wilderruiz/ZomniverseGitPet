namespace ZomniverseGitPet;

internal enum ProjectRegistrationAction
{
    Cancel,
    AddProject,
    UseWholeRepository,
    Rename
}

/* ==========================================================================
   PATCH: LOGICAL PROJECT REGISTRATION
   DATE.TIME: 2026-09-11 14:08 +03:00
   Name subprojects without creating nested Git repositories.
   ========================================================================== */
internal sealed class ProjectRegistrationForm : Form
{
    private readonly TextBox _name = new();
    private readonly bool _renameOnly;

    public ProjectRegistrationForm(
        string selectedPath,
        string repositoryRoot,
        string suggestedName,
        bool renameOnly = false)
    {
        _renameOnly = renameOnly;
        Text = renameOnly ? "Rename GitPet project" : "Add project inside repository";
        Icon = AppIconProvider.Icon;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(720, renameOnly ? 330 : 430);
        BackColor = GuardianTheme.Window;
        ForeColor = GuardianTheme.Ink;
        Font = new Font("Segoe UI", 9.5f);
        WindowChrome.ApplyGuardianChrome(this);

        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 74,
            Padding = new Padding(24, 14, 24, 8),
            BackColor = GuardianTheme.SurfaceRaised,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            Text = renameOnly
                ? "◇ RENAME GITPET PROJECT"
                : "◇ ADD PROJECT INSIDE THIS REPOSITORY"
        };

        var explanation = new Label
        {
            Dock = DockStyle.Top,
            Height = renameOnly ? 72 : 112,
            Padding = new Padding(24, 14, 24, 8),
            BackColor = GuardianTheme.Window,
            ForeColor = GuardianTheme.MutedInk,
            Text = renameOnly
                ? "This changes only the name GitPet shows. The folder and Git repository are not renamed."
                : "This folder already belongs to a larger Git repository. GitPet can register it as its own logical project without creating another .git folder.\r\n\r\nGet, Send and Reconcile still use the shared repository history; Save and project status stay inside this project's selected scope."
        };

        var card = new Panel
        {
            Dock = DockStyle.Top,
            Height = renameOnly ? 110 : 154,
            Margin = new Padding(24),
            Padding = new Padding(24, 12, 24, 12),
            BackColor = GuardianTheme.SurfaceSoft
        };

        var details = new Label
        {
            Dock = DockStyle.Top,
            Height = renameOnly ? 26 : 68,
            ForeColor = GuardianTheme.FaintInk,
            Font = new Font("Cascadia Mono", 8.3f),
            AutoEllipsis = true,
            Text = renameOnly
                ? "PROJECT NAME"
                : $"SELECTED   {selectedPath}\r\nREPOSITORY {repositoryRoot}"
        };

        _name.Dock = DockStyle.Bottom;
        _name.Height = 34;
        _name.Text = string.IsNullOrWhiteSpace(suggestedName)
            ? Path.GetFileName(Path.TrimEndingDirectorySeparator(selectedPath))
            : suggestedName.Trim();
        _name.BackColor = GuardianTheme.Console;
        _name.ForeColor = GuardianTheme.Ink;
        _name.BorderStyle = BorderStyle.FixedSingle;
        _name.Font = new Font("Segoe UI", 10.5f);
        card.Controls.Add(_name);
        card.Controls.Add(details);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 72,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(16, 14, 16, 10),
            BackColor = GuardianTheme.SurfaceRaised
        };

        var cancel = MakeButton("Cancel", GuardianActionKind.Standard, 96);
        cancel.Click += (_, _) => Finish(ProjectRegistrationAction.Cancel);
        footer.Controls.Add(cancel);

        if (renameOnly)
        {
            var rename = MakeButton("Save name", GuardianActionKind.Primary, 126);
            rename.Click += (_, _) => Finish(ProjectRegistrationAction.Rename);
            footer.Controls.Add(rename);
            AcceptButton = rename;
        }
        else
        {
            var whole = MakeButton("Use whole repository", GuardianActionKind.Standard, 180);
            whole.Click += (_, _) => Finish(ProjectRegistrationAction.UseWholeRepository);
            footer.Controls.Add(whole);

            var add = MakeButton("Add GitPet project", GuardianActionKind.Primary, 170);
            add.Click += (_, _) => Finish(ProjectRegistrationAction.AddProject);
            footer.Controls.Add(add);
            AcceptButton = add;
        }

        CancelButton = cancel;
        Controls.Add(footer);
        Controls.Add(card);
        Controls.Add(explanation);
        Controls.Add(title);
        Shown += (_, _) => { _name.Focus(); _name.SelectAll(); };
    }

    public string ProjectName => _name.Text.Trim();
    public ProjectRegistrationAction SelectedAction { get; private set; }

    private void Finish(ProjectRegistrationAction action)
    {
        if (action is ProjectRegistrationAction.AddProject or ProjectRegistrationAction.Rename)
        {
            if (string.IsNullOrWhiteSpace(ProjectName))
            {
                MessageBox.Show(this, "Give this GitPet project a name first.", Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
        }

        SelectedAction = action;
        DialogResult = action == ProjectRegistrationAction.Cancel ? DialogResult.Cancel : DialogResult.OK;
        Close();
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
