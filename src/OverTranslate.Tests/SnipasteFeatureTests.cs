using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OverTranslate.Models;
using OverTranslate.Services;
using OverTranslate.Views.Overlay;
using Xunit;

namespace OverTranslate.Tests;

/// <summary>
/// The Snipaste-style additions: the capture gestures' defaults, the 貼圖 shortcut, and the
/// 馬賽克 / 模糊 / 序號 marks.
/// </summary>
public class SnipasteFeatureTests
{
    private static void OnStaThread(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { body(); }
            catch (Exception e) { failure = e; }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw new Xunit.Sdk.XunitException(failure.ToString());
    }

    [Fact]
    public void CaptureGesturesAreOnByDefault()
    {
        var capture = new CaptureSettings();
        Assert.True(capture.DoubleClickToFinish);
        Assert.True(capture.RightClickToCancel);
    }

    [Fact]
    public void ClipboardPinShortcutDefaultsToF3AndIsLive()
    {
        var settings = new AppSettings();
        var pin = HotkeyBindings.Resolve(settings).Single(b => b.Action == HotkeyAction.Pin);

        Assert.True(pin.IsActive);
        Assert.Equal(ShortcutTrigger.Keyboard(0, 0x72), pin.Trigger);
        Assert.True(HotkeyBindings.IsBindable(pin.Trigger));
    }

    [Fact]
    public void SelectionPinDuringACaptureDefaultsToTheMiddleButton() =>
        Assert.Equal(PinSelectionTrigger.MiddleClick, new CaptureSettings().PinSelectionTrigger);

    [Fact]
    public void PinGestureDefaultsMatchTheOldBehaviour()
    {
        var s = new PinSettings();
        Assert.Equal(PinAction.Zoom, PinGestures.ForWheel(s, System.Windows.Input.ModifierKeys.None));
        Assert.Equal(PinAction.Opacity, PinGestures.ForWheel(s, System.Windows.Input.ModifierKeys.Control));
        Assert.Null(PinGestures.ForWheel(s, System.Windows.Input.ModifierKeys.Shift));
        Assert.Equal(PinAction.Opacity, PinGestures.ForPlusMinus(s, System.Windows.Input.ModifierKeys.Control));
        Assert.Equal(PinAction.Close,
            PinGestures.ForClick(s, System.Windows.Input.ModifierKeys.None, PinClickGesture.LeftDoubleClick));
        Assert.Equal(PinAction.Reset,
            PinGestures.ForClick(s, System.Windows.Input.ModifierKeys.None, PinClickGesture.MiddleClick));
    }

    [Fact]
    public void PinGesturesMatchModifiersExactlyAndTheHigherRowWins()
    {
        var s = new PinSettings
        {
            CloseModifier = PinModifier.Ctrl,
            OpacityModifier = PinModifier.None,     // same as zoom: zoom is listed first
            ResetModifier = PinModifier.Ctrl,
            ResetGesture = PinClickGesture.LeftDoubleClick, // same as close: close is listed first
        };

        Assert.Null(PinGestures.ForClick(s, System.Windows.Input.ModifierKeys.None, PinClickGesture.LeftDoubleClick));
        Assert.Equal(PinAction.Close,
            PinGestures.ForClick(s, System.Windows.Input.ModifierKeys.Control, PinClickGesture.LeftDoubleClick));
        Assert.Equal(PinAction.Zoom, PinGestures.ForWheel(s, System.Windows.Input.ModifierKeys.None));
        Assert.Null(PinGestures.ForWheel(s,
            System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Shift));

        s.ZoomWithWheel = false;
        Assert.Equal(PinAction.Opacity, PinGestures.ForWheel(s, System.Windows.Input.ModifierKeys.None));
    }

    [Fact]
    public void PictureSurvivesTheTripToGdiAndBack()
    {
        // 2x1: opaque red, then half-transparent blue (premultiplied-free BGRA).
        byte[] pixels = [0, 0, 255, 255, 255, 0, 0, 128];
        var source = BitmapSource.Create(2, 1, 96, 96, PixelFormats.Bgra32, null, pixels, 8);

        using var gdi = BitmapInterop.ToGdiBitmap(source);
        Assert.Equal(System.Drawing.Color.FromArgb(255, 255, 0, 0), gdi.GetPixel(0, 0));
        Assert.Equal(System.Drawing.Color.FromArgb(128, 0, 0, 255), gdi.GetPixel(1, 0));

        var back = BitmapInterop.ToBitmapSource(gdi);
        var round = new byte[8];
        back.CopyPixels(round, 8, 0);
        Assert.Equal(pixels, round);
    }

    [Fact]
    public void PixelateAveragesEachBlock()
    {
        // 4x2 picture: left 2x2 block black and white, right 2x2 block all red.
        byte[] pixels =
        [
            0, 0, 0, 255,       255, 255, 255, 255,   0, 0, 255, 255,   0, 0, 255, 255,
            255, 255, 255, 255, 0, 0, 0, 255,         0, 0, 255, 255,   0, 0, 255, 255,
        ];

        InkSurface.Pixelate(pixels, 4, 2, 2);

        for (int i = 0; i < 2; i++)
        {
            int left = i * 16;
            Assert.Equal(127, pixels[left]);
            Assert.Equal(127, pixels[left + 4 + 1]);
            Assert.Equal(255, pixels[left + 8 + 2]);
            Assert.Equal(0, pixels[left + 12]);
        }
    }

    [Fact]
    public void BlurLeavesAFlatColourAloneAndSoftensAnEdge()
    {
        const int w = 20, h = 4;
        var flat = Enumerable.Range(0, w * h).SelectMany(_ => new byte[] { 40, 80, 120, 255 }).ToArray();
        InkSurface.BoxBlur(flat, w, h, 4);
        Assert.All(Enumerable.Range(0, w * h), i =>
        {
            Assert.Equal(40, flat[i * 4]);
            Assert.Equal(120, flat[i * 4 + 2]);
        });

        // Black on the left half, white on the right: after blurring the two pixels either side of
        // the edge are no longer pure black or white.
        var edge = new byte[w * h * 4];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                byte v = x < w / 2 ? (byte)0 : (byte)255;
                int o = (y * w + x) * 4;
                edge[o] = edge[o + 1] = edge[o + 2] = v;
                edge[o + 3] = 255;
            }

        InkSurface.BoxBlur(edge, w, h, 4);
        int before = (1 * w + w / 2 - 1) * 4, after = (1 * w + w / 2) * 4;
        Assert.InRange(edge[before], 1, 254);
        Assert.InRange(edge[after], 1, 254);
        Assert.True(edge[before] < edge[after]);
    }

    [Fact]
    public void RegionPatchCoversWholeCapturePixelsAtTheSurfaceScale()
    {
        OnStaThread(() =>
        {
            var source = BitmapSource.Create(
                100, 80, 96, 96, PixelFormats.Bgra32, null,
                Enumerable.Range(0, 100 * 80).SelectMany(_ => new byte[] { 10, 20, 30, 255 }).ToArray(), 400);

            var patch = InkSurface.MakeRegion(
                source, new Rect(new Point(10.2, 5), new Point(20, 15.5)), 1.5, AnnotationTool.Mosaic, 4);

            Assert.NotNull(patch);
            Assert.Equal(15, patch!.Image.PixelWidth);   // floor(15.3) .. ceil(30)
            Assert.Equal(17, patch.Image.PixelHeight);   // floor(7.5) .. ceil(23.25)
            Assert.Equal(10, patch.Dip.X, 6);
            Assert.Equal(7 / 1.5, patch.Dip.Y, 6);
        });
    }

    [Fact]
    public void RegionAndNumberMarksAreFoundByTheEraser()
    {
        var mosaic = new AnnotationStroke
        {
            Tool = AnnotationTool.Mosaic, Color = Colors.Transparent, Thickness = 12, Opacity = 1,
            Points = [new Point(10, 10), new Point(50, 30)],
        };
        Assert.True(mosaic.IsRegion);
        Assert.True(mosaic.IsWithin(new Point(30, 20), 1));
        Assert.False(mosaic.IsWithin(new Point(80, 80), 5));
        // The block size is not a nib: the bounds are the box, not the box widened by it.
        Assert.True(mosaic.Bounds.Width < 45);

        var number = new AnnotationStroke
        {
            Tool = AnnotationTool.Number, Color = Colors.Red, Thickness = 30, Opacity = 1,
            Points = [new Point(100, 100)], Text = "1",
        };
        Assert.True(number.IsWithin(new Point(110, 100), 1));
        Assert.False(number.IsWithin(new Point(140, 100), 1));
        Assert.True(number.Bounds.Contains(new Point(114, 114)));
    }

    [Fact]
    public void NumberedDiscsAreDrawnIntoTheInk()
    {
        OnStaThread(() =>
        {
            var ink = new InkSurface();
            ink.Ensure(new Rect(0, 0, 100, 100), 1);
            ink.Lay(new AnnotationStroke
            {
                Tool = AnnotationTool.Number, Color = Colors.Red, Thickness = 30, Opacity = 1,
                Points = [new Point(50, 50)], Text = "3",
            });

            Assert.True(ink.InkAt(new Point(40, 50)) > 0.9);
            Assert.Equal(0, ink.InkAt(new Point(5, 5)));
        });
    }

    [Fact]
    public void MosaicIsPaintedFromTheBackdrop()
    {
        OnStaThread(() =>
        {
            var ink = new InkSurface();
            ink.Ensure(new Rect(0, 0, 60, 60), 1);
            ink.Backdrop = BitmapSource.Create(
                60, 60, 96, 96, PixelFormats.Bgra32, null,
                Enumerable.Range(0, 60 * 60).SelectMany(_ => new byte[] { 0, 0, 255, 255 }).ToArray(), 240);

            ink.Lay(new AnnotationStroke
            {
                Tool = AnnotationTool.Mosaic, Color = Colors.Transparent, Thickness = 6, Opacity = 1,
                Points = [new Point(10, 10), new Point(40, 40)],
            });

            Assert.True(ink.InkAt(new Point(25, 25)) > 0.99);
            Assert.Equal(0, ink.InkAt(new Point(50, 50)));
        });
    }
}
