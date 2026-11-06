using System.Windows;
using System.Windows.Controls;
using OverTranslate.Models;
using OverTranslate.Services;
// UseWindowsForms puts System.Windows.Forms in the implicit usings
using ComboBox = System.Windows.Controls.ComboBox;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;
using TextBlock = System.Windows.Controls.TextBlock;
using UserControl = System.Windows.Controls.UserControl;

namespace OverTranslate.Views.Settings;

/// <summary>
/// 貼圖操作: one row per action on a pinned picture, each a modifier plus a gesture (Snipaste's
/// page of the same name). Saved as soon as a box changes; open pins read it on every gesture.
/// </summary>
public sealed class PinSettingsPanel : UserControl
{
    private bool _loading;

    public PinSettingsPanel()
    {
        var root = new StackPanel();

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        for (int i = 0; i < 5; i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var s = SettingsService.Instance.Current.Pin;
        _loading = true;

        AddRow(grid, 0, "S.Settings.PinZoom",
            Modifier(s.ZoomModifier, v => Settings.ZoomModifier = v),
            Toggle("S.Settings.PinWheel", s.ZoomWithWheel, v => Settings.ZoomWithWheel = v),
            Toggle("+/-", s.ZoomWithKeys, v => Settings.ZoomWithKeys = v, literal: true));
        AddRow(grid, 1, "S.Settings.PinOpacity",
            Modifier(s.OpacityModifier, v => Settings.OpacityModifier = v),
            Toggle("S.Settings.PinWheel", s.OpacityWithWheel, v => Settings.OpacityWithWheel = v),
            Toggle("+/-", s.OpacityWithKeys, v => Settings.OpacityWithKeys = v, literal: true));
        AddRow(grid, 2, "S.Settings.PinClose",
            Modifier(s.CloseModifier, v => Settings.CloseModifier = v),
            Click(s.CloseGesture, v => Settings.CloseGesture = v),
            null);
        AddRow(grid, 3, "S.Settings.PinReset",
            Modifier(s.ResetModifier, v => Settings.ResetModifier = v),
            Click(s.ResetGesture, v => Settings.ResetGesture = v),
            null);

        _loading = false;
        root.Children.Add(grid);

        var note = new TextBlock { Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap };
        note.SetResourceReference(StyleProperty, "FieldHint");
        note.SetResourceReference(TextBlock.TextProperty, "S.Settings.PinConflictNote");
        root.Children.Add(note);

        Content = root;
    }

    private static PinSettings Settings => SettingsService.Instance.Current.Pin ??= new PinSettings();

    /// <summary>Raised after a change is written, so the page can show 已儲存 like its own settings do.</summary>
    public event EventHandler? Saved;

    private void Save(Action apply)
    {
        if (_loading) return;
        apply();
        SettingsService.Instance.Save();
        Saved?.Invoke(this, EventArgs.Empty);
    }

    private static void AddRow(Grid grid, int row, string labelKey, ComboBox modifier, ComboBox gesture, ComboBox? alternative)
    {
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var label = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
        label.SetResourceReference(StyleProperty, "OptionTitle");
        label.SetResourceReference(TextBlock.TextProperty, labelKey);
        Place(grid, label, row, 0);

        Place(grid, modifier, row, 1);
        Place(grid, Glyph("+"), row, 2);
        Place(grid, gesture, row, 3);

        if (alternative is not null)
        {
            var or = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 8, 0),
            };
            or.SetResourceReference(TextBlock.TextProperty, "S.Settings.PinOr");
            or.SetResourceReference(TextBlock.ForegroundProperty, "AppTextSecondary");
            var pair = new StackPanel { Orientation = Orientation.Horizontal };
            pair.Children.Add(or);
            pair.Children.Add(alternative);
            Place(grid, pair, row, 4);
        }

        static TextBlock Glyph(string text)
        {
            var t = new TextBlock
            {
                Text = text,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(8, 0, 8, 0),
            };
            t.SetResourceReference(TextBlock.ForegroundProperty, "AppTextSecondary");
            return t;
        }

        static void Place(Grid grid, UIElement element, int row, int column)
        {
            if (element is FrameworkElement fe && column > 0 && fe is not TextBlock)
                fe.Margin = new Thickness(fe.Margin.Left, 3, fe.Margin.Right, 3);
            Grid.SetRow(element, row);
            Grid.SetColumn(element, column);
            grid.Children.Add(element);
        }
    }

    private static ComboBox NewBox(double width)
    {
        var box = new ComboBox { Width = width, VerticalAlignment = VerticalAlignment.Center };
        box.SetResourceReference(StyleProperty, "ModernComboBox");
        return box;
    }

    private static ComboBoxItem Item(string text, object tag, bool literal)
    {
        var item = new ComboBoxItem { Tag = tag };
        if (literal) item.Content = text;
        else item.SetResourceReference(ContentProperty, text);
        return item;
    }

    private ComboBox Modifier(PinModifier current, Action<PinModifier> set)
    {
        var box = NewBox(86);
        box.Items.Add(Item("S.Settings.PinNone", PinModifier.None, literal: false));
        box.Items.Add(Item("Ctrl", PinModifier.Ctrl, literal: true));
        box.Items.Add(Item("Shift", PinModifier.Shift, literal: true));
        box.Items.Add(Item("Alt", PinModifier.Alt, literal: true));
        Select(box, current);
        box.SelectionChanged += (_, _) =>
        {
            if (box.SelectedItem is ComboBoxItem { Tag: PinModifier m }) Save(() => set(m));
        };
        return box;
    }

    /// <summary>A gesture that is either on or 無: the wheel, or the +/- keys.</summary>
    private ComboBox Toggle(string onText, bool on, Action<bool> set, bool literal = false)
    {
        var box = NewBox(literal ? 86 : 120);
        box.Items.Add(Item("S.Settings.PinNone", false, literal: false));
        box.Items.Add(Item(onText, true, literal));
        Select(box, on);
        box.SelectionChanged += (_, _) =>
        {
            if (box.SelectedItem is ComboBoxItem { Tag: bool v }) Save(() => set(v));
        };
        return box;
    }

    private ComboBox Click(PinClickGesture current, Action<PinClickGesture> set)
    {
        var box = NewBox(120);
        box.Items.Add(Item("S.Settings.PinNone", PinClickGesture.None, literal: false));
        box.Items.Add(Item("S.Settings.PinLeftDouble", PinClickGesture.LeftDoubleClick, literal: false));
        box.Items.Add(Item("S.Settings.PinMiddleClick", PinClickGesture.MiddleClick, literal: false));
        box.Items.Add(Item("S.Settings.PinMiddleDouble", PinClickGesture.MiddleDoubleClick, literal: false));
        Select(box, current);
        box.SelectionChanged += (_, _) =>
        {
            if (box.SelectedItem is ComboBoxItem { Tag: PinClickGesture g }) Save(() => set(g));
        };
        return box;
    }

    private static void Select(ComboBox box, object value)
    {
        foreach (ComboBoxItem item in box.Items)
            if (Equals(item.Tag, value)) { box.SelectedItem = item; return; }
        box.SelectedIndex = 0;
    }
}

/// <summary>
/// 截圖中貼出選區: what pins the selection during a capture. A capture gesture, not a global
/// shortcut, so it can be the middle button and takes no key from other programs.
/// </summary>
public sealed class PinSelectionTriggerRow : UserControl
{
    public event EventHandler? Saved;

    public PinSelectionTriggerRow()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0) };
        var title = new TextBlock();
        title.SetResourceReference(StyleProperty, "OptionTitle");
        title.SetResourceReference(TextBlock.TextProperty, "S.Settings.PinSelection");
        var hint = new TextBlock();
        hint.SetResourceReference(StyleProperty, "OptionDescription");
        hint.SetResourceReference(TextBlock.TextProperty, "S.Settings.PinSelectionHint");
        text.Children.Add(title);
        text.Children.Add(hint);
        grid.Children.Add(text);

        var box = new ComboBox { Width = 150, VerticalAlignment = VerticalAlignment.Center };
        box.SetResourceReference(StyleProperty, "ModernComboBox");
        Add(box, "S.Settings.PinNone", PinSelectionTrigger.None, literal: false);
        Add(box, "S.Settings.PinMiddleButton", PinSelectionTrigger.MiddleClick, literal: false);
        Add(box, "Ctrl+T", PinSelectionTrigger.CtrlT, literal: true);
        Add(box, "Ctrl+P", PinSelectionTrigger.CtrlP, literal: true);

        var current = SettingsService.Instance.Current.Capture.PinSelectionTrigger;
        foreach (ComboBoxItem item in box.Items)
            if (Equals(item.Tag, current)) box.SelectedItem = item;
        if (box.SelectedItem is null) box.SelectedIndex = 1;

        box.SelectionChanged += (_, _) =>
        {
            if (box.SelectedItem is not ComboBoxItem { Tag: PinSelectionTrigger trigger }) return;
            SettingsService.Instance.Current.Capture.PinSelectionTrigger = trigger;
            SettingsService.Instance.Save();
            Saved?.Invoke(this, EventArgs.Empty);
        };

        Grid.SetColumn(box, 1);
        grid.Children.Add(box);
        Content = grid;

        static void Add(ComboBox box, string text, PinSelectionTrigger tag, bool literal)
        {
            var item = new ComboBoxItem { Tag = tag };
            if (literal) item.Content = text;
            else item.SetResourceReference(ContentProperty, text);
            box.Items.Add(item);
        }
    }
}
