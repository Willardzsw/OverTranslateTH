using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OverTranslate.Models;
using OverTranslate.Services;
// UseWindowsForms puts System.Windows.Forms in the implicit usings
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;
using TextBlock = System.Windows.Controls.TextBlock;
using UserControl = System.Windows.Controls.UserControl;

namespace OverTranslate.Views.StickyNote;

/// <summary>
/// 便利貼 in the main window: every note on the desktop, newest save first, each with 定位, 編輯,
/// 置頂, 穿透 and 刪除, and 新建便利貼 at the top.
/// </summary>
public sealed class StickyNotesPage : UserControl
{
    private readonly StackPanel _list = new();
    private readonly TextBlock _empty = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 8, 0, 0) };

    public StickyNotesPage()
    {
        var root = new Grid { Margin = new Thickness(28, 22, 28, 20) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new Grid { Margin = new Thickness(0, 0, 0, 18) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var titles = new StackPanel();
        var title = new TextBlock();
        title.SetResourceReference(StyleProperty, "PageTitle");
        title.SetResourceReference(TextBlock.TextProperty, "S.Notes.Title");
        var subtitle = new TextBlock();
        subtitle.SetResourceReference(StyleProperty, "PageSubtitle");
        subtitle.SetResourceReference(TextBlock.TextProperty, "S.Notes.Subtitle");
        titles.Children.Add(title);
        titles.Children.Add(subtitle);
        header.Children.Add(titles);

        var create = new Button { Padding = new Thickness(14, 6, 14, 6), VerticalAlignment = VerticalAlignment.Center };
        create.SetResourceReference(StyleProperty, "AccentButton");
        create.SetResourceReference(ContentProperty, "S.Notes.New");
        create.Click += (_, _) =>
        {
            var shell = Shell.ShellWindow.Current is { } w ? ScreenGeometry.PhysicalBounds(w) : System.Drawing.Rectangle.Empty;
            var at = shell.IsEmpty
                ? (System.Drawing.Point?)null
                : new System.Drawing.Point(shell.Left + shell.Width / 2 - 120, shell.Top + shell.Height / 2 - 100);
            StickyNoteManager.Create("", startEditing: true, at);
        };
        Grid.SetColumn(create, 1);
        header.Children.Add(create);
        root.Children.Add(header);

        _empty.SetResourceReference(StyleProperty, "FieldHint");
        _empty.SetResourceReference(TextBlock.TextProperty, "S.Notes.Empty");
        var content = new StackPanel { MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Stretch };
        content.Children.Add(_empty);
        content.Children.Add(_list);
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(0, 0, 8, 0),
            Content = content,
        };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);
        Content = root;

        Loaded += (_, _) =>
        {
            StickyNoteManager.Changed += OnChanged;
            LocalizationService.LanguageChanged += OnChanged;
            Reload();
        };
        Unloaded += (_, _) =>
        {
            StickyNoteManager.Changed -= OnChanged;
            LocalizationService.LanguageChanged -= OnChanged;
        };
    }

    private void OnChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(Reload);

    public void Reload()
    {
        _list.Children.Clear();
        var notes = StickyNoteManager.Notes;
        _empty.Visibility = notes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var note in notes) _list.Children.Add(Row(note));
    }

    private static FrameworkElement Row(StickyNoteWindow note)
    {
        var border = new Border { Margin = new Thickness(0, 0, 0, 10) };
        border.SetResourceReference(StyleProperty, "ToggleRowBorder");
        border.Padding = new Thickness(12, 10, 12, 10);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // The note's own colours, as a small sheet with its tape.
        var s = StickyNoteManager.Settings;
        var swatch = new Grid { Width = 30, Height = 34, Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Top };
        swatch.Children.Add(new Border
        {
            Margin = new Thickness(0, 5, 0, 0),
            CornerRadius = new CornerRadius(2),
            Background = new SolidColorBrush(StickyNoteColors.Parse(s.PaperColor, StickyNoteColors.DefaultPaper)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x30, 0, 0, 0)),
            BorderThickness = new Thickness(1),
        });
        var tape = StickyNoteColors.Parse(s.TapeColor, StickyNoteColors.DefaultTape);
        swatch.Children.Add(new Border
        {
            Width = 16,
            Height = 7,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 2, 0, 0),
            Background = new SolidColorBrush(Color.FromArgb(0xC0, tape.R, tape.G, tape.B)),
        });
        grid.Children.Add(swatch);

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var preview = new TextBlock { Text = note.Preview, TextWrapping = TextWrapping.Wrap, MaxHeight = 60, TextTrimming = TextTrimming.CharacterEllipsis };
        // OptionTitle lives in the settings page's own resources, so the same look is set here.
        preview.SetResourceReference(TextBlock.FontFamilyProperty, "AppFont");
        preview.SetResourceReference(TextBlock.ForegroundProperty, "AppTextPrimary");
        preview.FontSize = 14;
        var states = new List<string> { note.Data.Saved.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) };
        if (note.Data.Topmost) states.Add(LocalizationService.Get("S.Notes.StateTopmost"));
        if (note.Data.PassThrough) states.Add(LocalizationService.Get("S.Notes.StatePassThrough"));
        var meta = new TextBlock { Text = string.Join(" · ", states), Margin = new Thickness(0, 4, 0, 0) };
        meta.SetResourceReference(StyleProperty, "FieldHint");
        text.Children.Add(preview);
        text.Children.Add(meta);
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        var actions = new WrapPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
        actions.Children.Add(Action("", "S.Notes.Locate", note.Locate));
        actions.Children.Add(Action("", "S.Note.Edit", () =>
        {
            note.Locate();
            note.BeginEdit();
        }));
        actions.Children.Add(Action(note.Data.Topmost ? "" : "",
            note.Data.Topmost ? "S.Note.Unpin" : "S.Note.Topmost", () => note.SetTopmost(!note.Data.Topmost)));
        actions.Children.Add(Action("", note.Data.PassThrough ? "S.Notes.PassThroughOff" : "S.Notes.PassThroughOn",
            () => note.SetPassThrough(!note.Data.PassThrough), highlighted: note.Data.PassThrough));
        actions.Children.Add(Action("", "S.Note.Delete", note.Delete));
        Grid.SetColumn(actions, 2);
        grid.Children.Add(actions);

        border.Child = grid;
        return border;
    }

    private static Button Action(string glyph, string tipKey, Action click, bool highlighted = false)
    {
        var button = new Button
        {
            Content = new TextBlock { Text = glyph, FontFamily = new System.Windows.Media.FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 14 },
            Width = 34,
            Height = 32,
            Margin = new Thickness(4, 0, 0, 0),
        };
        button.SetResourceReference(StyleProperty, highlighted ? "AccentButton" : "FlatButton");
        button.SetResourceReference(ToolTipProperty, tipKey);
        System.Windows.Automation.AutomationProperties.SetName(button, LocalizationService.Get(tipKey));
        button.Click += (_, _) => click();
        return button;
    }
}
