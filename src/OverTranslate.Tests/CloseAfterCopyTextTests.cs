using OverTranslate.Models;
using OverTranslate.Services;
using Xunit;

namespace OverTranslate.Tests;

/// <summary>複製文字 / 複製譯文後自動關閉: off by default, each its own switch, kept in the file.</summary>
public class CloseAfterCopyTextTests
{
    [Fact]
    public void BothAreOffByDefault()
    {
        var capture = new AppSettings().Capture;
        Assert.False(capture.CloseAfterCopyText);
        Assert.False(capture.CloseAfterCopyTranslation);
    }

    [Fact]
    public void EachSurvivesTheSettingsFileOnItsOwn()
    {
        var s = new AppSettings();
        s.Capture.CloseAfterCopyTranslation = true;

        var back = SettingsService.Parse(SettingsService.Serialize(s)).Capture;

        Assert.False(back.CloseAfterCopyText);
        Assert.True(back.CloseAfterCopyTranslation);
        Assert.False(back.CloseAfterCopy);
    }

    [Fact]
    public void OlderFilesWithoutThemReadAsOff()
    {
        var capture = SettingsService.Parse("""{"Capture":{"CloseAfterCopy":true}}""").Capture;
        Assert.True(capture.CloseAfterCopy);
        Assert.False(capture.CloseAfterCopyText);
        Assert.False(capture.CloseAfterCopyTranslation);
    }
}
