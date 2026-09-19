using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace ZomniverseGitPet;

internal enum ApplicationChannel
{
    Development,
    InstalledRelease,
    Portable
}

internal sealed record ApplicationIdentityInfo(
    ApplicationChannel Channel,
    string DisplayName,
    string GuardianTitle,
    string AppUserModelId,
    string Description);

internal enum ExistingInstanceResponse
{
    Unknown,
    Activated,
    SwitchApproved,
    SwitchDeclined,
    SwitchBusy,
    LegacyActivated
}

/* ==========================================================================
   PATCH: APPLICATION CHANNEL IDENTITY + SINGLE-INSTANCE HANDOFF
   DATE.TIME: 2026-09-19 22:45 +03:00
   Give DEV, installed release, and portable copies explicit Windows identities
   while retaining one global GitPet process per Windows user.
   ========================================================================== */
internal static class ApplicationIdentity
{
    private const string InstancePrefix = "ZomniverseGitPet-";

    public static ApplicationIdentityInfo Current { get; } =
        Detect(Application.ExecutablePath, File.Exists);

    internal static ApplicationIdentityInfo Detect(
        string executablePath,
        Func<string, bool>? fileExists = null)
    {
        fileExists ??= File.Exists;

        var fileName = Path.GetFileName(executablePath);
        if (fileName.StartsWith("DEV-", StringComparison.OrdinalIgnoreCase))
        {
            return ForChannel(ApplicationChannel.Development);
        }

        var directory = Path.GetDirectoryName(executablePath);
        if (!string.IsNullOrWhiteSpace(directory) &&
            fileExists(Path.Combine(directory, "unins000.exe")))
        {
            return ForChannel(ApplicationChannel.InstalledRelease);
        }

        return ForChannel(ApplicationChannel.Portable);
    }

    internal static ApplicationIdentityInfo ForChannel(ApplicationChannel channel) => channel switch
    {
        ApplicationChannel.Development => new(
            channel,
            "DEV-ZGitPet",
            "DEV-ZGitPet Guardian",
            "Zomniverse.ZGitPet.Dev",
            "local development build"),

        ApplicationChannel.InstalledRelease => new(
            channel,
            "ZGitPet",
            "ZGitPet Guardian",
            "Zomniverse.ZGitPet.Release",
            "installed release"),

        _ => new(
            ApplicationChannel.Portable,
            "ZGitPet Portable",
            "ZGitPet Portable Guardian",
            "Zomniverse.ZGitPet.Portable",
            "portable build")
    };

    internal static string BuildGlobalInstanceName(string userId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(userId));
        return InstancePrefix + Convert.ToHexString(hash)[..16];
    }

    public static void ApplyWindowsAppUserModelId(ApplicationIdentityInfo identity)
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            _ = SetCurrentProcessExplicitAppUserModelID(identity.AppUserModelId);
        }
        catch
        {
            // Taskbar identity is helpful but must never prevent GitPet from starting.
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
}

internal static class ApplicationInstanceProtocol
{
    private const string RequestPrefix = "ZGITPET/1 ACTIVATE ";
    private const string ResponsePrefix = "ZGITPET/1 ";

    internal static string BuildActivationRequest(ApplicationChannel channel) =>
        RequestPrefix + channel;

    internal static bool TryParseActivationRequest(string? message, out ApplicationChannel channel)
    {
        channel = default;
        if (string.IsNullOrWhiteSpace(message) ||
            !message.StartsWith(RequestPrefix, StringComparison.Ordinal))
            return false;

        var value = message[RequestPrefix.Length..].Trim();
        return Enum.TryParse(value, ignoreCase: true, out channel);
    }

    internal static string BuildResponse(ExistingInstanceResponse response) =>
        ResponsePrefix + response;

    internal static ExistingInstanceResponse ParseResponse(string? message)
    {
        if (string.IsNullOrWhiteSpace(message) ||
            !message.StartsWith(ResponsePrefix, StringComparison.Ordinal))
            return ExistingInstanceResponse.Unknown;

        var value = message[ResponsePrefix.Length..].Trim();
        return Enum.TryParse<ExistingInstanceResponse>(value, ignoreCase: true, out var response)
            ? response
            : ExistingInstanceResponse.Unknown;
    }
}
