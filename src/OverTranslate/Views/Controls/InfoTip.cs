using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
// UseWindowsForms puts System.Windows.Forms in the implicit usings
using Brushes = System.Windows.Media.Brushes;
using Cursors = System.Windows.Input.Cursors;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;
using TextBlock = System.Windows.Controls.TextBlock;

namespace OverTranslate.Views.Controls;

/// <summary>
/// ⓘ after a setting's title: its explanation, shown while the pointer rests on it (half a second
/// in) and gone when the pointer leaves. Every settings explanation is told this way, so a row is
/// its title and its controls and nothing else.
/// </summary>
/// <remarks>
/// <see cref="TextKey"/> is the explanation; <see cref="ExtraKey"/>, when set, a second paragraph —
/// the note a row used to carry under it. Both are string keys, so the hint follows a change of
/// interface language, and [[…]] in them is highlighted as it was in the row.
/// </remarks>
public sealed class InfoTip : Border
{
    public static readonly DependencyProperty TextKeyProperty = DependencyProperty.Register(
        nameof(TextKey), typeof(string), typeof(InfoTip), new PropertyMetadata(null, (d, _) => ((InfoTip)d).Build()));

    public static readonly DependencyProperty ExtraKeyProperty = DependencyProperty.Register(
        nameof(ExtraKey), typeof(string), typeof(InfoTip), new PropertyMetadata(null, (d, _) => ((InfoTip)d).Build()));

    public string? TextKey
    {
        get => (string?)GetValue(TextKeyProperty);
        set => SetValue(TextKeyProperty, value);
    }

    public string? ExtraKey
    {
        get => (string?)GetValue(ExtraKeyProperty);
        set => SetValue(ExtraKeyProperty, value);
    }

    public InfoTip()
    {
        Width = 18;
        Height = 18;
        Margin = new Thickness(6, 0, 0, 0);
        VerticalAlignment = VerticalAlignment.Center;
        // Hit-testable across the whole square, not only along the glyph's strokes.
        Background = Brushes.Transparent;
        Cursor = Cursors.Help;
        Focusable = false;

        var mark = new Path
        {
            Width = 12,
            Height = 12,
            Stretch = Stretch.None,
            StrokeThickness = 1.1,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        mark.SetResourceReference(Path.DataProperty, "InfoMarkGeometry");
        mark.SetResourceReference(Shape.StrokeProperty, "AppTextSecondary");
        Child = mark;
    }

    private void Build()
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical, MaxWidth = 400 };
        if (!string.IsNullOrEmpty(TextKey)) panel.Children.Add(Paragraph(TextKey, top: 0));
        if (!string.IsNullOrEmpty(ExtraKey)) panel.Children.Add(Paragraph(ExtraKey, top: panel.Children.Count > 0 ? 6 : 0));
        HoverToolTip.SetContent(this, panel.Children.Count > 0 ? panel : null);
        if (!string.IsNullOrEmpty(TextKey))
            System.Windows.Automation.AutomationProperties.SetHelpText(this, Services.LocalizationService.Get(TextKey));
    }

    private static TextBlock Paragraph(string key, double top)
    {
        var text = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, top, 0, 0), LineHeight = 18 };
        text.SetResourceReference(TextBlock.FontFamilyProperty, "AppFont");
        text.SetResourceReference(HighlightedText.SourceProperty, key);
        return text;
    }
}
