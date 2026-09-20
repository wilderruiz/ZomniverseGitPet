namespace LynxLab;

internal sealed record GuardianV3Layer(string Name, int Order);

internal static class GuardianV3Model
{
    public static IReadOnlyList<GuardianV3Layer> Layers { get; } =
    [
        new("Tail", 0),
        new("Body", 10),
        new("Legs", 20),
        new("Chest", 30),
        new("Head", 40),
        new("Ears", 50),
        new("Muzzle", 60),
        new("Eyes", 70),
        new("Collar", 80),
        new("Shield", 90)
    ];
}
