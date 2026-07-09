using System.Windows;

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

    private void OnCheckUpdatesNow(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Application.Current is App app)
        {
            app.CheckForUpdatesManually();
        }
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
