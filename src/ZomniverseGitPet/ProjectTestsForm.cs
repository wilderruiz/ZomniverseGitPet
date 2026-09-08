namespace ZomniverseGitPet;

internal sealed class ProjectTestsForm : Form
{
    private static readonly Color Surface = Color.FromArgb(18, 15, 27);
    private static readonly Color PanelSurface = Color.FromArgb(34, 25, 50);
    private static readonly Color CardSurface = Color.FromArgb(27, 21, 39);
    private static readonly Color InputSurface = Color.FromArgb(13, 11, 20);
    private static readonly Color Ink = Color.FromArgb(242, 237, 249);
    private static readonly Color MutedInk = Color.FromArgb(184, 173, 202);
    private static readonly Color Purple = Color.FromArgb(112, 70, 180);
    private static readonly Color HotPink = Color.FromArgb(236, 70, 170);
    private static readonly Color SoftPink = Color.FromArgb(246, 159, 195);

    private readonly TextBox _commands = new();
    private readonly Label _validation = new();

    public ProjectTestsForm(string projectName, string repositoryPath, IReadOnlyList<string> existingCommands)
    {
        Text = "Project tests";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        Size = new Size(920, 760);
        MinimumSize = new Size(760, 620);
        MaximumSize = new Size(1200, 980);
        BackColor = Surface;
        ForeColor = Ink;
        Font = new Font("Segoe UI", 9);
        Icon = AppIconProvider.Icon;

        var initial = existingCommands.Count > 0
            ? existingCommands
            : ProjectTestAdvisor.Suggest(repositoryPath);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Surface
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 116));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 138));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 138));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildIntro(projectName, existingCommands.Count == 0 && initial.Count > 0), 0, 1);
        root.Controls.Add(BuildCommandsCard(initial), 0, 2);
        root.Controls.Add(BuildSafetyCard(), 0, 3);
        root.Controls.Add(BuildButtons(), 0, 4);
        Controls.Add(root);
    }

    public IReadOnlyList<string> Commands => ParseCommands();
    public bool RunAfterSave { get; private set; }

    private Control BuildHeader()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = PanelSurface,
            Padding = new Padding(34, 20, 34, 16)
        };

        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 46,
            Text = "◇  PROJECT TESTS",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var subtitle = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Teach GitPet how this project checks itself.",
            Font = new Font("Segoe UI", 10.5f),
            ForeColor = Color.FromArgb(199, 186, 220),
            TextAlign = ContentAlignment.MiddleLeft
        };

        panel.Controls.Add(subtitle);
        panel.Controls.Add(title);
        return panel;
    }

    private Control BuildIntro(string projectName, bool suggestionsFound)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Padding = new Padding(38, 20, 38, 10)
        };

        var project = new Label
        {
            Dock = DockStyle.Top,
            Height = 32,
            Text = $"PROJECT  ·  {projectName}",
            ForeColor = Color.FromArgb(212, 188, 245),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };

        var explanation = new Label
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 10, 0, 0),
            Text = suggestionsFound
                ? "GitPet found likely test commands from this project's files. Review them below before saving. Put one command on each line."
                : "Put one test command on each line. GitPet runs them from the project root, in order, and stops when the first command fails.",
            ForeColor = Color.FromArgb(224, 216, 237),
            Font = new Font("Segoe UI", 10.2f),
            TextAlign = ContentAlignment.TopLeft
        };

        panel.Controls.Add(explanation);
        panel.Controls.Add(project);
        return panel;
    }

    private Control BuildCommandsCard(IReadOnlyList<string> initial)
    {
        var outer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Padding = new Padding(38, 0, 38, 16)
        };

        var card = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(22, 16, 22, 14),
            BackColor = CardSurface
        };
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

        card.Controls.Add(new Label
        {
            Text = "TEST COMMANDS  ·  ONE PER LINE",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(194, 171, 229),
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        _commands.Dock = DockStyle.Fill;
        _commands.Multiline = true;
        _commands.AcceptsReturn = true;
        _commands.AcceptsTab = false;
        _commands.ScrollBars = ScrollBars.Both;
        _commands.WordWrap = false;
        _commands.BackColor = InputSurface;
        _commands.ForeColor = Color.White;
        _commands.BorderStyle = BorderStyle.FixedSingle;
        _commands.Font = new Font("Cascadia Mono", 10);
        _commands.Text = string.Join(Environment.NewLine, initial);
        card.Controls.Add(_commands, 0, 1);

        _validation.Dock = DockStyle.Fill;
        _validation.Padding = new Padding(0, 8, 0, 0);
        _validation.ForeColor = MutedInk;
        _validation.Font = new Font("Segoe UI", 8.8f);
        _validation.TextAlign = ContentAlignment.TopLeft;
        _validation.Text = initial.Count > 0
            ? $"{initial.Count} command{(initial.Count == 1 ? "" : "s")} ready for review."
            : "No commands detected. Add the commands your project normally uses to verify itself.";
        card.Controls.Add(_validation, 0, 2);

        outer.Controls.Add(card);
        return outer;
    }

    private Control BuildSafetyCard()
    {
        var outer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Padding = new Padding(38, 4, 38, 14)
        };

        var text = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            BackColor = Color.FromArgb(38, 29, 54),
            ForeColor = MutedInk,
            Font = new Font("Segoe UI", 9.5f),
            WordWrap = true,
            DetectUrls = false,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            TabStop = false,
            Cursor = Cursors.Arrow,
            Text = "HOW TESTS WORK\n\n" +
                   "These commands are saved only in GitPet's local per-user configuration for this project. They are not written into the repository.\n\n" +
                   "GitPet runs them only when you press Tests, or before an automatic checkpoint if you explicitly enable automatic verified checkpoints with required tests. Treat test commands as trusted local commands."
        };

        outer.Controls.Add(text);
        return outer;
    }

    private Control BuildButtons()
    {
        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(18, 20, 28, 16),
            BackColor = PanelSurface
        };

        var saveRun = MakeButton("Save & run tests", true);
        var save = MakeButton("Save", false);
        var cancel = MakeButton("Cancel", false);

        saveRun.Click += (_, _) => TryAccept(true);
        save.Click += (_, _) => TryAccept(false);
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        footer.Controls.Add(saveRun);
        footer.Controls.Add(save);
        footer.Controls.Add(cancel);
        CancelButton = cancel;
        return footer;
    }

    private void TryAccept(bool runAfterSave)
    {
        var commands = ParseCommands();
        if (commands.Count > 20)
        {
            _validation.ForeColor = SoftPink;
            _validation.Text = "Keep this project to 20 test commands or fewer.";
            return;
        }

        if (commands.Any(command => command.Length > 2048 || command.Contains('\r') || command.Contains('\n')))
        {
            _validation.ForeColor = SoftPink;
            _validation.Text = "One of the test commands is too long or contains an invalid line break.";
            return;
        }

        RunAfterSave = runAfterSave;
        DialogResult = DialogResult.OK;
        Close();
    }

    private List<string> ParseCommands() =>
        _commands.Lines
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static Button MakeButton(string text, bool primary)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new Size(primary ? 160 : 96, 40),
            Height = 40,
            Margin = new Padding(8, 0, 0, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Purple : Color.FromArgb(64, 51, 80),
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderSize = primary ? 2 : 1;
        button.FlatAppearance.BorderColor = primary ? HotPink : Color.FromArgb(105, 90, 126);
        button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(132, 79, 198) : Color.FromArgb(78, 64, 98);
        return button;
    }
}
