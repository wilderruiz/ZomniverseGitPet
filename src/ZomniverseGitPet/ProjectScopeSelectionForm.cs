namespace ZomniverseGitPet;

internal sealed class ProjectScopeSelectionForm : Form
{
    /* ==========================================================================
       PATCH: THREE-STATE PROJECT TREE
       FUNCTION:
       Adds saved scope entries and three possible selection states to the
       project-scope tree.

       DATE.TIME ADDED: 2026-09-10 10:05 +03:00

       REASON:
       Support restored selections and partially selected parent folders.
       ========================================================================== */

    private readonly string _rootPath;
    private readonly ProjectScopeSelectionModel _selectionModel;
    private readonly TreeView _tree = new();
    private readonly Label _summary = new();
    private readonly Button _continueButton;

    public ProjectScopeSelectionForm(
        string rootPath,
        IReadOnlyList<ProjectScopeEntry>? initialEntries = null)
    {
        _rootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        _selectionModel = new ProjectScopeSelectionModel(initialEntries);

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
            Text = initialEntries is null
                ? "Git works from one project root, but that root can contain things that do not belong to the same project.\r\n\r\n" +
                  "Everything starts selected, like a selective-sync view. Uncheck anything GitPet should keep outside this repository. " +
                  "Expand a folder if you want to include only part of it. Existing nested Git repositories are protected and excluded automatically."
                : "GitPet restored the project's previous tracking scope.\r\n\r\n" +
                  "Checked items remain included; a minus means only part of that folder is selected. " +
                  "Expand folders to adjust individual items. Existing nested Git repositories stay protected and excluded automatically."
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

    /* ==========================================================================
       PATCH: RENDER THREE-STATE SCOPE CHECKBOXES
       FUNCTION:
       Configures custom checked, unchecked, and indeterminate images and
       handles mouse and keyboard selection changes.

       DATE.TIME ADDED: 2026-09-10 10:15 +03:00

       REASON:
       Render partial parent selections instead of displaying an empty native checkbox.
       ========================================================================== */

    private void ConfigureTree()
    {
        _tree.Dock = DockStyle.Fill;
        _tree.CheckBoxes = false;
        _tree.StateImageList = CreateScopeStateImages();
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

        _tree.NodeMouseClick += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            if (e.Node.Tag is not ScopeNodeInfo { Locked: false }) return;
            if (_tree.StateImageList is null) return;

            var imageSize = _tree.StateImageList.ImageSize;
            var stateImageBounds = new Rectangle(
                e.Node.Bounds.Left - imageSize.Width - 4,
                e.Node.Bounds.Top + ((_tree.ItemHeight - imageSize.Height) / 2),
                imageSize.Width + 4,
                imageSize.Height);

            if (!stateImageBounds.Contains(e.Location)) return;

            ToggleScopeNode(e.Node);
        };

        _tree.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Space || _tree.SelectedNode is null) return;
            if (_tree.SelectedNode.Tag is not ScopeNodeInfo { Locked: false }) return;

            ToggleScopeNode(_tree.SelectedNode);
            e.Handled = true;
            e.SuppressKeyPress = true;
        };
    }

    /* ==========================================================================
       PATCH: LAZY SCOPE OVERRIDE MODEL
       DATE.TIME: 2026-09-10 11:04 +03:00
       REASON:
       Keep user choices authoritative before unopened descendants are loaded.
       ========================================================================== */
    private void ToggleScopeNode(TreeNode node)
    {
        if (node.Tag is not ScopeNodeInfo info || info.Locked) return;

        var value = GetScopeNodeState(node) != ProjectScopeCheckState.Checked;
        _selectionModel.SetSubtree(info.RelativePath, value);

        SetScopeNodeState(
            node,
            value ? ProjectScopeCheckState.Checked : ProjectScopeCheckState.Unchecked);

        SetLoadedDescendants(node, value);
        UpdateAncestorStates(node.Parent);
        UpdateSummary();
    }

    private static ProjectScopeCheckState GetScopeNodeState(TreeNode node)
    {
        return Enum.IsDefined(typeof(ProjectScopeCheckState), node.StateImageIndex)
            ? (ProjectScopeCheckState)node.StateImageIndex
            : node.Checked
                ? ProjectScopeCheckState.Checked
                : ProjectScopeCheckState.Unchecked;
    }

    private static void SetScopeNodeState(
        TreeNode node,
        ProjectScopeCheckState state)
    {
        node.StateImageIndex = (int)state;
        node.Checked = state == ProjectScopeCheckState.Checked;
    }

    private static ImageList CreateScopeStateImages()
    {
        var images = new ImageList
        {
            ColorDepth = ColorDepth.Depth32Bit,
            ImageSize = new Size(18, 18),
            TransparentColor = Color.Transparent
        };

        images.Images.Add(CreateScopeStateImage(ProjectScopeCheckState.Unchecked));
        images.Images.Add(CreateScopeStateImage(ProjectScopeCheckState.Checked));
        images.Images.Add(CreateScopeStateImage(ProjectScopeCheckState.Indeterminate));

        return images;
    }

    private static Bitmap CreateScopeStateImage(ProjectScopeCheckState state)
    {
        var bitmap = new Bitmap(18, 18);

        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.SmoothingMode =
            System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        var box = new Rectangle(1, 1, 15, 15);
        var fillColor = state == ProjectScopeCheckState.Unchecked
            ? GuardianTheme.Console
            : Color.FromArgb(32, 118, 196);

        using var fill = new SolidBrush(fillColor);
        using var border = new Pen(
            state == ProjectScopeCheckState.Unchecked
                ? GuardianTheme.Border
                : Color.FromArgb(72, 148, 230),
            1.4f);

        graphics.FillRectangle(fill, box);
        graphics.DrawRectangle(border, box);

        using var symbol = new Pen(Color.White, 2.2f)
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round
        };

        if (state == ProjectScopeCheckState.Checked)
        {
            graphics.DrawLines(
                symbol,
                [
                    new PointF(4.2f, 8.8f),
                    new PointF(7.1f, 11.5f),
                    new PointF(13.2f, 5.2f)
                ]);
        }
        else if (state == ProjectScopeCheckState.Indeterminate)
        {
            graphics.DrawLine(symbol, 4.5f, 8.5f, 12.5f, 8.5f);
        }

        return bitmap;
    }

    private void LoadRootEntries()
    {
        _tree.BeginUpdate();
        try
        {
            _tree.Nodes.Clear();
            var defaultChecked = !_selectionModel.HasRestoredScope;
            foreach (var path in EnumerateEntries(_rootPath))
            {
                var node = CreateNode(path, inheritedChecked: defaultChecked);
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

        var initialState = _selectionModel.GetState(relative, isDirectory, inheritedChecked);

        var node = new TreeNode(prefix + name + suffix)
        {
            Tag = new ScopeNodeInfo(fullPath, relative, isDirectory, nestedRepository),
            Checked = initialState == ProjectScopeCheckState.Checked && !nestedRepository,
            StateImageIndex = nestedRepository
                ? (int)ProjectScopeCheckState.Unchecked
                : (int)initialState,
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

    /* ==========================================================================
       PATCH: PRESERVE COLLAPSED PARTIAL SCOPE
       DATE.TIME: 2026-09-10 11:04 +03:00
       REASON:
       Retain saved descendants even when their parent remains lazily collapsed.
       ========================================================================== */
    private IReadOnlyList<ProjectScopeEntry> BuildSelectedEntries()
    {
        var selected = new Dictionary<string, ProjectScopeEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (TreeNode node in _tree.Nodes)
            CollectSelected(node, selected);

        return selected.Values
            .OrderBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void CollectSelected(
        TreeNode node,
        Dictionary<string, ProjectScopeEntry> selected)
    {
        if (node.Tag is not ScopeNodeInfo info || info.Locked) return;

        var state = GetScopeNodeState(node);
        if (state == ProjectScopeCheckState.Checked)
        {
            selected[info.RelativePath] = new ProjectScopeEntry(info.RelativePath, info.IsDirectory);
            return;
        }

        var hasLazyChildren = node.Nodes
            .Cast<TreeNode>()
            .Any(child => child.Tag is LazyMarker);

        if (state == ProjectScopeCheckState.Indeterminate && hasLazyChildren)
        {
            foreach (var entry in _selectionModel.GetPreservedSelections(info.RelativePath))
                selected[entry.RelativePath] = entry;
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
        foreach (TreeNode node in _tree.Nodes)
        {
            if (node.Tag is not ScopeNodeInfo info || info.Locked) continue;

            _selectionModel.SetSubtree(info.RelativePath, value);
            SetScopeNodeState(
                node,
                value ? ProjectScopeCheckState.Checked : ProjectScopeCheckState.Unchecked);
            SetLoadedDescendants(node, value);
        }

        UpdateSummary();
    }

    private static void SetLoadedDescendants(TreeNode node, bool value)
    {
        var state = value
            ? ProjectScopeCheckState.Checked
            : ProjectScopeCheckState.Unchecked;

        foreach (TreeNode child in node.Nodes)
        {
            if (child.Tag is LazyMarker || child.Tag is ScopeNodeInfo { Locked: true }) continue;

            SetScopeNodeState(child, state);
            SetLoadedDescendants(child, value);
        }
    }

    private void UpdateAncestorStates(TreeNode? parent)
    {
        while (parent is not null)
        {
            var children = parent.Nodes
                .Cast<TreeNode>()
                .Where(child =>
                    child.Tag is not LazyMarker &&
                    child.Tag is not ScopeNodeInfo { Locked: true })
                .ToArray();

            if (children.Length > 0)
            {
                var states = children
                    .Select(GetScopeNodeState)
                    .ToArray();

                var state = states.All(
                    childState => childState == ProjectScopeCheckState.Checked)
                        ? ProjectScopeCheckState.Checked
                        : states.All(
                            childState => childState == ProjectScopeCheckState.Unchecked)
                            ? ProjectScopeCheckState.Unchecked
                            : ProjectScopeCheckState.Indeterminate;

                SetScopeNodeState(parent, state);
            }

            parent = parent.Parent;
        }
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
