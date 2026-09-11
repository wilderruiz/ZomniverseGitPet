using System.Text.Json;

namespace ZomniverseGitPet;

public static class ProjectTestAdvisor
{
    public static IReadOnlyList<string> Suggest(string repositoryPath)
    {
        var suggestions = new List<string>();
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath)) return suggestions;

        var projectPath = LogicalProjectScopeRuntime.GetWorkingDirectory(repositoryPath);
        SuggestNode(projectPath, suggestions);
        SuggestDotNet(projectPath, suggestions);
        SuggestPython(projectPath, suggestions);
        SuggestComposer(projectPath, suggestions);
        SuggestCargo(projectPath, suggestions);

        return suggestions
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToArray();
    }

    private static void SuggestNode(string root, List<string> suggestions)
    {
        var packageJson = Path.Combine(root, "package.json");
        if (!File.Exists(packageJson)) return;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(packageJson));
            if (!document.RootElement.TryGetProperty("scripts", out var scripts) || scripts.ValueKind != JsonValueKind.Object)
                return;

            var names = scripts.EnumerateObject()
                .Select(property => property.Name)
                .Where(name => name.Equals("test", StringComparison.OrdinalIgnoreCase) ||
                               name.StartsWith("test:", StringComparison.OrdinalIgnoreCase))
                .OrderBy(name => name.Equals("test", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Take(6);

            foreach (var name in names)
                suggestions.Add(name.Equals("test", StringComparison.OrdinalIgnoreCase) ? "npm test" : $"npm run {name}");
        }
        catch
        {
            // Discovery is advisory only. Invalid package metadata should never block GitPet.
        }
    }

    private static void SuggestDotNet(string root, List<string> suggestions)
    {
        try
        {
            var solution = Directory.EnumerateFiles(root, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (solution is not null)
            {
                suggestions.Add($"dotnet test \"{Path.GetFileName(solution)}\" -c Release");
                return;
            }

            var project = Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                               !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();
            if (project is not null)
            {
                var relative = Path.GetRelativePath(root, project);
                suggestions.Add($"dotnet test \"{relative}\" -c Release");
            }
        }
        catch
        {
            // Advisory discovery only.
        }
    }

    private static void SuggestPython(string root, List<string> suggestions)
    {
        if (File.Exists(Path.Combine(root, "pyproject.toml")) ||
            File.Exists(Path.Combine(root, "pytest.ini")) ||
            File.Exists(Path.Combine(root, "tox.ini")))
            suggestions.Add("python -m pytest");
    }

    private static void SuggestComposer(string root, List<string> suggestions)
    {
        var composer = Path.Combine(root, "composer.json");
        if (!File.Exists(composer)) return;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(composer));
            if (document.RootElement.TryGetProperty("scripts", out var scripts) &&
                scripts.ValueKind == JsonValueKind.Object && scripts.TryGetProperty("test", out _))
                suggestions.Add("composer test");
        }
        catch
        {
            // Advisory discovery only.
        }
    }

    private static void SuggestCargo(string root, List<string> suggestions)
    {
        if (File.Exists(Path.Combine(root, "Cargo.toml"))) suggestions.Add("cargo test");
    }
}
