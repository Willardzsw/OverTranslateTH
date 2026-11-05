using System.Windows;
using OverTranslate.Services;

namespace OverTranslate.Views.Settings;

/// <summary>
/// A modal panel listens while open and releases the static event even when its
/// containing window closes without going through the panel's Close method.
/// </summary>
internal sealed class OverlayLanguageSubscription : IDisposable
{
    private readonly EventHandler _handler;
    private bool _subscribed;

    public OverlayLanguageSubscription(FrameworkElement owner, EventHandler handler)
    {
        _handler = handler;
        owner.Unloaded += (_, _) => Dispose();
    }

    public void Subscribe()
    {
        if (_subscribed) return;
        LocalizationService.LanguageChanged += _handler;
        _subscribed = true;
    }

    public void Dispose()
    {
        if (!_subscribed) return;
        LocalizationService.LanguageChanged -= _handler;
        _subscribed = false;
    }
}
