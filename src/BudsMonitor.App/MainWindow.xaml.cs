using System.ComponentModel;
using System.Windows;

namespace BudsMonitor.App;

/// <summary>
/// Dashboard window. In GOAL 1 this is a placeholder empty-state. Pressing the
/// close button hides the window to the tray instead of exiting the app.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (System.Windows.Application.Current is App { IsShuttingDown: false })
        {
            // Close button minimizes to tray; the app keeps monitoring in the background.
            e.Cancel = true;
            Hide();
        }

        base.OnClosing(e);
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Application.Current is App app)
        {
            app.ShowSettingsWindow();
        }
    }
}
