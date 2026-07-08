using BudsMonitor.Infrastructure.Cache;
using BudsMonitor.Infrastructure.Devices;
using BudsMonitor.Infrastructure.Settings;

namespace BudsMonitor.Diagnostics;

/// <summary>
/// Runtime state captured by the app and handed to <see cref="DiagnosticsExportService"/>.
/// All fields are plain data (no WinRT/UI types) so the export service stays platform
/// independent and unit-testable. The service — not the caller — applies the privacy
/// masking, so the "addresses masked by default" guarantee lives in one place.
/// </summary>
public sealed record DiagnosticsInput
{
    public required DateTimeOffset GeneratedAt { get; init; }

    /// <summary>When true (default), raw Bluetooth addresses are masked in the bundle.</summary>
    public bool MaskBluetoothAddresses { get; init; } = true;

    /// <summary>When true, raw advertisement payload bytes are included (opt-in only).</summary>
    public bool IncludeRawPayloads { get; init; }

    public string? AppVersion { get; init; }

    public BudsMonitorSettings? Settings { get; init; }
    public DeviceRegistryFile? Devices { get; init; }
    public BatteryCacheFile? BatteryCache { get; init; }

    public DiagnosticsScannerStatus? Scanner { get; init; }
    public IReadOnlyList<DiagnosticsProviderAttempt> ProviderAttempts { get; init; } = [];
    public IReadOnlyList<DiagnosticsAdvertisementSample> AdvertisementSamples { get; init; } = [];
}

/// <summary>Snapshot of the BLE scanner and Bluetooth radio state.</summary>
public sealed record DiagnosticsScannerStatus
{
    public required string State { get; init; }
    public string? LastError { get; init; }
    public int TotalFramesReceived { get; init; }
    public bool? BluetoothRadioOn { get; init; }
}

/// <summary>A single provider read attempt (advertisement parse or GATT read) and its outcome.</summary>
public sealed record DiagnosticsProviderAttempt
{
    public required string ProviderId { get; init; }
    public required string DeviceKey { get; init; }
    public string? DisplayName { get; init; }
    public required string Status { get; init; }
    public string? FailureReason { get; init; }
    public string? Message { get; init; }
    public DateTimeOffset AttemptedAt { get; init; }
}

/// <summary>
/// A recent BLE advertisement summary. The raw <see cref="BluetoothAddress"/> and
/// <see cref="ManufacturerData"/> are only surfaced (masked/omitted) by the export service.
/// </summary>
public sealed record DiagnosticsAdvertisementSample
{
    public required DateTimeOffset ReceivedAt { get; init; }
    public required ushort CompanyId { get; init; }
    public required ulong BluetoothAddress { get; init; }
    public int DataLength { get; init; }
    public short? Rssi { get; init; }
    public string? LocalName { get; init; }
    public byte[]? ManufacturerData { get; init; }
}
