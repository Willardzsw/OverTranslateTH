using System.Diagnostics;
using System.Windows;
using NLog;
using OverTranslate.Services;

namespace OverTranslate.Views.Shell;

/// <summary>Official release notice only. This window has no download or installation action.</summary>
public partial class UpdateWindow : Window
{
    private static UpdateWindow? _instance;
    private readonly UpstreamReleaseInfo _release;

    public static void ShowOrActivate(UpstreamReleaseInfo info)
    {
        if (_instance is not null)
        {
            _instance.Activate();
            return;
        }
        var window = new UpdateWindow(info);
        _instance = window;
        window.Closed += (_, _) => _instance = null;
        window.Show();
    }

    public UpdateWindow(UpstreamReleaseInfo info)
    {
        InitializeComponent();
        _release = info;
        var icon = AppIconService.CreateCompactIcon();
        Icon = icon;
        TitleIcon.Source = icon;
        CurrentVersionText.Text = LocalizationService.Format("S.Update.OfficialBaseline", UpstreamReleaseService.BaselineVersion);
        LatestVersionText.Text = LocalizationService.Format("S.Update.OfficialLatest", info.LatestVersion);
        WindowFrame.Attach(this);
        ThemeService.Changed += OnThemeChanged;
        Closed += (_, _) => ThemeService.Changed -= OnThemeChanged;
    }

    private void OnThemeChanged(object? sender, EventArgs e) => WindowFrame.ApplyAppearance(this);
    private void MinimizeBtn_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();

    private void ReleaseNotesBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(_release.ReleaseNotesUri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            LogManager.GetCurrentClassLogger().Warn(ex, "Could not open official release notes");
            ErrorText.Text = LocalizationService.Get("S.Update.OpenNotesFailed");
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private void SkipVersionLink_Click(object sender, RoutedEventArgs e)
    {
        UpdateNotifier.Skip(_release);
        Close();
    }
}
