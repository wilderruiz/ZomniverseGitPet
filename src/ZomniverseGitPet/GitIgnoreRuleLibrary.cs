namespace ZomniverseGitPet;

internal sealed record GitIgnoreRuleOption(
    string Id,
    string Category,
    string Label,
    string Description,
    IReadOnlyList<string> Rules,
    bool DefaultSelected = false,
    bool Custom = false)
{
    public string RuleSummary => string.Join("  ·  ", Rules);
}

internal enum CustomIgnoreRuleKind
{
    FolderName,
    FileExtension,
    FileName,
    NameContains
}

internal static class GitIgnoreRuleLibrary
{
    public static IReadOnlyList<GitIgnoreRuleOption> Presets { get; } =
    [
        new(
            "env-files",
            "PRIVACY",
            "Environment secret files",
            "Ignore .env and environment-specific variants anywhere in the project while keeping common example/template files visible to Git.",
            [".env", ".env.*", "!.env.example", "!.env.sample", "!.env.template", "!.env.dist"]),
        new(
            "private-keys",
            "PRIVACY",
            "Private keys / certificates",
            "Private keys and certificate material should normally stay outside repository history and never be pushed accidentally.",
            ["*.pem", "*.key"]),
        new(
            "logs",
            "GENERATED",
            "Log files",
            "Generated application and tool logs can be recreated and usually create noisy repository history.",
            ["*.log"]),
        new(
            "node-deps",
            "GENERATED",
            "Node dependencies",
            "node_modules contains downloaded package dependencies and is normally recreated from package metadata.",
            ["node_modules/"]),
        new(
            "php-deps",
            "GENERATED",
            "Composer vendor dependencies",
            "vendor contains installed PHP dependencies and is normally recreated from composer.lock / composer.json.",
            ["vendor/"]),
        new(
            "python-venv",
            "GENERATED",
            "Python virtual environments",
            "Local Python environments are machine-specific and should normally be recreated from dependency declarations.",
            [".venv/", "venv/"]),
        new(
            "python-cache",
            "GENERATED",
            "Python caches",
            "Python bytecode and test/tool caches are generated automatically and do not belong in source history.",
            ["__pycache__/", ".pytest_cache/", ".mypy_cache/", ".ruff_cache/"]),
        new(
            "temp",
            "GENERATED",
            "Temporary files",
            "Temporary files are usually transient working material. Review this preset if your project intentionally tracks temp folders.",
            ["*.tmp", "tmp/", "temp/"]),
        new(
            "windows-macos",
            "SYSTEM",
            "Windows / macOS metadata",
            "Operating-system thumbnail and Finder metadata are unrelated to the project source.",
            ["Thumbs.db", ".DS_Store"]),
        new(
            "ide-state",
            "SYSTEM",
            "IDE user state",
            "Developer-specific Visual Studio and JetBrains workspace files should normally remain local to each computer.",
            [".vs/", ".idea/", "*.user", "*.suo"]),
        new(
            "legacy-name",
            "ARCHIVE",
            "Names containing LEGACY",
            "Ignore files or folders whose name contains LEGACY anywhere in the project. Useful when historical copies are intentionally kept beside live source.",
            ["**/*LEGACY*", "**/*legacy*"]),
        new(
            "backup-files",
            "ARCHIVE",
            "Backup / editor copies",
            "Common backup suffixes and editor backup files are usually historical or temporary copies rather than live source.",
            ["*.bak", "*~"])
    ];

    public static GitIgnoreRuleOption CreateCustom(CustomIgnoreRuleKind kind, string rawValue)
    {
        var value = NormalizeInput(rawValue);
        if (value.Length == 0) throw new ArgumentException("Enter a name or extension first.", nameof(rawValue));

        return kind switch
        {
            CustomIgnoreRuleKind.FolderName => new(
                "custom-" + Guid.NewGuid().ToString("N"),
                "CUSTOM",
                $"Folder: {value}",
                $"Ignore folders named '{value}' anywhere in the project.",
                [NormalizeFolderRule(value)],
                true,
                true),
            CustomIgnoreRuleKind.FileExtension => new(
                "custom-" + Guid.NewGuid().ToString("N"),
                "CUSTOM",
                $"Extension: {NormalizeExtension(value)}",
                $"Ignore files ending in '{NormalizeExtension(value)}' anywhere in the project.",
                ["*" + NormalizeExtension(value)],
                true,
                true),
            CustomIgnoreRuleKind.FileName => new(
                "custom-" + Guid.NewGuid().ToString("N"),
                "CUSTOM",
                $"File: {value}",
                $"Ignore files named '{value}' anywhere in the project.",
                [value],
                true,
                true),
            CustomIgnoreRuleKind.NameContains => new(
                "custom-" + Guid.NewGuid().ToString("N"),
                "CUSTOM",
                $"Name contains: {value}",
                $"Ignore files or folders whose name contains '{value}' anywhere in the project.",
                [$"**/*{value}*"] ,
                true,
                true),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    public static string KindLabel(CustomIgnoreRuleKind kind) => kind switch
    {
        CustomIgnoreRuleKind.FolderName => "Folder name",
        CustomIgnoreRuleKind.FileExtension => "File extension",
        CustomIgnoreRuleKind.FileName => "File name",
        CustomIgnoreRuleKind.NameContains => "Name contains",
        _ => kind.ToString()
    };

    private static string NormalizeInput(string raw)
    {
        var value = (raw ?? "").Trim().Replace('\\', '/').Trim('/');
        if (value.Contains('\r') || value.Contains('\n') || value.StartsWith('!') || value.StartsWith('#'))
            throw new ArgumentException("Use a simple folder name, file name, extension, or text fragment. GitPet generates the Git pattern for you.");
        if (value.Contains("../", StringComparison.Ordinal) || value.Equals("..", StringComparison.Ordinal))
            throw new ArgumentException("Parent-directory paths are not allowed in a custom ignore rule.");
        return value;
    }

    private static string NormalizeFolderRule(string value) => value.TrimEnd('/') + "/";

    private static string NormalizeExtension(string value)
    {
        value = value.Trim();
        if (value.StartsWith("*.", StringComparison.Ordinal)) value = value[1..];
        if (!value.StartsWith('.')) value = "." + value;
        if (value.Length < 2 || value.Contains('/') || value.Contains('*') || value.Contains('?'))
            throw new ArgumentException("Enter a simple extension such as log, tmp, or .cache.");
        return value;
    }
}
