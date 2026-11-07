using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using NLog;

namespace OverTranslate.Services;

/// <summary>
/// The name and icon Windows puts on this application's notifications.
/// </summary>
/// <remarks>
/// Without an explicit AppUserModelID the shell makes one up for a tray balloon and keeps the icon
/// it first saw there, so a new icon in the executable never reaches the notification. This ID is
/// this build's own: the installed OverTranslate's registration is never read or changed.
/// </remarks>
internal static class NotificationIdentityService
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    internal const string Aumid = "Willardzsw.TextTack";
    internal static readonly string[] LegacyAumids = ["Willardzsw.TTTools", "Willardzsw.OverTranslateTH"];
    internal const string AumidKeyPath = @"Software\Classes\AppUserModelId";
    internal const string DisplayName = "TextTack";

    /// <summary>The application icon, shipped beside the executable for this alone.</summary>
    internal const string IconRelativePath = @"icons\app.ico";

    /// <summary>Has to run before the first window or tray icon exists.</summary>
    public static void Register()
    {
        try
        {
            Marshal.ThrowExceptionForHR(SetCurrentProcessExplicitAppUserModelID(Aumid));
            using var root = Registry.CurrentUser.CreateSubKey(AumidKeyPath);
            Write(root, Aumid, Path.Combine(AppContext.BaseDirectory, IconRelativePath));
            // The registrations from before the rename to TextTack.
            foreach (var legacy in LegacyAumids)
                root.DeleteSubKeyTree(legacy, throwOnMissingSubKey: false);
        }
        catch (Exception ex)
        {
            // Only the notification header is at stake.
            Log.Warn(ex, "Could not register the notification identity {0}", Aumid);
        }
    }

    /// <summary>
    /// Brings the registration in line with this copy, and returns whether anything had to change.
    /// </summary>
    internal static bool Write(RegistryKey root, string aumid, string iconPath)
    {
        using (var existing = root.OpenSubKey(aumid, writable: false))
        {
            if (existing?.GetValue("DisplayName") as string == DisplayName
                && existing.GetValue("IconUri") as string == iconPath)
                return false;
        }

        using var key = root.CreateSubKey(aumid);
        key.SetValue("DisplayName", DisplayName);
        key.SetValue("IconUri", iconPath);
        return true;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
}
