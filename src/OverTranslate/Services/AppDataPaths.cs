using System.IO;

namespace OverTranslate.Services;

/// <summary>
/// Independent Thai OCR development profile. Settings, logs and assets remain beside this build;
/// the installed OverTranslate profile and credentials are never imported.
/// NLog.config uses the same profile-thai/logs location before this class is initialized.
/// </summary>
internal static class AppDataPaths
{
    /// <summary>
    /// Points <see cref="AssetsRoot"/> somewhere else, for trying a download without touching the
    /// real one. Uninstalling ignores it and always removes <see cref="DefaultAssetsRoot"/>.
    /// </summary>
    internal const string AssetsOverride = "OVERTRANSLATE_THAI_ASSETS_DIR";

    internal const string AssetsFolderName = "OverTranslate-assets";

    // This development fork must never read the installed application's profile or credentials.
    // Keep all writable data alongside this independent build, including when launched directly.
    internal static string Root { get; } = Path.Combine(AppContext.BaseDirectory, "profile-thai");

    internal static string Logs => Path.Combine(Root, "logs");

    internal static string Diagnostics => Path.Combine(Root, "diagnostics");

    /// <summary>The isolated build's assets directory, whatever <see cref="AssetsOverride"/> says.</summary>
    internal static string DefaultAssetsRoot { get; } = Path.Combine(Root, "assets");

    internal static string AssetsRoot { get; } =
        Environment.GetEnvironmentVariable(AssetsOverride) is { Length: > 0 } overridden
            ? Path.GetFullPath(overridden)
            : DefaultAssetsRoot;

    internal static string Models => Path.Combine(AssetsRoot, "models");

    /// <summary>Native libraries downloaded on demand, one folder per library and version.</summary>
    internal static string Runtimes => Path.Combine(AssetsRoot, "runtimes");
}
