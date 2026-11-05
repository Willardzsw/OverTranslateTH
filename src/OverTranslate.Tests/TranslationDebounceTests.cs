using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using OverTranslate.Services;
using OverTranslate.Views.Controls;
using OverTranslate.Views.QuickLookup;
using OverTranslate.Views.Translation;
using Xunit;

namespace OverTranslate.Tests;

public class TranslationDebounceTests
{
    [Theory]
    [InlineData(typeof(TranslationPage))]
    [InlineData(typeof(QuickLookupWindow))]
    public void NonemptyEditInvalidatesTranslationAndDictionaryBeforeDebounce(Type type)
    {
        OnUiThread(() =>
        {
            // Exercise the actual edit handler with standalone controls. Bypassing the
            // window constructor avoids global settings, credentials, native hooks and
            // network requests; no dispatcher pump means the debounce cannot fire.
            var surface = RuntimeHelpers.GetUninitializedObject(type);
            var request = new LatestRequest();
            var (seq, token) = request.Start();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
            Set(surface, "_request", request);
            Set(surface, "_debounce", timer);
            Set(surface, "SourceTextBox", new TextBox { Text = "edited synthetic text" });
            Set(surface, "DictionaryView", new DictionaryResultView());
            Set(surface, "DictionaryLoadingBar", new ProgressBar());
            Set(surface, "DictionaryHost", new Border());
            if (type == typeof(TranslationPage))
                Set(surface, "TranslatingBar", new ProgressBar());
            else
            {
                Set(surface, "TranslationLoadingBar", new ProgressBar());
                Set(surface, "CompactTranslationLoadingBar", new ProgressBar());
            }

            try
            {
                type.GetMethod("RequestTranslate", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(surface, null);

                Assert.True(timer.IsEnabled); // replacement has only been scheduled
                Assert.True(token.IsCancellationRequested);
                Assert.False(request.IsCurrent(seq)); // even a provider ignoring cancellation cannot publish
            }
            finally { timer.Stop(); }
        });
    }

    private static void Set(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!
            .SetValue(target, value);

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
