using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;

namespace OverTranslate.Services;

/// <summary>
/// Saves screenshots to a local folder. The image handed in is the same composited bitmap the
/// "copy screenshot" flow puts on the clipboard, so the saved file always matches what was copied.
/// </summary>
public static class ScreenshotSaveService
{
    /// <summary>圖片\OverTranslate — used whenever the user hasn't picked a custom folder.</summary>
    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "OverTranslate");

    /// <summary>The folder currently in effect: the user's choice, or the default when unset/blank.</summary>
    public static string ResolveDirectory(string? configured)
        => string.IsNullOrWhiteSpace(configured) ? DefaultDirectory : configured.Trim();

    /// <summary>
    /// Writes <paramref name="image"/> as a PNG into <paramref name="directory"/>, creating the
    /// folder when missing. Colliding timestamps get a suffix and never overwrite an older capture.
    /// Returns the full path of the written file.
    /// </summary>
    public static string Save(BitmapSource image, string? directory = null)
        => Save(image, directory, DateTime.Now);

    internal static string Save(BitmapSource image, string? directory, DateTime timestamp)
    {
        var dir = ResolveDirectory(directory);
        Directory.CreateDirectory(dir);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        var stem = $"OverTranslate_{timestamp:yyyyMMdd_HHmmssfff}";
        for (var suffix = 0; ; suffix++)
        {
            var name = suffix == 0 ? stem : $"{stem}_{suffix}";
            var path = Path.Combine(dir, name + ".png");
            FileStream stream;
            try { stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None); }
            catch (IOException) when (File.Exists(path)) { continue; }

            try
            {
                using (stream) encoder.Save(stream);
                return path;
            }
            catch
            {
                // Only this call created this file. Do not leave a partial image after a failed save.
                try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                throw;
            }
        }
    }

    /// <summary>Opens the folder in File Explorer, creating it first so the window isn't empty-handed.</summary>
    public static void OpenFolder(string? directory = null)
    {
        var dir = ResolveDirectory(directory);
        Directory.CreateDirectory(dir);
        Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true });
    }
}
