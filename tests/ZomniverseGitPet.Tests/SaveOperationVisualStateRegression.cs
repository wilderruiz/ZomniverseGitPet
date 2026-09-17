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
    }
}
