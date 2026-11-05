using System.IO;
using Velopack;
using Velopack.Locators;
using Velopack.Logging;
using Velopack.Sources;
using VelopackUpdateInfo = Velopack.UpdateInfo;

namespace OverTranslate.Services;

public sealed record UpdateInfo(
    string LatestVersion,
    DeltaAwareUpdateManager Manager,
    VelopackUpdateInfo VelopackInfo);

/// <summary>
/// Velopack's UpdateManager, with two changes to how its download treats the deltas: it says out
/// loud when it gives up on them, and an unfinished download no longer throws away what they need.
/// </summary>
/// <remarks>
/// Velopack catches a failed delta, logs a warning and starts the full package with nothing raised.
/// The window used to infer the switch from the first figure lower than the last, but the full
/// download only reports every 3%, which is several megabytes — a minute or more on a slow link,
/// spent under a line that still said the patches were being merged. This override sits on the
/// method Velopack itself falls back from, so the signal arrives the moment the fallback begins.
/// </remarks>
/// <param name="locator">For tests only, as on Velopack's own constructor; the app leaves it null.</param>
public sealed class DeltaAwareUpdateManager(IUpdateSource source, UpdateOptions? options, IVelopackLocator? locator = null)
    : UpdateManager(source, options, locator)
{
    /// <summary>
    /// Raised on a worker thread when the deltas are abandoned and the full package is about to be
    /// fetched. Not raised for a cancelled attempt, which falls back only to throw straight away.
    /// </summary>
    public Action? FellBackToFull { get; set; }

    // The installed version's full package, for the length of one download; see CleanPackagesExcept.
    private string? _basePackage;

    public override async Task DownloadUpdatesAsync(
        VelopackUpdateInfo updates, Action<int>? progress = null, CancellationToken cancelToken = default)
    {
        _basePackage = updates.BaseRelease?.FileName is { } name ? BasePackagePath(name) : null;
        try
        {
            await base.DownloadUpdatesAsync(updates, progress, cancelToken);
        }
        finally
        {
            _basePackage = null;
        }
    }

    /// <summary>
    /// Velopack's clean-up, except that a download which did not finish keeps the installed
    /// version's full package.
    /// </summary>
    /// <remarks>
    /// Velopack runs this on the way out of every download, finished or not, and it deletes every
    /// package but the target — the base the deltas are merged into included. After a finished
    /// download that costs nothing: the new full package takes over as the next update's base. After
    /// 取消更新 it cost the retry its deltas, and the user most likely to cancel and retry is the one
    /// on a slow link, who is then made to fetch the whole package in place of a few megabytes. So
    /// when the target is not there the base is spared, and everything else goes as Velopack would
    /// have it: the deltas are re-fetched anyway, and a ".partial" is never resumed.
    /// </remarks>
    protected override void CleanPackagesExcept(string? assetToKeep)
    {
        if (_basePackage is null || assetToKeep is null || File.Exists(assetToKeep) || !File.Exists(_basePackage))
        {
            base.CleanPackagesExcept(assetToKeep);
            return;
        }

        Log.Log(VelopackLogLevel.Information, "Download did not finish; keeping base package for the next attempt: " + _basePackage, null);

        var packagesDir = Locator.PackagesDir!;
        var leftovers = Directory.EnumerateFiles(packagesDir, "*.nupkg")
            .Where(path => !string.Equals(Path.GetFullPath(path), Path.GetFullPath(_basePackage), StringComparison.OrdinalIgnoreCase))
            .Concat(Directory.EnumerateFiles(packagesDir, "*.partial"))
            .ToArray();

        foreach (var path in leftovers)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception ex)
            {
                Log.Log(VelopackLogLevel.Warning, "Failed to delete partial package: " + path, ex);
            }
        }
    }

    /// <remarks>
    /// <para>
    /// Refuses up front when the package the deltas apply to is gone — cleaned up by a version of
    /// the app from before <see cref="CleanPackagesExcept"/> kept it, or removed by hand. The
    /// UpdateInfo from the check still promises deltas either way, and left alone Velopack would
    /// download every one of them only for the merge to fail on the missing base, then fetch the
    /// full package it could have started on. Throwing here lands in the same fallback, minus the
    /// waste.
    /// </para>
    /// <para>
    /// The merge itself runs in Update.exe with one thread; see <see cref="PatchThreadsVariable"/>.
    /// </para>
    /// </remarks>
    protected override async Task DownloadAndApplyDeltaUpdates(
        VelopackUpdateInfo updates, string targetFile, Action<int> progress, CancellationToken cancelToken)
    {
        var previousThreads = Environment.GetEnvironmentVariable(PatchThreadsVariable);
        try
        {
            var basePackage = BasePackagePath(updates.BaseRelease!.FileName);
            if (!File.Exists(basePackage))
                throw new FileNotFoundException("Base package for delta updates is missing.", basePackage);

            Environment.SetEnvironmentVariable(PatchThreadsVariable, "1");
            await base.DownloadAndApplyDeltaUpdates(updates, targetFile, progress, cancelToken);
        }
        catch when (!cancelToken.IsCancellationRequested)
        {
            FellBackToFull?.Invoke();
            throw;
        }
        finally
        {
            Environment.SetEnvironmentVariable(PatchThreadsVariable, previousThreads);
        }
    }

    /// <summary>
    /// Read by the thread pool Update.exe's <c>patch</c> unzips with; set to 1 for as long as the
    /// deltas are being fetched and merged, which Update.exe inherits when Velopack starts it.
    /// </summary>
    /// <remarks>
    /// Velopack's parallel unzip (fastzip, inside Update.exe, unchanged up to 1.2.161) shares one
    /// file reader between its threads, and that reader forgets its new position after a seek. A
    /// thread asking for exactly the offset it wrongly believes it is at reads from wherever the
    /// last seek left the file, and the entry comes out as "corrupt deflate stream". Whether the
    /// offsets ever line up depends on the package: 2.4.0's full package hit it on
    /// korean/rec.onnx in 7 merges of 20, each failure throwing away the deltas for a 120 MB
    /// download; one thread did 0 of 20 at about 2 seconds more a merge. Velopack only reads
    /// packages this way in <c>patch</c>, so nothing else Update.exe does is slowed.
    /// </remarks>
    internal const string PatchThreadsVariable = "RAYON_NUM_THREADS";

    // Where Velopack itself keeps it (its GetLocalPackagePath is internal). That also runs the name
    // through a filename sanitiser, which a name from our own feed never needs.
    private string BasePackagePath(string fileName) => Path.Combine(Locator.PackagesDir!, fileName);
}

/// <summary>This build never checks for or applies releases; legacy installer entry points stay disabled.</summary>
public static class UpdateService
{
    public static Task<UpdateInfo?> CheckAsync() => Task.FromResult<UpdateInfo?>(null);

    public static Task DownloadAndApplyAsync(
        UpdateInfo info, Action<int>? onProgress = null, Action? onFellBackToFull = null,
        Func<Task>? onApplying = null, CancellationToken cancelToken = default) =>
        Task.FromException(new NotSupportedException(
            "This build does not apply updates. Merge and validate the source manually."));
}
