using System.Text;

namespace ZomniverseGitPet;

internal sealed record ReconcileLocalEditDraft(
    string RelativePath,
    string PinnedLocalCommitSha,
    string OriginalText,
    string EditedText);

internal sealed record ReconcileLocalEditValidation(
    bool Success,
    string Message,
    string? FullPath = null,
    string? NormalizedEditedText = null);

internal sealed class ReconcileLocalEditService(GitService git)
{
    public async Task<ReconcileLocalEditValidation> ValidateAsync(
        string repositoryPath,
        ReconcileLocalEditDraft draft,
        bool requireCleanIndex,
        CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
            return new(false, "The active repository is not available.");

        var relativePath = NormalizeGitPath(draft.RelativePath);
        if (relativePath.Length == 0)
            return new(false, "The edited path is empty.");

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repositoryPath));
        var fullPath = Path.GetFullPath(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = root + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return new(false, "The edited path resolves outside the active repository.");

        var pathspecs = LogicalProjectScopeRuntime.GetPathspecs(
            repositoryPath,
            includeRootGitIgnore: true);
        if (pathspecs.Count > 0 &&
            !LogicalProjectScopeRuntime.ContainsPath(repositoryPath, relativePath))
        {
            return new(false,
                "This file is outside the active GitPet logical-project scope. Nothing was written.");
        }

        if (!File.Exists(fullPath))
            return new(false, "LOCAL no longer exists in the working tree. Refresh the Inspector.");

        var normalized = NormalizeDraftForOriginal(draft.OriginalText, draft.EditedText);
        if (normalized.IndexOf('\0') >= 0)
            return new(false, "The edited source contains a NUL character and cannot be written as text.");

        if (string.Equals(normalized, draft.OriginalText, StringComparison.Ordinal))
            return new(false, "No LOCAL source changes are present in the editor.");

        var head = await git.GetReconcileRevisionAsync(repositoryPath, "HEAD", token);
        if (!head.Success || string.IsNullOrWhiteSpace(head.Output))
            return new(false, "GitPet could not verify the current local HEAD.\r\n\r\n" + head.Output);

        var currentHead = FirstLine(head.Output);
        if (!string.Equals(
                currentHead,
                draft.PinnedLocalCommitSha,
                StringComparison.OrdinalIgnoreCase))
        {
            return new(false,
                "LOCAL HEAD moved after this Inspector snapshot was opened. " +
                "Refresh the Inspector before applying the edit.");
        }

        var targetStatus = await git.RunGitAsync(
            repositoryPath,
            ["status", "--porcelain=v1", "--untracked-files=all", "--", relativePath],
            TimeSpan.FromSeconds(12),
            token);
        if (!targetStatus.Success)
            return new(false, "GitPet could not verify the current working file.\r\n\r\n" + targetStatus.Output);

        if (!string.IsNullOrWhiteSpace(targetStatus.Output))
        {
            return new(false,
                "The LOCAL working file changed outside this Inspector after the pinned snapshot. " +
                "Nothing was overwritten. Review/save that working change first, then reopen Reconcile Inspector.");
        }

        if (requireCleanIndex)
        {
            var staged = await git.RunGitAsync(
                repositoryPath,
                ["diff", "--cached", "--name-only"],
                TimeSpan.FromSeconds(12),
                token);
            if (!staged.Success)
                return new(false, "GitPet could not verify the staged-file boundary.\r\n\r\n" + staged.Output);

            var stagedPaths = staged.Output
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (stagedPaths.Length > 0)
            {
                return new(false,
                    "Commit local is blocked because Git already has staged changes. " +
                    "Save or unstage those changes first so this correction commit contains only the edited file.\r\n\r\n" +
                    string.Join("\r\n", stagedPaths.Take(12)));
            }
        }

        return new(
            true,
            "Edit safety validation passed.",
            fullPath,
            normalized);
    }

    public async Task<ReconcileLocalEditValidation> WriteAsync(
        string repositoryPath,
        ReconcileLocalEditDraft draft,
        bool requireCleanIndex,
        CancellationToken token)
    {
        var validation = await ValidateAsync(
            repositoryPath,
            draft,
            requireCleanIndex,
            token);
        if (!validation.Success)
            return validation;

        await File.WriteAllTextAsync(
            validation.FullPath!,
            validation.NormalizedEditedText!,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            token);

        return validation with
        {
            Message = "LOCAL working file updated."
        };
    }

    internal static string NormalizeDraftForOriginal(
        string original,
        string edited)
    {
        var editedLf = (edited ?? "")
            .Replace("\r\n", "\n")
            .Replace("\r", "\n");

        if ((original ?? "").Contains("\r\n", StringComparison.Ordinal))
            return editedLf.Replace("\n", "\r\n");

        return editedLf;
    }

    private static string NormalizeGitPath(string path) =>
        (path ?? "").Replace('\\', '/').TrimStart('/');

    private static string FirstLine(string value) =>
        value.Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? "";
}

internal sealed class ReconcileLocalEditRequestEventArgs(
    GuardianWorkboardRow row,
    ReconcileLocalEditDraft draft) : EventArgs
{
    public GuardianWorkboardRow Row { get; } = row;
    public ReconcileLocalEditDraft Draft { get; } = draft;
}
