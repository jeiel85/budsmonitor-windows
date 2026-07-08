using System.Windows;

namespace BudsMonitor.App;

/// <summary>
/// Settings window. In GOAL 1 this is a placeholder that lays out the sections
/// from the UI/UX spec; the controls are disabled until later goals wire them up.
/// </summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
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
