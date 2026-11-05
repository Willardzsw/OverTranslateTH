namespace OverTranslate.Models;

/// <summary>Installed local OCR recognizer choice; no service configuration or credentials.</summary>
public class OcrModelSettings
{
    public string Selection { get; set; } = "language-default";
    public string DetectorSize { get; set; } = "tiny";
    public string RecognizerSize { get; set; } = "small";
    public string Device { get; set; } = "cpu";
    public OcrModelSettings Copy() => new()
    {
        Selection = Selection ?? "language-default",
        DetectorSize = DetectorSize ?? "tiny",
        RecognizerSize = RecognizerSize ?? "small",
        Device = Device ?? "cpu",
    };
}
