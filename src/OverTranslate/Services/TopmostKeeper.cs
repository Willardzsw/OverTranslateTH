using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace OverTranslate.Services;

/// <summary>
/// Keeps 貼圖 and pinned-on-top 便利貼 above everything, even after another always-on-top program
/// is brought forward.
/// </summary>
/// <remarks>
/// <para>WPF's Topmost puts a window in the topmost band, but within that band the most recently
/// activated window wins: a second always-on-top program brought forward covers the pin until the
/// pin is clicked. So whenever another program takes the foreground, every registered window is put
/// back on top of the band — without activating it, so the program just brought forward keeps the
/// keyboard.</para>
/// <para>Not while a capture session is up (its overlay must cover the pins), and not when the
/// window brought forward is this program's own: its menus are topmost popups too, and raising a
/// pin over its own right-click menu would hide the menu.</para>
/// </remarks>
internal static class TopmostKeeper
{
    private static readonly HashSet<Window> Windows = [];
    private static Func<bool> _suspended = () => false;
    private static WinEventDelegate? _callback;
    private static IntPtr _hook;
    private static DispatcherTimer? _delay;

    /// <summary>Starts listening for foreground changes. <paramref name="suspended"/> pauses it.</summary>
    public static void Start(Func<bool> suspended)
    {
        _suspended = suspended;
        if (_hook != IntPtr.Zero) return;
        _callback = OnForegroundChanged;
        _hook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _callback, 0, 0,
            WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
        _delay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _delay.Tick += (_, _) =>
        {
            _delay.Stop();
            ReassertAll();
        };
    }

    public static void Register(Window window) => Windows.Add(window);

    public static void Unregister(Window window) => Windows.Remove(window);

    /// <summary>Puts every registered, visible, topmost window back on top now.</summary>
    public static void ReassertAll()
    {
        if (_suspended()) return;
        foreach (var window in Windows.ToList())
        {
            if (!window.IsVisible || !window.Topmost) continue;
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd != IntPtr.Zero) AlwaysOnTop.Reassert(hwnd);
        }
    }

    private static void OnForegroundChanged(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild,
        uint thread, uint time)
    {
        if (Windows.Count == 0) return;
        // A moment later: the program coming forward raises its own topmost windows as it activates.
        _delay?.Stop();
        _delay?.Start();
    }

    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

    private delegate void WinEventDelegate(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild,
        uint thread, uint time);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr module, WinEventDelegate callback,
        uint process, uint thread, uint flags);
}
