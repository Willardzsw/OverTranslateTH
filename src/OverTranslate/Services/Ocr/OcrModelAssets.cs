using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using OverTranslate.Models;
using OverTranslate.Services.Ocr.Manga;

namespace OverTranslate.Services.Ocr;

/// <summary>Explicit preparation of pinned OCR assets in the independent profile. No startup downloads.</summary>
internal static class OcrModelAssets
{
    internal sealed record Asset(string Id, string Name, string Url, string Sha256, long Size);
    private sealed record Manifest(Asset[] Files);
    internal static string Root => Path.Combine(AppDataPaths.Models, "ppocrv6");
    internal static string RuntimePath => Path.Combine(AppDataPaths.Runtimes, "directml", "1.15.4", "DirectML.dll");
    internal static readonly MangaModelFile RuntimeFile = new("directml", "DirectML.dll", 18527776,
        "9c9e6d822561c6c41b90e6994b3e8857cf1d66dbfb1e0c4c799c7c89b4e92da1");
    private const string RuntimeUrl = "https://api.nuget.org/v3-flatcontainer/microsoft.ai.directml/1.15.4/microsoft.ai.directml.1.15.4.nupkg";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };
    private static readonly SemaphoreSlim Preparation = new(1, 1);
    private static readonly Lazy<Asset[]> Catalog = new(() => JsonSerializer.Deserialize<Manifest>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ocrmodels", "ppocrv6-performance.json")),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!.Files);

    internal static Asset[] Required(OcrModelSettings settings) => Catalog.Value.Where(f =>
        f.Id == "det-" + OcrModelCatalog.DetectorSize(settings) ||
        OcrModelCatalog.Normalize(settings.Selection) is not (OcrModelCatalog.Thai or OcrModelCatalog.Korean) &&
        OcrModelCatalog.RecognizerSize(settings) == "medium" && f.Id is "rec-medium" or "dict-medium").ToArray();

    internal static string ModelPath(string id)
    {
        var asset = Catalog.Value.Single(f => f.Id == id);
        var path = Path.Combine(Root, asset.Name);
        if (!Valid(path, asset.Size, asset.Sha256))
            throw new InvalidOperationException(LocalizationService.Get("S.OcrModel.PrepareNeeded"));
        return path;
    }

    internal static bool IsPrepared(OcrModelSettings settings) =>
        Required(settings).All(f => Valid(Path.Combine(Root, f.Name), f.Size, f.Sha256)) &&
        (OcrModelCatalog.Device(settings) == "cpu" || Valid(RuntimePath, RuntimeFile.Size, RuntimeFile.Sha256));

    internal static async Task PrepareAsync(OcrModelSettings settings, IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await Preparation.WaitAsync(cancellationToken);
        try
        {
            foreach (var asset in Required(settings))
            {
                var path = Path.Combine(Root, asset.Name);
                if (Valid(path, asset.Size, asset.Sha256)) continue;
                progress?.Report(asset.Name);
                await InstallAsync(path, asset.Size, asset.Sha256,
                    stream => DownloadAssetAsync(Http, asset.Url, stream, asset.Size, cancellationToken), cancellationToken);
            }
            if (OcrModelCatalog.Device(settings) != "cpu" && !Valid(RuntimePath, RuntimeFile.Size, RuntimeFile.Sha256))
            {
                progress?.Report("DirectML.dll");
                await InstallAsync(RuntimePath, RuntimeFile.Size, RuntimeFile.Sha256, async stream =>
                {
                    using var response = await Http.GetAsync(RuntimeUrl, cancellationToken);
                    response.EnsureSuccessStatusCode();
                    using var package = new MemoryStream(await response.Content.ReadAsByteArrayAsync(cancellationToken));
                    using var zip = new ZipArchive(package, ZipArchiveMode.Read);
                    using var entry = (zip.GetEntry("bin/x64-win/DirectML.dll")
                        ?? throw new InvalidDataException("DirectML package does not contain the pinned runtime.")).Open();
                    await CopyAssetAsync(entry, stream, RuntimeFile.Size, cancellationToken);
                }, cancellationToken, path => Authenticode.Problem(path, "Microsoft Corporation"));
            }
        }
        finally { Preparation.Release(); }
    }

    // A failed/cancelled download never replaces an installed asset. Only fixed manifest names are used.
    internal static async Task InstallAsync(string path, long size, string hash, Func<Stream, Task> download,
        CancellationToken cancellationToken, Func<string, string?>? validate = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".partial";
        try
        {
            await using (var stream = File.Create(temporary)) await download(stream);
            cancellationToken.ThrowIfCancellationRequested();
            if (!Valid(temporary, size, hash)) throw new InvalidDataException("OCR asset size/SHA-256 mismatch.");
            if (validate?.Invoke(temporary) is { } problem) throw new InvalidDataException(problem);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    internal static async Task DownloadAssetAsync(HttpClient client, string url, Stream target, long size,
        CancellationToken cancellationToken, TimeSpan? timeout = null)
    {
        // ResponseHeadersRead ends HttpClient.Timeout at the headers; keep a deadline for the body.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout ?? TimeSpan.FromMinutes(10));
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is { } length && length != size)
            throw new InvalidDataException("OCR asset content length does not match the manifest.");
        await using var source = await response.Content.ReadAsStreamAsync(deadline.Token);
        await CopyAssetAsync(source, target, size, deadline.Token);
    }

    internal static async Task CopyAssetAsync(Stream source, Stream target, long size, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        var buffer = new byte[65536];
        long written = 0;
        while (true)
        {
            // Read one byte beyond the expected size to reject even a chunked endless response,
            // without allowing it to fill the disk before the eventual SHA-256 check.
            int count = size - written >= buffer.Length ? buffer.Length : (int)(size - written) + 1;
            int read = await source.ReadAsync(buffer.AsMemory(0, count), cancellationToken);
            if (read == 0) break;
            if (read > size - written) throw new InvalidDataException("OCR asset exceeds its pinned size.");
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            written += read;
        }
        if (written != size) throw new InvalidDataException("OCR asset is shorter than its pinned size.");
    }

    internal static bool Valid(string path, long size, string hash)
    {
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists || file.Length != size) return false;
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).Equals(hash, StringComparison.OrdinalIgnoreCase);
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
}
