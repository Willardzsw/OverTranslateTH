using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OverTranslate.Services;
using Xunit;

namespace OverTranslate.Tests;

public class ScreenshotSaveTests
{
    [Fact]
    public async Task SimultaneousSavesWithTheSameTimestampKeepEveryImageAndExistingFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "overtranslate-save-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var time = new DateTime(2026, 11, 4, 12, 0, 0);
            var existing = Path.Combine(directory, "OverTranslate_20261104_120000000.png");
            byte[] original = [1, 2, 3, 4];
            File.WriteAllBytes(existing, original);
            var image = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null,
                new byte[] { 10, 20, 30, 255 }, 4);
            image.Freeze();
            var paths = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
                Task.Run(() => ScreenshotSaveService.Save(image, directory, time))));
            Assert.Equal(8, paths.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.Equal(original, File.ReadAllBytes(existing));
            foreach (var path in paths)
            {
                using var stream = File.OpenRead(path);
                var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                Assert.Equal(1, decoder.Frames[0].PixelWidth);
            }
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
