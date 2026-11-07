using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OverTranslate.Models;
using OverTranslate.Services;
using OverTranslate.Views.Realtime;
using Size = System.Windows.Size;

namespace LayoutProbe;

/// <summary>Renders the realtime page without showing windows, starting a session or reading the user's settings.</summary>
internal static class RealtimePageLayout
{
    internal static void Report(string directory)
    {
        Directory.CreateDirectory(directory);
        var singleton = typeof(SettingsService).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        if (singleton.GetValue(null) is not null) throw new InvalidOperationException("Probe must start before settings are loaded.");
        var synthetic = (SettingsService)RuntimeHelpers.GetUninitializedObject(typeof(SettingsService));
        var settings = new AppSettings();
        settings.Realtime.SourceLanguage = "TH";
        settings.Realtime.TargetLanguage = "ZH-HANS";
        typeof(SettingsService).GetProperty("Current")!.SetValue(synthetic, settings);
        singleton.SetValue(null, synthetic);
        try
        {
            foreach (var language in new[] { "zh-Hans", "en" })
            {
                settings.UiLanguage = language;
                var resources = Application.Current.Resources.MergedDictionaries;
                resources[2] = new ResourceDictionary { Source = new Uri($"pack://application:,,,/TTTools;component/Resources/Strings.{language}.xaml") };

                var page = new RealtimePage();
                var size = new Size(900, 900);
                page.Measure(size);
                page.Arrange(new Rect(size));
                page.UpdateLayout();
                var bitmap = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
                var host = new DrawingVisual();
                using (var context = host.RenderOpen())
                {
                    context.DrawRectangle(Brushes.White, null, new Rect(size));
                    context.DrawRectangle(new VisualBrush(page), null, new Rect(size));
                }
                bitmap.Render(host);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream = File.Create(Path.Combine(directory, $"realtime-{language}.png"));
                encoder.Save(stream);
                Console.WriteLine($"{language}: rendered, source={settings.Realtime.SourceLanguage}");
                page.Teardown();
            }
        }
        finally
        {
            singleton.SetValue(null, null);
        }
    }
}
