using System.Drawing.Drawing2D;

namespace ZomniverseGitPet;

internal static class GuardianTheme
{
    // CodBio Hub workspace palette — shared desktop visual language.
    public static readonly Color Window = Color.FromArgb(8, 11, 14);          // #080B0E
    public static readonly Color Surface = Color.FromArgb(14, 19, 24);       // #0E1318
    public static readonly Color SurfaceRaised = Color.FromArgb(18, 24, 32); // #121820
    public static readonly Color SurfaceSoft = Color.FromArgb(17, 23, 29);   // #11171D
    public static readonly Color Border = Color.FromArgb(53, 65, 77);        // #35414D
    public static readonly Color BorderSoft = Color.FromArgb(38, 48, 58);    // #26303A

    public static readonly Color Ink = Color.FromArgb(231, 236, 233);         // #E7ECE9
    public static readonly Color SoftInk = Color.FromArgb(189, 198, 194);    // #BDC6C2
    public static readonly Color MutedInk = Color.FromArgb(127, 138, 149);   // #7F8A95
    public static readonly Color FaintInk = Color.FromArgb(86, 98, 109);     // #56626D

    // Guardian identity. Violet is deliberately reserved for brand/pet identity,
    // not used as a generic surface colour.
    public static readonly Color Violet = Color.FromArgb(170, 150, 255);     // #AA96FF
    public static readonly Color VioletHover = Color.FromArgb(190, 175, 255);
    public static readonly Color VioletPressed = Color.FromArgb(139, 120, 220);

    // Semantic workboard/action colours.
    public static readonly Color Save = Color.FromArgb(239, 137, 158);       // #EF899E
    public static readonly Color Get = Color.FromArgb(114, 200, 255);        // #72C8FF
    public static readonly Color Send = Color.FromArgb(117, 226, 189);       // #75E2BD
    public static readonly Color Reconcile = Color.FromArgb(228, 164, 108);  // #E4A46C

    // Back-compat aliases used throughout the existing UI.
    public static readonly Color HotPink = Save;
    public static readonly Color HotPinkSoft = Save;
    public static readonly Color Healthy = Send;
    public static readonly Color HealthyFill = Color.FromArgb(13, 30, 27);
    public static readonly Color Changes = Reconcile;
    public static readonly Color ChangesFill = Color.FromArgb(31, 24, 18);
    public static readonly Color Warning = Color.FromArgb(255, 110, 127);
    public static readonly Color WarningFill = Color.FromArgb(36, 18, 22);
    public static readonly Color Info = Get;
    public static readonly Color InfoFill = Color.FromArgb(13, 24, 33);

    public static readonly Color Tooltip = Color.FromArgb(14, 19, 24);
    public static readonly Color Console = Color.FromArgb(8, 12, 16);
    public static readonly Color ConsoleHeader = Color.FromArgb(14, 19, 24);

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

    public static ToolStripRenderer CreateMenuRenderer() => new GuardianMenuRenderer();

    private sealed class GuardianMenuRenderer : ToolStripProfessionalRenderer
    {
        public GuardianMenuRenderer() : base(new GuardianColorTable())
        {
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            // WinForms can fall back to system-black text for dynamically added dropdown
            // items even when the drop-down itself is using GitPet's dark renderer.
            // Keep top-level menu colours untouched (for example Major update? ✦), but
            // make every drop-down command readable against the purple surface.
            if (e.Item.Owner is ToolStripDropDown)
                e.TextColor = e.Item.Enabled ? Ink : FaintInk;

            base.OnRenderItemText(e);
        }
    }

    private sealed class GuardianColorTable : ProfessionalColorTable
    {
        public GuardianColorTable() => UseSystemColors = false;

        /* ==========================================================================
           PATCH: THEMED DROPDOWN MENU BORDER
           FUNCTION:
           Replaces the standard Windows dropdown outline with GitPet's subtle
           purple border and existing selection styling.

           DATE.TIME ADDED: 2026-09-11 12:50 +03:00

           REASON:
           Remove the bright system border from GitPet dropdown menus.
           ========================================================================== */
        public override Color ToolStripDropDownBackground => SurfaceRaised;
        public override Color MenuBorder => Border;
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
