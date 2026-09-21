using System.Diagnostics;

namespace ZomniverseGitPet;

internal sealed class ApplicationReleaseBuildForm : Form
{
    private readonly string _repositoryPath;
    private readonly RichTextBox _output;
    private readonly Label _status;
    private readonly Button _build;
    private readonly Button _cancel;
    private readonly Button _close;

    private CancellationTokenSource? _run;
    private Process? _process;

    public ApplicationReleaseBuildForm(string repositoryPath)
    {
        _repositoryPath =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(repositoryPath));

        Text = "Prepare ZomniverseGitPet application release";
        Icon = AppIconProvider.Icon;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(760, 560);
        Size = new Size(900, 680);
        BackColor = GuardianTheme.Window;
        ForeColor = GuardianTheme.Ink;
        Font = new Font("Segoe UI", 9);
        WindowChrome.ApplyGuardianChrome(this);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = Padding.Empty,
            Margin = Padding.Empty,
            BackColor = GuardianTheme.Window
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));

        root.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 10, 24, 8),
            Text = "◇  PREPARE APPLICATION RELEASE",
            BackColor = GuardianTheme.SurfaceRaised,
            ForeColor = GuardianTheme.Send,
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        var intro = new Label
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 14, 24, 8),
            Text =
                "GitPet will run the repository's existing scripts\\build-release.ps1 pipeline.\r\n" +
                "It requires a clean working tree, builds Release, runs regression tests, creates the portable EXE and Windows installer, then writes the release manifest and SHA-256 checksums. Nothing is published automatically.",
            ForeColor = GuardianTheme.SoftInk,
            TextAlign = ContentAlignment.MiddleLeft
        };
        root.Controls.Add(intro, 0, 1);

        _output = new RichTextBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(20, 6, 20, 8),
            ReadOnly = true,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(8, 13, 18),
            ForeColor = GuardianTheme.Ink,
            Font = new Font("Cascadia Mono", 8.5f),
            DetectUrls = false,
            WordWrap = false
        };
        root.Controls.Add(_output, 0, 2);

        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(20, 12, 20, 10),
            BackColor = GuardianTheme.SurfaceRaised
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _status = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Ready to build the release package.",
            ForeColor = GuardianTheme.MutedInk,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = Padding.Empty
        };

        _close = MakeButton(
            "Close",
            100,
            GuardianTheme.SurfaceSoft,
            GuardianTheme.Border);
        _close.Click += (_, _) => Close();

        _cancel = MakeButton(
            "Cancel build",
            118,
            GuardianTheme.SurfaceSoft,
            GuardianTheme.Warning);
        _cancel.Visible = false;
        _cancel.Click += (_, _) => CancelBuild();

        _build = MakeButton(
            "Build release",
            120,
            GuardianTheme.Violet,
            GuardianTheme.Send);
        _build.Click += async (_, _) => await BuildAsync();

        buttons.Controls.Add(_close);
        buttons.Controls.Add(_cancel);
        buttons.Controls.Add(_build);

        footer.Controls.Add(_status, 0, 0);
        footer.Controls.Add(buttons, 1, 0);
        root.Controls.Add(footer, 0, 3);

        Controls.Add(root);
        AcceptButton = _build;
        CancelButton = _close;

        FormClosing += (_, e) =>
        {
            if (_run is null) return;

            var result = MessageBox.Show(
                this,
                "A release build is still running. Cancel it and close this window?",
                "Release build running",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
            {
                e.Cancel = true;
                return;
            }

            CancelBuild();
        };
    }

    public bool PackageBuiltSuccessfully { get; private set; }

    private async Task BuildAsync()
    {
        if (_run is not null) return;

        var scriptPath =
            Path.Combine(
                _repositoryPath,
                "scripts",
                "build-release.ps1");

        if (!File.Exists(scriptPath))
        {
            MessageBox.Show(
                this,
                "GitPet could not find scripts\\build-release.ps1 in the current source repository.",
                "Release builder",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            this,
            "Build the ZomniverseGitPet release package now?\r\n\r\n" +
            "GitPet will require a clean working tree, run the full Release build and regression suite, create the portable build and installer, and calculate release hashes.\r\n\r\n" +
            "This prepares files only. It does not publish a GitHub Release.",
            "Prepare application release",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information);

        if (confirm != DialogResult.Yes)
            return;

        PackageBuiltSuccessfully = false;
        _output.Clear();
        Append("Preparing release package...");
        Append("Repository: " + _repositoryPath);
        Append(string.Empty);

        _run = new CancellationTokenSource();
        SetBusy(true, "BUILDING RELEASE · tests and packaging are running…");

        try
        {
            var exitCode =
                await RunReleaseBuilderAsync(
                    scriptPath,
                    _run.Token);

            if (_run.IsCancellationRequested)
            {
                _status.Text = "Release build cancelled.";
                _status.ForeColor = GuardianTheme.Warning;
                Append(string.Empty);
                Append("Build cancelled.");
                return;
            }

            if (exitCode != 0)
            {
                _status.Text =
                    $"Release build failed · exit code {exitCode}.";
                _status.ForeColor = GuardianTheme.Warning;
                Append(string.Empty);
                Append(
                    $"Release builder exited with code {exitCode}. " +
                    "Nothing was published.");
                return;
            }

            PackageBuiltSuccessfully = true;
            _status.Text =
                "RELEASE PACKAGE READY ✓ · installer, portable build, manifest and checksums created.";
            _status.ForeColor = GuardianTheme.Healthy;
            Append(string.Empty);
            Append("✓ Release package prepared successfully.");
        }
        catch (OperationCanceledException)
        {
            _status.Text = "Release build cancelled.";
            _status.ForeColor = GuardianTheme.Warning;
            Append(string.Empty);
            Append("Build cancelled.");
        }
        catch (Exception ex)
        {
            _status.Text = "Release build could not complete.";
            _status.ForeColor = GuardianTheme.Warning;
            Append(string.Empty);
            Append("ERROR: " + ex.Message);
        }
        finally
        {
            _process?.Dispose();
            _process = null;

            _run?.Dispose();
            _run = null;

            SetBusy(false, null);
        }
    }

    private async Task<int> RunReleaseBuilderAsync(
        string scriptPath,
        CancellationToken token)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            WorkingDirectory = _repositoryPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(scriptPath);

        _process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        _process.OutputDataReceived +=
            (_, e) =>
            {
                if (e.Data is not null)
                    AppendThreadSafe(e.Data);
            };

        _process.ErrorDataReceived +=
            (_, e) =>
            {
                if (e.Data is not null)
                    AppendThreadSafe(e.Data);
            };

        if (!_process.Start())
            throw new InvalidOperationException(
                "Windows could not start the release builder.");

        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        using var registration =
            token.Register(() =>
            {
                try
                {
                    if (_process is { HasExited: false })
                        _process.Kill(entireProcessTree: true);
                }
                catch
                {
                }
            });

        await _process.WaitForExitAsync();

        token.ThrowIfCancellationRequested();
        return _process.ExitCode;
    }

    private void CancelBuild()
    {
        if (_run is null) return;

        _status.Text = "Cancelling release build…";
        _status.ForeColor = GuardianTheme.Warning;
        _run.Cancel();
    }

    private void SetBusy(bool busy, string? message)
    {
        _build.Enabled = !busy;
        _close.Enabled = !busy;
        _cancel.Visible = busy;
        UseWaitCursor = false;

        if (!string.IsNullOrWhiteSpace(message))
        {
            _status.Text = message;
            _status.ForeColor = GuardianTheme.Info;
        }
    }

    private void AppendThreadSafe(string value)
    {
        if (IsDisposed || Disposing) return;

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(
                    new Action<string>(Append),
                    value);
            }
            catch
            {
            }

            return;
        }

        Append(value);
    }

    private void Append(string value)
    {
        if (IsDisposed || Disposing) return;

        _output.AppendText(
            value +
            Environment.NewLine);

        _output.SelectionStart =
            _output.TextLength;
        _output.ScrollToCaret();
    }

    private static Button MakeButton(
        string text,
        int width,
        Color fill,
        Color border)
    {
        var button = new Button
        {
            Text = text,
            Width = width,
            Height = 38,
            Margin = new Padding(7, 0, 0, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = fill,
            ForeColor = Color.White,
            Font = new Font(
                "Segoe UI",
                9,
                FontStyle.Bold),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };

        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = border;
        return button;
    }
}
