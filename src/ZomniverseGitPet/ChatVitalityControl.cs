namespace ZomniverseGitPet;

internal sealed class ChatVitalityControl : Control
{
    private const int HeaderHeight = 44;
    private readonly Button _newChatButton;
    private readonly Panel _scrollBody;
    private readonly ChatVitalityBody _body;

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

        _scrollBody = new Panel
        {
            AutoScroll = true,
            BackColor = GuardianTheme.SurfaceRaised,
            TabStop = false
        };
        _scrollBody.HandleCreated += (_, _) => GuardianTheme.ApplyDarkScrollbarTheme(_scrollBody);

        _body = new ChatVitalityBody
        {
            BackColor = GuardianTheme.SurfaceRaised,
            Cursor = Cursors.Hand
        };
        _body.Click += (_, _) => HistoryRequested?.Invoke(this, EventArgs.Empty);
        _scrollBody.Controls.Add(_body);
        Controls.Add(_scrollBody);
        Controls.Add(_newChatButton);

        Click += (_, _) => HistoryRequested?.Invoke(this, EventArgs.Empty);
        Resize += (_, _) => LayoutChildren();
    }

    public void ShowSnapshot(ChatVitalitySnapshot? snapshot)
    {
        _body.ShowSnapshot(snapshot);
        LayoutBody();
    }

    private void LayoutChildren()
    {
        _newChatButton.Location = new Point(Math.Max(4, Width - _newChatButton.Width - 10), 8);
        _scrollBody.SetBounds(1, HeaderHeight, Math.Max(0, Width - 2), Math.Max(0, Height - HeaderHeight - 1));
        LayoutBody();
    }

    private void LayoutBody()
    {
        var viewportWidth = Math.Max(1, _scrollBody.ClientSize.Width);
        var contentHeight = _body.GetPreferredContentHeight(viewportWidth);
        _body.SetBounds(0, 0, viewportWidth, Math.Max(_scrollBody.ClientSize.Height, contentHeight));
        _scrollBody.AutoScrollMinSize = new Size(0, contentHeight);
        _body.Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var border = new Pen(GuardianTheme.BorderSoft);
        using var titleFont = new Font("Cascadia Mono", 7.5f, FontStyle.Bold);
        e.Graphics.DrawRectangle(border, 0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
        TextRenderer.DrawText(e.Graphics, "CHAT VITALITY", titleFont,
            new Point(12, 12), GuardianTheme.MutedInk);
    }

    private sealed class ChatVitalityBody : Control
    {
        private const int ContentLeft = 12;
        private const int ContentTop = 4;
        private const int PhraseGap = 8;
        private const int LineGap = 3;
        private ChatVitalitySnapshot? _snapshot;

        public void ShowSnapshot(ChatVitalitySnapshot? snapshot)
        {
            _snapshot = snapshot;
            Invalidate();
        }

        public int GetPreferredContentHeight(int width)
        {
            using var mainFont = new Font("Cascadia Mono", 8.5f, FontStyle.Bold);
            using var smallFont = new Font("Cascadia Mono", 7f);
            if (_snapshot is null) return ContentTop + mainFont.Height + 34 + smallFont.Height + 12;

            var anchor = ShortAnchor(_snapshot.CurrentAnchorCommit);
            var metricsHeight = MeasureMetricRows(smallFont, width,
            [
                $"{_snapshot.Session.TotalTurns} turns",
                $"· {_snapshot.InteractionCount} remote gets",
                $"· {_snapshot.Session.Saves} local checkpoints",
                $"· {anchor}"
            ]);
            var recommendationHeight = string.IsNullOrWhiteSpace(_snapshot.Recommendation)
                ? 0
                : smallFont.Height + 5;
            return ContentTop + mainFont.Height + 8 + smallFont.Height + 8 + metricsHeight + recommendationHeight + 12;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var mainFont = new Font("Cascadia Mono", 8.5f, FontStyle.Bold);
            using var smallFont = new Font("Cascadia Mono", 7f);

            if (_snapshot is null)
            {
                TextRenderer.DrawText(e.Graphics, "NO ACTIVE GENERATION", mainFont,
                    new Point(ContentLeft, ContentTop), GuardianTheme.FaintInk);
                TextRenderer.DrawText(e.Graphics, "Awaiting agent birth or manual start", smallFont,
                    new Point(ContentLeft, ContentTop + 26), GuardianTheme.MutedInk);
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
                e.Graphics.DrawLine(cord, 14, ContentTop + 8, 29, ContentTop + 8);
                e.Graphics.FillEllipse(birthBrush, 10, ContentTop + 4, 8, 8);
                e.Graphics.FillEllipse(anchorBrush, 26, ContentTop + 4, 8, 8);
            }

            var headingLeft = snapshot.LifecycleState == "NEWBORN" ? 42 : ContentLeft;
            TextRenderer.DrawText(e.Graphics, $"CHAT {session.Generation:00}  {snapshot.LifecycleState}",
                mainFont, new Point(headingLeft, ContentTop), stateColor);

            var loadTop = ContentTop + mainFont.Height + 8;
            TextRenderer.DrawText(e.Graphics,
                $"SESSION LOAD: {snapshot.RiskClass}  {session.RiskScore}/100",
                smallFont, new Point(ContentLeft, loadTop), GuardianTheme.SoftInk);

            var metricsTop = loadTop + smallFont.Height + 8;
            var metricsBottom = DrawWrappedMetrics(e.Graphics, smallFont, ContentLeft, metricsTop,
            [
                $"{session.TotalTurns} turns",
                $"· {snapshot.InteractionCount} remote gets",
                $"· {session.Saves} local checkpoints",
                $"· {ShortAnchor(snapshot.CurrentAnchorCommit)}"
            ]);
            if (!string.IsNullOrWhiteSpace(snapshot.Recommendation))
                TextRenderer.DrawText(e.Graphics, snapshot.Recommendation, smallFont,
                    new Point(ContentLeft, metricsBottom + 5), GuardianTheme.Warning);
        }

        private int MeasureMetricRows(Font font, int width, IReadOnlyList<string> phrases)
        {
            var right = Math.Max(ContentLeft, width - 12);
            var x = ContentLeft;
            var rows = 1;
            foreach (var phrase in phrases)
            {
                var size = TextRenderer.MeasureText(phrase, font, Size.Empty,
                    TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                if (x > ContentLeft && x + size.Width > right)
                {
                    x = ContentLeft;
                    rows++;
                }
                x += size.Width + PhraseGap;
            }
            return rows * font.Height + (rows - 1) * LineGap;
        }

        private int DrawWrappedMetrics(Graphics graphics, Font font, int left, int top,
            IReadOnlyList<string> phrases)
        {
            var right = Math.Max(left, ClientSize.Width - 12);
            var x = left;
            var y = top;
            var lineHeight = font.Height + LineGap;
            foreach (var phrase in phrases)
            {
                var size = TextRenderer.MeasureText(graphics, phrase, font,
                    Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                if (x > left && x + size.Width > right)
                {
                    x = left;
                    y += lineHeight;
                }
                TextRenderer.DrawText(graphics, phrase, font, new Point(x, y),
                    GuardianTheme.MutedInk,
                    TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                x += size.Width + PhraseGap;
            }
            return y + font.Height;
        }

        private static string ShortAnchor(string value) => value.Length > 8 ? value[..8] : value;
    }
}
