using OverTranslate.Services;
using Xunit;

namespace OverTranslate.Tests;

public class HookShutdownTests
{
    [Fact]
    public async Task BusyHookDoesNotBlockShutdownOrAllowASecondHookThread()
    {
        using var hook = new HookThread("blocked shutdown test", TimeSpan.FromMilliseconds(50));
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tornDown = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        hook.Start();
        var original = hook.Dispatcher!;
        _ = original.BeginInvoke(() =>
        {
            entered.SetResult();
            release.Wait();
        });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            await Task.Run(() => hook.Stop(() => tornDown.SetResult()))
                .WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Null(hook.Dispatcher);
            hook.Start();
            Assert.Null(hook.Dispatcher);
        }
        finally
        {
            release.Set();
        }
        await tornDown.Task.WaitAsync(TimeSpan.FromSeconds(5));
        hook.Stop();
    }

    [Fact]
    public async Task HookCanStopItselfWithoutJoiningItsOwnThread()
    {
        using var hook = new HookThread("self shutdown test", TimeSpan.FromSeconds(10));
        hook.Start();
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = hook.Dispatcher!.BeginInvoke(() =>
        {
            hook.Stop();
            stopped.SetResult();
        });
        await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Null(hook.Dispatcher);
    }
}
