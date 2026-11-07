using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OverTranslate.Models;
using OverTranslate.Services;
using OverTranslate.Views.Shell;
using Size = System.Windows.Size;

namespace LayoutProbe;

/// <summary>Renders the About panel per language without showing windows or reading the user's settings.</summary>
internal static class AboutLayout
{
    internal static void Report(string directory)
    {
        Directory.CreateDirectory(directory);
        var singleton = typeof(SettingsService).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        if (singleton.GetValue(null) is not null) throw new InvalidOperationException("Probe must start before settings are loaded.");
        var synthetic = (SettingsService)RuntimeHelpers.GetUninitializedObject(typeof(SettingsService));
        var settings = new AppSettings();
        typeof(SettingsService).GetProperty("Current")!.SetValue(synthetic, settings);
        singleton.SetValue(null, synthetic);
        try
        {
            foreach (var language in new[] { "zh-Hans", "zh-Hant", "en", "ja", "ko", "ru" })
            {
                settings.UiLanguage = language;
                var resources = Application.Current.Resources.MergedDictionaries;
                resources[2] = new ResourceDictionary { Source = new Uri($"pack://application:,,,/TextTack;component/Resources/Strings.{language}.xaml") };

                var overlay = new AboutOverlay { Visibility = Visibility.Visible };
                var size = new Size(520, 480);
                overlay.Measure(size);
                overlay.Arrange(new Rect(size));
                overlay.UpdateLayout();
                var bitmap = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(overlay);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream = File.Create(Path.Combine(directory, $"about-{language}.png"));
                encoder.Save(stream);
                Console.WriteLine($"{language}: rendered");
            }

            var font = (System.Windows.Media.FontFamily)Application.Current.Resources["AppFont"];
            var wordmark = new FormattedText("TextTack 贴译", System.Globalization.CultureInfo.InvariantCulture,
                System.Windows.FlowDirection.LeftToRight,
                new Typeface(font, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal), 15,
                System.Windows.Media.Brushes.Black, 1);
            Console.WriteLine($"rail wordmark width: {wordmark.Width:0.0}px (rail column 248px)");
        }
        finally
        {
            singleton.SetValue(null, null);
        }
    }
}
