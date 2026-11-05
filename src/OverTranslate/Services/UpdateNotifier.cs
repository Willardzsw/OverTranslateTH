using NLog;
using Velopack;

namespace OverTranslate.Services;

/// <summary>Checks official release metadata once at startup; never polls or installs updates.</summary>
public static class UpdateNotifier
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private static bool _checked;

    public static UpstreamReleaseInfo? Available { get; private set; }
    public static event EventHandler? AvailabilityChanged;

    public static async Task<UpstreamReleaseInfo?> CheckAsync()
    {
        if (_checked) return Available;
        _checked = true;
        try
        {
            var info = await UpstreamReleaseService.CheckAsync();
            if (info is null) return null;
            Available = info;
            AvailabilityChanged?.Invoke(null, EventArgs.Empty);
            return info;
        }
        catch (Exception ex)
        {
            // Offline, rate-limited or malformed responses must not interrupt OCR or translation.
            Log.Warn(ex, "Official release notification check failed; no update was applied");
            return null;
        }
    }

    internal static bool IsVersionSkipped(string latest, string skipped) =>
        SemanticVersion.TryParse(latest, out var version)
        && SemanticVersion.TryParse(skipped, out var skippedVersion)
        && version <= skippedVersion;

    public static bool IsSkipped(UpstreamReleaseInfo info) =>
        IsVersionSkipped(info.LatestVersion, SettingsService.Instance.Current.SkippedUpdateVersion);

    public static void Skip(UpstreamReleaseInfo info)
    {
        SettingsService.Instance.Current.SkippedUpdateVersion = info.LatestVersion;
        SettingsService.Instance.Save();
    }
}
