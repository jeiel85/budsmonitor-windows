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
            PairedOnlyCheckBox.IsChecked = app.IsShowPairedDevicesOnly;

            StartWithWindowsCheckBox.IsChecked = app.IsStartWithWindows;
            MinimizeToTrayCheckBox.IsChecked = app.MinimizeToTrayOnClose;

            var n = app.CurrentNotifications;
            NotifyEnabledCheckBox.IsChecked = n.Enabled;
            EarbudThresholdBox.Text = n.EarbudLowBatteryThreshold.ToString();
            CaseThresholdBox.Text = n.CaseLowBatteryThreshold.ToString();
            SuppressRepeatCheckBox.IsChecked = n.SuppressRepeatedMinutes > 0;
            SuppressMinutesBox.Text = (n.SuppressRepeatedMinutes > 0 ? n.SuppressRepeatedMinutes : 60).ToString();
            SuppressMinutesBox.IsEnabled = n.SuppressRepeatedMinutes > 0;
            QuietHoursCheckBox.IsChecked = n.QuietHoursEnabled;
            QuietStartBox.Text = n.QuietHoursStart;
            QuietEndBox.Text = n.QuietHoursEnd;

            MaskAddressesCheckBox.IsChecked = app.IsMaskBluetoothAddresses;
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

    private void OnPairedOnlyChanged(object sender, RoutedEventArgs e)
    {
        if (_initialized && System.Windows.Application.Current is App app
            && sender is System.Windows.Controls.CheckBox checkBox)
        {
            app.SetShowPairedDevicesOnly(checkBox.IsChecked == true);
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

    // ----- 일반 -----

    private void OnStartWithWindowsChanged(object sender, RoutedEventArgs e)
    {
        if (_initialized && System.Windows.Application.Current is App app
            && sender is System.Windows.Controls.CheckBox cb)
        {
            app.SetStartWithWindows(cb.IsChecked == true);
        }
    }

    private void OnMinimizeToTrayChanged(object sender, RoutedEventArgs e)
    {
        if (_initialized && System.Windows.Application.Current is App app
            && sender is System.Windows.Controls.CheckBox cb)
        {
            app.SetMinimizeToTray(cb.IsChecked == true);
        }
    }

    // ----- 알림 -----

    private void OnNotifyEnabledChanged(object sender, RoutedEventArgs e)
    {
        if (_initialized && System.Windows.Application.Current is App app
            && sender is System.Windows.Controls.CheckBox cb)
        {
            app.UpdateNotifications(n => n with { Enabled = cb.IsChecked == true });
        }
    }

    private void OnThresholdChanged(object sender, RoutedEventArgs e)
    {
        if (!_initialized || System.Windows.Application.Current is not App app)
        {
            return;
        }

        app.UpdateNotifications(n => n with
        {
            EarbudLowBatteryThreshold = ParsePercent(EarbudThresholdBox.Text, n.EarbudLowBatteryThreshold),
            CaseLowBatteryThreshold = ParsePercent(CaseThresholdBox.Text, n.CaseLowBatteryThreshold),
        });
        // Reflect any clamping back into the boxes.
        var n2 = app.CurrentNotifications;
        EarbudThresholdBox.Text = n2.EarbudLowBatteryThreshold.ToString();
        CaseThresholdBox.Text = n2.CaseLowBatteryThreshold.ToString();
    }

    private void OnSuppressRepeatChanged(object sender, RoutedEventArgs e)
    {
        if (!_initialized || System.Windows.Application.Current is not App app
            || sender is not System.Windows.Controls.CheckBox cb)
        {
            return;
        }

        var on = cb.IsChecked == true;
        SuppressMinutesBox.IsEnabled = on;
        var minutes = on ? ParseMinutes(SuppressMinutesBox.Text, 60) : 0;
        app.UpdateNotifications(n => n with { SuppressRepeatedMinutes = minutes });
    }

    private void OnSuppressMinutesChanged(object sender, RoutedEventArgs e)
    {
        if (!_initialized || System.Windows.Application.Current is not App app
            || SuppressRepeatCheckBox.IsChecked != true)
        {
            return;
        }

        var minutes = ParseMinutes(SuppressMinutesBox.Text, 60);
        app.UpdateNotifications(n => n with { SuppressRepeatedMinutes = minutes });
        SuppressMinutesBox.Text = minutes.ToString();
    }

    private void OnQuietHoursChanged(object sender, RoutedEventArgs e)
    {
        if (_initialized && System.Windows.Application.Current is App app
            && sender is System.Windows.Controls.CheckBox cb)
        {
            app.UpdateNotifications(n => n with { QuietHoursEnabled = cb.IsChecked == true });
        }
    }

    private void OnQuietTimeChanged(object sender, RoutedEventArgs e)
    {
        if (!_initialized || System.Windows.Application.Current is not App app)
        {
            return;
        }

        app.UpdateNotifications(n => n with
        {
            QuietHoursStart = NormalizeTime(QuietStartBox.Text, n.QuietHoursStart),
            QuietHoursEnd = NormalizeTime(QuietEndBox.Text, n.QuietHoursEnd),
        });
        var n2 = app.CurrentNotifications;
        QuietStartBox.Text = n2.QuietHoursStart;
        QuietEndBox.Text = n2.QuietHoursEnd;
    }

    // ----- 개인정보 -----

    private void OnMaskAddressesChanged(object sender, RoutedEventArgs e)
    {
        if (_initialized && System.Windows.Application.Current is App app
            && sender is System.Windows.Controls.CheckBox cb)
        {
            app.SetMaskBluetoothAddresses(cb.IsChecked == true);
        }
    }

    private static int ParsePercent(string text, int fallback)
        => int.TryParse(text, out var v) ? Math.Clamp(v, 1, 100) : fallback;

    private static int ParseMinutes(string text, int fallback)
        => int.TryParse(text, out var v) && v > 0 ? Math.Min(v, 1440) : fallback;

    private static string NormalizeTime(string text, string fallback)
        => TimeOnly.TryParse(text, out var t) ? t.ToString("HH:mm") : fallback;
}
