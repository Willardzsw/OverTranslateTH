using System.Windows;
using System.Windows.Controls;
using OverTranslate.Models;
using OverTranslate.Services;
// UseWindowsForms puts System.Windows.Forms in the implicit usings
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using Orientation = System.Windows.Controls.Orientation;
using TextBlock = System.Windows.Controls.TextBlock;
using UserControl = System.Windows.Controls.UserControl;

namespace OverTranslate.Views.Settings;

/// <summary>
/// 鼠標增強設定: the fold under the 鼠標增強 row, like 貼圖操作 under 貼圖. One section per tool —
/// its tick, its keys and its own options. The row's switch above is the master switch.
/// </summary>
public sealed class MouseEnhancePanel : UserControl
{
    private bool _loading;
    private readonly List<Action> _reloads = [];
    private readonly List<(CheckBox Toggle, FrameworkElement Controls)> _sections = [];

    public MouseEnhancePanel()
    {
        _loading = true;
        var root = new StackPanel();

        // 窗口透明度
        var fadeKeys = Modifier(v => Settings.TransparencyModifier = v);
        var step = Percent([5, 10, 15, 20, 25], v => Settings.TransparencyStep = v);
        var minimum = Percent([10, 20, 30, 40, 50], v => Settings.MinimumOpacity = v);
        root.Children.Add(Section("S.Settings.MouseFade", "S.Settings.MouseFadeHint",
            s => s.TransparencyEnabled, (s, on) => s.TransparencyEnabled = on,
            Row(fadeKeys, Glyph("+"), Label("S.Settings.MouseWheelGesture"), Spacer(),
                Label("S.Settings.MouseFadeStep"), step, Spacer(),
                Label("S.Settings.MouseFadeMinimum"), minimum)));
        _reloads.Add(() =>
        {
            Select(fadeKeys, Settings.TransparencyModifier);
            SelectPercent(step, Settings.TransparencyStep);
            SelectPercent(minimum, Settings.MinimumOpacity);
        });

        // 橫向捲動
        var scrollKeys = Modifier(v => Settings.HorizontalScrollModifier = v);
        var reverse = new CheckBox { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
        reverse.SetResourceReference(StyleProperty, "ModernCheckBox");
        reverse.Click += (_, _) => Save(() => Settings.HorizontalScrollReversed = reverse.IsChecked == true);
        root.Children.Add(Section("S.Settings.MouseScroll", "S.Settings.MouseScrollHint",
            s => s.HorizontalScrollEnabled, (s, on) => s.HorizontalScrollEnabled = on,
            Row(scrollKeys, Glyph("+"), Label("S.Settings.MouseWheelGesture"), Spacer(),
                reverse, Label("S.Settings.MouseScrollReverse"))));
        _reloads.Add(() =>
        {
            Select(scrollKeys, Settings.HorizontalScrollModifier);
            reverse.IsChecked = Settings.HorizontalScrollReversed;
        });

        // 調整視窗大小
        var resizeKeys = Modifier(v => Settings.ResizeModifier = v);
        var button = DragButton(v => Settings.ResizeButton = v);
        root.Children.Add(Section("S.Settings.MouseResize", "S.Settings.MouseResizeHint",
            s => s.ResizeEnabled, (s, on) => s.ResizeEnabled = on,
            Row(resizeKeys, Glyph("+"), button)));
        _reloads.Add(() =>
        {
            Select(resizeKeys, Settings.ResizeModifier);
            Select(button, Settings.ResizeButton);
        });

        Content = root;
        _loading = false;
        Reload();
    }

    private static MouseEnhanceSettings Settings =>
        SettingsService.Instance.Current.MouseEnhance ??= new MouseEnhanceSettings();

    /// <summary>Raised after a change is written, so the page can show 已儲存 and its summary.</summary>
    public event EventHandler? Saved;

    /// <summary>Shows what is stored now.</summary>
    public void Reload()
    {
        _loading = true;
        try
        {
            foreach (var reload in _reloads) reload();
        }
        finally
        {
            _loading = false;
        }

        RefreshEnabled();
    }

    /// <summary>
    /// What is set, in one line for the row: "Alt+滾輪 · Shift+滾輪 · Alt+右鍵拖曳", tools that are off
    /// left out.
    /// </summary>
    public static string Summary(MouseEnhanceSettings s)
    {
        var wheel = LocalizationService.Get("S.Settings.MouseWheelGesture");
        var parts = new List<string>(3);
        if (s.TransparencyEnabled) parts.Add($"{MouseEnhanceRules.Display(s.TransparencyModifier)}+{wheel}");
        if (s.HorizontalScrollEnabled) parts.Add($"{MouseEnhanceRules.Display(s.HorizontalScrollModifier)}+{wheel}");
        if (s.ResizeEnabled)
        {
            var drag = LocalizationService.Get(s.ResizeButton switch
            {
                MouseResizeButton.Middle => "S.Settings.MouseMiddleDrag",
                MouseResizeButton.Left => "S.Settings.MouseLeftDrag",
                _ => "S.Settings.MouseRightDrag",
            });
            parts.Add($"{MouseEnhanceRules.Display(s.ResizeModifier)}+{drag}");
        }
        return string.Join(" · ", parts);
    }

    private void Save(Action apply)
    {
        if (_loading) return;
        apply();
        SettingsService.Instance.Save();
        MouseEnhanceService.Instance.Apply(Settings);
        RefreshEnabled();
        Saved?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshEnabled()
    {
        foreach (var (toggle, controls) in _sections)
            controls.IsEnabled = toggle.IsChecked == true;
    }

    private FrameworkElement Section(string titleKey, string hintKey,
        Func<MouseEnhanceSettings, bool> isOn, Action<MouseEnhanceSettings, bool> setOn, FrameworkElement controls)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var toggle = new CheckBox { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 1, 10, 0) };
        toggle.SetResourceReference(StyleProperty, "ModernCheckBox");
        toggle.SetResourceReference(ToolTipProperty, titleKey);
        toggle.Click += (_, _) => Save(() => setOn(Settings, toggle.IsChecked == true));
        _reloads.Add(() => toggle.IsChecked = isOn(Settings));
        grid.Children.Add(toggle);

        var text = new StackPanel();
        var title = new TextBlock();
        title.SetResourceReference(StyleProperty, "OptionTitle");
        title.SetResourceReference(TextBlock.TextProperty, titleKey);
        var hint = new TextBlock();
        hint.SetResourceReference(StyleProperty, "OptionDescription");
        hint.SetResourceReference(TextBlock.TextProperty, hintKey);
        controls.Margin = new Thickness(0, 6, 0, 0);
        text.Children.Add(title);
        text.Children.Add(hint);
        text.Children.Add(controls);
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        _sections.Add((toggle, controls));
        return grid;
    }

    private static WrapPanel Row(params UIElement[] children)
    {
        var row = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var child in children)
        {
            if (child is FrameworkElement fe) fe.Margin = new Thickness(fe.Margin.Left, 2, fe.Margin.Right, 2);
            row.Children.Add(child);
        }
        return row;
    }

    private static TextBlock Glyph(string text)
    {
        var t = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0) };
        t.SetResourceReference(TextBlock.ForegroundProperty, "AppTextSecondary");
        return t;
    }

    private static TextBlock Label(string key)
    {
        var t = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
        t.SetResourceReference(TextBlock.ForegroundProperty, "AppTextSecondary");
        t.SetResourceReference(TextBlock.TextProperty, key);
        return t;
    }

    private static FrameworkElement Spacer() => new Border { Width = 14 };

    private static ComboBox NewBox(double width)
    {
        var box = new ComboBox { Width = width, VerticalAlignment = VerticalAlignment.Center };
        box.SetResourceReference(StyleProperty, "ModernComboBox");
        return box;
    }

    private ComboBox Modifier(Action<MouseModifierKeys> set)
    {
        var box = NewBox(108);
        foreach (var keys in MouseEnhanceRules.Choices)
            box.Items.Add(new ComboBoxItem { Content = MouseEnhanceRules.Display(keys), Tag = keys });
        box.SelectionChanged += (_, _) =>
        {
            if (box.SelectedItem is ComboBoxItem { Tag: MouseModifierKeys k }) Save(() => set(k));
        };
        return box;
    }

    private ComboBox Percent(int[] values, Action<int> set)
    {
        var box = NewBox(74);
        foreach (var v in values)
            box.Items.Add(new ComboBoxItem { Content = $"{v}%", Tag = v });
        box.SelectionChanged += (_, _) =>
        {
            if (box.SelectedItem is ComboBoxItem { Tag: int v }) Save(() => set(v));
        };
        return box;
    }

    private static void SelectPercent(ComboBox box, int value)
    {
        if (!box.Items.Cast<ComboBoxItem>().Any(i => Equals(i.Tag, value)))
            box.Items.Add(new ComboBoxItem { Content = $"{value}%", Tag = value });
        Select(box, value);
    }

    private ComboBox DragButton(Action<MouseResizeButton> set)
    {
        var box = NewBox(130);
        foreach (var (key, button) in new[]
                 {
                     ("S.Settings.MouseRightDrag", MouseResizeButton.Right),
                     ("S.Settings.MouseMiddleDrag", MouseResizeButton.Middle),
                     ("S.Settings.MouseLeftDrag", MouseResizeButton.Left),
                 })
        {
            var item = new ComboBoxItem { Tag = button };
            item.SetResourceReference(ContentProperty, key);
            box.Items.Add(item);
        }
        box.SelectionChanged += (_, _) =>
        {
            if (box.SelectedItem is ComboBoxItem { Tag: MouseResizeButton b }) Save(() => set(b));
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
