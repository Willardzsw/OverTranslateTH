using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Text.Json;
using Microsoft.ML.OnnxRuntime;
using OverTranslate.Services;
using OverTranslate.Services.Ocr;

/// <summary>Offline only. Never loads translation credentials or sends screen content anywhere.</summary>
internal static class ThaiOcrProbe
{
    private sealed record Fixture(string Name, string Language, string[] Expected, string Path);
    public static async Task<int> Run(string[] args)
    {
        var output = Path.GetFullPath(args.Length > 0 ? args[0] : ".test-artifacts/thai-ocr");
        Directory.CreateDirectory(output);
        NLog.LogManager.Configuration = new NLog.Config.LoggingConfiguration();
        var fixtures = new List<Fixture>();
        foreach (var (name, language, texts, font) in new[]
        {
            ("thai-menu", "TH", new[] { "ตั้งค่า", "ภาษาไทย", "บันทึก", "ยกเลิก" }, "Tahoma"),
            ("thai-sentence", "TH", new[] { "สวัสดีครับ", "ยินดีต้อนรับ", "ราคา ๑๒๓ บาท" }, "Tahoma"),
            ("english", "EN", new[] { "Settings", "Save changes", "Cancel" }, "Segoe UI"),
            ("japanese", "JA", new[] { "設定を変更します", "保存", "キャンセル" }, "Yu Gothic"),
            ("chinese", "ZH", new[] { "设置", "保存更改", "取消" }, "Microsoft YaHei"),
            ("korean", "KO", new[] { "설정", "저장", "취소" }, "Malgun Gothic"),
        })
        {
            var path = Path.Combine(output, name + ".png");
            using var image = new Bitmap(1050, 420);
            using var graphics = Graphics.FromImage(image);
            graphics.Clear(Color.White);
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using var typeface = new Font(font, 38, FontStyle.Regular, GraphicsUnit.Pixel);
            for (var i = 0; i < texts.Length; i++)
            {
                // Separate UI rows with enough space that they must stay independent.
                var x = name == "thai-menu" && i % 2 == 1 ? 560 : 50;
                var y = name == "thai-menu" ? 50 + i / 2 * 170 : 40 + i * 115;
                graphics.DrawString(texts[i], typeface, Brushes.Black, x, y);
            }
            image.Save(path, ImageFormat.Png);
            fixtures.Add(new Fixture(name, language, texts, path));
        }
        foreach (var (name, size, dark) in new[] { ("thai-small", 22, false), ("thai-dark", 30, true) })
        {
            var texts = new[] { "น้ำดื่ม (Water) 25 บาท", "วันนี้อากาศดีมาก", "ไปด้วยกันไหม?" };
            var path = Path.Combine(output, name + ".png");
            using var image = new Bitmap(1050, 420);
            using var graphics = Graphics.FromImage(image);
            graphics.Clear(dark ? Color.FromArgb(30, 30, 30) : Color.White);
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using var typeface = new Font("Tahoma", size, FontStyle.Regular, GraphicsUnit.Pixel);
            for (var i = 0; i < texts.Length; i++)
                graphics.DrawString(texts[i], typeface, dark ? Brushes.White : Brushes.Black, 50, 40 + i * 115);
            image.Save(path, ImageFormat.Png);
            fixtures.Add(new Fixture(name, "TH", texts, path));
        }
        for (var i = 1; i < args.Length; i++)
            fixtures.Add(new Fixture(Path.GetFileNameWithoutExtension(args[i]), "TH",
                Path.GetFileName(args[i]).Contains("crop") ? new[] { "อานุภาพทำลายล้างสูง" } : [], Path.GetFullPath(args[i])));

        var model = Path.Combine(AppContext.BaseDirectory, "ocrmodels", "onnx", "thai", "rec.onnx");
        var dictionary = File.ReadAllLines(Path.Combine(Path.GetDirectoryName(model)!, "dict.txt"));
        object modelInfo;
        using (var session = new InferenceSession(model))
        {
            var metadata = session.ModelMetadata.CustomMetadataMap;
            var chars = metadata.TryGetValue("character", out var character) ? character.TrimEnd('\r', '\n').Split('\n') : [];
            modelInfo = new
            {
                Input = session.InputMetadata.Select(p => new { p.Key, p.Value.Dimensions }),
                Output = session.OutputMetadata.Select(p => new { p.Key, p.Value.Dimensions }),
                MetadataKeys = metadata.Keys,
                EmbeddedDictionaryTail = chars.TakeLast(3).Select(c => string.Join(",", c.Select(ch => ((int)ch).ToString("X4")))),
                DictionaryEntries = dictionary.Length,
                EmbeddedDictionaryEntries = chars.Length,
                DictionaryMatchesMetadata = chars.Length == 0 ? (bool?)null : chars.SequenceEqual(dictionary),
            };
        }
        var results = new List<object>();
        var failures = 0;
        using var engine = new OnnxOcrEngine();
        using var service = new OcrService();
        foreach (var fixture in fixtures)
        {
            using var image = new Bitmap(fixture.Path);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var raw = await engine.RecognizeAsync(image, fixture.Language);
            var grouped = await service.RecognizeAsync(image, fixture.Language, layoutMode: CaptureLayoutMode.Interface);
            var realtime = await service.TryRecognizeAsync(image, fixture.Language, maxDetectSize: 1050);
            var actual = string.Concat(raw.Select(b => b.Text)).Replace(" ", "");
            var missing = fixture.Expected.Where(e => !actual.Contains(e.Replace(" ", ""))).ToArray();
            var invalidBounds = grouped.Any(b => !double.IsFinite(b.Bounds.X) || b.Bounds.Width <= 0 || b.Bounds.Height <= 0
                || b.Bounds.X < -5 || b.Bounds.Y < -5 || b.Bounds.Right > image.Width + 5 || b.Bounds.Bottom > image.Height + 5);
            if (missing.Length > 0 || raw.Count == 0 || invalidBounds) failures++;
            using var annotated = new Bitmap(image);
            using var graphics = Graphics.FromImage(annotated);
            using var pen = new Pen(Color.Red, 2);
            foreach (var block in raw)
                graphics.DrawRectangle(pen, (float)block.Bounds.X, (float)block.Bounds.Y, (float)block.Bounds.Width, (float)block.Bounds.Height);
            annotated.Save(Path.Combine(output, fixture.Name + "-boxes.png"), ImageFormat.Png);
            object Dump(OcrTextBlock b) => new { b.Text, X = b.Bounds.X, Y = b.Bounds.Y,
                W = b.Bounds.Width, H = b.Bounds.Height, b.Confidence, b.RenderGlyphHeight, Lines = b.Lines.Count };
            results.Add(new { fixture.Name, fixture.Language, fixture.Path, fixture.Expected,
                Missing = missing, InvalidBounds = invalidBounds, Raw = raw.Select(Dump),
                Grouped = grouped.Select(Dump), Realtime = realtime?.Select(Dump), Milliseconds = watch.ElapsedMilliseconds });
            Console.WriteLine($"{fixture.Name}: {raw.Count} raw / {grouped.Count} grouped; missing=[{string.Join(", ", missing)}]; invalidBounds={invalidBounds}");
            foreach (var block in raw) Console.WriteLine($"  {block.Text}  [{block.Bounds}] confidence={block.Confidence:0.000}");
        }
        var report = Path.Combine(output, "report.json");
        File.WriteAllText(report, JsonSerializer.Serialize(new { Model = modelInfo, Failures = failures, Results = results },
            new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        Console.WriteLine($"report={report}; failures={failures}");
        return failures == 0 ? 0 : 1;
    }
}
