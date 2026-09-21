using System.Text.Json;

namespace ZomniverseGitPet;

/* ==========================================================================
   PATCH: PERSISTENT PROJECT ALLOW LISTS
   DATE.TIME: 2026-09-11 21:10 +03:00
   Keep each project's pasted allow list across sessions.
   ========================================================================== */
internal sealed class ProjectAllowListStore
{
    private sealed class StoredAllowList
    {
        public string ProjectId { get; set; } = "";
        public string RepositoryRoot { get; set; } = "";
        public string ProjectPath { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public string Text { get; set; } = "";
        public DateTimeOffset UpdatedUtc { get; set; }
    }

    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _storePath;

    public ProjectAllowListStore()
        : this(Path.Combine(AppPaths.Root, "project-allow-lists.json"))
    {
    }

    internal ProjectAllowListStore(string storePath)
    {
        _storePath = Path.GetFullPath(storePath);
    }

    public string Load(
        string? projectId,
        string repositoryRoot,
        string projectPath,
        string projectName)
    {
        lock (Gate)
        {
            var entries = LoadUnsafe();
            var entry = Find(entries, projectId, repositoryRoot, projectPath, projectName);
            if (entry is null) return string.Empty;

            if (!string.IsNullOrWhiteSpace(projectId) &&
                !string.Equals(entry.ProjectId, projectId, StringComparison.OrdinalIgnoreCase))
            {
                entry.ProjectId = projectId.Trim();
                SaveUnsafe(entries);
            }

            return NormalizeText(entry.Text);
        }
    }

    public void Save(
        string? projectId,
        string repositoryRoot,
        string projectPath,
        string projectName,
        string? text)
    {
        lock (Gate)
        {
            var entries = LoadUnsafe();
            var existing = Find(entries, projectId, repositoryRoot, projectPath, projectName);
            var normalizedText = NormalizeText(text);

            if (normalizedText.Length == 0)
            {
                if (existing is not null)
                {
                    entries.Remove(existing);
                    SaveUnsafe(entries);
                }
                return;
            }

            existing ??= new StoredAllowList();
            if (!entries.Contains(existing)) entries.Add(existing);

            existing.ProjectId = string.IsNullOrWhiteSpace(projectId)
                ? existing.ProjectId
                : projectId.Trim();
            existing.RepositoryRoot = NormalizePath(repositoryRoot);
            existing.ProjectPath = NormalizePath(projectPath);
            existing.ProjectName = (projectName ?? string.Empty).Trim();
            existing.Text = normalizedText;
            existing.UpdatedUtc = DateTimeOffset.UtcNow;
            SaveUnsafe(entries);
        }
    }

    private StoredAllowList? Find(
        IEnumerable<StoredAllowList> entries,
        string? projectId,
        string repositoryRoot,
        string projectPath,
        string projectName)
    {
        var id = (projectId ?? string.Empty).Trim();
        if (id.Length > 0)
        {
            var byId = entries.FirstOrDefault(entry =>
                string.Equals(entry.ProjectId, id, StringComparison.OrdinalIgnoreCase));
            if (byId is not null) return byId;
        }

        var root = NormalizePath(repositoryRoot);
        var path = NormalizePath(projectPath);
        var name = (projectName ?? string.Empty).Trim();
        return entries
            .OrderByDescending(entry => entry.UpdatedUtc)
            .FirstOrDefault(entry =>
                PathEquals(entry.RepositoryRoot, root) &&
                PathEquals(entry.ProjectPath, path) &&
                string.Equals(entry.ProjectName, name, StringComparison.OrdinalIgnoreCase));
    }

    private List<StoredAllowList> LoadUnsafe()
    {
        try
        {
            if (!File.Exists(_storePath)) return [];
            return JsonSerializer.Deserialize<List<StoredAllowList>>(
                       File.ReadAllText(_storePath), JsonOptions)
                   ?? [];
        }
        catch
        {
            return [];
        }
    }

    private void SaveUnsafe(IReadOnlyCollection<StoredAllowList> entries)
    {
        var directory = Path.GetDirectoryName(_storePath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        var temporary = _storePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(entries, JsonOptions));
        File.Move(temporary, _storePath, true);
    }

    private static string NormalizeText(string? text) =>
        (text ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim('\n')
            .Replace("\n", Environment.NewLine, StringComparison.Ordinal);

    private static string NormalizePath(string path)
    {
        try { return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)); }
        catch { return (path ?? string.Empty).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
    }

    private static bool PathEquals(string left, string right) =>
        string.Equals(NormalizePath(left), NormalizePath(right), StringComparison.OrdinalIgnoreCase);
}
