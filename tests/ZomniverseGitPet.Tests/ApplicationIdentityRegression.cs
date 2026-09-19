using System.Runtime.CompilerServices;
using ZomniverseGitPet;

internal static class ApplicationIdentityRegression
{
    [ModuleInitializer]
    public static void Run()
    {
        var dev = ApplicationIdentity.Detect(
            @"C:\Local\DEV\DEV-ZomniverseGitPet.exe",
            _ => true);
        if (dev.Channel != ApplicationChannel.Development ||
            dev.AppUserModelId != "Zomniverse.ZGitPet.Dev")
            throw new InvalidOperationException("Application identity regression: DEV channel detection failed.");

        var installed = ApplicationIdentity.Detect(
            @"C:\Users\Test\AppData\Local\Programs\ZomniverseGitPet\ZomniverseGitPet.exe",
            path => Path.GetFileName(path).Equals("unins000.exe", StringComparison.OrdinalIgnoreCase));
        if (installed.Channel != ApplicationChannel.InstalledRelease ||
            installed.AppUserModelId != "Zomniverse.ZGitPet.Release")
            throw new InvalidOperationException("Application identity regression: installed release detection failed.");

        var portable = ApplicationIdentity.Detect(
            @"C:\Temp\ZomniverseGitPet.exe",
            _ => false);
        if (portable.Channel != ApplicationChannel.Portable ||
            portable.AppUserModelId != "Zomniverse.ZGitPet.Portable")
            throw new InvalidOperationException("Application identity regression: portable detection failed.");

        var mutexA = ApplicationIdentity.BuildGlobalInstanceName("same-user");
        var mutexB = ApplicationIdentity.BuildGlobalInstanceName("same-user");
        if (mutexA != mutexB)
            throw new InvalidOperationException("Application identity regression: global instance identity must be channel-independent.");

        var request = ApplicationInstanceProtocol.BuildActivationRequest(ApplicationChannel.Development);
        if (!ApplicationInstanceProtocol.TryParseActivationRequest(request, out var requested) ||
            requested != ApplicationChannel.Development)
            throw new InvalidOperationException("Application identity regression: channel activation request did not round-trip.");

        var response = ApplicationInstanceProtocol.ParseResponse(
            ApplicationInstanceProtocol.BuildResponse(ExistingInstanceResponse.SwitchApproved));
        if (response != ExistingInstanceResponse.SwitchApproved)
            throw new InvalidOperationException("Application identity regression: handoff response did not round-trip.");

        Console.WriteLine("Application identity regression passed (DEV/release/portable + global handoff protocol).");
    }
}
