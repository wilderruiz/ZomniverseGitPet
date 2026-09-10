using System.Runtime.CompilerServices;
using ZomniverseGitPet;

internal static class GuardianWorkboardRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        var changed = GuardianWorkboardService.ParseNameStatus(
            "M\tsrc/App.cs\nA\tdocs/new.md\nR100\told.txt\tnew.txt\n",
            "incoming");
        if (changed.Count != 3 ||
            changed[0].State != "MODIFIED" ||
            changed[1].State != "ADDED" ||
            changed[2].State != "RENAMED" ||
            changed[2].Path != "new.txt")
            throw new InvalidOperationException("Guardian workboard name-status projection regression failed.");

        var commits = GuardianWorkboardService.ParseCommitRows(
            "abc1234\tfirst saved update\ndef5678\tsecond saved update\n");
        if (commits.Count != 2 ||
            !commits.All(row => row.IsCommit) ||
            commits[0].Path != "abc1234  first saved update")
            throw new InvalidOperationException("Guardian workboard commit projection regression failed.");

        var reconcile = GuardianWorkboardService.CombineReconciliation(
            [
                new GuardianWorkboardRow("MODIFIED", "shared.cs"),
                new GuardianWorkboardRow("ADDED", "local.cs")
            ],
            [
                new GuardianWorkboardRow("MODIFIED", "shared.cs"),
                new GuardianWorkboardRow("DELETED", "remote.cs")
            ]);

        if (reconcile.Count != 3 ||
            reconcile.Single(row => row.Path == "shared.cs").State != "BOTH SIDES" ||
            reconcile.Single(row => row.Path == "local.cs").State != "LOCAL" ||
            reconcile.Single(row => row.Path == "remote.cs").State != "REMOTE")
            throw new InvalidOperationException("Guardian workboard reconciliation projection regression failed.");

        Console.WriteLine("Guardian workboard projection regression passed.");
    }
}
