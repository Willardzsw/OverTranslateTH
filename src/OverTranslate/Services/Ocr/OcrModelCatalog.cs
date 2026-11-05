using OverTranslate.Models;

namespace OverTranslate.Services.Ocr;

internal sealed record OcrModelOption(string Id, string NameKey)
{
    public string Display => LocalizationService.Get(NameKey);
}

internal static class OcrModelCatalog
{
    internal const string ByLanguage = "language-default";
    internal const string General = "ppocr-general";
    internal const string Thai = "ppocr-thai";
    internal const string Korean = "ppocr-korean";

    internal static readonly OcrModelOption[] Options =
    [
        new(ByLanguage, "S.OcrModel.ByLanguage"),
        new(General, "S.OcrModel.General"),
        new(Thai, "S.OcrModel.Thai"),
        new(Korean, "S.OcrModel.Korean"),
    ];

    internal static string Normalize(string? choice) =>
        Options.FirstOrDefault(o => o.Id.Equals(choice?.Trim(), StringComparison.OrdinalIgnoreCase))?.Id ?? ByLanguage;

    internal static string LocalModelKey(string language, OcrModelSettings settings) => Normalize(settings.Selection) switch
    {
        General => "cjk",
        Thai => "thai",
        Korean => "korean",
        _ => OnnxOcrEngine.GetModelKeyForLanguage(language),
    };

    internal static void Validate(OcrModelSettings settings, string language, bool vertical)
    {
        var lang = OcrLanguageRouter.Normalize(language);
        var valid = Normalize(settings.Selection) switch
        {
            General => lang is "AUTO" or "EN" or "ZH" or "ZH-HANT" or "JA",
            Thai => lang is "TH" or "EN",
            Korean => lang == "KO",
            _ => true,
        };
        if (!valid) throw new NotSupportedException(LocalizationService.Get("S.OcrModel.Incompatible"));
    }

    internal static string DetectorSize(OcrModelSettings settings) =>
        settings.DetectorSize is "small" or "medium" ? settings.DetectorSize : "tiny";

    internal static string RecognizerSize(OcrModelSettings settings) =>
        settings.RecognizerSize == "medium" ? "medium" : "small";

    internal static string Device(OcrModelSettings settings) =>
        settings.Device?.StartsWith("directml:", StringComparison.Ordinal) == true &&
        int.TryParse(settings.Device.AsSpan(9), out var index) && index >= 0
            ? $"directml:{index}" : "cpu";

    internal static string Identity(OcrModelSettings settings) =>
        $"{Normalize(settings.Selection)}|{DetectorSize(settings)}|{RecognizerSize(settings)}|{Device(settings)}";
}
