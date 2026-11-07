using System.IO;
using Microsoft.Win32;

namespace OverTranslate.Services;

public static class StartupService
{
    internal const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    internal const string AppName = "TTTools";

    /// <summary>The entry's name before the program was renamed TT Tools.</summary>
    internal const string LegacyAppName = "OverTranslate-Thai-Dev";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            return key?.GetValue(AppName) is not null || key?.GetValue(LegacyAppName) is not null;
        }
    }

    public static void Set(bool enable)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key is null) return;

        key.DeleteValue(LegacyAppName, throwOnMissingValue: false);
        if (enable)
        {
            var exePath = Environment.ProcessPath
                ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath)) return;
            key.SetValue(AppName, $"\"{exePath}\"");
        }
        else
        {
            key.DeleteValue(AppName, throwOnMissingValue: false);
        }
    }

    /// <summary>
    /// Replaces a startup entry left under the old name, which points at the old executable, with
    /// one under the new name pointing at this one. Does nothing when there is none.
    /// </summary>
    public static void MigrateLegacyEntry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            if (key?.GetValue(LegacyAppName) is null) return;
        }
        catch
        {
            return;
        }

        Set(true);
    }

}
