namespace ZomniverseGitPet;

internal sealed class ProjectScopeSelectionForm : Form
{
    private readonly string _rootPath;
    private readonly TreeView _tree = new();
    private readonly Label _summary = new();
    private readonly Button _continueButton;
    private bool _updatingChecks;

    public ProjectScopeSelectionForm(string rootPath)
    {
        _rootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));

        Text = "Choose project contents";
        Icon = AppIconProvider.Icon;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        MaximizeBox = true;
        ShowInTaskbar = false;
        MinimumSize = new Size(820, 620);
        ClientSize = new Size(1040, 760);
        BackColor = GuardianTheme.Window;
        ForeColor = GuardianTheme.Ink;
        Font = new Font("Segoe UI", 9);
        WindowChrome.ApplyGuardianChrome(this);

        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 68,
            Padding = new Padding(24, 13, 24, 7),
            Text = "◇ CHOOSE WHAT BELONGS TO THIS PROJECT",
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = GuardianTheme.SurfaceRaised
        };

        var intro = new Label
        {
            Dock = DockStyle.Top,
            Height = 126,
            Padding = new Padding(24, 14, 24, 10),
            ForeColor = GuardianTheme.MutedInk,
            BackColor = GuardianTheme.Window,
            Text =
                "Git works from one project root, but that root can contain things that do not belong to the same project.\r\n\r\n" +
                "Everything starts selected, like a selective-sync view. Uncheck anything GitPet should keep outside this repository. " +
                "Expand a folder if you want to include only part of it. Existing nested Git repositories are protected and excluded automatically."
        };

        var rootCard = new Panel
        {
            Dock = DockStyle.Top,
            Height = 62,
            Padding = new Padding(18, 7, 18, 7),
            BackColor = GuardianTheme.SurfaceSoft
        };
        var rootLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "PROJECT ROOT\r\n" + _rootPath,
            ForeColor = GuardianTheme.Ink,
            Font = new Font("Cascadia Mono", 8.6f),
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft
        };
        rootCard.Controls.Add(rootLabel);

        ConfigureTree();
        LoadRootEntries();

        var treeHost = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18, 12, 18, 8),
            BackColor = GuardianTheme.Window
        };
        treeHost.Controls.Add(_tree);

        var footer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 94,
            Padding = new Padding(18, 10, 18, 10),
            BackColor = GuardianTheme.SurfaceRaised
        };

        _summary.Dock = DockStyle.Fill;
        _summary.ForeColor = GuardianTheme.MutedInk;
        _summary.TextAlign = ContentAlignment.MiddleLeft;

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 430,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 15, 0, 0),
            BackColor = GuardianTheme.SurfaceRaised
        };

        _continueButton = MakeButton("Continue →", primary: true, 128);
        var cancel = MakeButton("Cancel", primary: false, 88);
        var clear = MakeButton("Clear", primary: false, 82);
        var all = MakeButton("Select all", primary: false, 96);

        _continueButton.Click += (_, _) =>
        {
            if (BuildSelectedEntries().Count == 0) return;
            DialogResult = DialogResult.OK;
            Close();
        };
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        clear.Click += (_, _) => SetAllRootChecks(false);
        all.Click += (_, _) => SetAllRootChecks(true);

        buttons.Controls.Add(_continueButton);
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(clear);
        buttons.Controls.Add(all);
        footer.Controls.Add(buttons);
        footer.Controls.Add(_summary);

        Controls.Add(treeHost);
        Controls.Add(footer);
        Controls.Add(rootCard);
        Controls.Add(intro);
        Controls.Add(title);

        AcceptButton = _continueButton;
        CancelButton = cancel;
        UpdateSummary();
    }

    public ProjectScopePlan ScopePlan
    {
        get
        {
            var entries = BuildSelectedEntries();
            var selectableTopLevel = _tree.Nodes.Cast<TreeNode>()
                .Where(node => node.Tag is ScopeNodeInfo { Locked: false })
                .ToArray();
            var trackEverything = selectableTopLevel.Length > 0 && selectableTopLevel.All(node => node.Checked);
            return ProjectScopePlanner.Create(_rootPath, entries, trackEverything);
        }
    }

    private void ConfigureTree()
    {
        _tree.Dock = DockStyle.Fill;
        _tree.CheckBoxes = true;
        _tree.ShowNodeToolTips = true;
        _tree.HideSelection = false;
        _tree.FullRowSelect = true;
        _tree.BorderStyle = BorderStyle.FixedSingle;
        _tree.BackColor = GuardianTheme.Console;
        _tree.ForeColor = GuardianTheme.Ink;
        _tree.Font = new Font("Segoe UI", 10);
        _tree.ItemHeight = 28;
        _tree.LineColor = GuardianTheme.Border;

        _tree.BeforeExpand += (_, e) =>
        {
            if (e.Node is TreeNode node) EnsureChildrenLoaded(node);
        };
        _tree.BeforeCheck += (_, e) =>
        {
            if (e.Node is not TreeNode node) return;
            if (e.Action != TreeViewAction.Unknown && node.Tag is ScopeNodeInfo { Locked: true })
                e.Cancel = true;
        };
        _tree.AfterCheck += (_, e) =>
        {
            if (_updatingChecks || e.Node is not TreeNode node) return;
            if (node.Tag is ScopeNodeInfo { Locked: true })
            {
                SetNodeChecked(node, false, propagateLoadedChildren: false);
                return;
            }

            _updatingChecks = true;
            try
            {
                foreach (TreeNode child in node.Nodes)
                {
                    if (child.Tag is LazyMarker) continue;
                    if (child.Tag is ScopeNodeInfo { Locked: true }) continue;
                    child.Checked = node.Checked;
                    SetLoadedDescendants(child, node.Checked);
                }
            }
            finally { _updatingChecks = false; }
            UpdateSummary();
        };
    }

    private void LoadRootEntries()
    {
        _tree.BeginUpdate();
        try
        {
            _tree.Nodes.Clear();
            foreach (var path in EnumerateEntries(_rootPath))
            {
                var node = CreateNode(path, inheritedChecked: true);
                _tree.Nodes.Add(node);
            }
        }
        finally { _tree.EndUpdate(); }
    }

    private TreeNode CreateNode(string fullPath, bool inheritedChecked)
    {
        var isDirectory = Directory.Exists(fullPath);
        var relative = Path.GetRelativePath(_rootPath, fullPath).Replace('\\', '/');
        var nestedRepository = isDirectory && ProjectScopePlanner.IsDirectNestedRepository(fullPath);
        var name = Path.GetFileName(fullPath);
        var prefix = isDirectory ? "▸  " : "·  ";
        var suffix = nestedRepository ? "    [existing Git repository — excluded]" : "";

        var node = new TreeNode(prefix + name + suffix)
        {
            Tag = new ScopeNodeInfo(fullPath, relative, isDirectory, nestedRepository),
            Checked = inheritedChecked && !nestedRepository,
            ForeColor = nestedRepository ? GuardianTheme.FaintInk : GuardianTheme.Ink,
            ToolTipText = nestedRepository
                ? "This folder already has its own Git metadata. GitPet protects it from being absorbed into the new parent repository."
                : fullPath
        };

        if (isDirectory && !nestedRepository && HasVisibleChild(fullPath))
            node.Nodes.Add(new TreeNode("Loading…") { Tag = LazyMarker.Instance });

        return node;
    }

    private void EnsureChildrenLoaded(TreeNode node)
    {
        if (node.Tag is not ScopeNodeInfo info || !info.IsDirectory || info.Locked) return;
        if (node.Nodes.Count != 1 || node.Nodes[0].Tag is not LazyMarker) return;

        var inherit = node.Checked;
        node.Nodes.Clear();
        foreach (var path in EnumerateEntries(info.FullPath))
            node.Nodes.Add(CreateNode(path, inherit));
    }

    private IReadOnlyList<ProjectScopeEntry> BuildSelectedEntries()
    {
        var selected = new List<ProjectScopeEntry>();
        foreach (TreeNode node in _tree.Nodes)
            CollectSelected(node, selected);
        return selected;
    }

    private static void CollectSelected(TreeNode node, List<ProjectScopeEntry> selected)
    {
        if (node.Tag is not ScopeNodeInfo info || info.Locked) return;
        if (node.Checked)
        {
            selected.Add(new ProjectScopeEntry(info.RelativePath, info.IsDirectory));
            return;
        }

        foreach (TreeNode child in node.Nodes)
        {
            if (child.Tag is LazyMarker) continue;
            CollectSelected(child, selected);
        }
    }

    private void SetAllRootChecks(bool value)
    {
        _updatingChecks = true;
        try
        {
            foreach (TreeNode node in _tree.Nodes)
            {
                if (node.Tag is ScopeNodeInfo { Locked: true }) continue;
                node.Checked = value;
                SetLoadedDescendants(node, value);
            }
        }
        finally { _updatingChecks = false; }
        UpdateSummary();
    }

    private static void SetLoadedDescendants(TreeNode node, bool value)
    {
        foreach (TreeNode child in node.Nodes)
        {
            if (child.Tag is LazyMarker || child.Tag is ScopeNodeInfo { Locked: true }) continue;
            child.Checked = value;
            SetLoadedDescendants(child, value);
        }
    }

    private void SetNodeChecked(TreeNode node, bool value, bool propagateLoadedChildren)
    {
        _updatingChecks = true;
        try
        {
            node.Checked = value;
            if (propagateLoadedChildren) SetLoadedDescendants(node, value);
        }
        finally { _updatingChecks = false; }
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        var entries = BuildSelectedEntries();
        var nestedVisible = _tree.Nodes.Cast<TreeNode>().Count(node => node.Tag is ScopeNodeInfo { Locked: true });
        _summary.Text = entries.Count == 0
            ? "Choose at least one folder or file to continue."
            : $"{entries.Count} selected scope item{(entries.Count == 1 ? "" : "s")}. " +
              (nestedVisible > 0 ? $"{nestedVisible} visible nested Git repo{(nestedVisible == 1 ? " is" : "s are")} protected." : "You can expand folders for a narrower selection.");
        _continueButton.Enabled = entries.Count > 0;
    }

    private static IEnumerable<string> EnumerateEntries(string directory)
    {
        try
        {
            return Directory.EnumerateFileSystemEntries(directory)
                .Where(path => !Path.GetFileName(path).Equals(".git", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => Directory.Exists(path) ? 0 : 1)
                .ThenBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .Take(1000)
                .ToArray();
        }
        catch { return []; }
    }

    private static bool HasVisibleChild(string directory)
    {
        try
        {
            return Directory.EnumerateFileSystemEntries(directory)
                .Any(path => !Path.GetFileName(path).Equals(".git", StringComparison.OrdinalIgnoreCase));
        }
        catch { return false; }
    }

    private static Button MakeButton(string text, bool primary, int width)
    {
        var button = new Button
        {
            Text = text,
            Width = width,
            Height = 38,
            Margin = new Padding(6, 0, 0, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? GuardianTheme.Violet : GuardianTheme.SurfaceSoft,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderSize = primary ? 2 : 1;
        button.FlatAppearance.BorderColor = primary ? GuardianTheme.HotPink : GuardianTheme.Border;
        button.FlatAppearance.MouseOverBackColor = primary ? GuardianTheme.VioletHover : GuardianTheme.SurfaceRaised;
        return button;
    }

    private sealed record ScopeNodeInfo(
        string FullPath,
        string RelativePath,
        bool IsDirectory,
        bool Locked);

    private sealed class LazyMarker
    {
        public static LazyMarker Instance { get; } = new();
    }
}
