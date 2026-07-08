namespace BudsMonitor.Providers.AirPods;

/// <summary>
/// Maps AirPods BLE proximity model ids to display names.
/// Model id table adapted from the librepods-windows-ble feasibility parser (GPLv3-or-later).
/// </summary>
public static class AirPodsModelCatalog
{
    public const string UnknownModelName = "Unknown";

    private static readonly IReadOnlyDictionary<ushort, string> Models = new Dictionary<ushort, string>
    {
        [0x0220] = "AirPods 1",
        [0x0F20] = "AirPods 2",
        [0x1320] = "AirPods 3",
        [0x1920] = "AirPods 4",
        [0x1B20] = "AirPods 4 ANC",
        [0x0A20] = "AirPods Max Lightning",
        [0x1F20] = "AirPods Max USB-C",
        [0x0E20] = "AirPods Pro",
        [0x1420] = "AirPods Pro 2 Lightning",
        [0x2420] = "AirPods Pro 2 USB-C",
    };

    public static string GetModelName(ushort modelId) =>
        Models.TryGetValue(modelId, out var name) ? name : UnknownModelName;

    public static bool IsKnown(ushort modelId) => Models.ContainsKey(modelId);
}
