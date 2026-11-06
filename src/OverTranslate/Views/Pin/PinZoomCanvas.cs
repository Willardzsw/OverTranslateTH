using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OverTranslate.Services;
// UseWindowsForms puts System.Drawing and System.Windows.Forms in the implicit usings
using Brushes = System.Windows.Media.Brushes;
using Cursors = System.Windows.Input.Cursors;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace OverTranslate.Views.Pin;

/// <summary>
/// A transparent window over the pin's whole monitor that shows the pin while it is being zoomed.
/// </summary>
/// <remarks>
/// <para>Why it exists: the pin is a transparent (layered) window, and moving and resizing one at
/// the same time shows a frame of the old picture at the new position before the new picture is
/// painted. Zooming around the pointer has to do both on every notch, so the picture jumped. Here
/// the window never moves or resizes while the wheel turns — only the picture inside it changes —
/// so every frame is right, which is how Snipaste's zoom stays smooth.</para>
///
/// <para>Only up while zooming. Its transparent part passes clicks through, as any layered window's
/// does, so nothing underneath is blocked meanwhile. It appears cloaked until its first frame is
/// painted, so it is never seen half drawn.</para>
/// </remarks>
internal sealed class PinZoomCanvas : Window
{
    private readonly System.Drawing.Rectangle _monitor;
    private readonly Canvas _root = new() { Background = null };
    private bool _cloaked;
    private double _dpi = 1;

    /// <summary>The same picture, frame and shadow the pin draws, so the hand-over is not seen.</summary>
    public PinChrome Chrome { get; }

    /// <summary>Raised once the first frame is on screen, when the pin can step aside.</summary>
    public event EventHandler? Ready;

    public event Action<PinZoomCanvas, MouseWheelEventArgs>? Wheel;
    public event Action<PinZoomCanvas, MouseButtonEventArgs>? Pressed;

    /// <summary>The pointer moved while a drag started on the canvas holds it.</summary>
    public event Action<PinZoomCanvas>? DragMoved;

    /// <summary>The button that started a drag on the canvas came up.</summary>
    public event Action<PinZoomCanvas>? DragEnded;

    public PinZoomCanvas(BitmapSource image, System.Drawing.Rectangle monitor)
    {
        _monitor = monitor;

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        UseLayoutRounding = true;

        Chrome = new PinChrome(image);
        Chrome.Surface.Cursor = Cursors.SizeAll;
        _root.Children.Add(Chrome.Surface);
        Content = _root;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        _cloaked = WindowScreenPresence.SetCloaked(hwnd, cloaked: true);

        // One scale for the whole window, and it stays on the monitor it was put on.
        ScreenGeometry.PinPhysicalBounds(this, _monitor);
        var dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
        _dpi = dpi > 0 ? dpi : 1;
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        if (_cloaked)
        {
            WindowScreenPresence.SetCloaked(new WindowInteropHelper(this).Handle, cloaked: false);
            _cloaked = false;
        }
        Ready?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Shows the picture with its top-left at a physical point, at a physical size.</summary>
    public void Present(double picX, double picY, double widthPx, double heightPx, double opacity, double zoom)
    {
        var surface = Chrome.Surface;
        Canvas.SetLeft(surface, (picX - _monitor.Left) / _dpi);
        Canvas.SetTop(surface, (picY - _monitor.Top) / _dpi);
        surface.Width = Math.Max(1, widthPx / _dpi);
        surface.Height = Math.Max(1, heightPx / _dpi);
        surface.Opacity = opacity;
        Chrome.SetScaling(zoom);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        Wheel?.Invoke(this, e);
        e.Handled = true;
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        Pressed?.Invoke(this, e);
    }

    /// <summary>Holds the pointer for a drag of the picture across the canvas.</summary>
    public void BeginDrag() => CaptureMouse();

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (IsMouseCaptured) DragMoved?.Invoke(this);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!IsMouseCaptured) return;
        ReleaseMouseCapture();
        DragEnded?.Invoke(this);
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        DragEnded?.Invoke(this);
    }
}
