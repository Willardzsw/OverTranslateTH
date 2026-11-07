using System.Runtime.InteropServices;
using System.Windows.Forms;
using OverTranslate.Models;
using OverTranslate.Services;
using Xunit;

namespace OverTranslate.Tests;

/// <summary>
/// Tests that put a real window on the desktop and find it again by screen position run alone:
/// another test's window shown at the same moment would be the one under the pointer.
/// </summary>
public sealed class DesktopFactAttribute : FactAttribute
{
    /// <summary>
    /// Real windows on the user's live desktop: a window of theirs that ends up under the test point
    /// would be the one faded or resized. Run only on request, with TT_DESKTOP_TESTS=1, on a desktop
    /// with nothing open where the test windows go (200,200 to 800,600 and the work area's corner).
    /// </summary>
    public DesktopFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("TT_DESKTOP_TESTS") != "1")
            Skip = "Acts on real desktop windows; set TT_DESKTOP_TESTS=1 to run";
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DesktopWindowsCollection
{
    public const string Name = "Desktop windows";
}

/// <summary>
/// 鼠標增強 end to end on real windows, minus the hooks themselves: real input cannot be injected
/// without the injected mark the hook ignores, so the events are fed through the same decisions.
/// </summary>
/// <remarks>
/// Positions are read with GetWindowRect on plain Win32 windows, never a Form's own idea of its
/// bounds, and every action is refused unless the test window is the one under the point.
/// </remarks>
[Collection(DesktopWindowsCollection.Name)]
public class MouseResizePipelineTests
{
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }

    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(System.Drawing.Point point);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);

    /// <summary>
    /// Refuses to go on unless the test window is what is at (x, y): otherwise the action would land
    /// on whatever the user has open there.
    /// </summary>
    private static void AssertUnderPoint(TestWindow form, int x, int y)
    {
        var hit = GetAncestor(WindowFromPoint(new System.Drawing.Point(x, y)), 2);
        Assert.True(hit == form.Hwnd, $"the test window is not at {x},{y}; another window is ({hit})");
    }
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);
    [DllImport("user32.dll")] private static extern bool GetLayeredWindowAttributes(IntPtr hwnd, out uint key, out byte alpha, out uint flags);

    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd, int command);
    [DllImport("user32.dll")] private static extern bool IsZoomed(IntPtr hwnd);

    /// <summary>
    /// A plain top-level window with a sizing border, always on top. Not a WinForms Form: a Form on
    /// a per-monitor thread rescales itself, and the sizes asserted here would be its, not ours.
    /// </summary>
    private sealed class TestWindow : NativeWindow, IDisposable
    {
        public int HorizontalDelta;
        public IntPtr Hwnd => Handle;

        public TestWindow()
        {
            CreateHandle(new CreateParams
            {
                Caption = "TextTack mouse test",
                Style = unchecked((int)0x10CF0000), // WS_OVERLAPPEDWINDOW | WS_VISIBLE
                ExStyle = 0x00000008, // WS_EX_TOPMOST
                X = 200, Y = 200, Width = 600, Height = 400,
            });
            Application.DoEvents();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x020E) HorizontalDelta += (short)((long)m.WParam >> 16);
            base.WndProc(ref m);
        }

        public void Dispose() => DestroyHandle();
    }

    private static RECT Rect(TestWindow window)
    {
        GetWindowRect(window.Hwnd, out var r);
        return r;
    }

    private static void OnStaThread(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            // The thread keeps the process's DPI mode, as the service's worker does, so both read
            // the same coordinates. (The app itself is per-monitor throughout, by its manifest.)
            try { body(); }
            catch (Exception e) { failure = e; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw new Xunit.Sdk.XunitException(failure.ToString());
    }

    private static void Pump(MouseEnhanceService service)
    {
        // The worker waits on this thread's window (repaints, size/move notices), so keep it answering.
        for (int i = 0; i < 20; i++)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
        var done = Task.Run(service.DrainWorker);
        while (!done.IsCompleted) Application.DoEvents();
        for (int i = 0; i < 10; i++)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
    }

    [DesktopFact]
    public void AltRightDrag_FromTheLowerRight_GrowsTheWindow()
    {
        OnStaThread(() =>
        {
            using var form = new TestWindow();
            var start = Rect(form);
            var service = new MouseEnhanceService(() => MouseModifierKeys.Alt);
            var settings = new MouseEnhanceSettings();
            int x = start.Right - 20, y = start.Bottom - 20;
            AssertUnderPoint(form, x, y);

            Assert.True(service.Simulate(settings, MouseEnhanceService.MessageRightDown, x, y));
            Assert.False(service.Simulate(settings, MouseEnhanceService.MessageMove, x + 60, y + 40));
            Pump(service);
            Assert.True(service.Simulate(settings, MouseEnhanceService.MessageRightUp, x + 60, y + 40));
            Pump(service);

            var end = Rect(form);
            Assert.Equal(start.Left, end.Left);
            Assert.Equal(start.Top, end.Top);
            Assert.Equal(start.Right + 60, end.Right);
            Assert.Equal(start.Bottom + 40, end.Bottom);
        });
    }

    [DesktopFact]
    public void AltRightDrag_FromTheLeft_MovesOnlyTheLeftEdge()
    {
        OnStaThread(() =>
        {
            using var form = new TestWindow();
            var start = Rect(form);
            var service = new MouseEnhanceService(() => MouseModifierKeys.Alt);
            var settings = new MouseEnhanceSettings();
            int x = start.Left + 20, y = (start.Top + start.Bottom) / 2;
            AssertUnderPoint(form, x, y);

            Assert.True(service.Simulate(settings, MouseEnhanceService.MessageRightDown, x, y));
            service.Simulate(settings, MouseEnhanceService.MessageMove, x - 50, y + 30);
            Pump(service);
            service.Simulate(settings, MouseEnhanceService.MessageRightUp, x - 50, y + 30);
            Pump(service);

            var end = Rect(form);
            Assert.Equal(start.Left - 50, end.Left);
            Assert.Equal(start.Top, end.Top);
            Assert.Equal(start.Right, end.Right);
            Assert.Equal(start.Bottom, end.Bottom);
        });
    }

    [DesktopFact]
    public void AltRightDrag_OnAMaximizedWindow_RestoresItAndResizes()
    {
        OnStaThread(() =>
        {
            using var form = new TestWindow();
            ShowWindow(form.Hwnd, 3); // SW_MAXIMIZE
            Application.DoEvents();
            var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            GetMonitorInfo(MonitorFromWindow(form.Hwnd, 2), ref info);
            var work = info.rcWork;
            var service = new MouseEnhanceService(() => MouseModifierKeys.Alt);
            var settings = new MouseEnhanceSettings();
            int x = work.Right - 30, y = work.Bottom - 30;
            AssertUnderPoint(form, x, y);

            Assert.True(service.Simulate(settings, MouseEnhanceService.MessageRightDown, x, y));
            service.Simulate(settings, MouseEnhanceService.MessageMove, x - 100, y - 80);
            Pump(service);
            service.Simulate(settings, MouseEnhanceService.MessageRightUp, x - 100, y - 80);
            Pump(service);

            var end = Rect(form);
            Assert.False(IsZoomed(form.Hwnd));
            Assert.Equal(work.Right - work.Left - 100, end.Right - end.Left);
            Assert.Equal(work.Bottom - work.Top - 80, end.Bottom - end.Top);
        });
    }

    [DesktopFact]
    public void CtrlAltWheelDown_FadesTheWindow_AndUpMakesItSolidAgain()
    {
        OnStaThread(() =>
        {
            using var form = new TestWindow();
            var r = Rect(form);
            var service = new MouseEnhanceService(() => MouseModifierKeys.Ctrl | MouseModifierKeys.Alt);
            var settings = new MouseEnhanceSettings();
            int x = (r.Left + r.Right) / 2, y = (r.Top + r.Bottom) / 2;
            AssertUnderPoint(form, x, y);

            Assert.True(service.Simulate(settings, MouseEnhanceService.MessageWheel, x, y, -120));
            Pump(service);
            Assert.True(GetLayeredWindowAttributes(form.Hwnd, out _, out byte faded, out _));
            Assert.Equal(230, faded);

            Assert.True(service.Simulate(settings, MouseEnhanceService.MessageWheel, x, y, 120));
            Pump(service);
            Assert.False(GetLayeredWindowAttributes(form.Hwnd, out _, out _, out _)); // no longer layered
        });
    }

    [DesktopFact]
    public void AltWheelDown_SendsAHorizontalWheelToTheWindowUnderThePointer()
    {
        OnStaThread(() =>
        {
            using var form = new TestWindow();
            var r = Rect(form);
            var service = new MouseEnhanceService(() => MouseModifierKeys.Alt);
            AssertUnderPoint(form, (r.Left + r.Right) / 2, (r.Top + r.Bottom) / 2);

            Assert.True(service.Simulate(new MouseEnhanceSettings(), MouseEnhanceService.MessageWheel,
                (r.Left + r.Right) / 2, (r.Top + r.Bottom) / 2, -120));
            Pump(service);

            Assert.Equal(120, form.HorizontalDelta); // wheel down scrolls right
        });
    }

    [DesktopFact]
    public void MasterSwitchOff_LeavesTheClickAlone()
    {
        OnStaThread(() =>
        {
            using var form = new TestWindow();
            var r = Rect(form);
            var service = new MouseEnhanceService(() => MouseModifierKeys.Alt);
            var settings = new MouseEnhanceSettings { Enabled = false };

            Assert.False(service.Simulate(settings, MouseEnhanceService.MessageRightDown, r.Right - 20, r.Bottom - 20));
        });
    }
}
