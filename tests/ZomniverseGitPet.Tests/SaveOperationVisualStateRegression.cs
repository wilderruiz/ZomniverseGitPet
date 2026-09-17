using System.Runtime.CompilerServices;
using ZomniverseGitPet;

internal static class SaveOperationVisualStateRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        var controller = new SaveOperationStateController();
        var observed = new List<SaveOperationVisualState>();
        controller.Changed += (_, state) => observed.Add(state);

        controller.Transition(SaveOperationPhase.Preparing);
        controller.Transition(SaveOperationPhase.CheckingPathSupport);
        controller.Transition(SaveOperationPhase.Staging);
        controller.Transition(SaveOperationPhase.CreatingCheckpoint);
        controller.Transition(SaveOperationPhase.Completed);
        controller.Transition(SaveOperationPhase.Idle);

        var expected = new[]
        {
            SaveOperationPhase.Preparing,
            SaveOperationPhase.CheckingPathSupport,
            SaveOperationPhase.Staging,
            SaveOperationPhase.CreatingCheckpoint,
            SaveOperationPhase.Completed,
            SaveOperationPhase.Idle
        };
        if (!observed.Select(state => state.Phase).SequenceEqual(expected) ||
            !observed.Take(4).All(state => state.IsActive) ||
            observed.Skip(4).Any(state => state.IsActive))
            throw new InvalidOperationException("Save operation phases did not remain synchronized.");

        if (GuardianForm.MapActivityToSavePhase(GuardianActivityKind.LongPathChecking) !=
                SaveOperationPhase.CheckingPathSupport ||
            GuardianForm.MapActivityToSavePhase(GuardianActivityKind.SaveStaging) != SaveOperationPhase.Staging ||
            GuardianForm.MapActivityToSavePhase(GuardianActivityKind.SaveCreatingCheckpoint) !=
                SaveOperationPhase.CreatingCheckpoint ||
            GuardianForm.MapActivityToSavePhase(GuardianActivityKind.Information) is not null)
            throw new InvalidOperationException("Structured activity did not map to Save visual phases.");

        if (GuardianWorkboardControl.SaveBadgeFor(SaveOperationPhase.Preparing) != "PREPARING SAVE…" ||
            GuardianWorkboardControl.SaveBadgeFor(SaveOperationPhase.Completed) != "SAVED ✓" ||
            GuardianWorkboardControl.SaveBadgeFor(SaveOperationPhase.Idle) is not null)
            throw new InvalidOperationException("Save workboard badges do not reflect operation phases.");

        foreach (var operation in new[]
                 {
                     GuardianOperationKind.Get,
                     GuardianOperationKind.Send,
                     GuardianOperationKind.Reconcile
                 })
        {
            var operationController = new SaveOperationStateController(operation);
            operationController.Transition(SaveOperationPhase.Preparing);
            if (operationController.Current.Operation != operation || !operationController.Current.IsActive ||
                GuardianWorkboardControl.OperationBadgeFor(operation, SaveOperationPhase.Preparing) != "PREPARING…" ||
                GuardianWorkboardControl.OperationBadgeFor(operation, SaveOperationPhase.Completed) is null)
                throw new InvalidOperationException($"{operation} visual state was not projected consistently.");
        }

        Console.WriteLine("Save operation visual-state regression passed.");
        using var assets = new PetAssets();
        foreach (var phase in new[] { SaveOperationPhase.Preparing, SaveOperationPhase.CheckingPathSupport,
                     SaveOperationPhase.Staging, SaveOperationPhase.CreatingCheckpoint, SaveOperationPhase.Completed,
                     SaveOperationPhase.Warning, SaveOperationPhase.Failed })
        {
            foreach (var alternate in new[] { false, true })
            {
                var state = new SaveOperationVisualState(phase, "test", DateTimeOffset.UtcNow);
                var image = assets.ForOperation(state, alternate, assets.Idle);
                if (new[] { assets.Idle, assets.Happy, assets.Warning, assets.ReviewReady }.Contains(image) ||
                    image.Width != 320 || image.Height != 320)
                    throw new InvalidOperationException($"Dedicated Save artwork missing for {phase}.");
            }
        }
        var completed = new SaveOperationVisualState(SaveOperationPhase.Completed, "saved", DateTimeOffset.UtcNow);
        if (assets.ForOperation(completed with { Operation = GuardianOperationKind.Send }, false, assets.Idle) != assets.Happy ||
            assets.ForOperation(completed with { Phase = SaveOperationPhase.Cancelled }, false, assets.Idle) != assets.Idle)
            throw new InvalidOperationException("Save artwork changed unrelated operations or cancellation.");
        // Simulate a missing optional asset and verify that fallback remains usable.
        var images = (Dictionary<string, System.Drawing.Image>)typeof(PetAssets)
            .GetField("_saveImages", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(assets)!;
        var name = PetAssets.SaveAssetName(SaveOperationPhase.Completed, false)!;
        images[name].Dispose();
        images.Remove(name);
        if (assets.ForOperation(completed, false, assets.Idle) != assets.Happy)
            throw new InvalidOperationException("Missing Save artwork did not fall back safely.");
        Console.WriteLine("Dedicated Save pet assets and fallback regression passed.");
    }
}
