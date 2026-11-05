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
    public void MainNavigationHasTextRealtimeAndSettings()
    {
        var doc = Read("Views/Shell/ShellWindow.xaml");
        var panel = doc.Descendants().Single(e => (string?)e.Attribute(X + "Name") == "NavPanel");
        Assert.Equal(new[] { "TranslationNav", "RealtimeNav", "SettingsNav" }, panel.Elements().Select(e => (string?)e.Attribute(X + "Name")));
        Assert.DoesNotContain(doc.Descendants(), e => (string?)e.Attribute(X + "Name") is "CaptureBtn" or "QuickLookupBtn");
    }

    [Fact]
    public void SettingsKeepTheOcrSelectorAndRealtimeShortcutWithoutMangaCard()
    {
        var doc = Read("Views/Settings/SettingsPage.xaml");
        Assert.Contains(doc.Descendants(), e => e.Name.LocalName == "OcrModelCard");
        Assert.DoesNotContain(doc.Descendants(), e => e.Name.LocalName == "MangaModelCard");
        Assert.Contains(doc.Descendants(), e => (string?)e.Attribute(X + "Name") == "RealtimePauseHotkeyBox");
    }

    [Fact]
    public void ScreenshotOcrRemainsAvailableOnTheTextTranslationPage()
    {
        var doc = Read("Views/Translation/TranslationPage.xaml");
        Assert.Contains(doc.Descendants(), e => (string?)e.Attribute(X + "Name") == "CaptureOcrBtn"
            && (string?)e.Attribute("Click") == "CaptureOcrBtn_Click");
    }

    [Fact]
    public void TheRealtimeShortcutIsRegisteredWhenEnabled()
    {
        var bindings = HotkeyBindings.Resolve(new AppSettings());
        Assert.Contains(bindings, b => b.Action == HotkeyAction.RealtimePause && b.IsActive);
        Assert.Contains(bindings, b => b.Action == HotkeyAction.Capture && b.IsActive);
        Assert.Contains(bindings, b => b.Action == HotkeyAction.TranslationWindow && b.IsActive);
    }
}
