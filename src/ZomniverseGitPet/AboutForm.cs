using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ZomniverseGitPet;

internal sealed class AboutForm : Form
{
    private static readonly Color Surface = Color.FromArgb(27, 20, 40);
    private static readonly Color PanelSurface = Color.FromArgb(45, 31, 66);
    private static readonly Color CardSurface = Color.FromArgb(35, 27, 51);
    private static readonly Color Ink = Color.FromArgb(242, 237, 249);
    private static readonly Color MutedInk = Color.FromArgb(187, 176, 205);
    private static readonly Color Purple = Color.FromArgb(112, 70, 180);
    private static readonly Color HotPink = Color.FromArgb(236, 70, 170);
    private const string RepositoryUrl = "https://github.com/wilderruiz/ZomniverseGitPet";
    private const string LicenseUrl = "https://github.com/wilderruiz/ZomniverseGitPet/blob/main/LICENSE";

    public AboutForm()
    {
        Text = "About ZomniverseGitPet";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(650, 540);
        BackColor = Surface;
        ForeColor = Ink;
        Font = new Font("Segoe UI", 9);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Surface
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildIntro(), 0, 1);
        root.Controls.Add(BuildDetails(), 0, 2);
        root.Controls.Add(BuildFooter(), 0, 3);
        Controls.Add(root);
    }

    private Control BuildHeader()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = PanelSurface,
            Padding = new Padding(28, 16, 28, 14)
        };

        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 42,
            Text = "◇  ZOMNIVERSE GITPET",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var subtitle = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Your purple desktop Git guardian",
            Font = new Font("Segoe UI", 10),
            ForeColor = Color.FromArgb(205, 192, 224),
            TextAlign = ContentAlignment.MiddleLeft
        };

        panel.Controls.Add(subtitle);
        panel.Controls.Add(title);
        return panel;
    }

    private Control BuildIntro()
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(30, 18, 30, 8),
            Text = "ZomniverseGitPet is a lightweight Windows Git guardian designed to make repository safety visible, understandable, and friendly for both developers and non-programmers.",
            Font = new Font("Segoe UI", 10),
            ForeColor = Color.FromArgb(224, 216, 237),
            TextAlign = ContentAlignment.TopLeft
        };
    }

    private Control BuildDetails()
    {
        var outer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Padding = new Padding(30, 4, 30, 16)
        };

        var card = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 7,
            Padding = new Padding(20, 16, 20, 14),
            BackColor = CardSurface
        };
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < 7; i++) card.RowStyles.Add(new RowStyle(SizeType.Percent, 14.2857f));

        AddDetail(card, 0, "VERSION", GetVersion());
        AddDetail(card, 1, "BUILDER", "Wilder Ruiz");
        AddDetail(card, 2, "BUILD DATE", GetBuildDate());
        AddDetail(card, 3, "PLATFORM", $"Windows · .NET {Environment.Version.Major} · {RuntimeInformation.ProcessArchitecture}");
        AddDetail(card, 4, "LICENSE", BuildLink("MIT License", LicenseUrl));
        AddDetail(card, 5, "REPOSITORY", BuildLink("wilderruiz/ZomniverseGitPet", RepositoryUrl));
        AddDetail(card, 6, "COPYRIGHT", "© 2026 Wilder Ruiz");

        outer.Controls.Add(card);
        return outer;
    }

    private Control BuildFooter()
    {
        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(14, 16, 20, 12),
            BackColor = PanelSurface
        };

        var close = new Button
        {
            Text = "Close",
            Width = 100,
            Height = 38,
            Margin = new Padding(8, 0, 0, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = Purple,
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            UseVisualStyleBackColor = false
        };
        close.FlatAppearance.BorderSize = 2;
        close.FlatAppearance.BorderColor = HotPink;
        close.FlatAppearance.MouseOverBackColor = Color.FromArgb(132, 79, 198);
        close.Click += (_, _) => Close();
        footer.Controls.Add(close);
        AcceptButton = close;
        CancelButton = close;
        return footer;
    }

    private static void AddDetail(TableLayoutPanel card, int row, string label, string value)
    {
        card.Controls.Add(MakeLabel(label), 0, row);
        card.Controls.Add(new Label
        {
            Text = value,
            Dock = DockStyle.Fill,
            ForeColor = Ink,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            Font = new Font("Segoe UI", 9.5f)
        }, 1, row);
    }

    private static void AddDetail(TableLayoutPanel card, int row, string label, Control control)
    {
        card.Controls.Add(MakeLabel(label), 0, row);
        card.Controls.Add(control, 1, row);
    }

    private static Label MakeLabel(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        ForeColor = MutedInk,
        TextAlign = ContentAlignment.MiddleLeft,
        Font = new Font("Segoe UI", 8, FontStyle.Bold)
    };

    private static LinkLabel BuildLink(string text, string url)
    {
        var link = new LinkLabel
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            Font = new Font("Segoe UI", 9.5f),
            LinkColor = Color.FromArgb(215, 173, 255),
            ActiveLinkColor = HotPink,
            VisitedLinkColor = Color.FromArgb(215, 173, 255),
            Cursor = Cursors.Hand
        };
        link.LinkClicked += (_, _) => OpenUrl(url);
        return link;
    }

    private static string GetVersion()
    {
        var version = typeof(AboutForm).Assembly.GetName().Version;
        return version is null ? Application.ProductVersion : $"{version.Major}.{version.Minor}.{version.Build}";
    }

    private static string GetBuildDate()
    {
        try
        {
            var path = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                return File.GetLastWriteTime(path).ToString("yyyy-MM-dd HH:mm");
        }
        catch { }
        return "Unavailable";
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            MessageBox.Show("Unable to open the web browser.", "ZomniverseGitPet", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
