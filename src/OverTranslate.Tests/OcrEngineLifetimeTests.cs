using System.Drawing;
using OverTranslate.Services.Ocr;
using Xunit;

namespace OverTranslate.Tests;

public class OcrEngineLifetimeTests
{
    [Fact]
    public async Task DisposeWakesQueuedRequestsAndAllowsOutstandingSlotsToReturn()
    {
        using var engine = new OnnxOcrEngine();
        var slots = Enumerable.Range(0, OnnxOcrEngine.ConcurrentRecognitions)
            .Select(_ => engine.TryTakeSlot()!).ToArray();
        try
        {
            var waiting = engine.TakeSlotAsync(CancellationToken.None);
            Assert.False(waiting.IsCompleted);

            engine.Dispose();

            await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
                await waiting.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Throws<ObjectDisposedException>(() => engine.TryTakeSlot());
            await Assert.ThrowsAsync<ObjectDisposedException>(() => engine.TakeSlotAsync(CancellationToken.None));
        }
        finally
        {
            foreach (var slot in slots) slot.Dispose();
        }
    }

    [Fact]
    public async Task CancellingAQueuedRequestDoesNotConsumeASlot()
    {
        using var engine = new OnnxOcrEngine();
        var slots = Enumerable.Range(0, OnnxOcrEngine.ConcurrentRecognitions)
            .Select(_ => engine.TryTakeSlot()!).ToArray();
        using var cancellation = new CancellationTokenSource();
        var waiting = engine.TakeSlotAsync(cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        foreach (var slot in slots) slot.Dispose();
        using var next = engine.TryTakeSlot();
        Assert.NotNull(next);
    }

    [Fact]
    public void DisposeKeepsLeasedNativeRuntimeAliveUntilTheLastSessionCloses()
    {
        using var engine = new OnnxOcrEngine();
        using var bitmap = new Bitmap(160, 60);
        using var first = engine.BeginDetection(bitmap, "EN");
        using var second = engine.BeginDetection(bitmap, "EN");

        engine.Dispose();
        Assert.Equal("CPU", engine.ExecutionDevice);

        first.Dispose();
        first.Dispose(); // Must not release the other session's lease.
        Assert.Equal("CPU", engine.ExecutionDevice);
        Assert.Throws<ObjectDisposedException>(() => first.Recognize([]));
        Assert.NotNull(second.Recognize([]));

        second.Dispose();
        Assert.Null(engine.ExecutionDevice);
    }
}
