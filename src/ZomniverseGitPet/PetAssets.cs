using System.Reflection;

namespace ZomniverseGitPet;

internal sealed class PetAssets : IDisposable
{
    private readonly Dictionary<string, Image> _saveImages = new();

    internal static string? SaveAssetName(SaveOperationPhase phase, bool alternate) => phase switch
    {
        SaveOperationPhase.Preparing => alternate ? "pet_save_prepare_01.png" : "pet_thinking_01.png",
        SaveOperationPhase.CheckingPathSupport => "pet_thinking_01.png",
        SaveOperationPhase.Staging => alternate ? "pet_save_sorting_02.png" : "pet_save_sorting_01.png",
        SaveOperationPhase.CreatingCheckpoint => alternate ? "pet_save_packing_02.png" : "pet_save_packing_01.png",
        SaveOperationPhase.Completed => "pet_save_success_01.png",
        SaveOperationPhase.Warning => "pet_save_warning_01.png",
        SaveOperationPhase.Failed => "pet_save_error_01.png",
        _ => null
    };

    public PetAssets()
    {
        foreach (var name in Enum.GetValues<SaveOperationPhase>()
                     .SelectMany(phase => new[] { SaveAssetName(phase, false), SaveAssetName(phase, true) })
                     .OfType<string>().Distinct())
        {
            // Optional dedicated artwork must never prevent the pet from opening.
            try { _saveImages[name] = Load(name); }
            catch (InvalidOperationException) { }
            catch (ArgumentException) { }
        }
    }

    public Image ForOperation(SaveOperationVisualState state, bool alternate, Image idleImage)
    {
        if (state.Operation == GuardianOperationKind.Save && SaveAssetName(state.Phase, alternate) is { } name &&
            _saveImages.TryGetValue(name, out var dedicated)) return dedicated;
        return state.Phase switch
        {
            SaveOperationPhase.Preparing or SaveOperationPhase.CheckingPathSupport => alternate ? ReviewReady : Idle,
            SaveOperationPhase.Staging or SaveOperationPhase.CreatingCheckpoint => alternate ? ReviewReady : Happy,
            SaveOperationPhase.Completed => Happy,
            SaveOperationPhase.Warning or SaveOperationPhase.Failed => Warning,
            SaveOperationPhase.Cancelled => Idle,
            _ => idleImage
        };
    }
    public Image Idle { get; } = Load("pet_idle_01.png");
    public Image Happy { get; } = Load("pet_happy_01.png");
    public Image ReviewReady { get; } = Load("pet_review_ready_01.png");
    public Image Warning { get; } = Load("pet_warning_01.png");

    private static Image Load(string fileName)
    {
        var resourceName = $"ZomniverseGitPet.Assets.Pet.{fileName}";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded pet asset was not found: {resourceName}");
        using var source = Image.FromStream(stream);
        return new Bitmap(source);
    }

    public void Dispose()
    {
        foreach (var image in _saveImages.Values) image.Dispose();
        Idle.Dispose();
        Happy.Dispose();
        ReviewReady.Dispose();
        Warning.Dispose();
    }
}
