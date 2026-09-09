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
    NameStartsWith,
    NameContains,
    NameEndsWith,
    RelativeFilePath,
    RelativeFolderPath
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
            [
                ".env", ".env.*",
                "!.env.example", "!.env.*.example",
                "!.env.sample", "!.env.*.sample",
                "!.env.template", "!.env.*.template",
                "!.env.dist", "!.env.*.dist"
            ]),
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
        if (value.Length == 0) throw new ArgumentException("Enter a name, path, or extension first.", nameof(rawValue));

        return kind switch
        {
            CustomIgnoreRuleKind.FolderName => CreateOption(
                $"Folder: {NormalizeSimpleName(value, "folder name")}",
                $"Ignore folders named '{NormalizeSimpleName(value, "folder name")}' anywhere in the project.",
                [NormalizeFolderRule(NormalizeSimpleName(value, "folder name"))]),
            CustomIgnoreRuleKind.FileExtension => CreateOption(
                $"Extension: {NormalizeExtension(value)}",
                $"Ignore files ending in '{NormalizeExtension(value)}' anywhere in the project.",
                ["*" + NormalizeExtension(value)]),
            CustomIgnoreRuleKind.FileName => CreateOption(
                $"File: {NormalizeSimpleName(value, "file name")}",
                $"Ignore files named '{NormalizeSimpleName(value, "file name")}' anywhere in the project.",
                [NormalizeSimpleName(value, "file name")]),
            CustomIgnoreRuleKind.NameStartsWith => CreateOption(
                $"Name starts: {NormalizeNameFragment(value)}",
                $"Ignore files or folders whose name starts with '{NormalizeNameFragment(value)}' anywhere in the project.",
                [$"**/{NormalizeNameFragment(value)}*"]),
            CustomIgnoreRuleKind.NameContains => CreateOption(
                $"Name contains: {NormalizeNameFragment(value)}",
                $"Ignore files or folders whose name contains '{NormalizeNameFragment(value)}' anywhere in the project.",
                [$"**/*{NormalizeNameFragment(value)}*"]),
            CustomIgnoreRuleKind.NameEndsWith => CreateOption(
                $"Name ends: {NormalizeNameFragment(value)}",
                $"Ignore files or folders whose name ends with '{NormalizeNameFragment(value)}' anywhere in the project.",
                [$"**/*{NormalizeNameFragment(value)}"]),
            CustomIgnoreRuleKind.RelativeFilePath => CreateOption(
                $"File path: {NormalizeRelativePath(value, false)}",
                $"Ignore the project-relative file path '{NormalizeRelativePath(value, false)}'.",
                [NormalizeRelativePath(value, false)]),
            CustomIgnoreRuleKind.RelativeFolderPath => CreateOption(
                $"Folder path: {NormalizeRelativePath(value, true)}",
                $"Ignore the project-relative folder path '{NormalizeRelativePath(value, true)}'.",
                [NormalizeRelativePath(value, true)]),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    public static string KindLabel(CustomIgnoreRuleKind kind) => kind switch
    {
        CustomIgnoreRuleKind.FolderName => "Folder name",
        CustomIgnoreRuleKind.FileExtension => "File extension",
        CustomIgnoreRuleKind.FileName => "File name",
        CustomIgnoreRuleKind.NameStartsWith => "Name starts with",
        CustomIgnoreRuleKind.NameContains => "Name contains",
        CustomIgnoreRuleKind.NameEndsWith => "Name ends with",
        CustomIgnoreRuleKind.RelativeFilePath => "Relative file path",
        CustomIgnoreRuleKind.RelativeFolderPath => "Relative folder path",
        _ => kind.ToString()
    };

    public static string KindPlaceholder(CustomIgnoreRuleKind kind) => kind switch
    {
        CustomIgnoreRuleKind.FolderName => "e.g. cache",
        CustomIgnoreRuleKind.FileExtension => "e.g. .tmp",
        CustomIgnoreRuleKind.FileName => "e.g. secrets.json",
        CustomIgnoreRuleKind.NameStartsWith => "e.g. ~$  →  **/~$*",
        CustomIgnoreRuleKind.NameContains => "e.g. LEGACY",
        CustomIgnoreRuleKind.NameEndsWith => "e.g. .old",
        CustomIgnoreRuleKind.RelativeFilePath => "e.g. docs/private-notes.md",
        CustomIgnoreRuleKind.RelativeFolderPath => "e.g. tools/generated",
        _ => "Enter a value"
    };

    private static GitIgnoreRuleOption CreateOption(string label, string description, IReadOnlyList<string> rules) =>
        new(
            "custom-" + Guid.NewGuid().ToString("N"),
            "CUSTOM",
            label,
            description,
            rules,
            true,
            true);

    private static string NormalizeInput(string raw)
    {
        var value = (raw ?? "").Trim().Replace('\\', '/').Trim('/');
        if (value.Contains('\r') || value.Contains('\n') || value.StartsWith('!') || value.StartsWith('#'))
            throw new ArgumentException("Use a name, relative path, extension, or text fragment. GitPet generates the Git pattern for you.");
        if (value.Contains("../", StringComparison.Ordinal) || value.Equals("..", StringComparison.Ordinal))
            throw new ArgumentException("Parent-directory paths are not allowed in a custom ignore rule.");
        return value;
    }

    private static string NormalizeSimpleName(string value, string label)
    {
        if (value.Contains('/') || value.Contains('*') || value.Contains('?'))
            throw new ArgumentException($"Enter a simple {label} without folders or wildcard characters.");
        return value;
    }

    private static string NormalizeNameFragment(string value)
    {
        if (value.Contains('/') || value.Contains('*') || value.Contains('?'))
            throw new ArgumentException("Enter plain text for the name match. GitPet adds the wildcard pattern automatically.");
        return value;
    }

    private static string NormalizeFolderRule(string value) => value.TrimEnd('/') + "/";

    private static string NormalizeRelativePath(string value, bool folder)
    {
        if (value.Contains('*') || value.Contains('?'))
            throw new ArgumentException("Relative paths should not contain wildcard characters. Use a name-matching option instead.");
        var normalized = value.Trim('/');
        if (normalized.Length == 0) throw new ArgumentException("Enter a project-relative path.");
        return folder ? normalized.TrimEnd('/') + "/" : normalized;
    }

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
