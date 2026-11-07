using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OverTranslate.Models;
using OverTranslate.Services;
using OverTranslate.Services.Ocr;
using OverTranslate.Views.Settings;
using OverTranslate.Views.Shell;
using Size = System.Windows.Size;
using Brush = System.Windows.Media.Brush;

namespace LayoutProbe;

internal static class CompactOcrLayout
{
    internal static void Report(string directory)
    {
        Directory.CreateDirectory(directory);
        var singleton = typeof(SettingsService).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        if (singleton.GetValue(null) is not null) throw new InvalidOperationException("Probe must start before settings are loaded.");
        var synthetic = (SettingsService)RuntimeHelpers.GetUninitializedObject(typeof(SettingsService));
        var settings = new AppSettings { SourceLanguage = "TH", TargetLanguage = "ZH-HANS", UiLanguage = "zh-Hans" };
        typeof(SettingsService).GetProperty("Current")!.SetValue(synthetic, settings);
        singleton.SetValue(null, synthetic);
        var reports = new List<object>();
        try
        {
            foreach (var theme in new[] { "Light", "Dark" })
            foreach (var lang in new[] { "zh-Hans", "en", "zh-Hant", "ja", "ko", "ru" })
            {
                settings.Theme = theme;
                settings.UiLanguage = lang;
                var resources = Application.Current.Resources.MergedDictionaries;
                resources[0] = new ResourceDictionary { Source = new Uri($"pack://application:,,,/TextTack;component/Themes/{theme}Theme.xaml") };
                resources[2] = new ResourceDictionary { Source = new Uri($"pack://application:,,,/TextTack;component/Resources/Strings.{lang}.xaml") };
                foreach (var choice in new[] { OcrModelCatalog.ByLanguage, OcrModelCatalog.Thai })
                foreach (var scale in new[] { "default", "medium" })
                {
                    var model = new OcrModelSettings { Selection = choice,
                        DetectorSize = scale == "medium" ? "medium" : "tiny",
                        RecognizerSize = scale == "medium" ? "medium" : "small" };
                    var saves = 0;
                    var card = new OcrModelCard(model, () => saves++);
                    var picker = (ComboBox)card.FindName("ModelBox");
                    picker.SelectedValue = OcrModelCatalog.Korean;
                    if (model.Selection != OcrModelCatalog.Korean || saves != 1) throw new InvalidOperationException("Picker did not persist selection.");
                    picker.SelectedValue = choice;
                                        var detector = (ComboBox)card.FindName("DetectorBox");
                    var recognizer = (ComboBox)card.FindName("RecognizerBox");
                    var device = (ComboBox)card.FindName("DeviceBox");
                    var targetDevice = OcrExecution.Devices().FirstOrDefault(d => d.Id != "cpu");
                    if (scale == "medium" && targetDevice is not null) device.SelectedValue = targetDevice.Id;
                    if (model.DetectorSize != (scale == "medium" ? "medium" : "tiny"))
                        throw new InvalidOperationException("Detector size was not preserved by the UI.");
                    if (recognizer.IsEnabled != (choice == OcrModelCatalog.ByLanguage))
                        throw new InvalidOperationException("General size must be disabled for the Thai recognizer.");
                    Save(card, 420, null, Path.Combine(directory, $"ocr-{choice}-{scale}-{lang}-{theme}.png"));
                    var selected = ((OcrModelOption)picker.SelectedItem).Display;
                    if (!VisibleTexts(picker).Contains(selected)) throw new InvalidOperationException("Selected model label is not visible.");
                    reports.Add(new { kind = "model-card", lang, theme, choice, scale, Device = model.Device, card.ActualWidth, card.ActualHeight, SyntheticSettings = true });
                }
                if (lang is not ("zh-Hans" or "en")) continue;
                var window = new ShellWindow();
                try
                {
                    foreach (var page in new[] { ShellPage.Translation, ShellPage.Settings })
                    {
                        window.Navigate(page);
                        var host = (Border)window.FindName("ContentHost");
                        host.BeginAnimation(UIElement.OpacityProperty, null);
                        host.Opacity = 1;
                        host.RenderTransform = Transform.Identity;
                        var content = (FrameworkElement)window.Content;
                        Save(content, 1120, 780, Path.Combine(directory, $"shell-{page}-{lang}-{theme}.png"));
                        reports.Add(new { kind = "shell", lang, theme, page = page.ToString(), SyntheticSettings = true });
                    }
                }
                finally { window.Close(); }
            }
        }
        finally { singleton.SetValue(null, null); }
        File.WriteAllText(Path.Combine(directory, "geometry.json"), JsonSerializer.Serialize(reports, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Rendered {reports.Count} synthetic views without loading user settings or showing windows.");
    }

    private static IEnumerable<string> VisibleTexts(DependencyObject node)
    {
        if (node is TextBlock text) yield return text.Text;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            foreach (var value in VisibleTexts(VisualTreeHelper.GetChild(node, i))) yield return value;
    }

    private static void Save(FrameworkElement content, double width, double? height, string path)
    {
        content.Measure(new Size(width, height ?? double.PositiveInfinity));
        content.Arrange(new Rect(new Size(width, height ?? content.DesiredSize.Height)));
        content.UpdateLayout();
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawRectangle((Brush)Application.Current.FindResource("AppWindowBg"), null,
                new Rect(0, 0, width, content.ActualHeight));
            drawing.DrawRectangle(new VisualBrush(content), null, new Rect(0, 0, width, content.ActualHeight));
        }
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(width), (int)Math.Ceiling(content.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path);encoder.Save(file);
    }

    internal static async Task<int> CheckLocalModels(string directory)
    {
        Directory.CreateDirectory(directory);
        using var baseline = JsonDocument.Parse(File.ReadAllText(".test-artifacts/thai-ocr/report.json"));
        var reports = new List<object>();
        foreach (var (choice, lang, fixture) in new[]
        {
            (OcrModelCatalog.ByLanguage, "TH", "thai-menu"),
            (OcrModelCatalog.Thai, "TH", "thai-menu"),
            (OcrModelCatalog.General, "EN", "english"),
            (OcrModelCatalog.Korean, "KO", "korean"),
        })
        {
            var data = baseline.RootElement.GetProperty("Results").EnumerateArray().Single(v => v.GetProperty("Name").GetString() == fixture);
            using var image = new Bitmap(data.GetProperty("Path").GetString()!);
            var settings = new OcrModelSettings { Selection = choice };
            using var service = new OcrService(() => settings);
            var blocks = await service.RecognizeAsync(image, lang, layoutMode: CaptureLayoutMode.Interface);
            var text = string.Concat(blocks.Select(b => b.Text)).Replace(" ", "");
            var missing = data.GetProperty("Expected").EnumerateArray().Select(v => v.GetString()!)
                .Where(v => !text.Contains(v.Replace(" ", ""))).ToArray();
            var invalid = blocks.Any(b => b.Bounds.X < 0 || b.Bounds.Y < 0 || b.Bounds.Right > image.Width || b.Bounds.Bottom > image.Height);
            reports.Add(new { choice, lang, fixture, Missing = missing, InvalidBounds = invalid, SyntheticFixture = true,
                Blocks = blocks.Select(b => new { b.Text, b.Bounds.X, b.Bounds.Y, b.Bounds.Width, b.Bounds.Height }) });
            if (missing.Length > 0 || invalid) throw new InvalidOperationException("Local model selection regression: " + choice);
            Console.WriteLine($"{choice}/{lang}: {blocks.Count} blocks, no missing fixture text or invalid bounds.");
        }
        File.WriteAllText(Path.Combine(directory, "local-models.json"), JsonSerializer.Serialize(reports, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
}
