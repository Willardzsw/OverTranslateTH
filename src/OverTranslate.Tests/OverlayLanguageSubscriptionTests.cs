using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using OverTranslate.Services;
using OverTranslate.Views.Settings;
using Xunit;

namespace OverTranslate.Tests;

public class OverlayLanguageSubscriptionTests
{
    [Fact]
    public void ClosingHostWithoutClosingPanelReleasesStaticSubscription()
    {
        OnUiThread(() =>
        {
            var owner = new UserControl();
            EventHandler handler = (_, _) => { };
            using var subscription = new OverlayLanguageSubscription(owner, handler);
            subscription.Subscribe();
            subscription.Subscribe(); // reopening cannot stack handlers
            Assert.Equal(1, HandlerCount(handler));

            owner.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            Assert.Equal(0, HandlerCount(handler));

            subscription.Subscribe(); // a remounted panel can listen again
            Assert.Equal(1, HandlerCount(handler));
            subscription.Dispose();
            subscription.Dispose();
            Assert.Equal(0, HandlerCount(handler));
        });
    }

    private static int HandlerCount(EventHandler handler) =>
        ((Delegate?)typeof(LocalizationService).GetField("LanguageChanged", BindingFlags.Static | BindingFlags.NonPublic)!
            .GetValue(null))?.GetInvocationList().Count(candidate => candidate.Equals(handler)) ?? 0;

    private static void OnUiThread(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw new Xunit.Sdk.XunitException(failure.ToString());
    }
}
