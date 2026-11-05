using OverTranslate.Services;
using Xunit;

namespace OverTranslate.Tests;

// Fake foreground and clipboard only; never sends input or reads the user's clipboard.
public class SelectionReplacerTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FocusMovingDuringClipboardAccessPreventsPaste(bool duringCapture)
    {
        var foreground = new IntPtr(1);
        var puts = 0;
        var restores = 0;
        var pastes = 0;
        var result = await SelectionReplacer.ReplaceSelectionAsync("translation", foreground,
            () => foreground,
            () => { if (duringCapture) foreground = new IntPtr(2); return "original"; },
            _ => { puts++; foreground = new IntPtr(2); return new object(); },
            _ => true,
            () => { pastes++; return Task.CompletedTask; },
            () => Task.CompletedTask,
            _ => restores++);
        Assert.Equal(PasteOutcome.FocusMoved, result);
        Assert.Equal(0, pastes);
        Assert.Equal(duringCapture ? 0 : 1, puts);
        Assert.Equal(duringCapture ? 0 : 1, restores);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UserCopyDuringPasteSettleIsPreserved(bool userCopies)
    {
        var ownedData = new object();
        object clipboard = ownedData;
        var restored = false;
        var result = await SelectionReplacer.ReplaceSelectionAsync("translation", new IntPtr(1),
            () => new IntPtr(1), () => "original", _ => ownedData, data => ReferenceEquals(data, clipboard),
            () => Task.CompletedTask,
            () => { if (userCopies) clipboard = new object(); return Task.CompletedTask; },
            _ => restored = true);
        Assert.Equal(PasteOutcome.Pasted, result);
        Assert.Equal(!userCopies, restored);
    }

    [Fact]
    public async Task FailedClipboardWriteDoesNotReplaceExistingClipboard()
    {
        var restored = false;
        var result = await SelectionReplacer.ReplaceSelectionAsync("translation", new IntPtr(1),
            () => new IntPtr(1), () => "original", _ => null, _ => false,
            () => throw new Xunit.Sdk.XunitException("Should not paste"), () => Task.CompletedTask,
            _ => restored = true);
        Assert.Equal(PasteOutcome.ClipboardUnavailable, result);
        Assert.False(restored);
    }
}
