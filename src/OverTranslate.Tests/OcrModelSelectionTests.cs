using System.Windows;
using OverTranslate.Models;
using OverTranslate.Services;
using OverTranslate.Services.Ocr;
using Xunit;

namespace OverTranslate.Tests;

public class OcrModelSelectionTests
{
    [Theory]
    [InlineData("AUTO", "cjk")]
    [InlineData("EN", "cjk")]
    [InlineData("JA", "cjk")]
    [InlineData("ZH", "cjk")]
    [InlineData("ZH-HANT", "cjk")]
    [InlineData("KO", "korean")]
    [InlineData("TH", "thai")]
    public void DefaultChoice_PreservesOriginalLanguageRouting(string language, string model) =>
        Assert.Equal(model, OcrModelCatalog.LocalModelKey(language, new OcrModelSettings()));

    [Theory]
    [InlineData(OcrModelCatalog.General, "EN", "cjk")]
    [InlineData(OcrModelCatalog.Thai, "TH", "thai")]
    [InlineData(OcrModelCatalog.Korean, "KO", "korean")]
    public void ExplicitChoices_ReachTheCorrespondingRuntime(string choice, string language, string model) =>
        Assert.Equal(model, OcrModelCatalog.LocalModelKey(language, new() { Selection = choice }));

    [Theory]
    [InlineData(OcrModelCatalog.General, "TH")]
    [InlineData(OcrModelCatalog.Thai, "JA")]
    [InlineData(OcrModelCatalog.Korean, "ZH")]
    public void IncompatibleChoices_AreExplicitlyRejected(string choice, string language) =>
        Assert.Throws<NotSupportedException>(() => OcrModelCatalog.Validate(new() { Selection = choice }, language, false));

    [Fact]
    public void LocalChoiceSurvivesSettingsRoundTrip()
    {
        var settings = new AppSettings { OcrModels = new() { Selection = OcrModelCatalog.Thai } };
        Assert.Equal(OcrModelCatalog.Thai, SettingsService.Parse(SettingsService.Serialize(settings)).OcrModels.Selection);
        Assert.Equal(OcrModelCatalog.ByLanguage, SettingsService.Parse("{}").OcrModels.Selection);
    }

    [Fact]
    public void RemovedServiceChoiceAndFieldsCannotReactivateANetworkBackend()
    {
        var loaded = SettingsService.Parse("{\"OcrModels\":{\"Selection\":\"typhoon-local\",\"TyphoonBaseUrl\":\"http://127.0.0.1:8000/v1\",\"TyphoonModel\":\"old\"}}");
        Assert.Equal(OcrModelCatalog.ByLanguage, loaded.OcrModels.Selection);
        Assert.Equal("thai", OcrModelCatalog.LocalModelKey("TH", loaded.OcrModels));
        Assert.DoesNotContain("Typhoon", SettingsService.Serialize(loaded));
        Assert.Equal(new[] { OcrModelCatalog.ByLanguage, OcrModelCatalog.General, OcrModelCatalog.Thai, OcrModelCatalog.Korean },
            OcrModelCatalog.Options.Select(o => o.Id));
    }

    [Fact]
    public void ChangingModelInvalidatesTheScreenshotReuse()
    {
        var cache = new CaptureOcrReuse();
        var capture = new object();
        var region = new Rect(0, 0, 320, 180);
        var settings = new OcrModelSettings();
        var identity = OcrModelCatalog.Identity(settings);
        cache.Remember(capture, region, "TH", false, CaptureLayoutMode.Interface,
            [new("old", new Rect(40, 30, 180, 50))], identity);
        Assert.True(cache.TryGet(capture, region, "TH", false, CaptureLayoutMode.Interface, out _, identity));
        settings.Selection = OcrModelCatalog.Thai;
        Assert.False(cache.TryGet(capture, region, "TH", false, CaptureLayoutMode.Interface, out _, OcrModelCatalog.Identity(settings)));
    }
}
