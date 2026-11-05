using OverTranslate.Models;
using OverTranslate.Services;
using OverTranslate.Services.Ocr;
using Xunit;

namespace OverTranslate.Tests;

public class OcrPerformanceSettingsTests
{
    [Fact]
    public void OldProfilePreservesCpuAndOriginalModelSizes()
    {
        var settings = SettingsService.Parse("{}").OcrModels;
        Assert.Equal("tiny", settings.DetectorSize);
        Assert.Equal("small", settings.RecognizerSize);
        Assert.Equal("cpu", settings.Device);
    }

    [Fact]
    public void SizesAndDeviceSurviveRoundTripAndCopy()
    {
        var original = new OcrModelSettings
        {
            Selection = OcrModelCatalog.Thai, DetectorSize = "medium", RecognizerSize = "medium", Device = "directml:1",
        };
        var copy = SettingsService.Parse(SettingsService.Serialize(new AppSettings { OcrModels = original })).OcrModels.Copy();
        Assert.Equal(OcrModelCatalog.Identity(original), OcrModelCatalog.Identity(copy));
        Assert.Equal("thai", OcrModelCatalog.LocalModelKey("TH", copy));
        Assert.Equal("korean", OcrModelCatalog.LocalModelKey("KO", new() { RecognizerSize = "medium" }));
    }

    [Theory]
    [InlineData("detector")]
    [InlineData("recognizer")]
    [InlineData("device")]
    public void EveryPerformanceChoiceInvalidatesScreenshotReuse(string changed)
    {
        var settings = new OcrModelSettings();
        var before = OcrModelCatalog.Identity(settings);
        var cache = new CaptureOcrReuse();
        var capture = new object();
        var region = new System.Windows.Rect(0, 0, 320, 180);
        cache.Remember(capture, region, "TH", false, CaptureLayoutMode.Interface,
            [new("old", new System.Windows.Rect(40, 30, 180, 50))], before);
        if (changed == "detector") settings.DetectorSize = "medium";
        if (changed == "recognizer") settings.RecognizerSize = "medium";
        if (changed == "device") settings.Device = "directml:0";
        Assert.False(cache.TryGet(capture, region, "TH", false, CaptureLayoutMode.Interface, out _, OcrModelCatalog.Identity(settings)));
    }

    [Fact]
    public void UnknownChoicesCannotReachPathsOrLoadAnUnknownBackend()
    {
        var settings = SettingsService.Parse("{\"OcrModels\":{\"DetectorSize\":\"../../other\",\"RecognizerSize\":\"large\",\"Device\":\"directml:-1\"}}").OcrModels;
        Assert.Equal("tiny", settings.DetectorSize);
        Assert.Equal("small", settings.RecognizerSize);
        Assert.Equal("cpu", settings.Device);
    }

    [Fact]
    public void CpuDoesNotPrepareExtraAssets()
    {
        Assert.Empty(OcrModelAssets.Required(new()));
        Assert.True(OcrModelAssets.IsPrepared(new()));
    }

    [Fact]
    public void MediumPreparationIncludesMatchingRecognizerDictionary()
    {
        Assert.Equal(new[] { "det-medium", "rec-medium", "dict-medium" },
            OcrModelAssets.Required(new() { DetectorSize = "medium", RecognizerSize = "medium" }).Select(f => f.Id));
    }

    [Theory]
    [InlineData(OcrModelCatalog.Thai)]
    [InlineData(OcrModelCatalog.Korean)]
    public void DedicatedRecognizersDoNotPrepareTheGeneralMediumModel(string selection)
    {
        Assert.Equal(new[] { "det-medium" }, OcrModelAssets.Required(new()
        {
            Selection = selection, DetectorSize = "medium", RecognizerSize = "medium",
        }).Select(f => f.Id));
    }

    [Fact]
    public void AssetValidationRejectsCorruption()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ocr-asset-test-" + Guid.NewGuid());
        System.IO.File.WriteAllText(root, "test");
        try
        {
            const string hash = "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";
            Assert.True(OcrModelAssets.Valid(root, 4, hash));
            System.IO.File.WriteAllText(root, "fail");
            Assert.False(OcrModelAssets.Valid(root, 4, hash));
        }
        finally { System.IO.File.Delete(root); }
    }

    [Fact]
    public void UnavailableExplicitGpuThrowsInsteadOfSilentlyUsingCpu()
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            using var options = OcrExecution.Options(new() { Device = "directml:2147483647" }, 1, out _);
        });
        using var cpu = OcrExecution.Options(new(), 1, out var device);
        Assert.Equal("CPU", device);
    }
}
