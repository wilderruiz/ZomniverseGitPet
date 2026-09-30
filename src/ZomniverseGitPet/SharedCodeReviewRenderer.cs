using System.Text.RegularExpressions;

namespace ZomniverseGitPet;

internal enum CodeSyntaxKind
{
    Keyword,
    String,
    Comment,
    Variable,
    Number,
    Markup,
    Property
}

internal sealed record CodeSyntaxSpan(int Start, int Length, CodeSyntaxKind Kind);

internal static class SharedCodeReviewRenderer
{
    internal static readonly Color BeforeChangeBackground = Color.FromArgb(53, 27, 40);
    internal static readonly Color LocalChangeBackground = Color.FromArgb(25, 52, 43);
    internal static readonly Color RemoteChangeBackground = Color.FromArgb(48, 38, 24);

    private const int SyntaxHighlightCharacterLimit = 750_000;

    public static void RenderCode(
        RichTextBox box,
        string text,
        string path,
        IReadOnlySet<int>? changedLines = null,
        Color? changeBackground = null)
    {
        var normalized = NormalizeLineEndings(text);
        box.SuspendLayout();
        try
        {
            box.WordWrap = false;
            box.Text = normalized;
            box.SelectAll();
            box.SelectionColor = GuardianTheme.Ink;
            box.SelectionBackColor = GuardianTheme.Console;
            box.SelectionFont = new Font("Cascadia Mono", 9.1f);

            if (normalized.Length <= SyntaxHighlightCharacterLimit)
            {
                foreach (var span in GetSyntaxSpans(normalized, path))
                {
                    if (span.Start < 0 || span.Length <= 0 || span.Start + span.Length > box.TextLength)
                        continue;
                    box.Select(span.Start, span.Length);
                    box.SelectionColor = ColorFor(span.Kind);
                }
            }

            if (changedLines is { Count: > 0 } && changeBackground.HasValue)
                ApplyChangedLineBackgrounds(box, changedLines, changeBackground.Value);

            box.Select(0, 0);
        }
        finally
        {
            box.ResumeLayout();
        }
    }

    public static void RenderPlain(RichTextBox box, string text, bool wordWrap)
    {
        box.SuspendLayout();
        try
        {
            box.WordWrap = wordWrap;
            box.Text = NormalizeLineEndings(text);
            box.SelectAll();
            box.SelectionColor = GuardianTheme.Ink;
            box.SelectionBackColor = GuardianTheme.Console;
            box.SelectionFont = wordWrap
                ? new Font("Segoe UI", 9.5f)
                : new Font("Cascadia Mono", 9.1f);
            box.Select(0, 0);
        }
        finally
        {
            box.ResumeLayout();
        }
    }

    internal static IReadOnlyList<CodeSyntaxSpan> GetSyntaxSpans(string text, string path)
    {
        if (string.IsNullOrEmpty(text)) return [];
        var spans = new List<CodeSyntaxSpan>();
        var language = LanguageFor(path);
        AddStrings(spans, text);

        switch (language)
        {
            case "php":
                Add(spans, text, @"\b(?:function|class|if|else|elseif|foreach|for|while|do|return|new|public|private|protected|static|const|try|catch|finally|throw|namespace|use|as|echo|true|false|null|array|match|switch|case|default|break|continue|extends|implements|interface|trait|yield|fn|include|require|include_once|require_once)\b", CodeSyntaxKind.Keyword);
                Add(spans, text, @"\$[A-Za-z_][A-Za-z0-9_]*", CodeSyntaxKind.Variable);
                AddNumbers(spans, text);
                Add(spans, text, @"//.*$|#.*$|/\*[\s\S]*?\*/", CodeSyntaxKind.Comment);
                break;
            case "csharp":
            case "js":
            case "ts":
            case "java":
                Add(spans, text, @"\b(?:class|interface|namespace|using|public|private|protected|internal|static|readonly|const|var|let|function|async|await|new|return|if|else|switch|case|default|for|foreach|while|do|break|continue|try|catch|finally|throw|true|false|null|undefined|this|base|extends|implements|import|export|from|typeof|instanceof|record|struct|enum|void|int|string|bool|object)\b", CodeSyntaxKind.Keyword);
                AddNumbers(spans, text);
                Add(spans, text, @"//.*$|/\*[\s\S]*?\*/", CodeSyntaxKind.Comment);
                break;
            case "python":
                Add(spans, text, @"\b(?:def|class|if|elif|else|for|while|return|yield|try|except|finally|raise|with|as|import|from|async|await|lambda|True|False|None|and|or|not|in|is|pass|break|continue|global|nonlocal)\b", CodeSyntaxKind.Keyword);
                AddNumbers(spans, text);
                Add(spans, text, @"#.*$", CodeSyntaxKind.Comment);
                break;
            case "json":
                Add(spans, text, @"""(?:\\.|[^""\\])*""(?=\s*:)", CodeSyntaxKind.Property);
                Add(spans, text, @"\b(?:true|false|null)\b", CodeSyntaxKind.Keyword);
                AddNumbers(spans, text);
                break;
            case "css":
                Add(spans, text, @"(?m)(?:^|[;{])\s*[A-Za-z_-][A-Za-z0-9_-]*(?=\s*:)", CodeSyntaxKind.Property);
                Add(spans, text, @"#[0-9A-Fa-f]{3,8}\b", CodeSyntaxKind.Number);
                AddNumbers(spans, text);
                Add(spans, text, @"/\*[\s\S]*?\*/", CodeSyntaxKind.Comment);
                break;
            case "html":
            case "xml":
                Add(spans, text, @"</?[A-Za-z][^>]*>", CodeSyntaxKind.Markup);
                Add(spans, text, @"<!--[\s\S]*?-->", CodeSyntaxKind.Comment);
                break;
            case "sql":
                Add(spans, text, @"\b(?:select|from|where|join|left|right|inner|outer|on|group|by|order|having|insert|into|update|set|delete|create|alter|drop|table|view|index|primary|key|foreign|references|and|or|not|null|as|case|when|then|else|end|distinct|limit|offset|union|all)\b", CodeSyntaxKind.Keyword);
                AddNumbers(spans, text);
                Add(spans, text, @"--.*$|/\*[\s\S]*?\*/", CodeSyntaxKind.Comment);
                break;
            case "powershell":
                Add(spans, text, @"\b(?:function|param|if|elseif|else|foreach|for|while|switch|return|throw|try|catch|finally|class|enum|filter|begin|process|end)\b", CodeSyntaxKind.Keyword);
                Add(spans, text, @"\$[A-Za-z_][A-Za-z0-9_:]*", CodeSyntaxKind.Variable);
                AddNumbers(spans, text);
                Add(spans, text, @"#.*$", CodeSyntaxKind.Comment);
                break;
        }

        return spans;
    }

    internal static string LanguageFor(string path)
    {
        var extension = Path.GetExtension(path ?? "").ToLowerInvariant();
        return extension switch
        {
            ".php" or ".phtml" => "php",
            ".cs" => "csharp",
            ".js" or ".jsx" or ".mjs" or ".cjs" => "js",
            ".ts" or ".tsx" => "ts",
            ".java" => "java",
            ".py" => "python",
            ".json" => "json",
            ".css" or ".scss" or ".sass" or ".less" => "css",
            ".html" or ".htm" or ".svg" => "html",
            ".xml" or ".xaml" or ".csproj" or ".props" or ".targets" => "xml",
            ".sql" => "sql",
            ".ps1" or ".psm1" or ".psd1" => "powershell",
            _ => "text"
        };
    }

    private static void AddStrings(List<CodeSyntaxSpan> spans, string text) =>
        Add(spans, text, @"""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*'", CodeSyntaxKind.String);

    private static void AddNumbers(List<CodeSyntaxSpan> spans, string text) =>
        Add(spans, text, @"\b(?:0x[0-9A-Fa-f]+|\d+(?:\.\d+)?)\b", CodeSyntaxKind.Number);

    private static void Add(
        List<CodeSyntaxSpan> spans,
        string text,
        string pattern,
        CodeSyntaxKind kind)
    {
        try
        {
            foreach (Match match in Regex.Matches(
                         text,
                         pattern,
                         RegexOptions.Multiline | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
                         TimeSpan.FromMilliseconds(250)))
            {
                if (match.Success && match.Length > 0)
                    spans.Add(new CodeSyntaxSpan(match.Index, match.Length, kind));
            }
        }
        catch (RegexMatchTimeoutException)
        {
        }
    }

    private static Color ColorFor(CodeSyntaxKind kind) => kind switch
    {
        CodeSyntaxKind.Keyword => GuardianTheme.VioletHover,
        CodeSyntaxKind.String => GuardianTheme.Healthy,
        CodeSyntaxKind.Comment => GuardianTheme.MutedInk,
        CodeSyntaxKind.Variable => GuardianTheme.Info,
        CodeSyntaxKind.Number => GuardianTheme.Reconcile,
        CodeSyntaxKind.Markup => GuardianTheme.HotPinkSoft,
        CodeSyntaxKind.Property => GuardianTheme.Info,
        _ => GuardianTheme.Ink
    };

    private static void ApplyChangedLineBackgrounds(
        RichTextBox box,
        IReadOnlySet<int> changedLines,
        Color background)
    {
        if (changedLines.Count == 0 || box.TextLength == 0) return;
        var text = box.Text;
        var lineNumber = 1;
        var lineStart = 0;

        for (var i = 0; i <= text.Length; i++)
        {
            var atEnd = i == text.Length;
            var atLineBreak = !atEnd && text[i] == '\n';
            if (!atEnd && !atLineBreak) continue;

            if (changedLines.Contains(lineNumber))
            {
                var length = Math.Max(0, i - lineStart);
                if (length > 0)
                {
                    box.Select(lineStart, length);
                    box.SelectionBackColor = background;
                }
            }

            lineNumber++;
            lineStart = i + 1;
        }
    }

    internal static string NormalizeLineEndings(string value) =>
        (value ?? "").Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine);
}
