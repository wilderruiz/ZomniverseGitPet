using System.Reflection;

namespace ZomniverseGitPet;

internal sealed class PetAssets : IDisposable
{
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
        Idle.Dispose();
        Happy.Dispose();
        ReviewReady.Dispose();
        Warning.Dispose();
    }
}
