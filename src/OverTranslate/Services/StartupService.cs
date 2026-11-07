using System.IO;
using Microsoft.Win32;

namespace OverTranslate.Services;

public static class StartupService
{
    internal const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    internal const string AppName = "TextTack";

    /// <summary>The entry's names from before the program was renamed TextTack, newest first.</summary>
    internal static readonly string[] LegacyAppNames = ["TTTools", "OverTranslate-Thai-Dev"];

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            return key?.GetValue(AppName) is not null || HasLegacyEntry(key);
        }
    }

    public static void Set(bool enable)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key is null) return;

        foreach (var legacy in LegacyAppNames)
            key.DeleteValue(legacy, throwOnMissingValue: false);
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
            if (!HasLegacyEntry(key)) return;
        }
        catch
        {
            return;
        }

        Set(true);
    }

    private static bool HasLegacyEntry(RegistryKey? key) =>
        key is not null && LegacyAppNames.Any(name => key.GetValue(name) is not null);

}
