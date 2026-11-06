using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
// UseWindowsForms puts System.Drawing and System.Windows.Forms in the implicit usings
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Image = System.Windows.Controls.Image;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace OverTranslate.Views.Pin;

/// <summary>
/// What a pin looks like: the picture, a thin accent frame, the shadow, and the badge that shows
/// the zoom or opacity. Built here for both the pin and its zoom canvas, because the hand-over
/// between the two is only invisible while they draw exactly the same thing.
/// </summary>
internal sealed class PinChrome
{
    public Grid Surface { get; }
    public Image View { get; }
    public Border Frame { get; }
    public TextBlock Badge { get; }

    public PinChrome(BitmapSource image)
    {
        View = new Image { Source = image, Stretch = Stretch.Fill, SnapsToDevicePixels = true };

        Frame = new Border { BorderThickness = new Thickness(1), IsHitTestVisible = false };
        SetFrameColour(passThrough: false);

        Badge = new TextBlock
        {
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromArgb(0xB0, 0, 0, 0)),
            Padding = new Thickness(6, 2, 6, 2),
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(4),
            TextWrapping = TextWrapping.Wrap,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed,
        };

        Surface = new Grid
        {
            Background = Brushes.White,
            Effect = new DropShadowEffect { BlurRadius = 14, ShadowDepth = 0, Opacity = 0.45, Color = Colors.Black },
        };
        Surface.Children.Add(View);
        Surface.Children.Add(Frame);
        Surface.Children.Add(Badge);
    }

    /// <summary>The accent edge normally; a grey one while the pin lets clicks through.</summary>
    public void SetFrameColour(bool passThrough) =>
        Frame.SetResourceReference(Border.BorderBrushProperty, passThrough ? "AppTextSecondary" : "AppAccent");

    public void SetBadge(string? text)
    {
        Badge.Text = text ?? "";
        Badge.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>Pixel for pixel at the picture's own size, smoothly resampled otherwise.</summary>
    public void SetScaling(double zoom) =>
        RenderOptions.SetBitmapScalingMode(View,
            Math.Abs(zoom - 1) < 1e-9 ? BitmapScalingMode.NearestNeighbor : BitmapScalingMode.HighQuality);
}
