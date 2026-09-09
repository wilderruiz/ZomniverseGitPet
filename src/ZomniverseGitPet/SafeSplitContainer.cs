using System.Runtime.CompilerServices;

namespace ZomniverseGitPet;

/// <summary>
/// SplitContainer that remains valid while WinForms passes through transient tiny sizes
/// during modal dialogs, DPI/layout changes, visibility switches, and window resizing.
/// Native panel minimums intentionally stay at zero; the preferred minimum is a visual
/// policy applied only when there is enough real space.
/// </summary>
internal sealed class SafeSplitContainer : System.Windows.Forms.SplitContainer
{
    private bool _applyingLayout;
    private double _preferredRatio = 0.5;
    private int _preferredPaneMinimum = 120;

    public SafeSplitContainer()
    {
        Panel1MinSize = 0;
        Panel2MinSize = 0;
        SplitterMoved += OnUserSplitterMoved;
    }

    public double PreferredRatio
    {
        get => _preferredRatio;
        set
        {
            _preferredRatio = Math.Clamp(value, 0.02, 0.98);
            ApplyPreferredLayout();
        }
    }

    public int PreferredPaneMinimum
    {
        get => _preferredPaneMinimum;
        set
        {
            _preferredPaneMinimum = Math.Max(0, value);
            ApplyPreferredLayout();
        }
    }

    internal static int CalculateSafeDistance(
        int primarySize,
        int splitterWidth,
        double ratio,
        int preferredPaneMinimum)
    {
        var available = Math.Max(0, primarySize - Math.Max(0, splitterWidth));
        if (available == 0) return 0;

        ratio = Math.Clamp(ratio, 0.02, 0.98);
        var minimum = Math.Min(Math.Max(0, preferredPaneMinimum), available / 2);
        var desired = (int)Math.Round(available * ratio);
        return Math.Clamp(desired, minimum, available - minimum);
    }

    protected override void SetBoundsCore(
        int x,
        int y,
        int width,
        int height,
        BoundsSpecified specified)
    {
        EnsureNativeMinimumsAreZero();

        // WinForms validates SplitterDistance while applying new bounds. If the control is
        // about to become smaller, lower the divider first while the old bounds are still
        // valid. Expansions need no pre-adjustment.
        var proposedPrimary = Orientation == Orientation.Vertical ? width : height;
        var proposedAvailable = Math.Max(0, proposedPrimary - SplitterWidth);
        if (SplitterDistance > proposedAvailable)
            TrySetDistance(proposedAvailable);

        base.SetBoundsCore(x, y, width, height, specified);
        ApplyPreferredLayout();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        EnsureNativeMinimumsAreZero();
        ApplyPreferredLayout();
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        EnsureNativeMinimumsAreZero();
        base.OnLayout(levent);
        ApplyPreferredLayout();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible) ApplyPreferredLayout();
    }

    private void OnUserSplitterMoved(object? sender, SplitterEventArgs e)
    {
        if (_applyingLayout) return;
        var available = GetAvailablePrimarySize();
        if (available <= 0) return;
        _preferredRatio = Math.Clamp((double)SplitterDistance / available, 0.02, 0.98);
    }

    private int GetAvailablePrimarySize()
    {
        var primary = Orientation == Orientation.Vertical ? ClientSize.Width : ClientSize.Height;
        return Math.Max(0, primary - SplitterWidth);
    }

    private void ApplyPreferredLayout()
    {
        if (_applyingLayout || IsDisposed || !IsHandleCreated) return;

        var primary = Orientation == Orientation.Vertical ? ClientSize.Width : ClientSize.Height;
        var safeDistance = CalculateSafeDistance(
            primary,
            SplitterWidth,
            _preferredRatio,
            _preferredPaneMinimum);

        TrySetDistance(safeDistance);
    }

    private void TrySetDistance(int distance)
    {
        if (_applyingLayout || IsDisposed) return;

        try
        {
            _applyingLayout = true;
            EnsureNativeMinimumsAreZero();
            if (SplitterDistance != distance)
                SplitterDistance = Math.Max(0, distance);
        }
        catch (InvalidOperationException)
        {
            // A parent may change size again between measurement and assignment. Leaving
            // the current divider in place is safer than surfacing a UI exception.
        }
        catch (ArgumentOutOfRangeException)
        {
            // Same transient-layout case on some WinForms versions/DPI combinations.
        }
        finally
        {
            _applyingLayout = false;
        }
    }

    private void EnsureNativeMinimumsAreZero()
    {
        try
        {
            if (Panel1MinSize != 0) Panel1MinSize = 0;
            if (Panel2MinSize != 0) Panel2MinSize = 0;
        }
        catch (InvalidOperationException)
        {
            // A layout transition can briefly make even a harmless property assignment
            // revalidate the current divider. The next lifecycle pass will retry.
        }
    }
}

/// <summary>
/// Retrofits any SplitContainer created outside GitPet source code. Current in-project
/// SplitContainer references are aliased to SafeSplitContainer, but this remains as a
/// defensive guard for third-party/designer-created controls.
/// </summary>
internal static class SplitContainerSafety
{
    private sealed class LegacyState
    {
        public double Ratio { get; set; } = 0.5;
        public bool Applying { get; set; }
    }

    private static readonly ConditionalWeakTable<System.Windows.Forms.SplitContainer, LegacyState> Guarded = new();
    private static bool _installed;

    public static void InstallForApplication()
    {
        if (_installed) return;
        _installed = true;
        Application.Idle += (_, _) => GuardOpenForms();
    }

    internal static void GuardOpenForms()
    {
        foreach (Form form in Application.OpenForms)
            GuardTree(form);
    }

    private static void GuardTree(Control root)
    {
        if (root is System.Windows.Forms.SplitContainer split && root is not SafeSplitContainer)
            GuardLegacy(split);

        foreach (Control child in root.Controls)
            GuardTree(child);
    }

    private static void GuardLegacy(System.Windows.Forms.SplitContainer split)
    {
        if (Guarded.TryGetValue(split, out _)) return;

        var state = new LegacyState();
        var available = Available(split);
        if (available > 0)
            state.Ratio = Math.Clamp((double)split.SplitterDistance / available, 0.02, 0.98);

        Guarded.Add(split, state);
        NormalizeLegacy(split, state);

        split.SizeChanged += (_, _) => NormalizeLegacy(split, state);
        split.VisibleChanged += (_, _) =>
        {
            if (split.Visible) NormalizeLegacy(split, state);
        };
        split.SplitterMoved += (_, _) =>
        {
            if (state.Applying) return;
            var currentAvailable = Available(split);
            if (currentAvailable > 0)
                state.Ratio = Math.Clamp((double)split.SplitterDistance / currentAvailable, 0.02, 0.98);
        };
    }

    private static int Available(System.Windows.Forms.SplitContainer split)
    {
        var primary = split.Orientation == Orientation.Vertical
            ? split.ClientSize.Width
            : split.ClientSize.Height;
        return Math.Max(0, primary - split.SplitterWidth);
    }

    private static void NormalizeLegacy(System.Windows.Forms.SplitContainer split, LegacyState state)
    {
        if (state.Applying || split.IsDisposed) return;

        try
        {
            state.Applying = true;
            if (split.Panel1MinSize != 0) split.Panel1MinSize = 0;
            if (split.Panel2MinSize != 0) split.Panel2MinSize = 0;

            var primary = split.Orientation == Orientation.Vertical
                ? split.ClientSize.Width
                : split.ClientSize.Height;
            var safe = SafeSplitContainer.CalculateSafeDistance(primary, split.SplitterWidth, state.Ratio, 0);
            if (split.SplitterDistance != safe)
                split.SplitterDistance = safe;
        }
        catch (InvalidOperationException)
        {
            // The next Application.Idle / size lifecycle pass retries safely.
        }
        catch (ArgumentOutOfRangeException)
        {
            // Transient WinForms bounds; do not surface an application-level exception.
        }
        finally
        {
            state.Applying = false;
        }
    }
}
