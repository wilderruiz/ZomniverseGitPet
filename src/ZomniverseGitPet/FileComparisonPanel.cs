using System.Diagnostics;
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
    private readonly FileReviewPane _beforePane = new(isBefore: true);
    private readonly FileReviewPane _afterPane = new(isBefore: false);
    private readonly Button _activityButton;
    private readonly Button _checkpointButton;
    private readonly Button _technicalButton;
    private readonly Button _openButton;
    private readonly SplitContainer _split = new();

    private FileComparisonModel? _currentModel;
    private bool _technicalMode;
    private bool _applyingSplitLayout;
    private double _splitRatio = 0.5;
    private int _renderGeneration;

    public FileComparisonPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = GuardianTheme.Console;
        Padding = Padding.Empty;

        var header = BuildHeader();

        _split.Dock = DockStyle.Fill;
        _split.Orientation = Orientation.Vertical;
        _split.SplitterWidth = 6;
        _split.Panel1MinSize = 0;
        _split.Panel2MinSize = 0;
        _split.BackColor = GuardianTheme.BorderSoft;
        _split.BorderStyle = BorderStyle.None;
        _split.SizeChanged += (_, _) => ApplySafeSplitLayout();
        _split.SplitterMoved += (_, _) =>
        {
            if (_applyingSplitLayout) return;
            var available = _split.ClientSize.Width - _split.SplitterWidth;
            if (available <= 0) return;
            _splitRatio = Math.Clamp((double)_split.SplitterDistance / available, 0.05, 0.95);
        };

        _split.Panel1.Controls.Add(_beforePane);
        _split.Panel2.Controls.Add(_afterPane);

        Controls.Add(_split);
        Controls.Add(header);

        HandleCreated += (_, _) => BeginInvoke(new Action(ApplySafeSplitLayout));

        _activityButton = FindHeaderButton(header, "Activity");
        _checkpointButton = FindHeaderButton(header, "Create checkpoint");
        _technicalButton = FindHeaderButton(header, "Technical view");
        _openButton = FindHeaderButton(header, "Open / locate");

        _activityButton.Click += (_, _) => ActivityRequested?.Invoke(this, EventArgs.Empty);
        _checkpointButton.Click += (_, _) => CreateCheckpointRequested?.Invoke(this, EventArgs.Empty);
        _technicalButton.Click += (_, _) => ToggleTechnicalView();
        _openButton.Click += (_, _) => OpenOrLocateCurrentItem();
    }

    public event EventHandler? ActivityRequested;
    public event EventHandler? CreateCheckpointRequested;

    public void ShowLoading(string relativePath)
    {
        _renderGeneration++;
        _currentModel = null;
        _technicalMode = false;

        _pathLabel.Text = relativePath;
        _baselineLabel.Text = "Loading the saved baseline and the current working item…";
        _checkpointButton.Visible = false;
        _technicalButton.Visible = false;
        _openButton.Visible = false;

        _beforePane.SetTitle("BEFORE  ·  SAVED VERSION");
        _afterPane.SetTitle("NOW  ·  CURRENT VERSION");
        _beforePane.ShowSummary("LOADING…", "GitPet is checking the latest local checkpoint.", GuardianTheme.MutedInk);
        _afterPane.ShowSummary("LOADING…", "GitPet is checking the current working item.", GuardianTheme.MutedInk);
    }

    public void ShowComparison(FileComparisonModel model)
    {
        _currentModel = model;
        _technicalMode = false;
        var generation = ++_renderGeneration;

        _pathLabel.Text = model.RelativePath;
        _checkpointButton.Visible = !model.HasBaseline;
        _baselineLabel.Text = model.HasBaseline
            ? $"Human view · comparing the saved version against {model.BaselineLabel}. Technical text is optional."
            : "Human view · no local checkpoint exists yet. Create one to establish a BEFORE baseline.";

        _beforePane.SetTitle(model.HasBaseline
            ? "BEFORE  ·  SAVED VERSION / CHECKPOINT"
            : "BEFORE  ·  NO BASELINE YET");
        _afterPane.SetTitle("NOW  ·  CURRENT WORKING ITEM");

        _technicalButton.Visible = CanShowTechnical(model);
        _technicalButton.Text = "Technical view";

        var location = ResolveCurrentPath(model.RelativePath);
        _openButton.Visible = location is { Exists: true };

        RenderHumanSummary(model, beforeSize: null, afterSize: location?.SizeBytes, afterIsDirectory: location?.IsDirectory == true);

        _ = LoadExactBaselineSizeAsync(model, generation);
    }

    public void ShowProblem(string relativePath, string message)
    {
        _renderGeneration++;
        _currentModel = null;
        _technicalMode = false;

        _pathLabel.Text = relativePath;
        _baselineLabel.Text = "GitPet could not build this file review.";
        _checkpointButton.Visible = false;
        _technicalButton.Visible = false;
        _openButton.Visible = false;

        _beforePane.SetTitle("BEFORE");
        _afterPane.SetTitle("NOW");
        _beforePane.ShowSummary("REVIEW UNAVAILABLE", "Nothing was changed by GitPet.", GuardianTheme.Warning);
        _afterPane.ShowSummary("DETAILS", message, GuardianTheme.MutedInk);
    }

    private Control BuildHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 70,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = GuardianTheme.ConsoleHeader,
            Padding = new Padding(14, 6, 12, 5)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var info = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = GuardianTheme.ConsoleHeader,
            Margin = Padding.Empty
        };

        var title = new Label
        {
            AutoSize = false,
            Width = 112,
            Height = 24,
            Location = new Point(0, 0),
            Text = "FILE REVIEW",
            ForeColor = GuardianTheme.MutedInk,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _pathLabel.AutoSize = false;
        _pathLabel.Height = 24;
        _pathLabel.Location = new Point(116, 0);
        _pathLabel.ForeColor = GuardianTheme.Ink;
        _pathLabel.Font = new Font("Cascadia Mono", 8.4f);
        _pathLabel.TextAlign = ContentAlignment.MiddleLeft;
        _pathLabel.AutoEllipsis = true;
        _pathLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        _baselineLabel.AutoSize = false;
        _baselineLabel.Height = 28;
        _baselineLabel.Location = new Point(0, 28);
        _baselineLabel.ForeColor = GuardianTheme.FaintInk;
        _baselineLabel.Font = new Font("Segoe UI", 8.2f);
        _baselineLabel.TextAlign = ContentAlignment.MiddleLeft;
        _baselineLabel.AutoEllipsis = true;
        _baselineLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        info.Controls.Add(title);
        info.Controls.Add(_pathLabel);
        info.Controls.Add(_baselineLabel);
        info.Resize += (_, _) =>
        {
            _pathLabel.Width = Math.Max(100, info.ClientSize.Width - _pathLabel.Left);
            _baselineLabel.Width = Math.Max(100, info.ClientSize.Width);
        };

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = GuardianTheme.ConsoleHeader,
            Margin = new Padding(8, 8, 0, 0)
        };

        var technical = MakeHeaderButton("Technical view", 118);
        technical.Name = "Technical view";
        technical.Visible = false;

        var open = MakeHeaderButton("Open / locate", 112);
        open.Name = "Open / locate";
        open.Visible = false;

        var checkpoint = MakeHeaderButton("Create checkpoint", 136);
        checkpoint.Name = "Create checkpoint";
        checkpoint.BackColor = GuardianTheme.Violet;
        checkpoint.FlatAppearance.BorderColor = GuardianTheme.HotPink;
        checkpoint.Visible = false;

        var activity = MakeHeaderButton("Activity", 86);
        activity.Name = "Activity";

        buttons.Controls.Add(technical);
        buttons.Controls.Add(open);
        buttons.Controls.Add(checkpoint);
        buttons.Controls.Add(activity);

        header.Controls.Add(info, 0, 0);
        header.Controls.Add(buttons, 1, 0);
        return header;
    }

    private static Button FindHeaderButton(Control root, string name)
    {
        foreach (Control child in root.Controls)
        {
            if (child is Button button && button.Name == name) return button;
            var nested = FindHeaderButtonOrNull(child, name);
            if (nested is not null) return nested;
        }

        throw new InvalidOperationException($"GitPet could not find the '{name}' File Review button.");
    }

    private static Button? FindHeaderButtonOrNull(Control root, string name)
    {
        foreach (Control child in root.Controls)
        {
            if (child is Button button && button.Name == name) return button;
            var nested = FindHeaderButtonOrNull(child, name);
            if (nested is not null) return nested;
        }

        return null;
    }

    private void RenderHumanSummary(
        FileComparisonModel model,
        long? beforeSize,
        long? afterSize,
        bool afterIsDirectory)
    {
        if (!model.HasBaseline)
        {
            _beforePane.ShowSummary(
                "NO BASELINE YET",
                "GitPet does not have a saved checkpoint to compare against.\r\n\r\n" +
                "Create a checkpoint to establish the first BEFORE version.",
                GuardianTheme.Warning);
        }
        else if (!model.BeforeExists)
        {
            _beforePane.ShowSummary(
                "DID NOT EXIST",
                "This item was not present in the latest local checkpoint.\r\n\r\n" +
                "That means the current item is NEW.",
                GuardianTheme.HotPinkSoft);
        }
        else
        {
            _beforePane.ShowSummary(
                "SAVED VERSION",
                "Status     Saved in the latest local checkpoint\r\n" +
                $"Size       {FormatSize(beforeSize)}\r\n" +
                $"Baseline   {model.BaselineLabel}",
                GuardianTheme.HotPinkSoft);
        }

        var currentLocation = ResolveCurrentPath(model.RelativePath);
        var currentExists = model.AfterExists || currentLocation is { Exists: true };

        if (!currentExists)
        {
            _afterPane.ShowSummary(
                "DELETED",
                "This item existed in the saved version but is no longer present in the working folder.",
                GuardianTheme.Warning);
            return;
        }

        if (afterIsDirectory || currentLocation is { IsDirectory: true })
        {
            _afterPane.ShowSummary(
                model.BeforeExists ? "FOLDER CHANGED" : "NEW FOLDER",
                "This is a folder. GitPet tracks the changed items inside it rather than assigning one file size to the folder.\r\n\r\n" +
                "Open / locate will show it in Windows Explorer.",
                GuardianTheme.Healthy);
            return;
        }

        var status = model.BeforeExists ? "MODIFIED" : "NEW FILE";
        var detail =
            $"Status     {(model.BeforeExists ? "Changed since the saved checkpoint" : "Added after the saved checkpoint")}\r\n" +
            $"Size       {FormatSize(afterSize)}";

        if (model.BeforeExists && beforeSize.HasValue && afterSize.HasValue)
            detail += "\r\n" + $"Difference {FormatSizeDifference(beforeSize.Value, afterSize.Value)}";

        _afterPane.ShowSummary(status, detail, GuardianTheme.Healthy);
    }

    private async Task LoadExactBaselineSizeAsync(FileComparisonModel model, int generation)
    {
        if (!model.HasBaseline || !model.BeforeExists) return;

        var repository = TryGetRepositoryPath();
        if (repository is null) return;

        var beforeSize = await ReadGitBlobSizeAsync(repository, model.RelativePath);
        if (generation != _renderGeneration || IsDisposed || _currentModel != model) return;

        var location = ResolveCurrentPath(model.RelativePath);
        RenderHumanSummary(
            model,
            beforeSize,
            location?.SizeBytes,
            location?.IsDirectory == true);
    }

    private static async Task<long?> ReadGitBlobSizeAsync(string repositoryPath, string relativePath)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git.exe",
                    WorkingDirectory = repositoryPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.StartInfo.ArgumentList.Add("cat-file");
            process.StartInfo.ArgumentList.Add("-s");
            process.StartInfo.ArgumentList.Add("HEAD:" + NormalizeGitPath(relativePath));

            process.Start();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            _ = await stderr;

            var output = (await stdout).Trim();
            return process.ExitCode == 0 && long.TryParse(output, out var value) ? value : null;
        }
        catch
        {
            return null;
        }
    }

    private void ToggleTechnicalView()
    {
        var model = _currentModel;
        if (model is null || !CanShowTechnical(model)) return;

        _technicalMode = !_technicalMode;
        _technicalButton.Text = _technicalMode ? "Human view" : "Technical view";

        if (!_technicalMode)
        {
            var location = ResolveCurrentPath(model.RelativePath);
            RenderHumanSummary(model, beforeSize: null, afterSize: location?.SizeBytes, afterIsDirectory: location?.IsDirectory == true);
            _ = LoadExactBaselineSizeAsync(model, _renderGeneration);
            _baselineLabel.Text = model.HasBaseline
                ? $"Human view · comparing the saved version against {model.BaselineLabel}. Technical text is optional."
                : "Human view · no local checkpoint exists yet. Create one to establish a BEFORE baseline.";
            return;
        }

        _baselineLabel.Text =
            "Technical text view · read-only. GitPet is showing raw text only because this item appears safe to render as text.";

        if (!model.HasBaseline)
            _beforePane.ShowTechnicalMessage("NO BASELINE YET\r\n\r\nCreate a checkpoint first.");
        else if (!model.BeforeExists)
            _beforePane.ShowTechnicalMessage(model.BeforeMessage ?? "NEW FILE\r\n\r\nThis item did not exist in the saved version.");
        else if (IsReasonablyText(model.BeforeText))
            _beforePane.ShowTechnical(model.BeforeText, model.ChangedLines.BeforeLines, isAfter: false);
        else
            _beforePane.ShowTechnicalMessage("TECHNICAL TEXT VIEW UNAVAILABLE\r\n\r\nThe saved version does not look like ordinary text.");

        if (!model.AfterExists)
            _afterPane.ShowTechnicalMessage(model.AfterMessage ?? "DELETED FROM WORKING TREE");
        else if (IsReasonablyTextPreview(model.AfterText))
            _afterPane.ShowTechnical(model.AfterText, model.ChangedLines.AfterLines, isAfter: true);
        else
            _afterPane.ShowTechnicalMessage(
                "TECHNICAL TEXT VIEW UNAVAILABLE\r\n\r\n" +
                "This item does not look like ordinary text. Use the Human view or Open / locate instead.");
    }

    private void OpenOrLocateCurrentItem()
    {
        var model = _currentModel;
        if (model is null) return;

        var location = ResolveCurrentPath(model.RelativePath);
        if (location is not { Exists: true }) return;

        try
        {
            if (location.IsDirectory)
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{location.FullPath}\"")
                {
                    UseShellExecute = true
                });
                return;
            }

            if (ShouldOnlyLocate(location.FullPath))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{location.FullPath}\"")
                {
                    UseShellExecute = true
                });
                return;
            }

            Process.Start(new ProcessStartInfo(location.FullPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                FindForm(),
                "GitPet could not open this item.\r\n\r\n" + ex.Message,
                "Open file",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }

    private static bool ShouldOnlyLocate(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".com", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".scr", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".msi", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".bat", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".ps1", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".psm1", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".vbs", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".vbe", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".js", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jse", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".wsf", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".wsh", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".reg", StringComparison.OrdinalIgnoreCase);
    }

    private static bool CanShowTechnical(FileComparisonModel model)
    {
        if (model.BeforeExists && IsReasonablyText(model.BeforeText)) return true;
        return model.AfterExists && IsReasonablyTextPreview(model.AfterText);
    }

    private static bool IsReasonablyTextPreview(string text)
    {
        if (string.IsNullOrEmpty(text)) return true;

        if (text.StartsWith("BINARY FILE", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("BINARY-LIKE FILE", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("LARGE FILE PREVIEW", StringComparison.OrdinalIgnoreCase))
            return false;

        return IsReasonablyText(text);
    }

    private static bool IsReasonablyText(string text)
    {
        if (string.IsNullOrEmpty(text)) return true;

        var sampleLength = Math.Min(4096, text.Length);
        var controls = 0;
        for (var i = 0; i < sampleLength; i++)
        {
            var c = text[i];
            if (c == '\0') return false;
            if (char.IsControl(c) && c is not '\r' and not '\n' and not '\t')
                controls++;
        }

        return controls <= Math.Max(2, sampleLength / 100);
    }

    private CurrentLocation? ResolveCurrentPath(string relativePath)
    {
        var repository = TryGetRepositoryPath();
        if (repository is null) return null;

        try
        {
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repository));
            var fullPath = Path.GetFullPath(
                Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

            var requiredPrefix = root + Path.DirectorySeparatorChar;
            if (!fullPath.Equals(root, StringComparison.OrdinalIgnoreCase) &&
                !fullPath.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase))
                return null;

            if (File.Exists(fullPath))
            {
                var info = new FileInfo(fullPath);
                return new(true, false, fullPath, info.Length);
            }

            if (Directory.Exists(fullPath))
                return new(true, true, fullPath, null);

            return new(false, false, fullPath, null);
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetRepositoryPath()
    {
        try
        {
            var repository = new ConfigStore().Load().RepositoryPath;
            return string.IsNullOrWhiteSpace(repository) || !Directory.Exists(repository)
                ? null
                : repository;
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeGitPath(string relativePath) =>
        (relativePath ?? "").Replace('\\', '/').TrimStart('/');

    private static string FormatSize(long? bytes)
    {
        if (!bytes.HasValue) return "Calculating…";
        if (bytes.Value < 1024) return $"{bytes.Value:N0} B";

        var kb = bytes.Value / 1024d;
        if (kb < 1024) return $"{kb:N1} KB";

        var mb = kb / 1024d;
        if (mb < 1024) return $"{mb:N1} MB";

        return $"{mb / 1024d:N2} GB";
    }

    private static string FormatSizeDifference(long before, long after)
    {
        var delta = after - before;
        if (delta == 0) return "Same size · contents still changed";

        var sign = delta > 0 ? "+" : "−";
        var magnitude = FormatSize(Math.Abs(delta));
        double? percent = before == 0 ? null : Math.Abs(delta) * 100d / before;
        return percent.HasValue
            ? $"{sign}{magnitude}  ({sign}{percent.Value:N1}%)"
            : $"{sign}{magnitude}";
    }

    private void ApplySafeSplitLayout()
    {
        if (_applyingSplitLayout || _split.IsDisposed) return;

        var available = _split.ClientSize.Width - _split.SplitterWidth;
        if (available <= 0) return;

        var preferredMinimum = available >= 320 ? 120 : 0;
        var desired = (int)Math.Round(available * _splitRatio);
        var minimumDistance = Math.Min(preferredMinimum, available);
        var maximumDistance = Math.Max(minimumDistance, available - preferredMinimum);
        var safeDistance = Math.Clamp(desired, minimumDistance, maximumDistance);

        try
        {
            _applyingSplitLayout = true;
            if (_split.SplitterDistance != safeDistance)
                _split.SplitterDistance = safeDistance;
        }
        catch (InvalidOperationException)
        {
            if (IsHandleCreated && !IsDisposed && !Disposing)
                BeginInvoke(new Action(ApplySafeSplitLayout));
        }
        finally
        {
            _applyingSplitLayout = false;
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
            UseVisualStyleBackColor = false,
            Margin = new Padding(6, 0, 0, 0)
        };
        button.FlatAppearance.BorderColor = GuardianTheme.Border;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = GuardianTheme.VioletHover;
        button.FlatAppearance.MouseDownBackColor = GuardianTheme.VioletPressed;
        return button;
    }

    private sealed record CurrentLocation(bool Exists, bool IsDirectory, string FullPath, long? SizeBytes);
}

internal sealed class FileReviewPane : Panel
{
    private readonly Label _title = new();
    private readonly RichTextBox _summary = new();
    private readonly RichTextBox _technical = new();

    private static readonly Color BeforeChange = Color.FromArgb(53, 27, 40);
    private static readonly Color AfterChange = Color.FromArgb(25, 52, 43);

    public FileReviewPane(bool isBefore)
    {
        Dock = DockStyle.Fill;
        BackColor = GuardianTheme.Console;
        Padding = isBefore ? new Padding(0, 0, 3, 0) : new Padding(3, 0, 0, 0);

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 38,
            BackColor = GuardianTheme.SurfaceSoft,
            Padding = new Padding(12, 0, 10, 0)
        };

        _title.Dock = DockStyle.Fill;
        _title.ForeColor = isBefore ? GuardianTheme.HotPinkSoft : GuardianTheme.Healthy;
        _title.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        _title.TextAlign = ContentAlignment.MiddleLeft;
        _title.AutoEllipsis = true;
        header.Controls.Add(_title);

        ConfigureTextBox(_summary, new Font("Segoe UI", 10));
        _summary.WordWrap = true;
        _summary.ScrollBars = RichTextBoxScrollBars.Vertical;

        ConfigureTextBox(_technical, new Font("Cascadia Mono", 9.1f));
        _technical.WordWrap = false;
        _technical.ScrollBars = RichTextBoxScrollBars.Both;
        _technical.Visible = false;

        Controls.Add(_summary);
        Controls.Add(_technical);
        Controls.Add(header);
    }

    public void SetTitle(string value) => _title.Text = value;

    public void ShowSummary(string headline, string detail, Color headlineColor)
    {
        _technical.Visible = false;
        _summary.Visible = true;
        _summary.Clear();

        _summary.SelectionColor = headlineColor;
        _summary.SelectionFont = new Font("Segoe UI", 15, FontStyle.Bold);
        _summary.AppendText(headline + "\r\n\r\n");

        _summary.SelectionColor = GuardianTheme.Ink;
        _summary.SelectionFont = new Font("Segoe UI", 10);
        _summary.AppendText(detail);

        _summary.Select(0, 0);
    }

    public void ShowTechnicalMessage(string message)
    {
        _summary.Visible = false;
        _technical.Visible = true;
        _technical.Clear();
        _technical.SelectionColor = GuardianTheme.MutedInk;
        _technical.SelectionFont = new Font("Segoe UI", 9.5f);
        _technical.AppendText(message);
        _technical.Select(0, 0);
    }

    public void ShowTechnical(string text, IReadOnlySet<int> changedLines, bool isAfter)
    {
        _summary.Visible = false;
        _technical.Visible = true;
        _technical.Text = NormalizeLineEndings(text);
        _technical.SelectAll();
        _technical.SelectionColor = Color.FromArgb(232, 226, 239);
        _technical.SelectionBackColor = GuardianTheme.Console;
        _technical.SelectionFont = new Font("Cascadia Mono", 9.1f);

        ApplyChangedLineBackgrounds(_technical, changedLines, isAfter ? AfterChange : BeforeChange);
        _technical.Select(0, 0);
    }

    private static void ConfigureTextBox(RichTextBox box, Font font)
    {
        box.Dock = DockStyle.Fill;
        box.ReadOnly = true;
        box.BorderStyle = BorderStyle.None;
        box.BackColor = GuardianTheme.Console;
        box.ForeColor = GuardianTheme.Ink;
        box.Font = font;
        box.DetectUrls = false;
        box.HideSelection = false;
        box.TabStop = true;
    }

    private static void ApplyChangedLineBackgrounds(
        RichTextBox box,
        IReadOnlySet<int> changedLines,
        Color background)
    {
        if (changedLines.Count == 0 || box.TextLength == 0) return;

        var lineNumber = 1;
        var lineStart = 0;
        for (var i = 0; i <= box.TextLength; i++)
        {
            var atEnd = i == box.TextLength;
            var atLineBreak = !atEnd && box.Text[i] == '\n';
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

    private static string NormalizeLineEndings(string value) =>
        (value ?? "").Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine);
}
