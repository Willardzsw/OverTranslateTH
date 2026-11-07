using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using OverTranslate.Models;
using OverTranslate.Services;
// UseWindowsForms puts System.Windows.Forms in the implicit usings
using CheckBox = System.Windows.Controls.CheckBox;
using Orientation = System.Windows.Controls.Orientation;
using TextBlock = System.Windows.Controls.TextBlock;
using UserControl = System.Windows.Controls.UserControl;

namespace OverTranslate.Views.Settings;

/// <summary>
/// 托盤右鍵選單: which feature switches the tray icon's right-click menu offers. Ticked means shown.
/// </summary>
public sealed class TrayMenuOptionsPanel : UserControl
{
    private bool _loading;
    private readonly Dictionary<FeatureToggle, CheckBox> _boxes = [];

    public TrayMenuOptionsPanel()
    {
        var root = new StackPanel();

        var grid = new UniformGrid { Columns = 2 };
        foreach (var toggle in FeatureToggles.All)
        {
            var box = new CheckBox { Margin = new Thickness(0, 3, 8, 3), VerticalContentAlignment = VerticalAlignment.Center };
            box.SetResourceReference(StyleProperty, "ModernCheckBox");
            var label = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(6, 0, 0, 0) };
            label.SetResourceReference(TextBlock.TextProperty, FeatureToggles.NameKey(toggle));
            label.SetResourceReference(TextBlock.ForegroundProperty, "AppTextPrimary");
            box.Content = label;
            box.Checked += (_, _) => Save(toggle, shown: true);
            box.Unchecked += (_, _) => Save(toggle, shown: false);
            _boxes[toggle] = box;
            grid.Children.Add(box);
        }
        root.Children.Add(grid);

        Content = root;
        Reload();
    }

    /// <summary>Raised after a change is written, so the page can show 已儲存.</summary>
    public event EventHandler? Saved;

    public void Reload()
    {
        _loading = true;
        try
        {
            var hidden = SettingsService.Instance.Current.TrayMenu?.HiddenToggles ?? [];
            foreach (var (toggle, box) in _boxes) box.IsChecked = !hidden.Contains(toggle);
        }
        finally
        {
            _loading = false;
        }
    }

    private void Save(FeatureToggle toggle, bool shown)
    {
        if (_loading) return;
        var menu = SettingsService.Instance.Current.TrayMenu ??= new TrayMenuSettings();
        menu.HiddenToggles.Remove(toggle);
        if (!shown) menu.HiddenToggles.Add(toggle);
        SettingsService.Instance.Save();
        Saved?.Invoke(this, EventArgs.Empty);
    }
}
