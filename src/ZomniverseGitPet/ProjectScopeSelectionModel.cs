namespace ZomniverseGitPet;

internal enum ProjectScopeCheckState
{
    Unchecked = 0,
    Checked = 1,
    Indeterminate = 2
}

/* ==========================================================================
   PATCH: AUTHORITATIVE PROJECT SCOPE STATE
   DATE.TIME: 2026-09-10 11:04 +03:00
   REASON:
   Preserve restored selections across lazy loading and later user overrides.
   ========================================================================== */
internal sealed class ProjectScopeSelectionModel
{
    private readonly IReadOnlyList<ProjectScopeEntry>? _initialEntries;
    private readonly Dictionary<string, bool> _subtreeOverrides = new(StringComparer.OrdinalIgnoreCase);

    public ProjectScopeSelectionModel(IEnumerable<ProjectScopeEntry>? initialEntries)
    {
        _initialEntries = initialEntries is null
            ? null
            : initialEntries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.RelativePath))
                .Select(entry => new ProjectScopeEntry(Normalize(entry.RelativePath), entry.IsDirectory))
                .Where(entry => entry.RelativePath.Length > 0)
                .DistinctBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase)
                .OrderBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToArray();
    }

    public bool HasRestoredScope => _initialEntries is not null;

    public ProjectScopeCheckState GetState(
        string relativePath,
        bool isDirectory,
        bool inheritedChecked)
    {
        var normalized = Normalize(relativePath);

        if (TryGetOverride(normalized, out var overridden))
        {
            if (isDirectory && HasContraryDescendantOverride(normalized, overridden))
                return ProjectScopeCheckState.Indeterminate;

            return overridden ? ProjectScopeCheckState.Checked : ProjectScopeCheckState.Unchecked;
        }

        if (_initialEntries is null)
            return inheritedChecked ? ProjectScopeCheckState.Checked : ProjectScopeCheckState.Unchecked;

        if (inheritedChecked)
            return ProjectScopeCheckState.Checked;

        var coveredBySelection = _initialEntries.Any(entry =>
            PathsEqual(entry.RelativePath, normalized) ||
            (entry.IsDirectory && IsDescendant(normalized, entry.RelativePath)));

        if (coveredBySelection)
            return ProjectScopeCheckState.Checked;

        var containsSelectedDescendant = isDirectory && _initialEntries.Any(entry =>
            IsDescendant(entry.RelativePath, normalized));

        return containsSelectedDescendant
            ? ProjectScopeCheckState.Indeterminate
            : ProjectScopeCheckState.Unchecked;
    }

    public void SetSubtree(string relativePath, bool selected)
    {
        var normalized = Normalize(relativePath);
        if (normalized.Length == 0) return;

        foreach (var key in _subtreeOverrides.Keys
                     .Where(key => PathsEqual(key, normalized) || IsDescendant(key, normalized))
                     .ToArray())
        {
            _subtreeOverrides.Remove(key);
        }

        _subtreeOverrides[normalized] = selected;
    }

    public IReadOnlyList<ProjectScopeEntry> GetPreservedSelections(string relativePath)
    {
        if (_initialEntries is null) return Array.Empty<ProjectScopeEntry>();

        var normalized = Normalize(relativePath);
        return _initialEntries
            .Where(entry => PathsEqual(entry.RelativePath, normalized) || IsDescendant(entry.RelativePath, normalized))
            .Where(entry => !TryGetOverride(entry.RelativePath, out var selected) || selected)
            .ToArray();
    }

    private bool TryGetOverride(string relativePath, out bool selected)
    {
        var normalized = Normalize(relativePath);
        var bestLength = -1;
        var found = false;
        selected = false;

        foreach (var (path, value) in _subtreeOverrides)
        {
            if (!PathsEqual(path, normalized) && !IsDescendant(normalized, path)) continue;
            if (path.Length <= bestLength) continue;

            bestLength = path.Length;
            selected = value;
            found = true;
        }

        return found;
    }

    private bool HasContraryDescendantOverride(string relativePath, bool inheritedValue) =>
        _subtreeOverrides.Any(pair =>
            IsDescendant(pair.Key, relativePath) && pair.Value != inheritedValue);

    private static bool IsDescendant(string candidate, string ancestor)
    {
        if (ancestor.Length == 0) return candidate.Length > 0;
        return candidate.StartsWith(ancestor + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string path) =>
        (path ?? string.Empty).Replace('\\', '/').Trim().Trim('/');
}
