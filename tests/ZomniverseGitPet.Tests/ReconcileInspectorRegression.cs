using System.Runtime.CompilerServices;
using ZomniverseGitPet;

internal static class ReconcileInspectorRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        foreach (var state in new[] { "LOCAL", "REMOTE", "BOTH SIDES", "CONFLICT" })
        {
            if (!GuardianWorkboardControl.IsReconcileInspectableState(state))
                throw new InvalidOperationException($"Reconcile Inspector should accept {state} rows.");
        }

        foreach (var state in new[] { "MODIFIED", "ADDED", "DELETED", "COMMIT", "MORE", "" })
        {
            if (GuardianWorkboardControl.IsReconcileInspectableState(state))
                throw new InvalidOperationException($"Reconcile Inspector should ignore non-reconcile state '{state}'.");
        }

        if (ReconcileInspectorPanel.DefaultViewForState("LOCAL") != ReconcileInspectorView.BaseLocal ||
            ReconcileInspectorPanel.DefaultViewForState("REMOTE") != ReconcileInspectorView.BaseRemote ||
            ReconcileInspectorPanel.DefaultViewForState("BOTH SIDES") != ReconcileInspectorView.LocalRemote ||
            ReconcileInspectorPanel.DefaultViewForState("CONFLICT") != ReconcileInspectorView.LocalRemote ||
            ReconcileInspectorPanel.DefaultViewForState("MODIFIED") != ReconcileInspectorView.Summary)
            throw new InvalidOperationException("Reconcile Inspector default-view routing regression failed.");

        Console.WriteLine("Reconcile Inspector Phase 1 routing regression passed.");
    }
}
