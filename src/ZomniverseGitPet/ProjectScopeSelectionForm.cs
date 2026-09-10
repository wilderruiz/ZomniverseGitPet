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
    private readonly IReadOnlyList<ProjectScopeEntry>? _initialEntries;
    private readonly TreeView _tree = new();
    private readonly Label _summary = new();
    private readonly Button _continueButton;
    private bool _updatingChecks;

    private enum ScopeCheckState
    {
        Unchecked = 0,
        Checked = 1,
        Indeterminate = 2
    }

    public ProjectScopeSelectionForm(
        string rootPath,
        IReadOnlyList<ProjectScopeEntry>? initialEntries = null)
    {
        _rootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        _initialEntries = initialEntries;

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
       HELPER: ToggleScopeNode
       FUNCTION:
       Toggles a selectable node, propagates its new state downward, and
       recalculates every parent state.

       DATE.TIME ADDED: 2026-09-10 10:15 +03:00

       REASON:
       Keep parent and child states synchronized after mouse or keyboard input.
       ========================================================================== */

    private void ToggleScopeNode(TreeNode node)
    {
        var value = GetScopeNodeState(node) != ScopeCheckState.Checked;

        _updatingChecks = true;
        try
        {
            SetScopeNodeState(
                node,
                value ? ScopeCheckState.Checked : ScopeCheckState.Unchecked);

            SetLoadedDescendants(node, value);
            UpdateAncestorStates(node.Parent);
        }
        finally
        {
            _updatingChecks = false;
        }

        UpdateSummary();
    }

    /* ==========================================================================
       HELPER: GetScopeNodeState
       FUNCTION:
       Reads and validates a tree node’s custom three-state checkbox value.

       DATE.TIME ADDED: 2026-09-10 10:15 +03:00

       REASON:
       Convert the displayed state-image index into a safe selection state.
       ========================================================================== */

    private static ScopeCheckState GetScopeNodeState(TreeNode node)
    {
        return Enum.IsDefined(typeof(ScopeCheckState), node.StateImageIndex)
            ? (ScopeCheckState)node.StateImageIndex
            : node.Checked
                ? ScopeCheckState.Checked
                : ScopeCheckState.Unchecked;
    }

    /* ==========================================================================
       HELPER: SetScopeNodeState
       FUNCTION:
       Updates both the custom state image and the logical checked value
       used when building the scope plan.

       DATE.TIME ADDED: 2026-09-10 10:15 +03:00

       REASON:
       Keep visual and saved selection states consistent.
       ========================================================================== */

    private static void SetScopeNodeState(
        TreeNode node,
        ScopeCheckState state)
    {
        node.StateImageIndex = (int)state;
        node.Checked = state == ScopeCheckState.Checked;
    }

    /* ==========================================================================
       HELPER: CreateScopeStateImages
       FUNCTION:
       Creates the image collection used for unchecked, checked, and
       partially selected project-scope nodes.

       DATE.TIME ADDED: 2026-09-10 10:15 +03:00

       REASON:
       Supply the missing visual image for indeterminate parent folders.
       ========================================================================== */

    private static ImageList CreateScopeStateImages()
    {
        var images = new ImageList
        {
            ColorDepth = ColorDepth.Depth32Bit,
            ImageSize = new Size(18, 18),
            TransparentColor = Color.Transparent
        };

        images.Images.Add(CreateScopeStateImage(ScopeCheckState.Unchecked));
        images.Images.Add(CreateScopeStateImage(ScopeCheckState.Checked));
        images.Images.Add(CreateScopeStateImage(ScopeCheckState.Indeterminate));

        return images;
    }

    /* ==========================================================================
       HELPER: CreateScopeStateImage
       FUNCTION:
       Draws one themed checkbox image for the requested project-scope
       selection state.

       DATE.TIME ADDED: 2026-09-10 10:15 +03:00

       REASON:
       Draw a clear checkmark or minus symbol for each checkbox state.
       ========================================================================== */

    private static Bitmap CreateScopeStateImage(ScopeCheckState state)
    {
        var bitmap = new Bitmap(18, 18);

        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.SmoothingMode =
            System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        var box = new Rectangle(1, 1, 15, 15);
        var fillColor = state == ScopeCheckState.Unchecked
            ? GuardianTheme.Console
            : Color.FromArgb(32, 118, 196);

        using var fill = new SolidBrush(fillColor);
        using var border = new Pen(
            state == ScopeCheckState.Unchecked
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

        if (state == ScopeCheckState.Checked)
        {
            graphics.DrawLines(
                symbol,
                [
                    new PointF(4.2f, 8.8f),
                    new PointF(7.1f, 11.5f),
                    new PointF(13.2f, 5.2f)
                ]);
        }
        else if (state == ScopeCheckState.Indeterminate)
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
            foreach (var path in EnumerateEntries(_rootPath))
            {
                var node = CreateNode(path, inheritedChecked: true);
                _tree.Nodes.Add(node);
            }
        }
        finally { _tree.EndUpdate(); }
    }

    /* ==========================================================================
       PATCH: INITIAL THREE-STATE NODE DISPLAY
       FUNCTION:
       Creates a project-scope node and assigns its restored checked,
       unchecked, or partially selected state.

       DATE.TIME ADDED: 2026-09-10 10:05 +03:00

       REASON:
       Restore saved node selections and identify partially selected folders.
       ========================================================================== */

    private TreeNode CreateNode(string fullPath, bool inheritedChecked)
    {
        var isDirectory = Directory.Exists(fullPath);
        var relative = Path.GetRelativePath(_rootPath, fullPath).Replace('\\', '/');
        var nestedRepository = isDirectory && ProjectScopePlanner.IsDirectNestedRepository(fullPath);
        var name = Path.GetFileName(fullPath);
        var prefix = isDirectory ? "▸  " : "·  ";
        var suffix = nestedRepository ? "    [existing Git repository — excluded]" : "";

        var initialState = GetInitialState(relative, isDirectory, inheritedChecked);

        var node = new TreeNode(prefix + name + suffix)
        {
            Tag = new ScopeNodeInfo(fullPath, relative, isDirectory, nestedRepository),
            Checked = initialState == ScopeCheckState.Checked && !nestedRepository,
            StateImageIndex = nestedRepository
                ? (int)ScopeCheckState.Unchecked
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
    /* ==========================================================================
       HELPER: GetInitialState
       FUNCTION:
       Compares a filesystem node with the saved project scope and returns
       its initial checked, unchecked, or indeterminate state.

       DATE.TIME ADDED: 2026-09-10 10:05 +03:00

       REASON:
       Reconstruct each node’s previous selection when reopening project scope.
       ========================================================================== */

    private ScopeCheckState GetInitialState(
        string relativePath,
        bool isDirectory,
        bool inheritedChecked)
    {
        if (_initialEntries is null)
        {
            return inheritedChecked
                ? ScopeCheckState.Checked
                : ScopeCheckState.Unchecked;
        }

        var normalized = relativePath.Replace('\\', '/').Trim('/');

        var exactSelection = _initialEntries.Any(entry =>
            entry.IsDirectory == isDirectory &&
            entry.RelativePath.Equals(
                normalized,
                StringComparison.OrdinalIgnoreCase));

        if (exactSelection)
            return ScopeCheckState.Checked;

        var containsSelectedDescendant = isDirectory && _initialEntries.Any(entry =>
            entry.RelativePath.StartsWith(
                normalized + "/",
                StringComparison.OrdinalIgnoreCase));

        return containsSelectedDescendant
            ? ScopeCheckState.Indeterminate
            : ScopeCheckState.Unchecked;
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

    /* ==========================================================================
       PATCH: CORRECT SELECT-ALL STATE UPDATE
       FUNCTION:
       Applies the requested checked state to every selectable root node and
       refreshes the summary after propagation completes.

       DATE.TIME ADDED: 2026-09-10 10:05 +03:00

       REASON:
       Remove an invalid reference to the loop-local node variable.
       ========================================================================== */

    private void SetAllRootChecks(bool value)
    {
        _updatingChecks = true;
        try
        {
            foreach (TreeNode node in _tree.Nodes)
            {
                if (node.Tag is ScopeNodeInfo { Locked: true }) continue;
                node.Checked = value;
                node.StateImageIndex = value
                    ? (int)ScopeCheckState.Checked
                    : (int)ScopeCheckState.Unchecked;
                SetLoadedDescendants(node, value);
            }
        }
        finally { _updatingChecks = false; }

        UpdateSummary();
    }

    /* ==========================================================================
       PATCH: SYNCHRONIZE DESCENDANT VISUAL STATES
       FUNCTION:
       Applies the selected logical and visual checkbox state to every loaded
       selectable descendant.

       DATE.TIME ADDED: 2026-09-10 10:15 +03:00

       REASON:
       Keep descendant checkboxes consistent when a parent selection changes.
       ========================================================================== */

    private static void SetLoadedDescendants(TreeNode node, bool value)
    {
        var state = value
            ? ScopeCheckState.Checked
            : ScopeCheckState.Unchecked;

        foreach (TreeNode child in node.Nodes)
        {
            if (child.Tag is LazyMarker || child.Tag is ScopeNodeInfo { Locked: true }) continue;

            SetScopeNodeState(child, state);
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

    /* ==========================================================================
       HELPER: UpdateAncestorStates
       FUNCTION:
       Walks upward from a changed node and recalculates each parent’s
       checked, unchecked, or indeterminate state.

       DATE.TIME ADDED: 2026-09-10 10:15 +03:00

       REASON:
       Display a minus whenever a folder contains mixed child selections.
       ========================================================================== */

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
                    childState => childState == ScopeCheckState.Checked)
                        ? ScopeCheckState.Checked
                        : states.All(
                            childState => childState == ScopeCheckState.Unchecked)
                            ? ScopeCheckState.Unchecked
                            : ScopeCheckState.Indeterminate;

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
