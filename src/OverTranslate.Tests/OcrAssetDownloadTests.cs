using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using OverTranslate.Services.Ocr;
using Xunit;

namespace OverTranslate.Tests;

public class OcrAssetDownloadTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    public async Task RejectsTruncatedAndOversizedStreamsWithoutWritingPastTheLimit(int actualSize)
    {
        using var source = new MemoryStream(new byte[actualSize]);
        using var destination = new MemoryStream();
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            OcrModelAssets.CopyAssetAsync(source, destination, 4, CancellationToken.None));
        Assert.True(destination.Length <= 4);
    }

    [Fact]
    public async Task CopiesExactlyThePinnedSize()
    {
        byte[] bytes = [1, 2, 3, 4];
        using var source = new MemoryStream(bytes);
        using var destination = new MemoryStream();
        await OcrModelAssets.CopyAssetAsync(source, destination, bytes.Length, CancellationToken.None);
        Assert.Equal(bytes, destination.ToArray());
    }

    [Fact]
    public async Task DeadlineAlsoCancelsABodyThatStallsAfterHeaders()
    {
        using var client = new HttpClient(new StalledBodyHandler());
        using var destination = new MemoryStream();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await OcrModelAssets.DownloadAssetAsync(client, "https://example.invalid/model", destination, 4,
                CancellationToken.None, TimeSpan.FromMilliseconds(50)).WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task FailedFinalValidationPreservesExistingAssetAndRemovesPartialFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "overtranslate-ocr-install-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "runtime.dll");
        byte[] existing = [9, 8, 7];
        byte[] replacement = [1, 2, 3, 4];
        await File.WriteAllBytesAsync(path, existing);
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => OcrModelAssets.InstallAsync(path,
                replacement.Length, Convert.ToHexString(SHA256.HashData(replacement)),
                stream => stream.WriteAsync(replacement).AsTask(), CancellationToken.None,
                _ => "signature validation failed"));
            Assert.Equal(existing, await File.ReadAllBytesAsync(path));
            Assert.Equal(new[] { path }, Directory.GetFiles(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class StalledBodyHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new StalledStream()) });
    }

    private sealed class StalledStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
