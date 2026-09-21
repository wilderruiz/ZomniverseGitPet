// Promoted from Lynx Lab after Direct2D Migration 5 validation.
// Production copy is intentionally independent from experiments/.
namespace ZomniverseGitPet;

internal enum LynxVisualState
{
    Idle,
    Clean,
    Changes,
    Attention,
    Save,
    Get,
    Send,
    Conflict
}

internal enum LynxActivityState
{
    None,
    Thinking,
    Preparing,
    Sorting,
    Packing,
    Incoming,
    Outgoing,
    Reconciling,
    Success,
    Warning,
    Failure,
    Resting
}

internal sealed record LynxPalette(
    string Name,
    Color Fur,
    Color Ear,
    Color Edge,
    Color Eye,
    Color Accent,
    Color Muzzle,
    Color Detail)
{
    public static IReadOnlyList<LynxPalette> All { get; } =
    [
        new("Cyan / Teal",
            Color.FromArgb(0x24, 0x33, 0x3D),
            Color.FromArgb(0x10, 0x18, 0x1F),
            Color.FromArgb(0x56, 0x72, 0x87),
            Color.FromArgb(0x78, 0xD8, 0xDF),
            Color.FromArgb(0x57, 0xD7, 0xA0),
            Color.FromArgb(0x10, 0x1A, 0x22),
            Color.FromArgb(0x5E, 0x7C, 0x91)),

        new("Midnight Blue",
            Color.FromArgb(0x23, 0x30, 0x3F),
            Color.FromArgb(0x10, 0x17, 0x22),
            Color.FromArgb(0x50, 0x6D, 0x8B),
            Color.FromArgb(0x78, 0xA9, 0xFF),
            Color.FromArgb(0x4F, 0x8F, 0xE8),
            Color.FromArgb(0x0F, 0x17, 0x21),
            Color.FromArgb(0x5C, 0x7C, 0xA0)),

        new("Forest Emerald",
            Color.FromArgb(0x22, 0x33, 0x2F),
            Color.FromArgb(0x10, 0x1A, 0x18),
            Color.FromArgb(0x4E, 0x76, 0x6B),
            Color.FromArgb(0x79, 0xD8, 0xCA),
            Color.FromArgb(0x43, 0xB8, 0x8F),
            Color.FromArgb(0x10, 0x1B, 0x18),
            Color.FromArgb(0x5B, 0x86, 0x7B)),

        new("Ember Copper",
            Color.FromArgb(0x33, 0x2C, 0x2A),
            Color.FromArgb(0x1B, 0x15, 0x14),
            Color.FromArgb(0x7B, 0x5B, 0x4D),
            Color.FromArgb(0xF0, 0xB3, 0x6B),
            Color.FromArgb(0xD9, 0x83, 0x53),
            Color.FromArgb(0x1B, 0x16, 0x15),
            Color.FromArgb(0x8A, 0x67, 0x56)),

        new("Deep Violet",
            Color.FromArgb(0x2D, 0x2A, 0x3A),
            Color.FromArgb(0x15, 0x13, 0x1E),
            Color.FromArgb(0x68, 0x5D, 0x86),
            Color.FromArgb(0xB7, 0xA0, 0xFF),
            Color.FromArgb(0x8C, 0x73, 0xE8),
            Color.FromArgb(0x17, 0x14, 0x20),
            Color.FromArgb(0x75, 0x69, 0x9A)),

        new("Crimson Night",
            Color.FromArgb(0x32, 0x26, 0x29),
            Color.FromArgb(0x1B, 0x11, 0x14),
            Color.FromArgb(0x7A, 0x4E, 0x58),
            Color.FromArgb(0xF2, 0x8B, 0x9A),
            Color.FromArgb(0xD9, 0x5C, 0x70),
            Color.FromArgb(0x1A, 0x12, 0x15),
            Color.FromArgb(0x87, 0x58, 0x67)),

        new("Graphite Gold",
            Color.FromArgb(0x32, 0x30, 0x28),
            Color.FromArgb(0x1B, 0x19, 0x12),
            Color.FromArgb(0x7D, 0x73, 0x50),
            Color.FromArgb(0xF0, 0xD1, 0x78),
            Color.FromArgb(0xD5, 0xA8, 0x4B),
            Color.FromArgb(0x1B, 0x19, 0x13),
            Color.FromArgb(0x8C, 0x80, 0x58)),

        new("Arctic Ice",
            Color.FromArgb(0x29, 0x35, 0x3B),
            Color.FromArgb(0x13, 0x1B, 0x1E),
            Color.FromArgb(0x65, 0x81, 0x8D),
            Color.FromArgb(0xB5, 0xED, 0xF3),
            Color.FromArgb(0x7B, 0xCB, 0xD6),
            Color.FromArgb(0x12, 0x1B, 0x1F),
            Color.FromArgb(0x6F, 0x91, 0x9E))
    ];

    public static LynxPalette Default { get; } =
        All.First(palette => string.Equals(
            palette.Name,
            "Deep Violet",
            StringComparison.Ordinal));

    public static LynxPalette ActivityPartner(LynxActivityState activity)
    {
        var name = activity switch
        {
            LynxActivityState.Thinking => "Cyan / Teal",
            LynxActivityState.Preparing => "Midnight Blue",
            LynxActivityState.Sorting => "Ember Copper",
            LynxActivityState.Packing => "Graphite Gold",
            LynxActivityState.Incoming => "Cyan / Teal",
            LynxActivityState.Outgoing => "Arctic Ice",
            LynxActivityState.Reconciling => "Ember Copper",
            LynxActivityState.Success => "Forest Emerald",
            LynxActivityState.Warning => "Graphite Gold",
            LynxActivityState.Failure => "Crimson Night",
            LynxActivityState.Resting => "Midnight Blue",
            _ => Default.Name
        };

        return All.First(palette => string.Equals(
            palette.Name,
            name,
            StringComparison.Ordinal));
    }

    public static float ActivityMix(
        LynxActivityState activity,
        double seconds)
    {
        if (activity == LynxActivityState.None)
            return 0f;

        var period = activity switch
        {
            LynxActivityState.Sorting => 2.4d,
            LynxActivityState.Packing => 2.8d,
            LynxActivityState.Incoming or
            LynxActivityState.Outgoing => 2.1d,
            LynxActivityState.Warning or
            LynxActivityState.Failure => 1.8d,
            LynxActivityState.Resting => 5.8d,
            _ => 3.4d
        };

        var wave = 0.5d + 0.5d * Math.Sin(
            seconds * Math.PI * 2d / period);

        // Keep a trace of the user's selected palette at all times.
        return (float)(0.18d + wave * 0.64d);
    }

    public static LynxPalette Blend(
        LynxPalette selected,
        LynxActivityState activity,
        double seconds)
    {
        if (activity == LynxActivityState.None)
            return selected;

        var partner = ActivityPartner(activity);
        var amount = ActivityMix(activity, seconds);

        return new LynxPalette(
            $"{selected.Name} ↔ {partner.Name}",
            MixColor(selected.Fur, partner.Fur, amount),
            MixColor(selected.Ear, partner.Ear, amount),
            MixColor(selected.Edge, partner.Edge, amount),
            MixColor(selected.Eye, partner.Eye, amount),
            MixColor(selected.Accent, partner.Accent, amount),
            MixColor(selected.Muzzle, partner.Muzzle, amount),
            MixColor(selected.Detail, partner.Detail, amount));
    }

    private static Color MixColor(Color a, Color b, float bWeight)
    {
        bWeight = Math.Clamp(bWeight, 0f, 1f);
        var aWeight = 1f - bWeight;

        return Color.FromArgb(
            (int)(a.A * aWeight + b.A * bWeight),
            (int)(a.R * aWeight + b.R * bWeight),
            (int)(a.G * aWeight + b.G * bWeight),
            (int)(a.B * aWeight + b.B * bWeight));
    }

    public override string ToString() => Name;
}
