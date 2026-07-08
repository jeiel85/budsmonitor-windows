using BudsMonitor.Domain;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace BudsMonitor.Providers.Gatt;

/// <summary>Maps WinRT GATT statuses/exceptions to domain failure reasons (see docs/07).</summary>
internal static class GattReadFailureMapper
{
    public static BatteryReadFailureReason FromGattStatus(GattCommunicationStatus status) => status switch
    {
        GattCommunicationStatus.Unreachable => BatteryReadFailureReason.DeviceNotConnected,
        GattCommunicationStatus.AccessDenied => BatteryReadFailureReason.GattAccessDenied,
        GattCommunicationStatus.ProtocolError => BatteryReadFailureReason.InvalidBatteryPayload,
        _ => BatteryReadFailureReason.UnknownError,
    };

    public static BatteryReadFailureReason FromException(Exception exception) => exception switch
    {
        OperationCanceledException => BatteryReadFailureReason.GattTimeout,
        _ => BatteryReadFailureReason.UnknownError,
    };
}
