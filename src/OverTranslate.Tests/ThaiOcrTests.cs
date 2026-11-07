using System.Net.Http;
using System.Windows;
using OverTranslate.Models;
using OverTranslate.Services;
using OverTranslate.Services.Ocr;
using OverTranslate.Services.Providers;
using RapidOcrNet;
using SkiaSharp;
using Xunit;

namespace OverTranslate.Tests;

public class ThaiOcrTests
{
    [Theory]
    [InlineData("TH")]
    [InlineData(" th ")]
    public void Thai_IsARealOcrSourceWithItsOwnRecognizer(string code)
    {
        Assert.True(OcrLanguageRouter.IsSupported(code));
        Assert.Equal("thai", OnnxOcrEngine.GetModelKeyForLanguage(code));
        Assert.False(OcrLanguageRouter.UsesCjkOnnx(code));
        Assert.Equal("TH", LanguageData.GetValidOcrSourceCode(code.Trim()));
        Assert.Contains(LanguageData.OcrSourceLanguages, l => l.Code == "TH");
        Assert.Equal("th", LanguageData.GetModelLanguageTag("TH"));
    }

    [Fact]
    public void Automatic_RemainsTheExistingCjkRecognizer()
    {
        Assert.Equal("cjk", OnnxOcrEngine.GetModelKeyForLanguage("AUTO"));
        Assert.Equal("korean", OnnxOcrEngine.GetModelKeyForLanguage("KO"));
    }

    [Theory]
    [InlineData("สวัสดีครับ")]
    [InlineData("น้ำดื่ม ๑๒๓ บาท")]
    [InlineData("ตั้งค่า (Settings)!")]
    public void ThaiCombiningMarksDigitsAndPunctuation_SurviveTheRealFilterChain(string text)
    {
        var raw = new TextBlock
        {
            Text = text,
            Chars = text.Select(c => c.ToString()).ToArray(),
            CharScores = Enumerable.Repeat(0.99f, text.Length).ToArray(),
            BoxPoints = [new SKPointI(40, 50), new SKPointI(440, 50), new SKPointI(440, 110), new SKPointI(40, 110)],
        };
        var blocks = OnnxOcrEngine.ApplyBlockFilters([raw], "TH", false, false);
        var block = Assert.Single(blocks);
        Assert.Equal(text, block.Text);
        Assert.Equal(new Rect(40, 50, 400, 60), block.LayoutBounds);
        Assert.Equal(new Rect(40, 50, 400, 60), block.Bounds);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Thai_GoesThroughTheExistingOpenAiProviderWithoutLosingOverlayCoordinates(bool batch)
    {
        var server = new OpenAiFakeServer
        {
            Answer = text => batch
                ? "<1>设置</1>\n<2>取消</2>"
                : text.Contains("ตั้งค่า") ? "设置" : "取消",
        };
        using var http = new HttpClient(server);
        var provider = new OpenAiCompatibleProvider(http,
            () => new OpenAiCompatibleOptions("http://localhost:1234/v1", "offline-test-model", Batch: batch));
        var input = new List<OcrTextBlock>
        {
            new("ตั้งค่า", new Rect(50, 60, 180, 45), [new Rect(50, 60, 180, 45)], 28),
            new("ยกเลิก", new Rect(560, 230, 200, 50), [new Rect(560, 230, 200, 50)], 30),
        };
        var (translated, _) = await provider.TranslateAsync(input, "TH", "ZH-HANS", "");
        Assert.Equal(["设置", "取消"], translated.Select(b => b.TranslatedText));
        Assert.Equal(input.Select(b => b.Bounds), translated.Select(b => b.Bounds));
        Assert.Equal(input.Select(b => b.SourceLineBounds), translated.Select(b => b.SourceLineBounds));
        Assert.Equal(input.Select(b => b.RenderGlyphHeight), translated.Select(b => b.RenderGlyphHeight));
        Assert.Contains("ตั้งค่า", server.Requests.First().Message);
        Assert.All(server.Requests, r => Assert.Null(r.Authorization));
    }

    [Fact]
    public void EdgeCropOverlayQuad_IsClippedToTheSourceImage()
    {
        SKPointI[] points = [new(-2, -1), new(1296, 0), new(1296, 176), new(0, 176)];
        OnnxOcrEngine.ClampToImage(points, 1280, 172);
        Assert.Equal(new SKPointI[] { new(0, 0), new(1280, 0), new(1280, 172), new(0, 172) }, points);
    }

    [Fact]
    public void DevelopmentProfile_DoesNotUseTheInstalledAppsProfileOrAssets()
    {
        Assert.Equal(System.IO.Path.Combine(AppContext.BaseDirectory, "profile-thai"), AppDataPaths.Root);
        Assert.Equal(System.IO.Path.Combine(AppDataPaths.Root, "assets"), AppDataPaths.DefaultAssetsRoot);
        Assert.Equal("TextTack", StartupService.AppName);
        Assert.Equal(new[] { "TTTools", "OverTranslate-Thai-Dev" }, StartupService.LegacyAppNames);
    }
}
