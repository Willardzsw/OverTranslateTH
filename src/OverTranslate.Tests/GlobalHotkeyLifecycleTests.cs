using OverTranslate.Services;
using Xunit;

namespace OverTranslate.Tests;

public class GlobalHotkeyLifecycleTests
{
    [Fact]
    public void MovingRegistrationToAnotherWindowReleasesTheOriginalWindowFirst()
    {
        var calls = new List<(string Operation, IntPtr Window, int Id)>();
        using var hotkey = new GlobalHotkey(123,
            (window, id, _, _) => { calls.Add(("register", window, id)); return true; },
            (window, id) => { calls.Add(("unregister", window, id)); return true; });
        // The injected native operations never register a real system shortcut.
        var first = new IntPtr(101);
        var second = new IntPtr(202);
        hotkey.Register(first, 3, 0x87);
        hotkey.Register(second, 3, 0x87);
        hotkey.Dispose();
        hotkey.Dispose();

        Assert.Equal(new[]
        {
            ("register", first, 123), ("unregister", first, 123),
            ("register", second, 123), ("unregister", second, 123),
        }, calls);
    }

    [Fact]
    public void FailedReplacementDoesNotKeepAnOldRegistrationOrInventANewOne()
    {
        var removed = new List<IntPtr>();
        int attempts = 0;
        using var hotkey = new GlobalHotkey(123,
            (_, _, _, _) => ++attempts == 1,
            (window, _) => { removed.Add(window); return true; });
        hotkey.Register(new IntPtr(101), 3, 0x87);
        hotkey.Register(new IntPtr(202), 3, 0x87);
        hotkey.Dispose();

        Assert.Equal(new[] { new IntPtr(101) }, removed);
    }
}
