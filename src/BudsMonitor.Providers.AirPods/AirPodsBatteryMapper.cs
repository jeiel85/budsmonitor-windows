namespace BudsMonitor.Providers.AirPods;

/// <summary>
/// Maps a 4-bit AirPods battery nibble to a percentage. Values 0..10 map to 0..100 in
/// 10% steps; 11..15 (including the 15 "unavailable" sentinel) map to null.
/// </summary>
public static class AirPodsBatteryMapper
{
    public static int? FromNibble(int nibble) =>
        nibble is >= 0 and <= 10 ? nibble * 10 : null;
}
