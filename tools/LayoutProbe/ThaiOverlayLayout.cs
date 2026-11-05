using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using OverTranslate.Services;
using OverTranslate.Views.Overlay;

namespace LayoutProbe;

/// <summary>Render the actual overlay from measured Thai OCR blocks, with fixed Chinese test text.</summary>
internal static class ThaiOverlayLayout
{
    internal static void Report(string directory)
    {
        directory = Path.GetFullPath(directory);
        using var report = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "report.json")));
        foreach (var name in new[] { "thai-menu", "thai-sentence" })
        {
            var fixture = report.RootElement.GetProperty("Results").EnumerateArray().Single(r => r.GetProperty("Name").GetString() == name);
            var answers = name == "thai-menu" ? new[] { "设置", "泰语", "保存", "取消" } : new[] { "你好", "欢迎", "价格 123 泰铢" };
            var blocks = fixture.GetProperty("Grouped").EnumerateArray().Select((b, i) => new TranslatedBlock(
                b.GetProperty("Text").GetString()!, answers[i],
                new Rect(b.GetProperty("X").GetDouble(), b.GetProperty("Y").GetDouble(), b.GetProperty("W").GetDouble(), b.GetProperty("H").GetDouble()),
                RenderGlyphHeight: b.GetProperty("RenderGlyphHeight").GetDouble(),
                BackgroundColor: Colors.White, TextColor: Colors.Black)).ToList();
            var source = new BitmapImage(new Uri(fixture.GetProperty("Path").GetString()!));
            // Opacity zero keeps the test overlay invisible on the user's desktop. Child canvases
            // are still rendered into a PNG below. No screen capture or clipboard access.
            var window = new OverlayWindow(blocks, [], 0, 0, source.PixelWidth, source.PixelHeight, "TH", "ZH-HANS", false)
                { Opacity = 0, ShowInTaskbar = false, ShowActivated = false };
            try
            {
                window.Show();
                window.UpdateLayout();
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.Loaded);
                window.UpdateLayout();
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var dpiX = (double)typeof(OverlayWindow).GetField("_dpiX", flags)!.GetValue(window)!;
                var dpiY = (double)typeof(OverlayWindow).GetField("_dpiY", flags)!.GetValue(window)!;
                var physical = (System.Drawing.Rectangle)typeof(OverlayWindow).GetField("_physBounds", flags)!.GetValue(window)!;
                var viewport = new Rect(-physical.Left / dpiX, -physical.Top / dpiY, source.PixelWidth / dpiX, source.PixelHeight / dpiY);
                var drawing = new DrawingVisual();
                using (var context = drawing.RenderOpen())
                {
                    var destination = new Rect(0, 0, source.PixelWidth, source.PixelHeight);
                    context.DrawImage(source, destination);
                    foreach (var canvasName in new[] { "BubbleBackgroundCanvas", "BubbleTextCanvas" })
                    {
                        var canvas = (Canvas)window.FindName(canvasName);
                        var brush = new VisualBrush(canvas) { ViewboxUnits = BrushMappingMode.Absolute, Viewbox = viewport, Stretch = Stretch.Fill };
                        context.DrawRectangle(brush, null, destination);
                    }
                }
                var rendered = new RenderTargetBitmap(source.PixelWidth, source.PixelHeight, 96, 96, PixelFormats.Pbgra32);
                rendered.Render(drawing);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rendered));
                using (var file = File.Create(Path.Combine(directory, name + "-overlay.png"))) encoder.Save(file);
                var textCanvas = (Canvas)window.FindName("BubbleTextCanvas");
                if (textCanvas.Children.Count != blocks.Count) throw new InvalidOperationException("Overlay block count differs from OCR groups");
                var measurements = textCanvas.Children.Cast<FrameworkElement>().Select((child, i) => new
                {
                    blocks[i].OriginalText, blocks[i].TranslatedText,
                    SourceX = blocks[i].Bounds.X, SourceY = blocks[i].Bounds.Y,
                    OverlayX = Canvas.GetLeft(child) * dpiX + physical.Left,
                    OverlayY = Canvas.GetTop(child) * dpiY + physical.Top,
                    OverlayWidth = child.ActualWidth * dpiX, OverlayHeight = child.ActualHeight * dpiY,
                }).ToArray();
                File.WriteAllText(Path.Combine(directory, name + "-overlay-geometry.json"), JsonSerializer.Serialize(
                    new { DpiX = dpiX, DpiY = dpiY, SyntheticTranslations = true, Blocks = measurements },
                    new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
                Console.WriteLine($"{name}: {blocks.Count} actual overlay bubbles; dpi={dpiX},{dpiY}; saved PNG and measured geometry");
            }
            finally { window.Close(); }
        }
    }
}
