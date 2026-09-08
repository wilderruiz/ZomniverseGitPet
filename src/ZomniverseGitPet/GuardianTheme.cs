using System.Drawing.Drawing2D;

namespace ZomniverseGitPet;

internal static class GuardianTheme
{
    public static readonly Color Window = Color.FromArgb(18, 15, 24);
    public static readonly Color Surface = Color.FromArgb(27, 22, 36);
    public static readonly Color SurfaceRaised = Color.FromArgb(35, 28, 47);
    public static readonly Color SurfaceSoft = Color.FromArgb(42, 33, 57);
    public static readonly Color Border = Color.FromArgb(77, 62, 101);
    public static readonly Color BorderSoft = Color.FromArgb(58, 47, 76);

    public static readonly Color Ink = Color.FromArgb(244, 240, 249);
    public static readonly Color MutedInk = Color.FromArgb(176, 166, 190);
    public static readonly Color FaintInk = Color.FromArgb(132, 122, 148);

    public static readonly Color Violet = Color.FromArgb(117, 72, 188);
    public static readonly Color VioletHover = Color.FromArgb(139, 88, 216);
    public static readonly Color VioletPressed = Color.FromArgb(89, 52, 151);
    public static readonly Color HotPink = Color.FromArgb(255, 50, 158);
    public static readonly Color HotPinkSoft = Color.FromArgb(246, 131, 193);

    public static readonly Color Healthy = Color.FromArgb(77, 208, 139);
    public static readonly Color HealthyFill = Color.FromArgb(24, 59, 45);
    public static readonly Color Changes = Color.FromArgb(255, 190, 83);
    public static readonly Color ChangesFill = Color.FromArgb(68, 49, 24);
    public static readonly Color Warning = Color.FromArgb(255, 111, 126);
    public static readonly Color WarningFill = Color.FromArgb(69, 28, 36);
    public static readonly Color Info = Color.FromArgb(133, 177, 255);
    public static readonly Color InfoFill = Color.FromArgb(27, 43, 69);

    public static readonly Color Tooltip = Color.FromArgb(24, 20, 31);
    public static readonly Color Console = Color.FromArgb(14, 12, 19);
    public static readonly Color ConsoleHeader = Color.FromArgb(24, 20, 31);

    public static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
    {
        var diameter = radius * 2f;
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static ToolStripRenderer CreateMenuRenderer() =>
        new ToolStripProfessionalRenderer(new GuardianColorTable());

    private sealed class GuardianColorTable : ProfessionalColorTable
    {
        public GuardianColorTable() => UseSystemColors = false;

        public override Color ToolStripDropDownBackground => SurfaceRaised;
        public override Color MenuItemSelected => SurfaceSoft;
        public override Color MenuItemBorder => Border;
        public override Color MenuItemSelectedGradientBegin => SurfaceSoft;
        public override Color MenuItemSelectedGradientEnd => SurfaceSoft;
        public override Color MenuItemPressedGradientBegin => SurfaceSoft;
        public override Color MenuItemPressedGradientEnd => SurfaceSoft;
        public override Color ImageMarginGradientBegin => SurfaceRaised;
        public override Color ImageMarginGradientMiddle => SurfaceRaised;
        public override Color ImageMarginGradientEnd => SurfaceRaised;
        public override Color SeparatorDark => Border;
        public override Color SeparatorLight => SurfaceRaised;
    }
}
