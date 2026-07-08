using BudsMonitor.Providers.AirPods;

namespace BudsMonitor.Tests;

public sealed class AirPodsAdvertisementParserTests
{
    private static AirPodsAdvertisementContext Context() => new()
    {
        ReceivedAt = new DateTimeOffset(2026, 7, 8, 13, 42, 0, TimeSpan.Zero),
        BluetoothAddress = 0x708AD722E45D,
        LocalName = "AirPods",
    };

    /// <summary>
    /// Builds an 11-byte AirPods proximity payload. Defaults describe AirPods Pro 2
    /// Lightning, primaryLeft set, L=80 R=90 Case=60 (charging), Playing Music.
    /// </summary>
    private static byte[] BuildPayload(
        ushort modelId = 0x1420,
        byte status = 0x20,
        byte podsBattery = 0x98,
        byte flagsAndCase = 0x46,
        byte lid = 0x00,
        byte connection = 0x05)
    {
        return
        [
            0x07,                    // 0: proximity message type
            0x19,                    // 1: length (unused by parser)
            0x01,                    // 2: prefix (non-zero → not pairing mode)
            (byte)(modelId >> 8),    // 3: model id high byte
            (byte)(modelId & 0xFF),  // 4: model id low byte
            status,                  // 5: status
            podsBattery,             // 6: pod battery nibbles
            flagsAndCase,            // 7: charge flags + case battery nibble
            lid,                     // 8: lid indicator
            0x00,                    // 9: color id (unused)
            connection,              // 10: connection state
        ];
    }

    [Fact]
    public void TryParse_ValidPayload_ReturnsSnapshot()
    {
        var ok = AirPodsAdvertisementParser.TryParse(BuildPayload(), Context(), out var s);

        Assert.True(ok);
        Assert.Equal(0x1420, s.ModelId);
        Assert.Equal("AirPods Pro 2 Lightning", s.ModelName);
        // primaryLeft → not flipped: left = low nibble (8) = 80, right = high nibble (9) = 90
        Assert.Equal(80, s.LeftBattery);
        Assert.Equal(90, s.RightBattery);
        Assert.Equal(60, s.CaseBattery);
        Assert.True(s.CaseCharging);
        Assert.True(s.PrimaryLeft);
        Assert.Equal("Playing Music", s.ConnectionState);
        Assert.Equal(11, s.RawPayload.Length);
        Assert.Equal(Context().ReceivedAt, s.ReceivedAt);
    }

    [Fact]
    public void TryParse_WrongMessageType_ReturnsFalse()
    {
        var payload = BuildPayload();
        payload[0] = 0x10; // not 0x07

        Assert.False(AirPodsAdvertisementParser.TryParse(payload, Context(), out _));
    }

    [Fact]
    public void TryParse_TooShort_ReturnsFalse()
    {
        byte[] payload = [0x07, 0x19, 0x01, 0x14, 0x20]; // 5 bytes < 11

        Assert.False(AirPodsAdvertisementParser.TryParse(payload, Context(), out _));
    }

    [Fact]
    public void TryParse_EmptyData_ReturnsFalse()
    {
        Assert.False(AirPodsAdvertisementParser.TryParse(ReadOnlySpan<byte>.Empty, Context(), out _));
    }

    [Fact]
    public void TryParse_PairingModePacket_ReturnsFalse()
    {
        var payload = BuildPayload();
        payload[2] = 0x00; // pairing-mode layout

        Assert.False(AirPodsAdvertisementParser.TryParse(payload, Context(), out _));
    }

    [Fact]
    public void TryParse_UnknownModel_ParsesWithUnknownName()
    {
        var ok = AirPodsAdvertisementParser.TryParse(BuildPayload(modelId: 0xABCD), Context(), out var s);

        Assert.True(ok);
        Assert.Equal(0xABCD, s.ModelId);
        Assert.Equal("Unknown", s.ModelName);
    }

    [Fact]
    public void TryParse_UnavailableBatteryNibble_MapsToNull()
    {
        // low nibble = 0xF (15) → left unavailable while not flipped; high nibble 9 → right 90
        var ok = AirPodsAdvertisementParser.TryParse(BuildPayload(podsBattery: 0x9F), Context(), out var s);

        Assert.True(ok);
        Assert.Null(s.LeftBattery);
        Assert.Equal(90, s.RightBattery);
    }

    [Fact]
    public void TryParse_PrimaryLeftFlip_SwapsLeftAndRight()
    {
        var notFlipped = BuildPayload(status: 0x20, podsBattery: 0x98); // primaryLeft: L=80 R=90
        var flipped = BuildPayload(status: 0x00, podsBattery: 0x98);    // flipped:     L=90 R=80

        AirPodsAdvertisementParser.TryParse(notFlipped, Context(), out var a);
        AirPodsAdvertisementParser.TryParse(flipped, Context(), out var b);

        Assert.True(a.PrimaryLeft);
        Assert.Equal(80, a.LeftBattery);
        Assert.Equal(90, a.RightBattery);

        Assert.False(b.PrimaryLeft);
        Assert.Equal(90, b.LeftBattery);
        Assert.Equal(80, b.RightBattery);
    }

    [Fact]
    public void TryParse_MissingLocalName_StillParses()
    {
        var context = new AirPodsAdvertisementContext
        {
            ReceivedAt = DateTimeOffset.UnixEpoch,
            BluetoothAddress = 0x1122334455,
            LocalName = null,
        };

        var ok = AirPodsAdvertisementParser.TryParse(BuildPayload(), context, out var s);

        Assert.True(ok);
        Assert.Equal("AirPods Pro 2 Lightning", s.ModelName);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, 50)]
    [InlineData(10, 100)]
    [InlineData(11, null)]
    [InlineData(15, null)]
    public void BatteryMapper_MapsNibbleToPercentage(int nibble, int? expected)
    {
        Assert.Equal(expected, AirPodsBatteryMapper.FromNibble(nibble));
    }
}
