using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace ZomniverseGitPet;

internal sealed record ReconcileValidationRequest(
    string RelativePath,
    string BaselineCommitSha,
    string SourceText,
    IReadOnlyList<string> TestCommands,
    string? ProjectRelativeWorkingDirectory = null,
    string? MergeRemoteCommitSha = null);

internal sealed record ReconcileValidationResult(
    bool InfrastructurePassed,
    bool SyntaxPassed,
    string SyntaxSummary,
    int TestsConfigured,
    int TestsCompleted,
    bool TestsPassed,
    bool TestsSkipped,
    IReadOnlyList<string> Details)
{
    public bool BlockingPassed => InfrastructurePassed && SyntaxPassed;

    public string TestsSummary =>
        TestsConfigured == 0
            ? "Tests — not configured"
            : TestsSkipped
                ? $"Tests ○ {TestsConfigured} configured · not run"
                : TestsPassed
                    ? $"Tests ✓ {TestsCompleted} / {TestsConfigured} passed"
                    : $"Tests ⚠ {TestsCompleted} / {TestsConfigured} stopped on failure";

    public string CompactSummary =>
        $"{SyntaxSummary} · {TestsSummary}";
}

internal sealed class ReconcileValidationService(GitService git)
{
    public async Task<ReconcileValidationResult> ValidateAsync(
        string repositoryPath,
        ReconcileValidationRequest request,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
            return InfrastructureFailure("Validation repository is not available.");

        var relativePath = NormalizeGitPath(request.RelativePath);
        if (relativePath.Length == 0)
            return InfrastructureFailure("Validation path is empty.");

        var source = request.SourceText ?? "";
        if (source.IndexOf('\0') >= 0)
            return SyntaxFailure("Syntax ✕ binary NUL detected", "Source contains a NUL character.");

        if (ReconcileMergedCandidateService.HasConflictMarkers(source))
        {
            return SyntaxFailure(
                "Syntax ✕ conflict markers present",
                "Source still contains a complete Git conflict-marker block.");
        }

        var builtIn = ValidateBuiltIn(relativePath, source);
        if (!builtIn.Passed)
            return SyntaxFailure(builtIn.Summary, builtIn.Detail);

        var tests = request.TestCommands
            .Where(command => !string.IsNullOrWhiteSpace(command))
            .Select(command => command.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var extension = Path.GetExtension(relativePath).ToLowerInvariant();
        var needsExternal = NeedsExternalLanguageCheck(extension);
        if (!needsExternal && tests.Length == 0)
        {
            return new(
                true,
                true,
                builtIn.Summary,
                0,
                0,
                true,
                true,
                builtIn.Detail.Length == 0 ? [] : [builtIn.Detail]);
        }

        var worktree = Path.Combine(
            Path.GetTempPath(),
            "GitPet-reconcile-validate-" + Guid.NewGuid().ToString("N"));

        try
        {
            var add = await git.RunGitAsync(
                repositoryPath,
                ["worktree", "add", "--detach", worktree, request.BaselineCommitSha],
                TimeSpan.FromMinutes(1),
                token);
            if (!add.Success)
                return InfrastructureFailure(
                    "GitPet could not create the isolated validation worktree.\r\n\r\n" + add.Output,
                    tests.Length);

            if (!string.IsNullOrWhiteSpace(request.MergeRemoteCommitSha))
            {
                var merge = await git.RunGitAsync(
                    worktree,
                    ["merge", "--no-commit", "--no-ff", request.MergeRemoteCommitSha!],
                    TimeSpan.FromMinutes(3),
                    token);

                var mergeHead = await git.RunGitAsync(
                    worktree,
                    ["rev-parse", "--verify", "-q", "MERGE_HEAD"],
                    TimeSpan.FromSeconds(8),
                    token);

                if (!merge.Success && !mergeHead.Success)
                {
                    return InfrastructureFailure(
                        "GitPet could not reproduce the candidate merge inside the isolated validation worktree.\r\n\r\n" +
                        merge.Output,
                        tests.Length);
                }
            }

            var worktreeRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(worktree));
            var target = Path.GetFullPath(Path.Combine(
                worktreeRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!target.StartsWith(
                    worktreeRoot + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
            {
                return InfrastructureFailure(
                    "Validation path escaped the isolated worktree.",
                    tests.Length);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await File.WriteAllTextAsync(
                target,
                source,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                token);

            if (!string.IsNullOrWhiteSpace(request.MergeRemoteCommitSha))
            {
                var stage = await git.RunGitAsync(
                    worktree,
                    ["add", "--", relativePath],
                    TimeSpan.FromSeconds(30),
                    token);
                if (!stage.Success)
                {
                    return InfrastructureFailure(
                        "GitPet could not stage the candidate inside the isolated validation worktree.\r\n\r\n" +
                        stage.Output,
                        tests.Length);
                }
            }

            var validationWorkingDirectory = ResolveValidationWorkingDirectory(
                worktreeRoot,
                request.ProjectRelativeWorkingDirectory);

            var details = new List<string>();
            if (!string.IsNullOrWhiteSpace(builtIn.Detail))
                details.Add(builtIn.Detail);

            var language = await RunExternalLanguageCheckAsync(
                validationWorkingDirectory,
                target,
                extension,
                token);
            if (!language.Passed)
            {
                details.Add(language.Detail);
                return new(
                    true,
                    false,
                    language.Summary,
                    tests.Length,
                    0,
                    false,
                    tests.Length == 0,
                    details);
            }

            var syntaxSummary = language.Ran
                ? language.Summary
                : builtIn.Summary;
            if (!string.IsNullOrWhiteSpace(language.Detail))
                details.Add(language.Detail);

            var completed = 0;
            var testsPassed = true;
            foreach (var command in tests)
            {
                token.ThrowIfCancellationRequested();
                completed++;

                var result = await git.RunValidationCommandAsync(
                    validationWorkingDirectory,
                    command,
                    token);

                var output = string.IsNullOrWhiteSpace(result.Output)
                    ? ""
                    : result.Output.Trim();
                details.Add(
                    $"TEST {completed}/{tests.Length}: {command}\r\n" +
                    (output.Length == 0 ? "" : output + "\r\n") +
                    (result.Success ? "PASS" : $"FAIL (exit {result.ExitCode})"));

                if (!result.Success)
                {
                    testsPassed = false;
                    break;
                }
            }

            return new(
                true,
                true,
                syntaxSummary,
                tests.Length,
                completed,
                tests.Length == 0 || testsPassed,
                tests.Length == 0,
                details);
        }
        finally
        {
            await CleanupWorktreeAsync(repositoryPath, worktree);
        }
    }

    private async Task<LanguageCheck> RunExternalLanguageCheckAsync(
        string workingDirectory,
        string targetPath,
        string extension,
        CancellationToken token)
    {
        var spec = extension switch
        {
            ".php" => new LanguageTool(
                "PHP",
                "php",
                $"php -l {QuoteCmd(targetPath)}"),
            ".js" or ".mjs" or ".cjs" => new LanguageTool(
                "JavaScript",
                "node",
                $"node --check {QuoteCmd(targetPath)}"),
            ".py" => new LanguageTool(
                "Python",
                "python",
                $"python -m py_compile {QuoteCmd(targetPath)}"),
            ".ps1" => new LanguageTool(
                "PowerShell",
                "powershell",
                BuildPowerShellParseCommand(targetPath)),
            _ => null
        };

        if (spec is null)
            return new(true, false, "", "");

        var probe = await git.RunValidationCommandAsync(
            workingDirectory,
            $"where {spec.Executable} >nul 2>nul",
            token);
        if (!probe.Success)
        {
            return new(
                true,
                false,
                $"Syntax ○ {spec.Name} parser unavailable · basic checks passed",
                $"{spec.Name} executable '{spec.Executable}' was not found; external syntax validation was skipped.");
        }

        var result = await git.RunValidationCommandAsync(
            workingDirectory,
            spec.Command,
            token);
        if (!result.Success)
        {
            return new(
                false,
                true,
                $"Syntax ✕ {spec.Name} check failed",
                string.IsNullOrWhiteSpace(result.Output)
                    ? $"{spec.Name} validator exited with {result.ExitCode}."
                    : result.Output.Trim());
        }

        return new(
            true,
            true,
            $"Syntax ✓ {spec.Name}",
            string.IsNullOrWhiteSpace(result.Output)
                ? $"{spec.Name} validator passed."
                : result.Output.Trim());
    }

    private static BuiltInCheck ValidateBuiltIn(string relativePath, string source)
    {
        var extension = Path.GetExtension(relativePath).ToLowerInvariant();

        try
        {
            switch (extension)
            {
                case ".json":
                    using (JsonDocument.Parse(source))
                    {
                    }
                    return new(true, "Syntax ✓ JSON parse", "JSON parsed successfully.");

                case ".xml":
                case ".svg":
                case ".xaml":
                case ".csproj":
                case ".props":
                case ".targets":
                    _ = XDocument.Parse(source, LoadOptions.PreserveWhitespace);
                    return new(true, "Syntax ✓ XML parse", "XML parsed successfully.");

                case ".css":
                case ".scss":
                case ".less":
                    var balance = CheckCssBalance(source);
                    return balance is null
                        ? new(true, "Syntax ✓ CSS structure", "CSS-family brace structure is balanced.")
                        : new(false, "Syntax ✕ CSS structure", balance);

                default:
                    return new(
                        true,
                        "Syntax ✓ basic text sanity",
                        "No blocking binary/conflict-marker problem was detected; project tests remain the stronger language-level check for this file type.");
            }
        }
        catch (JsonException ex)
        {
            return new(false, "Syntax ✕ JSON parse", ex.Message);
        }
        catch (System.Xml.XmlException ex)
        {
            return new(false, "Syntax ✕ XML parse", ex.Message);
        }
    }

    private static string? CheckCssBalance(string source)
    {
        var depth = 0;
        var quote = '\0';
        var escaped = false;
        var blockComment = false;

        for (var i = 0; i < source.Length; i++)
        {
            var ch = source[i];
            var next = i + 1 < source.Length ? source[i + 1] : '\0';

            if (blockComment)
            {
                if (ch == '*' && next == '/')
                {
                    blockComment = false;
                    i++;
                }
                continue;
            }

            if (quote != '\0')
            {
                if (escaped)
                {
                    escaped = false;
                    continue;
                }
                if (ch == '\\')
                {
                    escaped = true;
                    continue;
                }
                if (ch == quote)
                    quote = '\0';
                continue;
            }

            if (ch == '/' && next == '*')
            {
                blockComment = true;
                i++;
                continue;
            }

            if (ch is '\'' or '"')
            {
                quote = ch;
                continue;
            }

            if (ch == '{') depth++;
            else if (ch == '}')
            {
                depth--;
                if (depth < 0)
                    return "A closing } appears before a matching opening {.";
            }
        }

        if (quote != '\0')
            return "A quoted CSS string is not terminated.";
        if (blockComment)
            return "A CSS block comment is not terminated.";
        if (depth != 0)
            return $"CSS brace depth ended at {depth}; opening/closing braces do not balance.";
        return null;
    }

    private async Task CleanupWorktreeAsync(
        string repositoryPath,
        string worktreePath)
    {
        if (string.IsNullOrWhiteSpace(worktreePath))
            return;

        try
        {
            await git.RunGitAsync(
                repositoryPath,
                ["worktree", "remove", "--force", worktreePath],
                TimeSpan.FromMinutes(1),
                CancellationToken.None);
        }
        catch { }

        try
        {
            if (Directory.Exists(worktreePath))
            {
                foreach (var file in Directory.EnumerateFiles(
                             worktreePath,
                             "*",
                             SearchOption.AllDirectories))
                    File.SetAttributes(file, FileAttributes.Normal);
                Directory.Delete(worktreePath, recursive: true);
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        try
        {
            await git.RunGitAsync(
                repositoryPath,
                ["worktree", "prune"],
                TimeSpan.FromSeconds(30),
                CancellationToken.None);
        }
        catch { }
    }

    private static string ResolveValidationWorkingDirectory(
        string worktreeRoot,
        string? projectRelativeWorkingDirectory)
    {
        if (string.IsNullOrWhiteSpace(projectRelativeWorkingDirectory))
            return worktreeRoot;

        var relative = NormalizeGitPath(projectRelativeWorkingDirectory);
        var candidate = Path.GetFullPath(Path.Combine(
            worktreeRoot,
            relative.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = worktreeRoot + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
               Directory.Exists(candidate)
            ? candidate
            : worktreeRoot;
    }

    private static bool NeedsExternalLanguageCheck(string extension) =>
        extension is ".php" or ".js" or ".mjs" or ".cjs" or ".py" or ".ps1";

    private static string BuildPowerShellParseCommand(string targetPath)
    {
        var escaped = targetPath.Replace("'", "''", StringComparison.Ordinal);
        return
            "powershell -NoProfile -NonInteractive -Command " +
            QuoteCmd(
                "$ErrorActionPreference='Stop'; " +
                $"[void][scriptblock]::Create((Get-Content -Raw -LiteralPath '{escaped}'))");
    }

    private static string QuoteCmd(string value) =>
        """ + value.Replace(""", """", StringComparison.Ordinal) + """;

    private static ReconcileValidationResult InfrastructureFailure(
        string message,
        int testsConfigured = 0) =>
        new(
            false,
            false,
            "Syntax ? validation infrastructure unavailable",
            testsConfigured,
            0,
            false,
            testsConfigured == 0,
            [message]);

    private static ReconcileValidationResult SyntaxFailure(
        string summary,
        string detail) =>
        new(
            true,
            false,
            summary,
            0,
            0,
            false,
            true,
            [detail]);

    private static string NormalizeGitPath(string path) =>
        (path ?? "").Replace('\\', '/').TrimStart('/');

    private sealed record BuiltInCheck(
        bool Passed,
        string Summary,
        string Detail);

    private sealed record LanguageCheck(
        bool Passed,
        bool Ran,
        string Summary,
        string Detail);

    private sealed record LanguageTool(
        string Name,
        string Executable,
        string Command);
}
