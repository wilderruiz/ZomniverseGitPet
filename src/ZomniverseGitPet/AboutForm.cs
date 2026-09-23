using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Reflection;

namespace ZomniverseGitPet;

internal sealed class AboutForm : Form
{
    private static readonly Color Surface = GuardianTheme.Window;
    private static readonly Color PanelSurface = GuardianTheme.Surface;
    private static readonly Color CardSurface = GuardianTheme.SurfaceRaised;
    private static readonly Color Ink = GuardianTheme.Ink;
    private static readonly Color MutedInk = GuardianTheme.MutedInk;
    private static readonly Color Purple = GuardianTheme.VioletPressed;
    private static readonly Color HotPink = GuardianTheme.Violet;

    private const string RepositoryUrl = "https://github.com/wilderruiz/ZomniverseGitPet";
    private const string LicenseUrl = "https://github.com/wilderruiz/ZomniverseGitPet/blob/main/LICENSE";
    private const string GitHubProfileUrl = "https://github.com/wilderruiz";
    private const string ZomniverseUrl = "https://zomniverse.codbiohub.com/";
    private const string WildVerseUrl = "https://wildverse.codbiohub.com/";
    private const string CodBioHubUrl = "https://home.codbiohub.com/";

    public AboutForm()
    {
        Text = "About ZomniverseGitPet";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        ShowInTaskbar = false;
        Size = new Size(920, 790);
        MinimumSize = new Size(800, 700);
        BackColor = Surface;
        ForeColor = Ink;
        Font = new Font("Segoe UI", 9);
        AutoScroll = true;
        WindowChrome.ApplyGuardianChrome(this);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Surface
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 128));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 224));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildProfile(), 0, 1);
        root.Controls.Add(BuildProjects(), 0, 2);
        root.Controls.Add(BuildDetails(), 0, 3);
        root.Controls.Add(BuildFooter(), 0, 4);
        Controls.Add(root);
    }

    private Control BuildHeader()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = PanelSurface,
            Padding = new Padding(30, 18, 30, 16)
        };

        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 48,
            Text = "◇  ZOMNIVERSE GITPET",
            Font = new Font("Segoe UI", 17, FontStyle.Bold),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var subtitle = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Your purple desktop Git guardian\r\nBuilt by Wilder Ruiz · Computational Biology | AI Evaluation | Scientific Software",
            Font = new Font("Segoe UI", 10.2f),
            ForeColor = GuardianTheme.SoftInk,
            TextAlign = ContentAlignment.TopLeft,
            Padding = new Padding(2, 2, 0, 0)
        };

        panel.Controls.Add(subtitle);
        panel.Controls.Add(title);
        return panel;
    }

    private Control BuildProfile()
    {
        var outer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Padding = new Padding(30, 12, 30, 8)
        };

        var card = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(20, 12, 20, 10),
            BackColor = CardSurface,
            Margin = Padding.Empty
        };
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));

        card.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "BUILT BY WILDER RUIZ",
            ForeColor = GuardianTheme.VioletHover,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        card.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Computational biology researcher and scientific-software builder working across AI evaluation, transcriptomics, local-first tools, deterministic workflows, and human-controlled AI systems.",
            ForeColor = GuardianTheme.SoftInk,
            Font = new Font("Segoe UI", 9.5f),
            TextAlign = ContentAlignment.TopLeft,
            AutoEllipsis = false
        }, 0, 1);

        card.Controls.Add(BuildLink("github.com/wilderruiz", GitHubProfileUrl), 0, 2);

        outer.Controls.Add(card);
        return outer;
    }

    private Control BuildProjects()
    {
        var outer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Padding = new Padding(30, 8, 30, 8)
        };

        var card = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 6,
            Padding = new Padding(20, 12, 20, 12),
            BackColor = CardSurface,
            Margin = Padding.Empty
        };
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        for (var i = 1; i < 6; i++)
            card.RowStyles.Add(new RowStyle(SizeType.Percent, 20));

        card.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "SELECTED PROJECTS",
            ForeColor = GuardianTheme.VioletHover,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        card.SetColumnSpan(card.GetControlFromPosition(0, 0)!, 2);

        AddProject(card, 1, "ZomniverseGitPet", RepositoryUrl,
            "Windows Git guardian for safer review, local checkpoints, explicit Get/Send, repository health, and project management.");
        AddProject(card, 2, "Zomniverse", ZomniverseUrl,
            "Scientific data and AI research platform for reproducible computational-biology workflows and analysis tooling.");
        AddProject(card, 3, "ZAC", null,
            "Local-first controlled AI review/apply tooling designed around explicit human approval and auditable changes.");
        AddProject(card, 4, "WildVerse", WildVerseUrl,
            "Creator-audio platform combining original music, browser tools, searchable media, and experimental creative software.");
        AddProject(card, 5, "CodBio Hub", CodBioHubUrl,
            "Home for computational biology, scientific software, AI experiments, and related independent projects.");

        outer.Controls.Add(card);
        return outer;
    }

    private Control BuildDetails()
    {
        var outer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Padding = new Padding(30, 8, 30, 14)
        };

        var card = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 7,
            Padding = new Padding(20, 12, 20, 12),
            BackColor = CardSurface
        };
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < 7; i++)
            card.RowStyles.Add(new RowStyle(SizeType.Percent, 14.2857f));

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
            Padding = new Padding(14, 18, 20, 12),
            BackColor = PanelSurface
        };

        var close = new Button
        {
            Text = "Close",
            Width = 112,
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
        close.FlatAppearance.MouseOverBackColor = GuardianTheme.Violet;
        close.FlatAppearance.MouseDownBackColor = GuardianTheme.VioletPressed;
        close.Click += (_, _) => Close();
        footer.Controls.Add(close);
        AcceptButton = close;
        CancelButton = close;
        return footer;
    }

    private static void AddProject(
        TableLayoutPanel card,
        int row,
        string name,
        string? url,
        string description)
    {
        Control nameControl = string.IsNullOrWhiteSpace(url)
            ? new Label
            {
                Text = name,
                Dock = DockStyle.Fill,
                ForeColor = GuardianTheme.Healthy,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9.2f, FontStyle.Bold)
            }
            : BuildLink(name, url);

        card.Controls.Add(nameControl, 0, row);
        card.Controls.Add(new Label
        {
            Text = description,
            Dock = DockStyle.Fill,
            ForeColor = GuardianTheme.SoftInk,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            Font = new Font("Segoe UI", 9f)
        }, 1, row);
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
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            LinkColor = GuardianTheme.VioletHover,
            ActiveLinkColor = Color.White,
            VisitedLinkColor = GuardianTheme.VioletHover,
            Cursor = Cursors.Hand
        };
        link.LinkClicked += (_, _) => OpenUrl(url);
        return link;
    }

    private static string GetVersion()
    {
        var assembly = typeof(AboutForm).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion?
            .Trim();

        if (!string.IsNullOrWhiteSpace(informational))
        {
            var metadata = informational.IndexOf('+');
            return metadata >= 0
                ? informational[..metadata]
                : informational;
        }

        var product = Application.ProductVersion?.Trim();
        if (!string.IsNullOrWhiteSpace(product))
        {
            var metadata = product.IndexOf('+');
            return metadata >= 0
                ? product[..metadata]
                : product;
        }

        var version = assembly.GetName().Version;
        return version is null
            ? "Unknown"
            : $"{version.Major}.{version.Minor}.{version.Build}";
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
            MessageBox.Show(
                "Unable to open the web browser.",
                "ZomniverseGitPet",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }
}
