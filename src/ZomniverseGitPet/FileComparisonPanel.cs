using System.Text.RegularExpressions;

namespace ZomniverseGitPet;

internal sealed record FileComparisonModel(
    string RelativePath,
    string BaselineLabel,
    bool HasBaseline,
    bool BeforeExists,
    string BeforeText,
    bool AfterExists,
    string AfterText,
    DiffLineMap ChangedLines,
    string? BeforeMessage = null,
    string? AfterMessage = null);

internal sealed record DiffLineMap(IReadOnlySet<int> BeforeLines, IReadOnlySet<int> AfterLines)
{
    public static DiffLineMap Empty { get; } = new(new HashSet<int>(), new HashSet<int>());

    public static DiffLineMap ParseUnifiedZeroContext(string? diff)
    {
        var before = new HashSet<int>();
        var after = new HashSet<int>();
        if (string.IsNullOrWhiteSpace(diff)) return new(before, after);

        var header = new Regex(
            @"^@@\s+-(?<oldStart>\d+)(?:,(?<oldCount>\d+))?\s+\+(?<newStart>\d+)(?:,(?<newCount>\d+))?\s+@@",
            RegexOptions.Multiline,
            TimeSpan.FromMilliseconds(250));

        foreach (Match match in header.Matches(diff))
        {
            var oldStart = int.Parse(match.Groups["oldStart"].Value);
            var newStart = int.Parse(match.Groups["newStart"].Value);
            var oldCount = match.Groups["oldCount"].Success ? int.Parse(match.Groups["oldCount"].Value) : 1;
            var newCount = match.Groups["newCount"].Success ? int.Parse(match.Groups["newCount"].Value) : 1;

            for (var i = 0; i < oldCount; i++) before.Add(oldStart + i);
            for (var i = 0; i < newCount; i++) after.Add(newStart + i);
        }

        return new(before, after);
    }
}

internal sealed class FileComparisonPanel : Panel
{
    private readonly Label _pathLabel = new();
    private readonly Label _baselineLabel = new();
    private readonly Label _beforeTitle = new();
    private readonly Label _afterTitle = new();
    private readonly SyntaxCodeBox _before = new();
    private readonly SyntaxCodeBox _after = new();
    private readonly Button _activityButton;
    private readonly Button _checkpointButton;
    private readonly SplitContainer _split = new();
    private bool _splitLayoutInitialized;

    public FileComparisonPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = GuardianTheme.Console;
        Padding = Padding.Empty;

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 58,
            BackColor = GuardianTheme.ConsoleHeader,
            Padding = new Padding(14, 6, 12, 5)
        };

        var title = new Label
        {
            AutoSize = false,
            Width = 120,
            Height = 24,
            Location = new Point(14, 5),
            Text = "FILE REVIEW",
            ForeColor = GuardianTheme.MutedInk,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _pathLabel.AutoSize = false;
        _pathLabel.Height = 24;
        _pathLabel.Location = new Point(132, 5);
        _pathLabel.ForeColor = GuardianTheme.Ink;
        _pathLabel.Font = new Font("Cascadia Mono", 8.4f);
        _pathLabel.TextAlign = ContentAlignment.MiddleLeft;
        _pathLabel.AutoEllipsis = true;
        _pathLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        _baselineLabel.AutoSize = false;
        _baselineLabel.Height = 20;
        _baselineLabel.Location = new Point(14, 32);
        _baselineLabel.ForeColor = GuardianTheme.FaintInk;
        _baselineLabel.Font = new Font("Segoe UI", 8.2f);
        _baselineLabel.TextAlign = ContentAlignment.MiddleLeft;
        _baselineLabel.AutoEllipsis = true;
        _baselineLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        _activityButton = MakeHeaderButton("Activity", 92);
        _activityButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _activityButton.Click += (_, _) => ActivityRequested?.Invoke(this, EventArgs.Empty);

        _checkpointButton = MakeHeaderButton("Create checkpoint", 144);
        _checkpointButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _checkpointButton.BackColor = GuardianTheme.Violet;
        _checkpointButton.FlatAppearance.BorderColor = GuardianTheme.HotPink;
        _checkpointButton.Visible = false;
        _checkpointButton.Click += (_, _) => CreateCheckpointRequested?.Invoke(this, EventArgs.Empty);

        header.Controls.Add(title);
        header.Controls.Add(_pathLabel);
        header.Controls.Add(_baselineLabel);
        header.Controls.Add(_checkpointButton);
        header.Controls.Add(_activityButton);
        header.Resize += (_, _) =>
        {
            _activityButton.Location = new Point(header.ClientSize.Width - _activityButton.Width - 12, 11);
            _checkpointButton.Location = new Point(_activityButton.Left - _checkpointButton.Width - 8, 11);
            _pathLabel.Width = Math.Max(120, _checkpointButton.Visible
                ? _checkpointButton.Left - _pathLabel.Left - 10
                : _activityButton.Left - _pathLabel.Left - 10);
            _baselineLabel.Width = Math.Max(120, _activityButton.Left - _baselineLabel.Left - 10);
        };

        _split.Dock = DockStyle.Fill;
        _split.Orientation = Orientation.Vertical;
        _split.SplitterWidth = 6;
        _split.BackColor = GuardianTheme.BorderSoft;
        _split.BorderStyle = BorderStyle.None;
        _split.SizeChanged += (_, _) => ApplySafeSplitLayout();

        _split.Panel1.Controls.Add(BuildPane(_beforeTitle, _before, isBefore: true));
        _split.Panel2.Controls.Add(BuildPane(_afterTitle, _after, isBefore: false));

        Controls.Add(_split);
        Controls.Add(header);

        // SplitContainer starts life at a tiny default size before docking/layout runs.
        // Defer its 50/50 divider and minimum pane sizes until it has real dimensions.
        HandleCreated += (_, _) => BeginInvoke(new Action(ApplySafeSplitLayout));
    }

    public event EventHandler? ActivityRequested;
    public event EventHandler? CreateCheckpointRequested;

    public void ShowLoading(string relativePath)
    {
        _pathLabel.Text = relativePath;
        _baselineLabel.Text = "Loading the latest local baseline and working-tree version…";
        _checkpointButton.Visible = false;
        _beforeTitle.Text = "BEFORE  ·  LATEST LOCAL COMMIT";
        _afterTitle.Text = "NOW  ·  WORKING TREE";
        _before.SetMessage("Loading…");
        _after.SetMessage("Loading…");
        UpdateHeaderLayout();
    }

    public void ShowComparison(FileComparisonModel model)
    {
        _pathLabel.Text = model.RelativePath;
        _checkpointButton.Visible = !model.HasBaseline;
        _baselineLabel.Text = model.HasBaseline
            ? $"Comparing against {model.BaselineLabel}. Changed lines are softly illuminated."
            : "No local commit/checkpoint exists yet. Create one now to establish the baseline for future reviews.";

        _beforeTitle.Text = model.HasBaseline
            ? "BEFORE  ·  LATEST LOCAL COMMIT / CHECKPOINT"
            : "BEFORE  ·  NO BASELINE YET";
        _afterTitle.Text = "NOW  ·  WORKING TREE";

        if (!model.HasBaseline)
        {
            _before.SetMessage(
                "NO CHECKPOINT BASELINE YET\n\n" +
                "GitPet needs one local commit before it can show a meaningful Before view.\n\n" +
                "Choose Create checkpoint above. That stays local and becomes the baseline for the next changes you make.");
        }
        else if (!model.BeforeExists)
        {
            _before.SetMessage(model.BeforeMessage ??
                "NEW FILE\n\nThis file did not exist in the latest local commit/checkpoint.");
        }
        else
        {
            _before.SetDocument(model.BeforeText, model.RelativePath, model.ChangedLines.BeforeLines, isAfter: false);
        }

        if (!model.AfterExists)
        {
            _after.SetMessage(model.AfterMessage ??
                "DELETED FROM WORKING TREE\n\nThe file existed in the baseline but is no longer present on disk.");
        }
        else
        {
            _after.SetDocument(model.AfterText, model.RelativePath, model.ChangedLines.AfterLines, isAfter: true);
        }

        UpdateHeaderLayout();
    }

    public void ShowProblem(string relativePath, string message)
    {
        _pathLabel.Text = relativePath;
        _baselineLabel.Text = "GitPet could not build this comparison.";
        _checkpointButton.Visible = false;
        _before.SetMessage("FILE REVIEW UNAVAILABLE");
        _after.SetMessage(message);
        UpdateHeaderLayout();
    }

    private Control BuildPane(Label paneTitle, SyntaxCodeBox box, bool isBefore)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = GuardianTheme.Console,
            Padding = isBefore ? new Padding(0, 0, 3, 0) : new Padding(3, 0, 0, 0)
        };

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 36,
            BackColor = GuardianTheme.SurfaceSoft,
            Padding = new Padding(12, 0, 10, 0)
        };

        paneTitle.Dock = DockStyle.Fill;
        paneTitle.ForeColor = isBefore ? GuardianTheme.HotPinkSoft : GuardianTheme.Healthy;
        paneTitle.Font = new Font("Segoe UI", 8.3f, FontStyle.Bold);
        paneTitle.TextAlign = ContentAlignment.MiddleLeft;
        paneTitle.AutoEllipsis = true;
        header.Controls.Add(paneTitle);

        box.Dock = DockStyle.Fill;
        panel.Controls.Add(box);
        panel.Controls.Add(header);
        return panel;
    }

    private void ApplySafeSplitLayout()
    {
        var width = _split.ClientSize.Width;
        var available = width - _split.SplitterWidth;
        if (available < 2) return;

        // Reset minimums first so a resize can never temporarily violate an old constraint.
        _split.Panel1MinSize = 0;
        _split.Panel2MinSize = 0;

        var paneMinimum = Math.Min(180, Math.Max(0, (available - 1) / 2));
        var desired = _splitLayoutInitialized ? _split.SplitterDistance : available / 2;
        var maximumDistance = Math.Max(paneMinimum, available - paneMinimum);
        var safeDistance = Math.Clamp(desired, paneMinimum, maximumDistance);

        _split.SplitterDistance = safeDistance;
        _split.Panel1MinSize = paneMinimum;
        _split.Panel2MinSize = paneMinimum;
        _splitLayoutInitialized = true;
    }

    private void UpdateHeaderLayout()
    {
        if (Controls.Count == 0) return;
        var header = Controls.OfType<Panel>().LastOrDefault();
        header?.PerformLayout();
        header?.Invalidate();
        if (header is not null)
        {
            _activityButton.Location = new Point(header.ClientSize.Width - _activityButton.Width - 12, 11);
            _checkpointButton.Location = new Point(_activityButton.Left - _checkpointButton.Width - 8, 11);
            _pathLabel.Width = Math.Max(120, _checkpointButton.Visible
                ? _checkpointButton.Left - _pathLabel.Left - 10
                : _activityButton.Left - _pathLabel.Left - 10);
            _baselineLabel.Width = Math.Max(120, _activityButton.Left - _baselineLabel.Left - 10);
        }
    }

    private static Button MakeHeaderButton(string text, int width)
    {
        var button = new Button
        {
            Text = text,
            Width = width,
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            BackColor = GuardianTheme.SurfaceSoft,
            ForeColor = GuardianTheme.Ink,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 8.4f, FontStyle.Bold),
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderColor = GuardianTheme.Border;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = GuardianTheme.VioletHover;
        button.FlatAppearance.MouseDownBackColor = GuardianTheme.VioletPressed;
        return button;
    }
}

internal sealed class SyntaxCodeBox : RichTextBox
{
    private const int MaxPreviewCharacters = 300_000;

    private static readonly Color CodeInk = Color.FromArgb(232, 226, 239);
    private static readonly Color Keyword = Color.FromArgb(116, 200, 255);
    private static readonly Color String = Color.FromArgb(240, 154, 199);
    private static readonly Color Number = Color.FromArgb(255, 202, 111);
    private static readonly Color Comment = Color.FromArgb(111, 180, 104);
    private static readonly Color Property = Color.FromArgb(194, 155, 244);
    private static readonly Color Variable = Color.FromArgb(105, 218, 226);
    private static readonly Color BeforeChange = Color.FromArgb(53, 27, 40);
    private static readonly Color AfterChange = Color.FromArgb(25, 52, 43);

    public SyntaxCodeBox()
    {
        ReadOnly = true;
        BorderStyle = BorderStyle.None;
        BackColor = GuardianTheme.Console;
        ForeColor = CodeInk;
        Font = new Font("Cascadia Mono", 9.1f);
        WordWrap = false;
        DetectUrls = false;
        ScrollBars = RichTextBoxScrollBars.Both;
        HideSelection = false;
        TabStop = true;
        AcceptsTab = false;
    }

    public void SetMessage(string message)
    {
        Text = message;
        SelectAll();
        SelectionColor = GuardianTheme.MutedInk;
        SelectionBackColor = GuardianTheme.Console;
        SelectionFont = new Font("Segoe UI", 9.5f);
        Select(0, 0);
    }

    public void SetDocument(string text, string path, IReadOnlySet<int> changedLines, bool isAfter)
    {
        var truncated = text.Length > MaxPreviewCharacters;
        if (truncated)
        {
            text = text[..MaxPreviewCharacters] +
                   "\r\n\r\n/* Preview truncated by GitPet after 300,000 characters. The file itself was not changed. */";
        }

        Text = NormalizeLineEndings(text);
        SelectAll();
        SelectionColor = CodeInk;
        SelectionBackColor = GuardianTheme.Console;
        SelectionFont = new Font("Cascadia Mono", 9.1f);

        ApplySyntax(path);
        ApplyChangedLineBackgrounds(changedLines, isAfter ? AfterChange : BeforeChange);
        Select(0, 0);
    }

    private void ApplySyntax(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();

        // Comments first; later token passes keep quoted URLs/strings readable.
        if (extension is ".js" or ".jsx" or ".ts" or ".tsx" or ".cs" or ".java" or ".c" or ".cpp" or ".h" or ".hpp" or ".css" or ".scss" or ".php")
        {
            ApplyPattern(@"/\*[\s\S]*?\*/|//.*$", Comment, RegexOptions.Multiline);
        }
        else if (extension is ".py" or ".ps1" or ".sh" or ".yml" or ".yaml")
        {
            ApplyPattern(@"#.*$", Comment, RegexOptions.Multiline);
        }
        else if (extension is ".sql")
        {
            ApplyPattern(@"--.*$|/\*[\s\S]*?\*/", Comment, RegexOptions.Multiline);
        }
        else if (extension is ".html" or ".htm" or ".xml" or ".svg")
        {
            ApplyPattern(@"<!--[\s\S]*?-->", Comment, RegexOptions.Multiline);
        }

        ApplyPattern("\"(?:\\\\.|[^\"\\\\])*\"|'(?:\\\\.|[^'\\\\])*'|`(?:\\\\.|[^`\\\\])*`", String, RegexOptions.Multiline);
        ApplyPattern(@"\b\d+(?:\.\d+)?\b", Number, RegexOptions.Multiline);

        if (extension == ".json")
            ApplyPattern("\"(?:\\\\.|[^\"\\\\])*\"(?=\\s*:)", Property, RegexOptions.Multiline);

        var keywords = extension switch
        {
            ".py" => "and|as|assert|async|await|break|class|continue|def|del|elif|else|except|False|finally|for|from|global|if|import|in|is|lambda|None|nonlocal|not|or|pass|raise|return|True|try|while|with|yield",
            ".sql" => "SELECT|FROM|WHERE|JOIN|LEFT|RIGHT|INNER|OUTER|ON|INSERT|INTO|VALUES|UPDATE|SET|DELETE|CREATE|ALTER|DROP|TABLE|INDEX|VIEW|AS|AND|OR|NOT|NULL|PRIMARY|KEY|FOREIGN|REFERENCES|CASCADE|BEGIN|COMMIT|ROLLBACK|WITH|RETURNING",
            ".ps1" => "function|param|if|else|elseif|foreach|for|while|switch|return|throw|try|catch|finally|class|enum|using|begin|process|end",
            ".php" => "abstract|and|array|as|break|callable|case|catch|class|clone|const|continue|declare|default|do|echo|else|elseif|empty|enddeclare|endfor|endforeach|endif|endswitch|endwhile|extends|final|finally|fn|for|foreach|function|global|goto|if|implements|include|include_once|instanceof|insteadof|interface|isset|list|match|namespace|new|or|print|private|protected|public|readonly|require|require_once|return|static|switch|throw|trait|try|unset|use|var|while|yield",
            ".js" or ".jsx" or ".ts" or ".tsx" => "as|async|await|break|case|catch|class|const|continue|debugger|default|delete|do|else|export|extends|false|finally|for|from|function|get|if|import|in|instanceof|let|new|null|of|return|set|static|super|switch|this|throw|true|try|typeof|undefined|var|void|while|with|yield|interface|type|enum|implements|private|protected|public|readonly",
            ".cs" => "abstract|as|async|await|base|bool|break|byte|case|catch|char|checked|class|const|continue|decimal|default|delegate|do|double|else|enum|event|explicit|extern|false|finally|fixed|float|for|foreach|from|get|global|goto|if|implicit|in|int|interface|internal|is|lock|long|namespace|new|null|object|operator|out|override|params|partial|private|protected|public|readonly|record|ref|required|return|sbyte|sealed|set|short|sizeof|stackalloc|static|string|struct|switch|this|throw|true|try|typeof|uint|ulong|unchecked|unsafe|ushort|using|var|virtual|void|volatile|while|with|yield",
            _ => ""
        };

        if (keywords.Length > 0)
            ApplyPattern($@"\b(?:{keywords.Replace("|", "|")})\b", Keyword, RegexOptions.IgnoreCase | RegexOptions.Multiline);

        if (extension == ".php")
            ApplyPattern(@"\$[A-Za-z_][A-Za-z0-9_]*", Variable, RegexOptions.Multiline);

        if (extension is ".html" or ".htm" or ".xml" or ".svg")
            ApplyPattern(@"</?[A-Za-z][A-Za-z0-9:_-]*", Keyword, RegexOptions.Multiline);

        if (extension is ".css" or ".scss")
            ApplyPattern(@"(?<=^|[;{])\s*[A-Za-z-]+(?=\s*:)", Property, RegexOptions.Multiline);
    }

    private void ApplyPattern(string pattern, Color color, RegexOptions options)
    {
        try
        {
            var regex = new Regex(pattern, options, TimeSpan.FromMilliseconds(300));
            foreach (Match match in regex.Matches(Text))
            {
                Select(match.Index, match.Length);
                SelectionColor = color;
            }
        }
        catch (RegexMatchTimeoutException)
        {
            // Syntax highlighting is cosmetic. A difficult file must never block review.
        }
        catch (ArgumentException)
        {
            // Keep the raw readable text if a highlighting expression is unsupported.
        }
    }

    private void ApplyChangedLineBackgrounds(IReadOnlySet<int> changedLines, Color color)
    {
        foreach (var lineNumber in changedLines.OrderBy(value => value))
        {
            var lineIndex = lineNumber - 1;
            if (lineIndex < 0 || lineIndex >= Lines.Length) continue;

            var start = GetFirstCharIndexFromLine(lineIndex);
            if (start < 0) continue;
            var next = lineIndex + 1 < Lines.Length ? GetFirstCharIndexFromLine(lineIndex + 1) : TextLength;
            var length = Math.Max(0, next - start);
            if (length == 0) continue;

            Select(start, length);
            SelectionBackColor = color;
        }
    }

    private static string NormalizeLineEndings(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal)
             .Replace("\r", "\n", StringComparison.Ordinal)
             .Replace("\n", Environment.NewLine, StringComparison.Ordinal);
}
