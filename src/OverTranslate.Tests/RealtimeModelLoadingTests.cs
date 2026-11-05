using System.Drawing;
using System.Reflection;
using OverTranslate.Services;
using OverTranslate.Services.Ocr;
using OverTranslate.Services.Realtime;
using OverTranslate.Services.Realtime.Capture;
using OverTranslate.Views.Realtime;
using Xunit;

namespace OverTranslate.Tests;

/// <summary>
/// The models loaded as a realtime session starts and the 載入模型中… covering the wait.
/// </summary>
public sealed class RealtimeModelLoadingTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    // ── The hint: from the press until every load has ended ──────────────────

    private sealed class Shown
    {
        private readonly List<bool> _changes = [];
        private readonly List<long> _at = [];

        internal void Add(bool shown)
        {
            lock (_changes)
            {
                _changes.Add(shown);
                _at.Add(System.Diagnostics.Stopwatch.GetTimestamp());
            }
        }

        // How long it was up: the first change to the second.
        internal TimeSpan Up
        {
            get
            {
                lock (_changes) return System.Diagnostics.Stopwatch.GetElapsedTime(_at[0], _at[1]);
            }
        }

        internal long DownAt
        {
            get
            {
                lock (_changes) return _at[1];
            }
        }

        internal List<bool> Changes
        {
            get
            {
                lock (_changes) return [.. _changes];
            }
        }
    }

    // The end of a load arrives from a pool thread; here it is run where it arrives.
    private static RealtimeModelLoadingHint Hint(Shown shown) => new(shown.Add, action => action());

    private static TaskCompletionSource Load() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Fact]
    public void NothingToLoad_IsNeverShown()
    {
        var shown = new Shown();
        var hint = Hint(shown);

        hint.Wait(Task.CompletedTask);

        Assert.Empty(shown.Changes);
    }

    [Fact]
    public async Task ALoad_IsShownAtOnce_AndGoesWhenItEnds()
    {
        var shown = new Shown();
        var hint = Hint(shown);
        var load = Load();

        hint.Wait(load.Task);
        Assert.Equal([true], shown.Changes);

        load.SetResult();
        await WaitUntil(() => shown.Changes.Count == 2, Patience);
        Assert.Equal([true, false], shown.Changes);
    }

    [Fact]
    public async Task TwoLoads_StayShownUntilBothHaveEnded()
    {
        var shown = new Shown();
        var hint = Hint(shown);
        var recogniser = Load();
        var second = Load();

        hint.Wait(Task.WhenAll(recogniser.Task, second.Task));
        recogniser.SetResult();
        await Task.Delay(200);
        Assert.Equal([true], shown.Changes);

        second.SetResult();
        await WaitUntil(() => shown.Changes.Count == 2, Patience);
        Assert.Equal([true, false], shown.Changes);
    }

    [Fact]
    public async Task AFailedLoad_TakesItDownToo()
    {
        var shown = new Shown();
        var hint = Hint(shown);
        var load = Load();

        hint.Wait(load.Task);
        load.SetException(new InvalidOperationException("would not load"));

        await WaitUntil(() => shown.Changes.Count == 2, Patience);
        Assert.Equal([true, false], shown.Changes);
    }

    [Fact]
    public async Task ALaterStart_TakesOver_AndTheEarlierLoadEndingLeavesItUp()
    {
        var shown = new Shown();
        var hint = Hint(shown);
        var first = Load();
        var second = Load();

        hint.Wait(first.Task);
        hint.Wait(second.Task);
        first.SetResult();
        await Task.Delay(200);
        Assert.Equal([true], shown.Changes);

        second.SetResult();
        await WaitUntil(() => shown.Changes.Count == 2, Patience);
        Assert.Equal([true, false], shown.Changes);
    }

    [Fact]
    public async Task TheSessionEnding_TakesItDown_AndNothingAfterBringsItBack()
    {
        var shown = new Shown();
        var hint = Hint(shown);
        var load = Load();

        hint.Wait(load.Task);
        hint.Dispose();
        load.SetResult();
        hint.Wait(Load().Task);
        await Task.Delay(200);

        Assert.Equal([true, false], shown.Changes);
    }

    [Fact]
    public async Task AQuickLoad_StaysUpForHalfASecond()
    {
        var shown = new Shown();
        var hint = Hint(shown);
        var load = Load();

        hint.Wait(load.Task);
        await Task.Delay(110);
        load.SetResult();
        await Task.Delay(200);
        Assert.Equal([true], shown.Changes);

        await WaitUntil(() => shown.Changes.Count == 2, Patience);
        Assert.Equal([true, false], shown.Changes);
        Assert.InRange(shown.Up, RealtimeModelLoadingHint.MinShown, RealtimeModelLoadingHint.MinShown + TimeSpan.FromMilliseconds(250));
    }

    [Fact]
    public async Task ALongLoad_GoesTheMomentItEnds()
    {
        var shown = new Shown();
        var hint = Hint(shown);
        var load = Load();

        hint.Wait(load.Task);
        await Task.Delay(1600);
        var ended = System.Diagnostics.Stopwatch.GetTimestamp();
        load.SetResult();

        await WaitUntil(() => shown.Changes.Count == 2, Patience);
        Assert.Equal([true, false], shown.Changes);
        Assert.True(System.Diagnostics.Stopwatch.GetElapsedTime(ended, shown.DownAt) < TimeSpan.FromMilliseconds(150));
    }

    [Fact]
    public async Task TheSessionEndingDuringTheHold_TakesItDownAtOnce_AndTheHoldNeverCallsTheBar()
    {
        var shown = new Shown();
        var hint = Hint(shown);
        var load = Load();

        hint.Wait(load.Task);
        load.SetResult();
        await Task.Delay(100);
        Assert.Equal([true], shown.Changes);

        hint.Dispose();
        Assert.Equal([true, false], shown.Changes);
        await Task.Delay(700);
        Assert.Equal([true, false], shown.Changes);
    }

    [Fact]
    public async Task AStartDuringTheHold_IsNotTakenDownByTheEarlierOne()
    {
        var shown = new Shown();
        var hint = Hint(shown);
        var first = Load();
        var second = Load();

        hint.Wait(first.Task);
        first.SetResult();
        await Task.Delay(100);
        hint.Wait(second.Task);
        await Task.Delay(700);
        Assert.Equal([true], shown.Changes);

        second.SetResult();
        await WaitUntil(() => shown.Changes.Count == 2, Patience);
        Assert.Equal([true, false], shown.Changes);
    }

    [Fact]
    public async Task AStartDuringTheHold_WithNothingToLoad_StillHoldsTheHalfSecond()
    {
        var shown = new Shown();
        var hint = Hint(shown);
        var first = Load();

        hint.Wait(first.Task);
        first.SetResult();
        await Task.Delay(100);
        hint.Wait(Task.CompletedTask);
        Assert.Equal([true], shown.Changes);

        await WaitUntil(() => shown.Changes.Count == 2, Patience);
        Assert.Equal([true, false], shown.Changes);
        Assert.True(shown.Up >= RealtimeModelLoadingHint.MinShown);
    }

    // ── What a start waits for ────────────────────────────────────────────────

    private static RealtimeRegion Block(int id, RealtimeTextOrientation orientation) =>
        new(id, new Rectangle(id * 300, 0, 280, 200), RealtimeBlockMode.Panel, orientation);

    [Fact]
    public async Task Start_Cold_WaitsForTheRecogniser_ThenNothing()
    {
        using var ocr = new OcrService();
        var engine = Recogniser(ocr);

        var loading = ocr.KeepWarmForRealtime("JA", vertical: false);
        Assert.False(engine.IsLoaded("JA"));
        await loading.WaitAsync(Patience);
        Assert.True(engine.IsLoaded("JA"));

        // Already in memory, as after 暫停 or framing: nothing to wait for.
        Assert.True(ocr.KeepWarmForRealtime("JA", vertical: false).IsCompleted);
        Assert.True(ocr.KeepWarmForRealtime("JA", vertical: true).IsCompleted);
        ocr.ReleaseModel();
    }

    // ── The busy line in the log ─────────────────────────────────────────────

    private sealed class StillPicture : IRealtimeCaptureBackend
    {
        public string Name => "still";

        public Bitmap? GrabRegion(Rectangle screenBounds)
        {
            var frame = new Bitmap(screenBounds.Width, screenBounds.Height);
            using var g = Graphics.FromImage(frame);
            g.Clear(Color.White);
            return frame;
        }

        public CaptureMiss DescribeMiss(Rectangle screenBounds) => CaptureMiss.Waiting;
        public string DescribeActivity() => "";
        public string DescribeSteps() => "";

        public void Dispose()
        {
        }
    }

    // What the session logs while body runs, as "Level|message".
    private static async Task<string[]> SessionLog(Func<Task> body)
    {
        var target = new NLog.Targets.MemoryTarget { Layout = "${level}|${message}" };
        var config = NLog.LogManager.Configuration ?? new NLog.Config.LoggingConfiguration();
        var rule = new NLog.Config.LoggingRule(typeof(RealtimeTranslationSession).FullName, NLog.LogLevel.Debug, target);
        config.LoggingRules.Add(rule);
        NLog.LogManager.Configuration = config;
        try
        {
            await body();
        }
        finally
        {
            config.LoggingRules.Remove(rule);
            NLog.LogManager.Configuration = config;
        }

        return [.. target.Logs];
    }

    [Fact]
    public async Task Busy_WithTheModelsLoaded_IsStillTheSlotsWarning()
    {
        using var ocr = new OcrService();
        await ocr.KeepWarmForRealtime("JA", vertical: false).WaitAsync(Patience);
        var engine = Recogniser(ocr);
        var slots = Enumerable.Range(0, OcrService.ConcurrentRecognitions).Select(_ => engine.TryTakeSlot()).ToList();
        var session = new RealtimeTranslationSession(ocr, new TranslationService());

        try
        {
            var log = await SessionLog(async () =>
            {
                session.Start([Block(0, RealtimeTextOrientation.Horizontal)], "JA", "ZH-HANT",
                    Models.TranslationProvider.Google, new StillPicture(), readAtOnce: true);
                await Task.Delay(1000);
                session.Stop(releaseRecogniser: true);
            });

            Assert.Contains(log, line => line.StartsWith("Warn|") && line.Contains("recognition slots busy"));
            Assert.DoesNotContain(log, line => line.Contains("models still loading"));
        }
        finally
        {
            foreach (var slot in slots) slot?.Dispose();
        }
    }

    private static OnnxOcrEngine Recogniser(OcrService ocr) =>
        (OnnxOcrEngine)typeof(OcrService).GetField("_engine", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(ocr)!;

    private static async Task WaitUntil(Func<bool> condition, TimeSpan patience)
    {
        var until = DateTime.UtcNow + patience;
        while (!condition() && DateTime.UtcNow < until) await Task.Delay(20);
    }
}
