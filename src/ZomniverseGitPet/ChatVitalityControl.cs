namespace ZomniverseGitPet;

internal sealed class ChatVitalityControl : Control
{
    private ChatVitalitySnapshot? _snapshot;
    private readonly Button _newChatButton;

    public event EventHandler? NewChatRequested;
    public event EventHandler? HistoryRequested;

    public ChatVitalityControl()
    {
        DoubleBuffered = true;
        Cursor = Cursors.Hand;
        BackColor = GuardianTheme.SurfaceRaised;
        AccessibleName = "Chat Vitality";
        _newChatButton = new Button
        {
            Text = "+ NEW CHAT",
            FlatStyle = FlatStyle.Flat,
            BackColor = GuardianTheme.SurfaceSoft,
            ForeColor = GuardianTheme.Violet,
            Font = new Font("Cascadia Mono", 7f, FontStyle.Bold),
            Size = new Size(116, 30),
            TabStop = false,
            Cursor = Cursors.Hand
        };
        _newChatButton.FlatAppearance.BorderColor = GuardianTheme.Border;
        _newChatButton.Click += (_, _) => NewChatRequested?.Invoke(this, EventArgs.Empty);
        Controls.Add(_newChatButton);
        Click += (_, _) => HistoryRequested?.Invoke(this, EventArgs.Empty);
        Resize += (_, _) => PositionButton();
    }

    public void ShowSnapshot(ChatVitalitySnapshot? snapshot)
    {
        _snapshot = snapshot;
        Invalidate();
    }

    private void PositionButton() =>
        _newChatButton.Location = new Point(Math.Max(4, Width - _newChatButton.Width - 10), 8);

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var border = new Pen(GuardianTheme.BorderSoft);
        e.Graphics.DrawRectangle(border, 0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));

        using var titleFont = new Font("Cascadia Mono", 7.5f, FontStyle.Bold);
        using var mainFont = new Font("Cascadia Mono", 8.5f, FontStyle.Bold);
        using var smallFont = new Font("Cascadia Mono", 7f);
        TextRenderer.DrawText(e.Graphics, "CHAT VITALITY", titleFont, new Point(12, 12), GuardianTheme.MutedInk);

        if (_snapshot is null)
        {
            TextRenderer.DrawText(e.Graphics, "NO ACTIVE GENERATION", mainFont, new Point(12, 48), GuardianTheme.FaintInk);
            TextRenderer.DrawText(e.Graphics, "Start explicitly; nothing is inferred", smallFont, new Point(12, 74), GuardianTheme.MutedInk);
            return;
        }

        var snapshot = _snapshot;
        var session = snapshot.Session;
        var stateColor = snapshot.LifecycleState == "NEWBORN" ? GuardianTheme.Violet :
            session.RiskScore >= 70 ? GuardianTheme.Warning : GuardianTheme.Healthy;

        if (snapshot.LifecycleState == "NEWBORN")
        {
            using var cord = new Pen(GuardianTheme.Violet, 1.6f);
            using var birthBrush = new SolidBrush(GuardianTheme.Violet);
            using var anchorBrush = new SolidBrush(GuardianTheme.Get);
            e.Graphics.DrawLine(cord, 14, 52, 29, 52);
            e.Graphics.FillEllipse(birthBrush, 10, 48, 8, 8);
            e.Graphics.FillEllipse(anchorBrush, 26, 48, 8, 8);
        }

        var left = snapshot.LifecycleState == "NEWBORN" ? 42 : 12;
        TextRenderer.DrawText(e.Graphics, $"CHAT {session.Generation:00}  {snapshot.LifecycleState}", mainFont,
            new Point(left, 44), stateColor);
        var anchor = snapshot.CurrentAnchorCommit.Length > 8
            ? snapshot.CurrentAnchorCommit[..8]
            : snapshot.CurrentAnchorCommit;
        TextRenderer.DrawText(e.Graphics,
            $"SESSION LOAD: {snapshot.RiskClass}  {session.RiskScore}/100",
            smallFont, new Point(12, 68), GuardianTheme.SoftInk);
        TextRenderer.DrawText(e.Graphics,
            $"{snapshot.InteractionCount} interactions · {session.Saves} checkpoints · {session.Gets} receives · {anchor}",
            smallFont, new Point(12, 88), GuardianTheme.MutedInk);
        if (!string.IsNullOrWhiteSpace(snapshot.Recommendation))
            TextRenderer.DrawText(e.Graphics, snapshot.Recommendation, smallFont,
                new Point(12, 108), GuardianTheme.Warning);
    }
}
