using System.Windows;
using System.Windows.Media.Animation;

namespace BudsMonitor.App;

/// <summary>
/// Settings window. In GOAL 1 this is a placeholder that lays out the sections
/// from the UI/UX spec; the controls are disabled until later goals wire them up.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly bool _initialized;

    public SettingsWindow()
    {
        InitializeComponent();

        if (System.Windows.Application.Current is App app)
        {
            VersionText.Text = app.GetCurrentVersionString();
            CheckUpdatesCheckBox.IsChecked = app.IsUpdateCheckOnStartup;
            ThemeCombo.SelectedIndex = app.GetThemeIndex();
        }

        _initialized = true;
    }

    private void OnThemeChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_initialized && System.Windows.Application.Current is App app && ThemeCombo.SelectedIndex >= 0)
        {
            app.SetTheme(ThemeCombo.SelectedIndex);
        }
    }

    private void OnUpdateCheckToggled(object sender, RoutedEventArgs e)
    {
        if (_initialized && System.Windows.Application.Current is App app
            && sender is System.Windows.Controls.CheckBox checkBox)
        {
            app.SetUpdateCheckOnStartup(checkBox.IsChecked == true);
        }
    }

    private async void OnCheckUpdatesNow(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Application.Current is not App app)
        {
            return;
        }

        SetUpdateStatus("확인 중…", "InkMutedBrush", persistent: true);
        var result = await app.CheckNowAsync();
        switch (result.Outcome)
        {
            case App.ManualUpdateOutcome.UpToDate:
                SetUpdateStatus("✓ " + result.Message, "AccentGreenBrush", persistent: false);
                break;
            case App.ManualUpdateOutcome.UpdateAvailable:
                HideUpdateStatus();  // the update dialog is now on screen
                break;
            default:
                SetUpdateStatus(result.Message, "AccentOrangeBrush", persistent: false);
                break;
        }
    }

    /// <summary>Shows the inline update status; transient messages fade out after a beat.</summary>
    private void SetUpdateStatus(string text, string brushKey, bool persistent)
    {
        UpdateStatusText.BeginAnimation(OpacityProperty, null);
        UpdateStatusText.Text = text;
        UpdateStatusText.Foreground = TryFindResource(brushKey) as System.Windows.Media.Brush ?? UpdateStatusText.Foreground;
        UpdateStatusText.Opacity = 1;
        UpdateStatusText.Visibility = Visibility.Visible;

        if (persistent)
        {
            return;
        }

        var fade = new DoubleAnimation
        {
            From = 1,
            To = 0,
            BeginTime = TimeSpan.FromSeconds(2.2),
            Duration = TimeSpan.FromMilliseconds(600),
        };
        fade.Completed += (_, _) => UpdateStatusText.Visibility = Visibility.Collapsed;
        UpdateStatusText.BeginAnimation(OpacityProperty, fade);
    }

    private void HideUpdateStatus()
    {
        UpdateStatusText.BeginAnimation(OpacityProperty, null);
        UpdateStatusText.Visibility = Visibility.Collapsed;
    }

    private void OnShowHiddenChanged(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Application.Current is App app && sender is System.Windows.Controls.CheckBox checkBox)
        {
            app.SetShowHiddenDevices(checkBox.IsChecked == true);
        }
    }

    private void OnOpenDiagnostics(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Application.Current is App app)
        {
            app.ShowDiagnosticsWindow();
        }
    }

    private void OnOpenLogs(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Application.Current is App app)
        {
            app.OpenLogsFolder();
        }
    }
}
