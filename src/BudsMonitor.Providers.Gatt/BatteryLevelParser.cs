namespace BudsMonitor.Providers.Gatt;

/// <summary>
/// Parses a standard Battery Level (0x2A19) characteristic value: a single byte 0..100.
/// </summary>
public static class BatteryLevelParser
{
    public static bool TryParse(ReadOnlySpan<byte> value, out int percentage)
    {
        percentage = 0;
        if (value.Length == 0)
        {
            return false;
        }

        int level = value[0];
        if (level > 100)
        {
            return false;
        }

        percentage = level;
        return true;
    }
}
