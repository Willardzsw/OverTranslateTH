using System.IO;
using Microsoft.Win32;

namespace OverTranslate.Services;

public static class StartupService
{
    internal const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    internal const string AppName = "OverTranslate-Thai-Dev";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            return key?.GetValue(AppName) is not null;
        }
    }

    public static void Set(bool enable)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key is null) return;

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

}
