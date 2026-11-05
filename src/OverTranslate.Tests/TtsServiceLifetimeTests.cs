using System.IO;
using OverTranslate.Services;
using Xunit;

namespace OverTranslate.Tests;

// Synthetic bytes and fake playback only: no online speech service or audio device is used.
public sealed class TtsServiceLifetimeTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "overtranslate-tts-test-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task StopDuringFileWriteCannotRestartPlaybackOrLeaveThePartialFile()
    {
        var written = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var played = new List<string>();
        using var service = new TtsService((_, _, _) => Task.FromResult(new byte[] { 1 }),
            action => action(), played.Add, () => { }, _directory,
            async (path, data, _) =>
            {
                await File.WriteAllBytesAsync(path, data);
                written.SetResult();
                await resume.Task;
            });
        var pending = service.SpeakAsync("synthetic speech", "EN");
        await written.Task.WaitAsync(TimeSpan.FromSeconds(5));
        service.Stop();
        resume.SetResult();
        await pending;
        Assert.Empty(played);
        Assert.False(service.IsActive);
        Assert.Empty(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task CancelledOldFetchCannotReplaceNewPlayback()
    {
        var oldAudio = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var played = new List<byte[]>();
        using var service = new TtsService((text, _, _) => text == "old" ? oldAudio.Task : Task.FromResult(new byte[] { 2 }),
            action => action(), path => played.Add(File.ReadAllBytes(path)), () => { }, _directory);
        var old = service.SpeakAsync("old", "EN");
        await service.SpeakAsync("new", "EN");
        oldAudio.SetResult([1]); // deliberately ignores cancellation, like a completed network request
        await old;
        Assert.Equal(new byte[] { 2 }, Assert.Single(played));
        Assert.True(service.IsActive);
        Assert.Single(Directory.GetFiles(_directory));
        service.Stop();
        Assert.Empty(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task FailedWriteDeletesItsPartialFileAndResetsTheButton()
    {
        using var service = new TtsService((_, _, _) => Task.FromResult(new byte[] { 1 }),
            action => action(), _ => throw new Xunit.Sdk.XunitException("Should not play"), () => { }, _directory,
            (path, data, _) =>
            {
                File.WriteAllBytes(path, data);
                throw new IOException("Synthetic disk failure");
            });
        await Assert.ThrowsAsync<IOException>(() => service.SpeakAsync("speech", "EN"));
        Assert.False(service.IsActive);
        Assert.Empty(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task DisposeCancelsPendingSpeechAndPreventsReuse()
    {
        var audio = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var played = false;
        var service = new TtsService((_, _, _) => audio.Task, action => action(), _ => played = true, () => { }, _directory);
        var pending = service.SpeakAsync("speech", "EN");
        service.Dispose();
        service.Dispose();
        audio.SetResult([1]);
        await pending;
        Assert.False(played);
        Assert.False(service.IsActive);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => service.SpeakAsync("again", "EN"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
