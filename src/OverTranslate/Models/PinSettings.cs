using System.Windows.Input;

namespace OverTranslate.Models;

/// <summary>The modifier a 貼圖 gesture needs held. Matched exactly: 無 means no modifier at all.</summary>
public enum PinModifier { None, Ctrl, Shift, Alt }

/// <summary>A mouse gesture that triggers a one-shot 貼圖 action.</summary>
/// <remarks>
/// No single left click (that drags the pin) and no right click (that opens its menu).
/// </remarks>
public enum PinClickGesture { None, LeftDoubleClick, MiddleClick, MiddleDoubleClick }

/// <summary>What a 貼圖 gesture does.</summary>
public enum PinAction { Zoom, Opacity, Close, Reset }

/// <summary>
/// How a pinned picture is zoomed, faded, closed and reset — Snipaste's 貼圖操作 page. Each action
/// is a modifier plus a gesture; where two actions share a combination the one listed first wins.
/// </summary>
public class PinSettings
{
    public PinModifier ZoomModifier { get; set; } = PinModifier.None;
    public bool ZoomWithWheel { get; set; } = true;
    public bool ZoomWithKeys { get; set; } = true;

    public PinModifier OpacityModifier { get; set; } = PinModifier.Ctrl;
    public bool OpacityWithWheel { get; set; } = true;
    public bool OpacityWithKeys { get; set; } = true;

    public PinModifier CloseModifier { get; set; } = PinModifier.None;
    public PinClickGesture CloseGesture { get; set; } = PinClickGesture.LeftDoubleClick;

    /// <summary>Back to 100% and fully opaque.</summary>
    public PinModifier ResetModifier { get; set; } = PinModifier.None;
    public PinClickGesture ResetGesture { get; set; } = PinClickGesture.MiddleClick;
}

/// <summary>Which action, if any, a gesture on a pin means. Top of the list first.</summary>
public static class PinGestures
{
    public static PinModifier FromKeys(ModifierKeys keys) =>
        (keys & ModifierKeys.Control) != 0 ? PinModifier.Ctrl
        : (keys & ModifierKeys.Shift) != 0 ? PinModifier.Shift
        : (keys & ModifierKeys.Alt) != 0 ? PinModifier.Alt
        : PinModifier.None;

    /// <summary>True when exactly one modifier (or none) is held — two at once match nothing.</summary>
    private static bool Single(ModifierKeys keys)
    {
        int count = ((keys & ModifierKeys.Control) != 0 ? 1 : 0)
                  + ((keys & ModifierKeys.Shift) != 0 ? 1 : 0)
                  + ((keys & ModifierKeys.Alt) != 0 ? 1 : 0);
        return count <= 1;
    }

    /// <summary>The action the wheel performs with <paramref name="keys"/> held.</summary>
    public static PinAction? ForWheel(PinSettings s, ModifierKeys keys)
    {
        if (!Single(keys)) return null;
        var m = FromKeys(keys);
        if (s.ZoomWithWheel && s.ZoomModifier == m) return PinAction.Zoom;
        if (s.OpacityWithWheel && s.OpacityModifier == m) return PinAction.Opacity;
        return null;
    }

    /// <summary>The action +/- performs with <paramref name="keys"/> held.</summary>
    public static PinAction? ForPlusMinus(PinSettings s, ModifierKeys keys)
    {
        if (!Single(keys)) return null;
        var m = FromKeys(keys);
        if (s.ZoomWithKeys && s.ZoomModifier == m) return PinAction.Zoom;
        if (s.OpacityWithKeys && s.OpacityModifier == m) return PinAction.Opacity;
        return null;
    }

    /// <summary>The action a click gesture performs with <paramref name="keys"/> held.</summary>
    public static PinAction? ForClick(PinSettings s, ModifierKeys keys, PinClickGesture gesture)
    {
        if (gesture == PinClickGesture.None || !Single(keys)) return null;
        var m = FromKeys(keys);
        if (s.CloseGesture == gesture && s.CloseModifier == m) return PinAction.Close;
        if (s.ResetGesture == gesture && s.ResetModifier == m) return PinAction.Reset;
        return null;
    }
}
