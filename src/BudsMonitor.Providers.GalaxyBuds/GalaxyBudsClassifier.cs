namespace BudsMonitor.Providers.GalaxyBuds;

/// <summary>
/// Classifies whether a device is a Galaxy Buds model. Samsung's company id (0x0075) is shared
/// by many non-audio devices (phones, appliances), so it is only a weak hint; the reliable
/// signal is the device name, matched against <see cref="GalaxyBudsProfileCatalog"/>.
/// </summary>
public sealed class GalaxyBudsClassifier
{
    /// <summary>Samsung Electronics Bluetooth SIG company identifier.</summary>
    public const ushort SamsungCompanyId = 0x0075;

    private readonly GalaxyBudsProfileCatalog _catalog;

    public GalaxyBudsClassifier(GalaxyBudsProfileCatalog? catalog = null)
        => _catalog = catalog ?? GalaxyBudsProfileCatalog.Default;

    public static bool IsSamsungCompanyId(ushort companyId) => companyId == SamsungCompanyId;

    /// <summary>Returns the Galaxy Buds model for a device name, or null if it is not Galaxy Buds.</summary>
    public GalaxyBudsMatch? Classify(string? deviceName) => _catalog.Match(deviceName);
}
