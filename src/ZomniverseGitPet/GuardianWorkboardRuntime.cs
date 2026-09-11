namespace ZomniverseGitPet;

/* ==========================================================================
   PATCH: LIVE GUARDIAN WORKBOARD RUNTIME
   DATE.TIME: 2026-09-11 12:48 +03:00
   Keep projected Git work visible without blocking Guardian operations.
   ========================================================================== */
internal static class GuardianWorkboardRuntime
{
    private static readonly Dictionary<GuardianForm, BoardHost> Boards = [];
    private static AppConfig? _config;
    private static GitService? _git;
    private static AuditLog? _audit;
    private static GuardianWorkboardService? _service;
    private static System.Windows.Forms.Timer? _timer;
    private static bool _tickRunning;

    public static void Initialize(AppConfig config, GitService git, AuditLog audit)
    {
        if (_timer is not null) return;

        _config = config;
        _git = git;
        _audit = audit;
        _service = new GuardianWorkboardService(git);

        _timer = new System.Windows.Forms.Timer { Interval = 1800 };
        _timer.Tick += async (_, _) => await TickAsync();
        _timer.Start();

        Application.ApplicationExit += (_, _) =>
        {
            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;
            Boards.Clear();
        };
    }

    private static async Task TickAsync()
    {
        if (_tickRunning || _config is null || _git is null || _service is null) return;

        _tickRunning = true;
        try
        {
            var guardians = Application.OpenForms
                .OfType<GuardianForm>()
                .Where(form => !form.IsDisposed)
                .ToArray();

            RemoveClosedBoards(guardians);
            foreach (var guardian in guardians)
            {
                EnsureWorkboard(guardian);
                ApplyLogicalProjectIdentity(guardian);
            }
            if (guardians.Length == 0) return;

            var activeBoards = guardians
                .Where(guardian => Boards.ContainsKey(guardian))
                .Select(guardian => Boards[guardian])
                .ToArray();
            if (activeBoards.Length == 0) return;

            var anyOperation = guardians.Any(IsOperationRunning);
            if (anyOperation)
            {
                foreach (var host in activeBoards) host.Board.SetBusy(true);
                return;
            }

            using var refresh = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            var snapshot = await _service.BuildAsync(
                _config,
                GuardianSyncState.Current,
                refresh.Token);

            var fingerprint = BuildFingerprint(snapshot);
            foreach (var host in activeBoards)
            {
                host.Board.ApplySnapshot(snapshot);

                if (!string.Equals(host.LastFingerprint, fingerprint, StringComparison.Ordinal))
                {
                    host.LastFingerprint = fingerprint;
                    if (host.Guardian.Visible)
                        _ = host.Guardian.RefreshAsync();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Workboard projection is advisory and will retry on the next tick.
        }
        catch (Exception ex)
        {
            if (_audit is not null)
            {
                try
                {
                    await _audit.WriteAsync("guardian_workboard_refresh_error", new { error = ex.Message });
                }
                catch
                {
                }
            }
        }
        finally
        {
            _tickRunning = false;
        }
    }

    private static void EnsureWorkboard(GuardianForm guardian)
    {
        if (Boards.ContainsKey(guardian)) return;

        var grid = EnumerateControls(guardian)
            .OfType<DataGridView>()
            .FirstOrDefault(IsWorkingTreeGrid);
        if (grid?.Parent is not Control originalHost) return;

        var legacyEmpty = originalHost.Controls
            .OfType<Label>()
            .FirstOrDefault(label =>
                label.Text.Contains("ALL CLEAR", StringComparison.OrdinalIgnoreCase) ||
                label.Text.Contains("READY WHEN YOU ARE", StringComparison.OrdinalIgnoreCase));

        if (legacyEmpty is not null)
        {
            originalHost.Controls.Remove(legacyEmpty);
            legacyEmpty.Visible = false;
        }

        originalHost.Controls.Remove(grid);
        var board = new GuardianWorkboardControl(grid)
        {
            Dock = DockStyle.Fill
        };
        originalHost.Controls.Add(board);
        board.BringToFront();

        var host = new BoardHost(guardian, board, legacyEmpty);
        Boards.Add(guardian, host);
        guardian.Disposed += (_, _) => Boards.Remove(guardian);
    }

    /* ==========================================================================
       PATCH: LOGICAL PROJECT HEADER
       DATE.TIME: 2026-09-11 14:12 +03:00
       Show GitPet project name instead of shared repository folder.
       ========================================================================== */
    private static void ApplyLogicalProjectIdentity(GuardianForm guardian)
    {
        if (_config is null) return;
        var active = _config.GetActiveProject();
        var displayName = active?.DisplayName;
        if (string.IsNullOrWhiteSpace(displayName)) return;

        var title = EnumerateControls(guardian)
            .OfType<Label>()
            .FirstOrDefault(label =>
                label.Text.StartsWith("ZOMNIVERSE GITPET  /", StringComparison.OrdinalIgnoreCase));
        if (title is null) return;

        var expected = $"ZOMNIVERSE GITPET  /  {displayName.ToUpperInvariant()}";
        if (!string.Equals(title.Text, expected, StringComparison.Ordinal)) title.Text = expected;
    }

    private static bool IsWorkingTreeGrid(DataGridView grid)
    {
        if (grid.Columns.Count < 2) return false;
        var first = grid.Columns[0].HeaderText;
        var second = grid.Columns[1].HeaderText;
        return first.Equals("STATE", StringComparison.OrdinalIgnoreCase) &&
               second.Equals("PATH", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOperationRunning(GuardianForm guardian) =>
        EnumerateControls(guardian)
            .OfType<GuardianActionButton>()
            .Any(button =>
                button.Visible &&
                button.Text.Equals("Cancel", StringComparison.OrdinalIgnoreCase));

    private static void RemoveClosedBoards(IReadOnlyCollection<GuardianForm> guardians)
    {
        var live = guardians.ToHashSet();
        foreach (var guardian in Boards.Keys.Where(form => !live.Contains(form) || form.IsDisposed).ToArray())
            Boards.Remove(guardian);
    }

    private static string BuildFingerprint(GuardianWorkboardSnapshot snapshot)
    {
        static string Rows(IEnumerable<GuardianWorkboardRow> rows) =>
            string.Join('|', rows.Select(row => $"{row.State}:{row.Path}"));

        return string.Join("§",
            snapshot.HasRepository,
            snapshot.Branch,
            Rows(snapshot.SaveRows),
            Rows(snapshot.GetRows),
            Rows(snapshot.SendRows),
            Rows(snapshot.ReconcileRows),
            snapshot.SendCommitCount,
            snapshot.SendFileCount,
            snapshot.Diverged,
            snapshot.ReconciliationPending);
    }

    private static IEnumerable<Control> EnumerateControls(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var descendant in EnumerateControls(child)) yield return descendant;
        }
    }

    private sealed class BoardHost(
        GuardianForm guardian,
        GuardianWorkboardControl board,
        Label? legacyEmpty)
    {
        public GuardianForm Guardian { get; } = guardian;
        public GuardianWorkboardControl Board { get; } = board;
        public Label? LegacyEmpty { get; } = legacyEmpty;
        public string LastFingerprint { get; set; } = "";
    }
}
