using System.Runtime.CompilerServices;
using ZomniverseGitPet;

internal static class ProjectSwitchStateRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        ProjectSwitchRuntime.ResetForTests();
        var observed = new List<ProjectSwitchVisualState>();
        EventHandler<ProjectSwitchVisualState> handler = (_, state) => observed.Add(state);
        ProjectSwitchRuntime.Changed += handler;

        try
        {
            ProjectSwitchRuntime.Begin("project-a", "PROJECT_A");
            if (!ProjectSwitchRuntime.IsSwitching)
                throw new InvalidOperationException("Project switching did not enter its busy state.");

            ProjectSwitchRuntime.Transition(ProjectSwitchPhase.VerifyingRepository, "Reading repository...");
            ProjectSwitchRuntime.Transition(ProjectSwitchPhase.LoadingRepository, "Checking Git state...");
            ProjectSwitchRuntime.Transition(ProjectSwitchPhase.LoadingScope, "Loading project scope...");
            ProjectSwitchRuntime.Transition(ProjectSwitchPhase.ActivatingProject, "Activating project...");
            ProjectSwitchRuntime.Transition(ProjectSwitchPhase.LoadingRemoteState, "Checking online updates...");
            ProjectSwitchRuntime.Transition(ProjectSwitchPhase.PreparingWorkboard, "Preparing workboard...");
            ProjectSwitchRuntime.Complete(TimeSpan.FromMilliseconds(842));

            var expected = new[]
            {
                ProjectSwitchPhase.Preparing,
                ProjectSwitchPhase.VerifyingRepository,
                ProjectSwitchPhase.LoadingRepository,
                ProjectSwitchPhase.LoadingScope,
                ProjectSwitchPhase.ActivatingProject,
                ProjectSwitchPhase.LoadingRemoteState,
                ProjectSwitchPhase.PreparingWorkboard,
                ProjectSwitchPhase.Completed
            };

            if (!observed.Select(state => state.Phase).SequenceEqual(expected))
                throw new InvalidOperationException("Project switch phases did not remain ordered.");
            if (ProjectSwitchRuntime.IsSwitching || ProjectSwitchRuntime.Current.Phase != ProjectSwitchPhase.Completed)
                throw new InvalidOperationException("Completed project switch remained locked.");
            if (ProjectSwitchRuntime.Current.Elapsed != TimeSpan.FromMilliseconds(842))
                throw new InvalidOperationException("Project switch elapsed timing was not preserved.");

            ProjectSwitchRuntime.Begin("project-b", "PROJECT_B");
            ProjectSwitchRuntime.Fail("simulated failure", TimeSpan.FromMilliseconds(250));
            if (ProjectSwitchRuntime.IsSwitching ||
                ProjectSwitchRuntime.Current.Phase != ProjectSwitchPhase.Failed ||
                ProjectSwitchRuntime.Current.Error != "simulated failure")
                throw new InvalidOperationException("Failed project switch did not unlock cleanly.");

            Console.WriteLine("Project switching visual-state regression passed.");
        }
        finally
        {
            ProjectSwitchRuntime.Changed -= handler;
            ProjectSwitchRuntime.ResetForTests();
        }
    }
}
