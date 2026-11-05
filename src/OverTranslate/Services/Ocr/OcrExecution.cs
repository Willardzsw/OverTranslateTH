using Microsoft.ML.OnnxRuntime;
using System.Reflection;
using OverTranslate.Models;
using RapidOcrNet;

namespace OverTranslate.Services.Ocr;

internal static class OcrExecution
{
    internal sealed record DeviceOption(string Id, string Display);
    internal static event EventHandler? Changed;
    internal static string? LastIdentity { get; private set; }
    internal static string? LastDevice { get; private set; }
    // Offline harness only; the application never enables profiling or records screen contents here.
    internal static string? ProfilePrefix { get; set; }

    internal static IReadOnlyList<DeviceOption> Devices()
    {
        var result = new List<DeviceOption> { new("cpu", "CPU") };
        try
        {
            result.AddRange(DirectMlDevice.Enumerate().Where(a => !DirectMlDevice.IsSoftware(a))
                .OrderByDescending(a => a.DedicatedMemory)
                .Select(a => new DeviceOption($"directml:{a.Index}", $"DirectML · {a.Name}")));
        }
        catch { /* CPU remains available on machines without DXGI. */ }
        return result;
    }

    internal static SessionOptions Options(OcrModelSettings settings, int threads, out string device)
    {
        var options = RapidOcr.GetDefaultSessionOptions(threads);
        device = "CPU";
        if (OcrModelCatalog.Device(settings) == "cpu") return options;
        try
        {
            var index = int.Parse(OcrModelCatalog.Device(settings).AsSpan(9));
            var adapter = DirectMlDevice.Enumerate().Single(a => a.Index == index && !DirectMlDevice.IsSoftware(a));
            if (DirectMlDevice.Direct3D12Problem(adapter) is { } incompatible)
                throw new NotSupportedException(incompatible);
            if (DirectMlDevice.Load(OcrModelAssets.RuntimePath, OcrModelAssets.RuntimeFile) is { } missing)
                throw new InvalidOperationException(LocalizationService.Get("S.OcrModel.PrepareNeeded") + " " + missing);
            options.EnableMemoryPattern = false;
            options.ExecutionMode = ExecutionMode.ORT_SEQUENTIAL;
            options.LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_ERROR;
            options.AppendExecutionProvider_DML(index);
            device = $"DirectML · {adapter.Name}";
            return options;
        }
        catch
        {
            options.Dispose();
            // An explicit GPU choice must not silently become a CPU session.
            throw;
        }
    }

    internal static void Initialize(RapidOcr engine, RapidOcrModelSet models, SessionOptions options)
    {
        if (ProfilePrefix is not { } prefix)
        {
            engine.InitModels(models, options);
            return;
        }
        // Every session needs its own file: ORT timestamps have one-second resolution. Reusing
        // the same options' prefix across det/cls/rec otherwise overwrites profiles or locks files.
        void Profile(string role)
        {
            options.EnableProfiling = false;
            options.ProfileOutputPathPrefix = prefix + "-" + role + "-" + Guid.NewGuid().ToString("N");
            options.EnableProfiling = true;
        }
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        Profile("det");
        ((TextDetector)typeof(RapidOcr).GetField("_textDetector", flags)!.GetValue(engine)!)
            .InitModel(models.DetModelPath, models.DetMean, models.DetStd, options);
        Profile("cls");
        ((TextClassifier)typeof(RapidOcr).GetField("_textClassifier", flags)!.GetValue(engine)!)
            .InitModel(models.ClsModelPath, options);
        Profile("rec");
        ((TextRecognizer)typeof(RapidOcr).GetField("_textRecognizer", flags)!.GetValue(engine)!)
            .InitModel(models.RecModelPath, models.KeysPath, options);
    }

    internal static void Report(string identity, string device)
    {
        LastIdentity = identity;
        LastDevice = device;
        Changed?.Invoke(null, EventArgs.Empty);
    }
}
