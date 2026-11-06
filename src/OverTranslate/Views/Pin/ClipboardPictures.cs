using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;
using NLog;
using Clipboard = System.Windows.Clipboard;

namespace OverTranslate.Views.Pin;

/// <summary>
/// What on the clipboard can be pinned, the way Snipaste reads it: not only a copied picture, but
/// picture files copied in Explorer, the PNG that browsers and chat apps such as WeChat put there,
/// and a picture an HTML fragment points at.
/// </summary>
/// <remarks>
/// Asked in that order because the richer formats come first: a PNG keeps transparency that the
/// plain bitmap loses (it comes out on black), and a copied file is the original rather than a copy
/// the copying program re-rendered.
/// </remarks>
internal static class ClipboardPictures
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    /// <summary>More than this many copied files is a folder's worth, not a few pictures to pin.</summary>
    private const int MaxFiles = 12;

    private static readonly HashSet<string> PictureExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".jfif", ".bmp", ".gif", ".tif", ".tiff", ".webp", ".ico", ".heic",
    };

    private static readonly Regex HtmlImage = new(
        @"<img[^>]+src\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>The pictures on the clipboard, frozen; empty when there is none.</summary>
    public static IReadOnlyList<BitmapSource> Read()
    {
        var files = FromFiles();
        if (files.Count > 0) return files;

        if (FromPngStream() is { } png) return [png];

        if (Clipboard.ContainsImage() && Clipboard.GetImage() is { } bitmap)
            return [Frozen(bitmap)];

        if (FromHtml() is { } html) return [html];

        return [];
    }

    private static List<BitmapSource> FromFiles()
    {
        var pictures = new List<BitmapSource>();
        if (!Clipboard.ContainsFileDropList()) return pictures;

        foreach (var path in Clipboard.GetFileDropList().Cast<string>())
        {
            if (pictures.Count >= MaxFiles) break;
            if (!PictureExtensions.Contains(Path.GetExtension(path)) || !File.Exists(path)) continue;
            if (LoadFile(path) is { } picture) pictures.Add(picture);
        }
        return pictures;
    }

    private static BitmapSource? FromPngStream()
    {
        try
        {
            if (!Clipboard.ContainsData("PNG") || Clipboard.GetData("PNG") is not MemoryStream stream) return null;
            stream.Position = 0;
            return Frozen(BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad));
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Clipboard PNG could not be read");
            return null;
        }
    }

    /// <summary>The first picture an HTML fragment shows, if it is a local file or embedded data.</summary>
    private static BitmapSource? FromHtml()
    {
        try
        {
            if (!Clipboard.ContainsText(System.Windows.TextDataFormat.Html)) return null;
            var html = Clipboard.GetText(System.Windows.TextDataFormat.Html);
            var match = HtmlImage.Match(html);
            if (!match.Success) return null;

            var src = System.Net.WebUtility.HtmlDecode(match.Groups[1].Value);
            if (src.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            {
                int comma = src.IndexOf(',');
                if (comma < 0 || !src[..comma].EndsWith(";base64", StringComparison.OrdinalIgnoreCase)) return null;
                using var data = new MemoryStream(Convert.FromBase64String(src[(comma + 1)..]));
                return Frozen(BitmapFrame.Create(data, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad));
            }

            // Only pictures already on this computer: fetching one from the web is not something a
            // paste should quietly do.
            if (Uri.TryCreate(src, UriKind.Absolute, out var uri) && uri.IsFile && File.Exists(uri.LocalPath))
                return LoadFile(uri.LocalPath);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Clipboard HTML picture could not be read");
        }
        return null;
    }

    private static BitmapSource? LoadFile(string path)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;   // read now, so the file is not held open
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            image.UriSource = new Uri(path);
            image.EndInit();
            return Frozen(image);
        }
        catch (Exception ex)
        {
            Log.Info(ex, "Copied file {File} could not be opened as a picture", Path.GetFileName(path));
            return null;
        }
    }

    private static BitmapSource Frozen(BitmapSource source)
    {
        if (source.CanFreeze && !source.IsFrozen) source.Freeze();
        return source;
    }
}
