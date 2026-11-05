using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OverTranslate.Services;
using OverTranslate.Views.Shell;

namespace LayoutProbe;

/// <summary>Render synthetic release notices without showing windows, reading settings or making requests.</summary>
internal static class UpdateNoticeLayout
{
    internal static int CheckOfficialRelease()
    {
        try
        {
            var info = UpstreamReleaseService.CheckAsync().GetAwaiter().GetResult();
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                Baseline = UpstreamReleaseService.BaselineVersion,
                NewerOfficialVersion = info?.LatestVersion,
                ReleaseNotes = info?.ReleaseNotesUri.AbsoluteUri,
                CheckedAtUtc = DateTimeOffset.UtcNow,
                NotificationOnly = true,
            }));
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.GetType().Name + ": " + ex.Message);
            return 1;
        }
    }

    internal static void Report(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var measurements = new List<object>();
        foreach (var theme in new[] { "Light", "Dark" })
        foreach (var language in new[] { "zh-Hans", "zh-Hant", "en", "ja", "ko", "ru" })
        {
            var resources = Application.Current.Resources.MergedDictionaries;
            resources[0] = new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/OverTranslate;component/Themes/{theme}Theme.xaml")
            };
            resources[2] = new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/OverTranslate;component/Resources/Strings.{language}.xaml")
            };
            var window = new UpdateWindow(new UpstreamReleaseInfo("99.0.0",
                new Uri("https://github.com/Hon-Lu/OverTranslate/releases/tag/v99.0.0")));
            try
            {
                var content = (Grid)window.Content;
                content.Background = window.Background;
                content.Measure(new Size(window.Width, double.PositiveInfinity));
                content.Arrange(new Rect(new Size(window.Width, content.DesiredSize.Height)));
                content.UpdateLayout();
                var bitmap = new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth),
                    (int)Math.Ceiling(content.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(content);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using (var file = File.Create(Path.Combine(outputDirectory, $"notice-{language}-{theme}.png"))) encoder.Save(file);
                measurements.Add(new { theme, language, width = content.ActualWidth, height = content.ActualHeight,
                    SyntheticRelease = true, VisibleWindows = false, NetworkRequests = 0 });
                Console.WriteLine($"{language}/{theme}: {content.ActualWidth}x{content.ActualHeight}");
            }
            finally { window.Close(); }
        }
        File.WriteAllText(Path.Combine(outputDirectory, "geometry.json"),
            JsonSerializer.Serialize(measurements, new JsonSerializerOptions { WriteIndented = true }));
    }
}
