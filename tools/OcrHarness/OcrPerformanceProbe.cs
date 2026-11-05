using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Microsoft.ML.OnnxRuntime;
using OverTranslate.Models;
using OverTranslate.Services;
using OverTranslate.Services.Ocr;

/// <summary>Offline verification on synthetic/public fixtures only; never loads settings or calls translation.</summary>
internal static class OcrPerformanceProbe
{
    internal static async Task<int> Run(string[] args)
    {
        var output = Path.GetFullPath(args.FirstOrDefault() ?? ".test-artifacts/ocr-performance/results");
        Directory.CreateDirectory(output);
        NLog.LogManager.Configuration = new NLog.Config.LoggingConfiguration();
        using var baseline = JsonDocument.Parse(File.ReadAllText(".test-artifacts/thai-ocr/report.json"));
        var fixtures = baseline.RootElement.GetProperty("Results").EnumerateArray()
            .Where(f => f.GetProperty("Expected").GetArrayLength() > 0).ToArray();
        var gpu = OcrExecution.Devices().FirstOrDefault(d => d.Id != "cpu");
        if (args.Contains("--switch-only")) return await CheckSwitches(output, fixtures, gpu?.Id);
        var scenarios = new List<(string Name, OcrModelSettings Settings)>
        {
            ("default-cpu", new()),
            ("small-det-cpu", new() { DetectorSize = "small" }),
            ("medium-det-cpu", new() { DetectorSize = "medium" }),
            ("medium-rec-cpu", new() { RecognizerSize = "medium" }),
        };
        if (gpu is not null)
        {
            scenarios.Add(("default-gpu", new() { Device = gpu.Id }));
            scenarios.Add(("small-det-gpu", new() { DetectorSize = "small", Device = gpu.Id }));
            scenarios.Add(("medium-gpu", new() { DetectorSize = "medium", RecognizerSize = "medium", Device = gpu.Id }));
        }
        if (args.Contains("--gpu-only")) scenarios.RemoveAll(s => s.Settings.Device == "cpu");
        var reports = new List<object>();
        var errors = 0;
        foreach (var (name, settings) in scenarios)
        {
            Console.WriteLine($"Starting {name}");
            var profileDir = Path.Combine(output, name);
            Directory.CreateDirectory(profileDir);
            var profiled = new HashSet<InferenceSession>();
            OcrExecution.ProfilePrefix = Path.Combine(profileDir, "ort");
            try
            {
                await OcrModelAssets.PrepareAsync(settings);
                using (var service = new OcrService(() => settings))
                {
                    foreach (var fixture in fixtures)
                    {
                        var lang = fixture.GetProperty("Language").GetString()!;
                        using var image = new Bitmap(fixture.GetProperty("Path").GetString()!);
                        var watch = Stopwatch.StartNew();
                        var blocks = await service.RecognizeAsync(image, lang, layoutMode: CaptureLayoutMode.Interface);
                        var coldMs = watch.ElapsedMilliseconds;
                        watch.Restart();
                        blocks = await service.RecognizeAsync(image, lang, layoutMode: CaptureLayoutMode.Interface);
                        var warmMs = watch.ElapsedMilliseconds;
                        SaveProfiles(service, profileDir, profiled);
                        var text = string.Concat(blocks.Select(b => b.Text)).Replace(" ", "");
                        var missing = fixture.GetProperty("Expected").EnumerateArray().Select(e => e.GetString()!)
                            .Where(e => !text.Contains(e.Replace(" ", ""))).ToArray();
                        var invalidBounds = blocks.Any(b => b.Bounds.Left < 0 || b.Bounds.Top < 0 ||
                            b.Bounds.Right > image.Width || b.Bounds.Bottom > image.Height || !double.IsFinite(b.Bounds.Width));
                        if (invalidBounds) errors++;
                        reports.Add(new { Scenario = name, Fixture = fixture.GetProperty("Name").GetString(), Language = lang,
                            SyntheticOrPublic = true, ColdMilliseconds = coldMs, WarmMilliseconds = warmMs,
                            Device = service.ExecutionDevice, Known = fixture.GetProperty("Expected").GetArrayLength(),
                            Missing = missing, InvalidBounds = invalidBounds,
                            Blocks = blocks.Select(b => new { b.Text, b.Bounds.X, b.Bounds.Y, b.Bounds.Width, b.Bounds.Height }) });
                        Console.WriteLine($"{name}/{fixture.GetProperty("Name").GetString()}: missing={missing.Length}, cold={coldMs}, warm={warmMs}, {service.ExecutionDevice}");
                    }
                    // Exercise overlapping captures: DirectML must serialize all calls on each session.
                    var tasks = Enumerable.Range(0, 3).Select(async _ =>
                    {
                        using var image = new Bitmap(fixtures[0].GetProperty("Path").GetString()!);
                        return await service.RecognizeAsync(image, "TH", layoutMode: CaptureLayoutMode.Interface);
                    });
                    var concurrent = await Task.WhenAll(tasks);
                    if (concurrent.Any(b => b.Count == 0)) throw new InvalidOperationException("Concurrent capture lost text.");
                }
                var providers = Directory.GetFiles(profileDir, "*.json").SelectMany(file =>
                {
                    using var profile = JsonDocument.Parse(File.ReadAllText(file));
                    return profile.RootElement.EnumerateArray().Where(e => e.TryGetProperty("args", out var a) && a.TryGetProperty("provider", out _))
                        .Select(e => e.GetProperty("args").GetProperty("provider").GetString()!).ToArray();
                }).GroupBy(p => p).ToDictionary(g => g.Key, g => g.Count());
                File.WriteAllText(Path.Combine(profileDir, "providers-summary.json"), JsonSerializer.Serialize(providers));
                if (settings.Device != "cpu" && !providers.ContainsKey("DmlExecutionProvider"))
                    throw new InvalidOperationException("GPU selected but no DirectML kernel executions were profiled.");
                Console.WriteLine($"{name} actual providers: {JsonSerializer.Serialize(providers)}");
            }
            catch (Exception ex)
            {
                errors++;
                reports.Add(new { Scenario = name, Error = ex.ToString(), SyntheticOrPublic = true });
                Console.WriteLine($"{name}: {ex.Message}");
            }
            finally
            {
                OcrExecution.ProfilePrefix = null;
                File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(reports, new JsonSerializerOptions { WriteIndented = true }));
            }
        }
        // The dictionary must be the one embedded in the new recognizer, with unchanged character order.
        using (var session = new InferenceSession(OcrModelAssets.ModelPath("rec-medium")))
        {
            var embedded = session.ModelMetadata.CustomMetadataMap["character"].TrimEnd('\r', '\n').Split('\n');
            var dictionary = File.ReadAllLines(OcrModelAssets.ModelPath("dict-medium"));
            if (!embedded.SequenceEqual(dictionary)) errors++;
            File.WriteAllText(Path.Combine(output, "medium-dictionary.json"), JsonSerializer.Serialize(new
            {
                MatchesMetadata = embedded.SequenceEqual(dictionary), Entries = dictionary.Length,
                Output = session.OutputMetadata.Select(p => new { p.Key, p.Value.Dimensions }),
            }));
        }
        Console.WriteLine($"Offline performance verification errors: {errors}");
        return errors == 0 ? 0 : 1;
    }

    private static void SaveProfiles(OcrService service, string directory, HashSet<InferenceSession> seen)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var engine = (OnnxOcrEngine)typeof(OcrService).GetField("_engine", flags)!.GetValue(service)!;
        var runtime = (OnnxOcrEngine.RapidOcrRuntime)typeof(OnnxOcrEngine).GetField("_current", flags)!.GetValue(engine)!;
        foreach (var component in runtime.Engine.GetType().GetFields(flags))
        {
            if (component.GetValue(runtime.Engine) is not { } owner) continue;
            foreach (var field in owner.GetType().GetFields(flags))
            {
                if (field.GetValue(owner) is not InferenceSession session || !seen.Add(session)) continue;
                var path = session.EndProfiling();
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    File.Move(path, Path.Combine(directory, component.Name + "-" + Guid.NewGuid().ToString("N") + ".json"));
            }
        }
    }

    private static async Task<int> CheckSwitches(string directory, JsonElement[] fixtures, string? gpu)
    {
        OcrExecution.ProfilePrefix = null;
        var settings = new OcrModelSettings();
        using var service = new OcrService(() => settings);
        var reports = new List<object>();
        foreach (var (device, detector, recognizer, language) in new[]
        {
            ("cpu", "tiny", "small", "TH"), (gpu ?? "cpu", "tiny", "small", "TH"),
            (gpu ?? "cpu", "small", "small", "TH"), (gpu ?? "cpu", "medium", "medium", "TH"),
            (gpu ?? "cpu", "medium", "medium", "EN"), ("cpu", "tiny", "medium", "EN"),
            ("cpu", "tiny", "small", "TH"),
        })
        {
            settings.Device = device;
            settings.DetectorSize = detector;
            settings.RecognizerSize = recognizer;
            var fixture = fixtures.First(f => f.GetProperty("Language").GetString() == language);
            using var bitmap = new Bitmap(fixture.GetProperty("Path").GetString()!);
            var blocks = await service.RecognizeAsync(bitmap, language, layoutMode: CaptureLayoutMode.Interface);
            var text = string.Concat(blocks.Select(b => b.Text)).Replace(" ", "");
            var missing = fixture.GetProperty("Expected").EnumerateArray().Select(e => e.GetString()!)
                .Where(e => !text.Contains(e.Replace(" ", ""))).ToArray();
            if (missing.Length != 0 || service.ExecutionDevice!.StartsWith("DirectML") != (device != "cpu"))
                throw new InvalidOperationException("Runtime switch did not apply.");
            reports.Add(new { device, detector, recognizer, language, ActualDevice = service.ExecutionDevice, Missing = missing, SyntheticOrPublic = true });
            Console.WriteLine($"Switch {language}/{detector}/{recognizer}: {service.ExecutionDevice}");
        }
        settings.Device = "directml:2147483647";
        using var image = new Bitmap(fixtures[0].GetProperty("Path").GetString()!);
        try
        {
            await service.RecognizeAsync(image, "TH");
            throw new Exception("Unavailable GPU silently became CPU.");
        }
        catch (InvalidOperationException) { Console.WriteLine("Unavailable GPU explicitly rejected."); }
        settings.Device = "cpu";
        if ((await service.RecognizeAsync(image, "TH")).Count == 0) throw new Exception("CPU recovery failed.");
        File.WriteAllText(Path.Combine(directory, "report.json"), JsonSerializer.Serialize(new
        {
            SyntheticOrPublic = true, ProfileEnabled = false, Switches = reports, InvalidGpuRejected = true, CpuRecovery = true,
        }, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
}
