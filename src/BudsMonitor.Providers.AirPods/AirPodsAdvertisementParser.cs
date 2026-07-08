namespace BudsMonitor.Providers.AirPods;

/// <summary>
/// Parses AirPods BLE proximity advertisements (Apple manufacturer data, message type
/// 0x07). Adapted from the librepods-windows-ble feasibility parser (GPLv3-or-later).
/// Deterministic and side-effect free; never throws on malformed input.
/// </summary>
public static class AirPodsAdvertisementParser
{
    private const int MinimumLength = 11;
    private const byte ProximityMessageType = 0x07;

    public static bool TryParse(
        ReadOnlySpan<byte> data,
        AirPodsAdvertisementContext context,
        out AirPodsAdvertisementSnapshot snapshot)
    {
        snapshot = default!;

        // Require a type 0x07 proximity message long enough for the status block, and
        // skip pairing-mode packets (data[2] == 0 uses a different layout).
        if (data.Length < MinimumLength || data[0] != ProximityMessageType || data[2] == 0x00)
        {
            return false;
        }

        var modelId = (ushort)(data[4] | (data[3] << 8));
        var status = data[5];
        var podsBattery = data[6];
        var flagsAndCaseBattery = data[7];
        var lidIndicator = data[8];
        var connectionState = data[10];

        var primaryLeft = (status & 0x20) != 0;
        var valuesFlipped = !primaryLeft;

        var leftNibble = valuesFlipped ? (podsBattery >> 4) & 0x0F : podsBattery & 0x0F;
        var rightNibble = valuesFlipped ? podsBattery & 0x0F : (podsBattery >> 4) & 0x0F;
        var caseNibble = flagsAndCaseBattery & 0x0F;

        var flags = (flagsAndCaseBattery >> 4) & 0x0F;
        var rightCharging = valuesFlipped ? (flags & 0x01) != 0 : (flags & 0x02) != 0;
        var leftCharging = valuesFlipped ? (flags & 0x02) != 0 : (flags & 0x01) != 0;
        var caseCharging = (flags & 0x04) != 0;

        var thisPodInCase = (status & 0x40) != 0;
        var xorFactor = valuesFlipped ^ thisPodInCase;
        var leftInEar = xorFactor ? (status & 0x08) != 0 : (status & 0x02) != 0;
        var rightInEar = xorFactor ? (status & 0x02) != 0 : (status & 0x08) != 0;

        var lidState = thisPodInCase
            ? (((lidIndicator >> 3) & 0x01) == 0 ? "open" : "closed")
            : "unknown";

        snapshot = new AirPodsAdvertisementSnapshot
        {
            ModelId = modelId,
            ModelName = AirPodsModelCatalog.GetModelName(modelId),
            LeftBattery = AirPodsBatteryMapper.FromNibble(leftNibble),
            RightBattery = AirPodsBatteryMapper.FromNibble(rightNibble),
            CaseBattery = AirPodsBatteryMapper.FromNibble(caseNibble),
            LeftCharging = leftCharging,
            RightCharging = rightCharging,
            CaseCharging = caseCharging,
            LeftInEar = leftInEar,
            RightInEar = rightInEar,
            PrimaryLeft = primaryLeft,
            LidState = lidState,
            ConnectionState = ConnectionStateName(connectionState),
            RawPayload = data.ToArray(),
            ReceivedAt = context.ReceivedAt,
        };

        return true;
    }

    private static string ConnectionStateName(byte state) => state switch
    {
        0x00 => "Disconnected",
        0x04 => "Idle",
        0x05 => "Playing Music",
        0x06 => "On Call",
        0x07 => "Ringing",
        0x09 => "Hanging Up",
        _ => $"Unknown 0x{state:X2}",
    };
}
