namespace BudsMonitor.Application.Devices;

/// <summary>
/// Maps an earbud model name or a paired-device name to a coarse family key. AirPods rotate
/// their BLE address for privacy, so a specific pair cannot be tracked across time; the family
/// (plus signal strength) is the finest identity that survives — see <see cref="DeviceListResolver"/>.
/// Returns null for anything that is not a recognized earbud (phones, mice, etc.).
/// </summary>
public static class EarbudFamily
{
    public static string? Of(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var n = name.ToLowerInvariant();
        if (n.Contains("airpods pro") || n.Contains("airpod pro"))
        {
            return "airpods-pro";
        }

        if (n.Contains("airpods max"))
        {
            return "airpods-max";
        }

        if (n.Contains("airpods"))
        {
            return "airpods";
        }

        if (n.Contains("galaxy buds"))
        {
            return "galaxy-buds";
        }

        return null;
    }
}
