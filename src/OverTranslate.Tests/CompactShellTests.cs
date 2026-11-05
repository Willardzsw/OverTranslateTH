using System.Xml.Linq;
using OverTranslate.Models;
using OverTranslate.Services;
using Xunit;

namespace OverTranslate.Tests;

public class CompactShellTests
{
    private static XDocument Read(string path) => XDocument.Load(Path.Combine(StringsParityTests.ProjectDirectory(), path));
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void MainNavigationHasOnlyTextTranslationAndSettings()
    {
        var doc = Read("Views/Shell/ShellWindow.xaml");
        var panel = doc.Descendants().Single(e => (string?)e.Attribute(X + "Name") == "NavPanel");
        Assert.Equal(new[] { "TranslationNav", "SettingsNav" }, panel.Elements().Select(e => (string?)e.Attribute(X + "Name")));
        Assert.DoesNotContain(doc.Descendants(), e => (string?)e.Attribute(X + "Name") is "CaptureBtn" or "QuickLookupBtn" or "RealtimeNav");
    }

    [Fact]
    public void SettingsKeepTheOcrSelectorWithoutRealtimeOrMangaDownloadCards()
    {
        var doc = Read("Views/Settings/SettingsPage.xaml");
        Assert.Contains(doc.Descendants(), e => e.Name.LocalName == "OcrModelCard");
        Assert.DoesNotContain(doc.Descendants(), e => e.Name.LocalName == "MangaModelCard"
            || (string?)e.Attribute(X + "Name") == "RealtimePauseHotkeyBox");
    }

    [Fact]
    public void ScreenshotOcrRemainsAvailableOnTheTextTranslationPage()
    {
        var doc = Read("Views/Translation/TranslationPage.xaml");
        Assert.Contains(doc.Descendants(), e => (string?)e.Attribute(X + "Name") == "CaptureOcrBtn"
            && (string?)e.Attribute("Click") == "CaptureOcrBtn_Click");
    }

    [Fact]
    public void DesktopBindingsCannotRegisterRealtimeEvenWhenAnOldProfileEnablesIt()
    {
        var settings = new AppSettings { RealtimePauseHotkeyEnabled = true };
        var bindings = HotkeyBindings.Resolve(settings, includeRealtime: false);
        Assert.DoesNotContain(bindings, b => b.Action == HotkeyAction.RealtimePause);
        Assert.Contains(bindings, b => b.Action == HotkeyAction.Capture && b.IsActive);
        Assert.Contains(bindings, b => b.Action == HotkeyAction.TranslationWindow && b.IsActive);
    }
}
