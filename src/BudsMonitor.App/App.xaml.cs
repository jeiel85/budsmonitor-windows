using System.Collections.Concurrent;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using BudsMonitor.App.ViewModels;
using BudsMonitor.Application;
using BudsMonitor.Bluetooth;
using BudsMonitor.Diagnostics;
using BudsMonitor.Domain;
using BudsMonitor.Infrastructure.Cache;
using BudsMonitor.Infrastructure.Devices;
using BudsMonitor.Infrastructure.Logging;
using BudsMonitor.Infrastructure.Settings;
using BudsMonitor.Infrastructure.Storage;
using BudsMonitor.Providers.AirPods;
using BudsMonitor.Providers.GalaxyBuds;
using BudsMonitor.Providers.Gatt;
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
    private readonly GalaxyBudsAdvertisementProvider _galaxyBudsProvider = new();
    private readonly GalaxyBudsClassifier _galaxyBudsClassifier = new();
    private readonly NotificationRuleService _notificationService = new();
    // Written from both the BLE consumer thread (cache save) and the UI thread
    // (GATT poll, diagnostics, show-hidden), so it must be concurrency-safe.
    private readonly ConcurrentDictionary<string, BatterySnapshot> _latestSnapshots = new();
    private BatteryCacheRepository? _cacheRepository;
    private System.Windows.Threading.DispatcherTimer? _staleTimer;
    private DateTimeOffset _lastCacheSave = DateTimeOffset.MinValue;

    private readonly PairedBleDeviceEnumerator _deviceEnumerator = new();
    private readonly StandardGattBatteryProvider _gattProvider = new();
    private System.Windows.Threading.DispatcherTimer? _gattTimer;
    private bool _gattPolling;
    private DeviceRegistry? _deviceRegistry;
    private bool _showHiddenDevices;

    private DiagnosticsWindow? _diagnosticsWindow;
    // Keep recent advertisements per company id so a noisy environment cannot evict the
    // interesting frames (Apple/Samsung/etc.) before a diagnostics export. Bounded overall.
    private const int MaxSamplesPerCompany = 8;
    private const int MaxAdvertisementCompanies = 32;
    private readonly object _adSamplesLock = new();
    private readonly Dictionary<ushort, LinkedList<DiagnosticsAdvertisementSample>> _recentAdvertisements = new();
    private IReadOnlyList<DiagnosticsProviderAttempt> _lastGattAttempts = [];

    // Self-repair (GOAL 10): recover the scanner/providers across sleep, resume and
    // Bluetooth toggles, and back off polling/restarts after repeated failures.
    private BluetoothRadioWatcher? _radioWatcher;
    private readonly BackoffPolicy _gattBackoff = new(TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(15));
    private readonly BackoffPolicy _scannerBackoff = new(TimeSpan.FromSeconds(2), TimeSpan.FromMinutes(2));
    private const int MaxScannerRestartAttempts = 8;
    private int _gattFailureStreak;
    private int _scannerFailureStreak;
    private int _baseGattIntervalSeconds = 300;
    private bool _scannerRestartScheduled;

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
        StartGattPolling();
        InitializeSelfRepair();
        UpdateTrayFromDashboard();
    }

    private void InitializeStorageAndLogging()
    {
        try
        {
            _storagePaths = StoragePaths.CreateDefault();
            LoggingBootstrapper.Initialize(_storagePaths);

            _settings = new SettingsRepository(_storagePaths).LoadOrCreate();
            _deviceRegistry = new DeviceRegistry(new DeviceRegistryRepository(_storagePaths));
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

                RecordAdvertisement(frame);

                if (!_airPodsProvider.TryParseAdvertisement(frame, out var result) || result.Snapshot is null)
                {
                    TryApplyGalaxyBudsAdvertisement(frame);
                    continue;
                }

                var snapshot = result.Snapshot;
                var now = DateTimeOffset.Now;
                var notifications = _notificationService.Evaluate(snapshot, BuildNotificationOptions(), now);

                await Dispatcher.InvokeAsync(() =>
                {
                    ApplySnapshot(snapshot, now);
                    UpdateTrayFromDashboard();
                    ShowNotifications(notifications);
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
            ScheduleScannerRestart();
        }
        else
        {
            if (state == BleScannerState.Running)
            {
                _scannerFailureStreak = 0;
            }

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
            ApplySnapshot(snapshot, now);
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

    private void StartGattPolling()
    {
        _baseGattIntervalSeconds = Math.Max(30, _settings?.Monitoring.GenericGattPollingIntervalSeconds ?? 300);
        _gattTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(_baseGattIntervalSeconds),
        };
        _gattTimer.Tick += (_, _) => _ = PollGattAsync();
        _gattTimer.Start();
        _ = PollGattAsync();
    }

    private async Task PollGattAsync()
    {
        if (_gattPolling || _settings?.Monitoring.EnableGenericGattRefresh == false)
        {
            return;
        }

        _gattPolling = true;
        try
        {
            var devices = await _deviceEnumerator.GetPairedDevicesAsync(CancellationToken.None);
            var withBattery = 0;
            var attempts = new List<DiagnosticsProviderAttempt>(devices.Count);
            foreach (var candidate in devices)
            {
                var result = await _gattProvider.ReadAsync(candidate, CancellationToken.None);
                attempts.Add(new DiagnosticsProviderAttempt
                {
                    ProviderId = result.ProviderId,
                    DeviceKey = candidate.StableDeviceKey,
                    DisplayName = candidate.DisplayName,
                    Status = result.Status.ToString(),
                    FailureReason = result.Failure?.Reason.ToString(),
                    Message = result.Failure?.Message,
                    AttemptedAt = DateTimeOffset.Now,
                });

                if (result is { Status: BatteryReadStatus.Success, Snapshot: not null })
                {
                    withBattery++;
                    var snapshot = result.Snapshot;
                    var now = DateTimeOffset.Now;
                    ApplySnapshot(snapshot, now);
                    UpdateTrayFromDashboard();
                    SaveCacheThrottled(snapshot, now);
                }
                else
                {
                    if (result.Status == BatteryReadStatus.Failed)
                    {
                        Log.Debug("GATT read failed for {Device}: {Reason}",
                            candidate.DisplayName, result.Failure?.Reason);
                    }

                    TryApplyGalaxyBudsCandidate(candidate);
                }
            }

            _lastGattAttempts = attempts;
            Log.Information("GATT poll: {Total} paired BLE device(s), {WithBattery} with battery",
                devices.Count, withBattery);

            if (_gattFailureStreak != 0)
            {
                _gattFailureStreak = 0;
                ApplyGattInterval(0);
                Log.Information("GATT poll recovered; interval reset to {Seconds}s", _baseGattIntervalSeconds);
            }
        }
        catch (Exception ex)
        {
            _gattFailureStreak++;
            ApplyGattInterval(_gattFailureStreak);
            Log.Warning(ex, "GATT poll failed (streak {Streak}, next interval {Seconds}s)",
                _gattFailureStreak, (int)(_gattTimer?.Interval.TotalSeconds ?? _baseGattIntervalSeconds));
        }
        finally
        {
            _gattPolling = false;
        }
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

    private void ApplySnapshot(BatterySnapshot snapshot, DateTimeOffset now)
    {
        var entry = _deviceRegistry?.RecordSeen(
            snapshot.StableDeviceKey, snapshot.DisplayName, snapshot.Source.ToString(), now);
        _dashboard.Upsert(
            snapshot,
            entry?.Alias,
            entry?.IsPinned ?? false,
            entry?.IsHidden ?? false,
            _showHiddenDevices,
            now);
    }

    /// <summary>
    /// Surfaces Galaxy Buds seen in an advertisement as a limited-support card (GOAL 11).
    /// No battery is available (proprietary protocol); the card marks the device accordingly.
    /// </summary>
    private void TryApplyGalaxyBudsAdvertisement(BleAdvertisementFrame frame)
    {
        if (!_galaxyBudsProvider.TryParseAdvertisement(frame, out var result) || result.Snapshot is null)
        {
            return;
        }

        var snapshot = result.Snapshot;
        var now = DateTimeOffset.Now;
        Dispatcher.InvokeAsync(() =>
        {
            ApplySnapshot(snapshot, now);
            _latestSnapshots[snapshot.StableDeviceKey] = snapshot;
            UpdateTrayFromDashboard();
        });
    }

    /// <summary>
    /// If a paired device without standard GATT battery is a Galaxy Buds, show it as limited
    /// support so it is visible and captured in diagnostics (runs on the UI thread).
    /// </summary>
    private void TryApplyGalaxyBudsCandidate(DeviceCandidate candidate)
    {
        var match = _galaxyBudsClassifier.Classify(candidate.DisplayName);
        if (match is null)
        {
            return;
        }

        var now = DateTimeOffset.Now;
        var snapshot = GalaxyBudsAdvertisementProvider.CreateLimitedSupportSnapshot(
            candidate.StableDeviceKey, match.DisplayName, match.DisplayName, now);
        ApplySnapshot(snapshot, now);
        _latestSnapshots[snapshot.StableDeviceKey] = snapshot;
        UpdateTrayFromDashboard();
    }

    internal void TogglePinDevice(string key)
    {
        if (_deviceRegistry is null)
        {
            return;
        }

        var pinned = _deviceRegistry.Get(key)?.IsPinned ?? false;
        _deviceRegistry.SetPinned(key, !pinned);
        ReapplyDevice(key);
    }

    internal void ToggleHideDevice(string key)
    {
        if (_deviceRegistry is null)
        {
            return;
        }

        var hidden = _deviceRegistry.Get(key)?.IsHidden ?? false;
        _deviceRegistry.SetHidden(key, !hidden);
        ReapplyDevice(key);
    }

    internal void PromptDeviceAlias(string key)
    {
        if (_deviceRegistry is null)
        {
            return;
        }

        var current = _deviceRegistry.Get(key)?.Alias ?? string.Empty;
        var alias = Microsoft.VisualBasic.Interaction.InputBox(
            "별칭을 입력하세요 (비우면 원래 이름 사용)", "별칭 설정", current);
        _deviceRegistry.SetAlias(key, alias);
        ReapplyDevice(key);
    }

    internal void SetShowHiddenDevices(bool show)
    {
        _showHiddenDevices = show;
        foreach (var snapshot in _latestSnapshots.Values.ToList())
        {
            ApplySnapshot(snapshot, DateTimeOffset.Now);
        }

        UpdateTrayFromDashboard();
    }

    private void ReapplyDevice(string key)
    {
        if (_latestSnapshots.TryGetValue(key, out var snapshot))
        {
            ApplySnapshot(snapshot, DateTimeOffset.Now);
            UpdateTrayFromDashboard();
        }
    }

    // ----- Diagnostics (GOAL 9) -------------------------------------------------

    /// <summary>Keeps a small ring buffer of the most recent advertisements for diagnostics.</summary>
    private void RecordAdvertisement(BleAdvertisementFrame frame)
    {
        var sample = new DiagnosticsAdvertisementSample
        {
            ReceivedAt = frame.ReceivedAt,
            CompanyId = frame.CompanyId,
            BluetoothAddress = frame.BluetoothAddress,
            DataLength = frame.ManufacturerData.Length,
            Rssi = frame.RawRssi,
            LocalName = frame.LocalName,
            ManufacturerData = frame.ManufacturerData,
        };

        lock (_adSamplesLock)
        {
            if (!_recentAdvertisements.TryGetValue(frame.CompanyId, out var samples))
            {
                if (_recentAdvertisements.Count >= MaxAdvertisementCompanies)
                {
                    // Evict the company whose most recent sample is oldest.
                    var stalest = _recentAdvertisements
                        .OrderBy(kv => kv.Value.Last?.Value.ReceivedAt ?? DateTimeOffset.MinValue)
                        .First().Key;
                    _recentAdvertisements.Remove(stalest);
                }

                samples = new LinkedList<DiagnosticsAdvertisementSample>();
                _recentAdvertisements[frame.CompanyId] = samples;
            }

            samples.AddLast(sample);
            while (samples.Count > MaxSamplesPerCompany)
            {
                samples.RemoveFirst();
            }
        }
    }

    private DiagnosticsInput BuildDiagnosticsInput(DateTimeOffset now)
    {
        List<DiagnosticsAdvertisementSample> advertisements;
        lock (_adSamplesLock)
        {
            advertisements = _recentAdvertisements.Values
                .SelectMany(samples => samples)
                .OrderByDescending(sample => sample.ReceivedAt)
                .ToList();
        }

        var attempts = new List<DiagnosticsProviderAttempt>(_lastGattAttempts);
        foreach (var snapshot in _latestSnapshots.Values)
        {
            if (snapshot.Source is BatteryDataSource.AirPodsBleAdvertisement
                or BatteryDataSource.GalaxyBudsProvider)
            {
                attempts.Add(new DiagnosticsProviderAttempt
                {
                    ProviderId = snapshot.Source.ToString(),
                    DeviceKey = snapshot.StableDeviceKey,
                    DisplayName = snapshot.DisplayName,
                    Status = BatteryReadStatus.Success.ToString(),
                    AttemptedAt = snapshot.MeasuredAt,
                });
            }
        }

        return new DiagnosticsInput
        {
            GeneratedAt = now,
            MaskBluetoothAddresses = _settings?.Privacy.MaskBluetoothAddressesInLogs ?? true,
            IncludeRawPayloads = _settings?.Privacy.IncludeRawPayloadsInDiagnostics ?? false,
            AppVersion = GetAppVersion(),
            Settings = _settings,
            Devices = new DeviceRegistryFile { Devices = _deviceRegistry?.Snapshot() ?? [] },
            BatteryCache = new BatteryCacheFile
            {
                Snapshots = _latestSnapshots.Values.Select(CacheMapping.ToCache).ToList(),
            },
            Scanner = new DiagnosticsScannerStatus
            {
                State = _scanner?.State.ToString() ?? "Unknown",
                LastError = _scanner?.LastError?.ToString(),
                TotalFramesReceived = _totalFramesReceived,
            },
            ProviderAttempts = attempts,
            AdvertisementSamples = advertisements,
        };
    }

    /// <summary>Generates a diagnostics ZIP off the UI thread and returns its path (or null).</summary>
    internal async Task<string?> ExportDiagnosticsAsync()
    {
        if (_storagePaths is null)
        {
            return null;
        }

        try
        {
            var input = BuildDiagnosticsInput(DateTimeOffset.Now);
            var service = new DiagnosticsExportService(_storagePaths);
            var path = await Task.Run(() => service.Export(input));
            Log.Information("Diagnostics bundle created: {Path}", path);
            return path;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Diagnostics export failed");
            return null;
        }
    }

    /// <summary>Labeled key/value rows shown in the diagnostics window.</summary>
    internal IReadOnlyList<KeyValuePair<string, string>> BuildDiagnosticsSummaryLines()
    {
        var input = BuildDiagnosticsInput(DateTimeOffset.Now);
        return
        [
            new("앱 버전", input.AppVersion ?? "-"),
            new("OS", System.Runtime.InteropServices.RuntimeInformation.OSDescription),
            new(".NET", System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription),
            new("스캐너 상태", input.Scanner?.State ?? "-"),
            new("수신 프레임", (input.Scanner?.TotalFramesReceived ?? 0).ToString()),
            new("최근 광고 샘플", input.AdvertisementSamples.Count.ToString()),
            new("감지 기기", (input.BatteryCache?.Snapshots.Count ?? 0).ToString()),
            new("주소 마스킹", input.MaskBluetoothAddresses ? "켜짐(기본)" : "꺼짐"),
            new("원시 페이로드", input.IncludeRawPayloads ? "포함" : "제외(기본)"),
        ];
    }

    internal void ShowDiagnosticsWindow()
    {
        if (_diagnosticsWindow is null)
        {
            _diagnosticsWindow = new DiagnosticsWindow();
            _diagnosticsWindow.Closed += (_, _) => _diagnosticsWindow = null;
        }

        if (!_diagnosticsWindow.IsVisible)
        {
            _diagnosticsWindow.Show();
        }

        _diagnosticsWindow.Activate();
    }

    internal void OpenLogsFolder() => OpenFolder(_storagePaths?.LogsDirectory);

    internal void OpenDiagnosticsFolder() => OpenFolder(_storagePaths?.DiagnosticsDirectory);

    private static void OpenFolder(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(path);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Open folder failed: {Path}", path);
        }
    }

    private static string GetAppVersion()
        => System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";

    // ----- Sleep/resume + self-repair (GOAL 10) --------------------------------

    private void InitializeSelfRepair()
    {
        SystemEvents.PowerModeChanged += OnPowerModeChanged;

        _radioWatcher = new BluetoothRadioWatcher();
        _radioWatcher.StateChanged += OnRadioStateChanged;
        _ = _radioWatcher.StartAsync();
        Log.Information("Self-repair initialized (power + Bluetooth radio watch)");
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        switch (e.Mode)
        {
            case PowerModes.Resume:
                Log.Information("System resume detected; recovering scanner and providers");
                Dispatcher.InvokeAsync(() => _ = RecoverAsync("resume"));
                break;
            case PowerModes.Suspend:
                Log.Information("System suspend detected");
                break;
        }
    }

    private void OnRadioStateChanged(object? sender, bool isOn)
    {
        Dispatcher.InvokeAsync(() =>
        {
            Log.Information("Bluetooth radio turned {State}", isOn ? "on" : "off");
            if (isOn)
            {
                _ = RecoverAsync("bluetooth-on");
            }
            else
            {
                UpdateTrayStatus(BleScannerState.Stopped);
            }
        });
    }

    /// <summary>Resets failure backoff and restarts the scanner + a GATT poll after a disruption.</summary>
    private async Task RecoverAsync(string reason)
    {
        ResetGattInterval();
        _scannerFailureStreak = 0;

        try
        {
            await RestartScannerAsync();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Scanner restart during {Reason} recovery failed", reason);
        }

        _ = PollGattAsync();
        Log.Information("Recovery ({Reason}) complete", reason);
    }

    /// <summary>Schedules a single backed-off scanner restart after a failure (no overlap).</summary>
    private void ScheduleScannerRestart()
    {
        if (_scannerRestartScheduled)
        {
            return;
        }

        if (_scannerFailureStreak >= MaxScannerRestartAttempts)
        {
            Log.Warning("BLE scanner failed {Count} times; pausing auto-restart until resume, " +
                "Bluetooth on, or manual refresh", _scannerFailureStreak);
            Dispatcher.InvokeAsync(() => UpdateTrayStatus(BleScannerState.Failed));
            return;
        }

        _scannerRestartScheduled = true;
        _scannerFailureStreak++;
        var delay = _scannerBackoff.DelayFor(_scannerFailureStreak);
        Log.Information("Scheduling scanner restart in {Delay}s (failure streak {Streak})",
            (int)delay.TotalSeconds, _scannerFailureStreak);

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay);
                await RestartScannerAsync();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Scheduled scanner restart failed");
            }
            finally
            {
                _scannerRestartScheduled = false;
            }
        });
    }

    private void ApplyGattInterval(int failureStreak)
    {
        if (_gattTimer is null)
        {
            return;
        }

        var backoff = _gattBackoff.DelayFor(failureStreak);
        var seconds = _baseGattIntervalSeconds + (int)backoff.TotalSeconds;
        _gattTimer.Interval = TimeSpan.FromSeconds(seconds);
    }

    private void ResetGattInterval()
    {
        _gattFailureStreak = 0;
        ApplyGattInterval(0);
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

    private NotificationRuleOptions BuildNotificationOptions()
    {
        var notifications = _settings?.Notifications ?? new NotificationSettings();
        var monitoring = _settings?.Monitoring ?? new MonitoringSettings();

        return new NotificationRuleOptions
        {
            Enabled = notifications.Enabled,
            EarbudThresholdPercent = notifications.EarbudLowBatteryThreshold,
            CaseThresholdPercent = notifications.CaseLowBatteryThreshold,
            SuppressWindow = TimeSpan.FromMinutes(notifications.SuppressRepeatedMinutes),
            NotifyFromStaleData = notifications.NotifyFromStaleData,
            StaleAfter = TimeSpan.FromSeconds(monitoring.StaleAfterSeconds),
            QuietHoursEnabled = notifications.QuietHoursEnabled,
            QuietHoursStart = ParseTime(notifications.QuietHoursStart, new TimeOnly(22, 0)),
            QuietHoursEnd = ParseTime(notifications.QuietHoursEnd, new TimeOnly(7, 0)),
        };
    }

    private static TimeOnly ParseTime(string value, TimeOnly fallback)
        => TimeOnly.TryParse(value, out var parsed) ? parsed : fallback;

    private void ShowNotifications(IReadOnlyList<NotificationRequest> notifications)
    {
        if (_notifyIcon is null || notifications.Count == 0)
        {
            return;
        }

        foreach (var notification in notifications)
        {
            _notifyIcon.ShowBalloonTip(5000, notification.Title, notification.Body,
                System.Windows.Forms.ToolTipIcon.Warning);
            Log.Information("Notified: {Title} — {Body}", notification.Title, notification.Body);
        }
    }

    private void InitializeTrayIcon()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("대시보드 열기", image: null, (_, _) => ShowDashboard());
        // These surfaces are wired up in later goals (scanner / diagnostics).
        menu.Items.Add("지금 새로 고침", image: null, async (_, _) => await RestartScannerAsync());
        menu.Items.Add("진단", image: null, (_, _) => ShowDiagnosticsWindow());
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
        _gattTimer?.Stop();
        _deviceRegistry?.Save();
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
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _radioWatcher?.Dispose();
        _showRequestWait?.Unregister(waitObject: null);
        _notifyIcon?.Dispose();
        _showRequestEvent?.Dispose();
        _singleInstanceMutex?.Dispose();
        LoggingBootstrapper.Shutdown();
        base.OnExit(e);
    }
}
