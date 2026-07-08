using System.Windows;
using Microsoft.Win32;
using BudsMonitor.App.ViewModels;
using BudsMonitor.Bluetooth;
using BudsMonitor.Domain;
using BudsMonitor.Infrastructure.Cache;
using BudsMonitor.Infrastructure.Logging;
using BudsMonitor.Infrastructure.Settings;
using BudsMonitor.Infrastructure.Storage;
using BudsMonitor.Providers.AirPods;
using Serilog;

namespace BudsMonitor.App;

/// <summary>
/// Application entry point for the tray-first shell (GOAL 1).
/// Owns the tray icon, enforces a single running instance, and manages the
/// dashboard/settings windows. The app keeps running in the tray until the
/// user explicitly chooses Quit.
/// </summary>
public partial class App : System.Windows.Application
{
    private const string SingleInstanceMutexName =
        @"Local\BudsMonitor.SingleInstance.4b8e1c0e-2b8a-4c2f-9a1d-3f6e5d7c9a10";
    private const string ShowRequestEventName =
        @"Local\BudsMonitor.ShowRequest.4b8e1c0e-2b8a-4c2f-9a1d-3f6e5d7c9a10";

    private Mutex? _singleInstanceMutex;
    private EventWaitHandle? _showRequestEvent;
    private RegisteredWaitHandle? _showRequestWait;
    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    private MainWindow? _dashboardWindow;
    private SettingsWindow? _settingsWindow;
    private StoragePaths? _storagePaths;
    private BudsMonitorSettings? _settings;
    private BatteryCacheFile? _batteryCache;
    private BleAdvertisementScannerService? _scanner;
    private CancellationTokenSource? _frameConsumerCts;
    private readonly Dictionary<ulong, DateTimeOffset> _lastAppleLog = new();
    private int _totalFramesReceived;

    private readonly DashboardViewModel _dashboard = new();
    private readonly AirPodsBleAdvertisementProvider _airPodsProvider = new();
    private readonly Dictionary<string, BatterySnapshot> _latestSnapshots = new();
    private BatteryCacheRepository? _cacheRepository;
    private System.Windows.Threading.DispatcherTimer? _staleTimer;
    private DateTimeOffset _lastCacheSave = DateTimeOffset.MinValue;

    /// <summary>True once the user has chosen Quit, so windows may close for real.</summary>
    public bool IsShuttingDown { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out bool createdNew);
        _showRequestEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowRequestEventName);

        if (!createdNew)
        {
            // Another instance already owns the tray. Ask it to surface the dashboard, then exit.
            _showRequestEvent.Set();
            Shutdown();
            return;
        }

        // Wake up and show the dashboard when a second instance signals us.
        _showRequestWait = ThreadPool.RegisterWaitForSingleObject(
            _showRequestEvent,
            (_, _) => Dispatcher.InvokeAsync(ShowDashboard),
            state: null,
            millisecondsTimeOutInterval: Timeout.Infinite,
            executeOnlyOnce: false);

        InitializeStorageAndLogging();
        ApplyTheme(_settings?.App.Theme);
        InitializeTrayIcon();
        InitializeScanner();
        ShowDashboard();
        StartStaleTimer();
        UpdateTrayFromDashboard();
    }

    private void InitializeStorageAndLogging()
    {
        try
        {
            _storagePaths = StoragePaths.CreateDefault();
            LoggingBootstrapper.Initialize(_storagePaths);

            _settings = new SettingsRepository(_storagePaths).LoadOrCreate();
            _cacheRepository = new BatteryCacheRepository(_storagePaths);
            _batteryCache = _cacheRepository.Load();
            LoadCachedDevices();

            Log.Information(
                "BudsMonitor started. theme={Theme}, minimizeToTray={MinimizeToTray}, cachedSnapshots={Count}",
                _settings.App.Theme,
                _settings.App.MinimizeToTray,
                _batteryCache.Snapshots.Count);
        }
        catch (Exception ex)
        {
            // Storage/logging failure must not prevent the tray app from running.
            System.Diagnostics.Debug.WriteLine($"BudsMonitor initialization failed: {ex}");
        }
    }

    /// <summary>
    /// Applies the light/dark theme by swapping the merged color dictionary.
    /// "system" follows the Windows app theme; DynamicResource brushes update live.
    /// </summary>
    internal void ApplyTheme(string? themeSetting)
    {
        var dark = themeSetting?.ToLowerInvariant() switch
        {
            "dark" => true,
            "light" => false,
            _ => IsWindowsDarkTheme(),
        };

        var source = new Uri(dark ? "Themes/Dark.xaml" : "Themes/Light.xaml", UriKind.Relative);
        var merged = Resources.MergedDictionaries;
        merged.Clear();
        merged.Add(new ResourceDictionary { Source = source });
    }

    private static bool IsWindowsDarkTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch
        {
            return false;
        }
    }

    private void InitializeScanner()
    {
        try
        {
            _scanner = new BleAdvertisementScannerService();
            _scanner.StateChanged += OnScannerStateChanged;

            _frameConsumerCts = new CancellationTokenSource();
            _ = ConsumeFramesAsync(_frameConsumerCts.Token);

            _ = _scanner.StartAsync();
            Log.Information("BLE scanner started");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to start BLE scanner");
        }
    }

    private async Task ConsumeFramesAsync(CancellationToken cancellationToken)
    {
        if (_scanner is null)
        {
            return;
        }

        try
        {
            await foreach (var frame in _scanner.Frames.ReadAllAsync(cancellationToken))
            {
                _totalFramesReceived++;
                if (_totalFramesReceived == 1)
                {
                    Log.Information("BLE scanner receiving advertisements (first frame)");
                }

                if (!_airPodsProvider.TryParseAdvertisement(frame, out var result) || result.Snapshot is null)
                {
                    continue;
                }

                var snapshot = result.Snapshot;
                var now = DateTimeOffset.Now;

                await Dispatcher.InvokeAsync(() =>
                {
                    _dashboard.Upsert(snapshot, now);
                    UpdateTrayFromDashboard();
                });

                SaveCacheThrottled(snapshot, now);
                LogSnapshotThrottled(snapshot, frame);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
        catch (Exception ex)
        {
            Log.Error(ex, "BLE frame consumer stopped unexpectedly");
        }
    }

    private void OnScannerStateChanged(object? sender, BleScannerState state)
    {
        if (state == BleScannerState.Failed)
        {
            Log.Warning("BLE scanner failed: {Error}", _scanner?.LastError);
        }
        else
        {
            Log.Information("BLE scanner state: {State}", state);
        }

        Dispatcher.InvokeAsync(() => UpdateTrayStatus(state));
    }

    private async Task RestartScannerAsync()
    {
        if (_scanner is null)
        {
            return;
        }

        try
        {
            Log.Information("Restarting BLE scanner from tray");
            await _scanner.RestartAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to restart BLE scanner");
        }
    }

    private void UpdateTrayStatus(BleScannerState state)
    {
        if (_notifyIcon is null)
        {
            return;
        }

        _notifyIcon.Text = state switch
        {
            BleScannerState.Running => "BudsMonitor · 스캔 중",
            BleScannerState.Failed => "BudsMonitor · 스캐너 오류",
            BleScannerState.Stopped => "BudsMonitor · 스캐너 정지",
            _ => "BudsMonitor",
        };
    }

    private string FormatAddress(ulong address)
    {
        var bytes = new byte[6];
        for (var i = 0; i < 6; i++)
        {
            bytes[5 - i] = (byte)(address >> (i * 8));
        }

        var mask = _settings?.Privacy.MaskBluetoothAddressesInLogs ?? true;
        return mask
            ? $"{bytes[0]:X2}:{bytes[1]:X2}:{bytes[2]:X2}:**:**:**"
            : string.Join(":", bytes.Select(b => b.ToString("X2")));
    }

    private void LoadCachedDevices()
    {
        if (_batteryCache is null)
        {
            return;
        }

        var now = DateTimeOffset.Now;
        foreach (var cached in _batteryCache.Snapshots)
        {
            var snapshot = CacheMapping.ToDomain(cached);
            _latestSnapshots[snapshot.StableDeviceKey] = snapshot;
            _dashboard.Upsert(snapshot, now);
        }
    }

    private void StartStaleTimer()
    {
        _staleTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5),
        };
        _staleTimer.Tick += (_, _) =>
        {
            _dashboard.RefreshFreshness(DateTimeOffset.Now);
            UpdateTrayFromDashboard();
        };
        _staleTimer.Start();
    }

    private void UpdateTrayFromDashboard()
    {
        if (_notifyIcon is null)
        {
            return;
        }

        var text = _dashboard.BuildTraySummary();
        _notifyIcon.Text = text.Length <= 63 ? text : text[..63];
    }

    private void SaveCacheThrottled(BatterySnapshot snapshot, DateTimeOffset now)
    {
        _latestSnapshots[snapshot.StableDeviceKey] = snapshot;
        if (now - _lastCacheSave < TimeSpan.FromSeconds(30))
        {
            return;
        }

        _lastCacheSave = now;
        try
        {
            var file = new BatteryCacheFile
            {
                Snapshots = _latestSnapshots.Values.Select(CacheMapping.ToCache).ToList(),
            };
            _cacheRepository?.Save(file);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Battery cache save failed");
        }
    }

    private void LogSnapshotThrottled(BatterySnapshot snapshot, BleAdvertisementFrame frame)
    {
        if (_lastAppleLog.TryGetValue(frame.BluetoothAddress, out var last)
            && (frame.ReceivedAt - last) < TimeSpan.FromSeconds(5))
        {
            return;
        }

        _lastAppleLog[frame.BluetoothAddress] = frame.ReceivedAt;
        Log.Information("AirPods {Model} addr={Address}: L={Left} R={Right} Case={Case}",
            snapshot.ModelName,
            FormatAddress(frame.BluetoothAddress),
            ComponentValue(snapshot, BatteryComponentType.LeftBud),
            ComponentValue(snapshot, BatteryComponentType.RightBud),
            ComponentValue(snapshot, BatteryComponentType.Case));
    }

    private static string ComponentValue(BatterySnapshot snapshot, BatteryComponentType type)
    {
        var component = snapshot.Components.FirstOrDefault(c => c.Type == type);
        return component is null ? "—" : $"{component.Percentage}%";
    }

    private void InitializeTrayIcon()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("대시보드 열기", image: null, (_, _) => ShowDashboard());
        // These surfaces are wired up in later goals (scanner / diagnostics).
        menu.Items.Add("지금 새로 고침", image: null, async (_, _) => await RestartScannerAsync());
        menu.Items.Add(new System.Windows.Forms.ToolStripMenuItem("진단") { Enabled = false });
        menu.Items.Add("설정", image: null, (_, _) => ShowSettingsWindow());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("종료", image: null, (_, _) => QuitApplication());

        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "BudsMonitor",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _notifyIcon.DoubleClick += (_, _) => ShowDashboard();
    }

    private static System.Drawing.Icon LoadTrayIcon()
    {
        var resource = GetResourceStream(new Uri("pack://application:,,,/Assets/tray.ico"));
        if (resource is null)
        {
            return System.Drawing.SystemIcons.Application;
        }

        using var stream = resource.Stream;
        return new System.Drawing.Icon(stream);
    }

    private void ShowDashboard()
    {
        _dashboardWindow ??= new MainWindow { DataContext = _dashboard };

        if (!_dashboardWindow.IsVisible)
        {
            _dashboardWindow.Show();
        }

        if (_dashboardWindow.WindowState == WindowState.Minimized)
        {
            _dashboardWindow.WindowState = WindowState.Normal;
        }

        _dashboardWindow.Activate();
    }

    internal void ShowSettingsWindow()
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow();
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }

        if (!_settingsWindow.IsVisible)
        {
            _settingsWindow.Show();
        }

        _settingsWindow.Activate();
    }

    private void QuitApplication()
    {
        IsShuttingDown = true;
        Log.Information("BudsMonitor shutting down");

        _staleTimer?.Stop();
        _frameConsumerCts?.Cancel();
        _scanner?.Dispose();

        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _showRequestWait?.Unregister(waitObject: null);
        _notifyIcon?.Dispose();
        _showRequestEvent?.Dispose();
        _singleInstanceMutex?.Dispose();
        LoggingBootstrapper.Shutdown();
        base.OnExit(e);
    }
}
