using System.Buffers.Binary;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage.Streams;

var duration = ParseDuration(args);
using var done = new CancellationTokenSource(duration);

Console.WriteLine("LibrePods Windows BLE Advertisement Probe");
Console.WriteLine($"Scanning for {duration.TotalSeconds:N0}s. Open the AirPods case near this PC.");
Console.WriteLine();

var seenApplePackets = 0;
var seenAirPodsPackets = 0;
var watcher = new BluetoothLEAdvertisementWatcher
{
    ScanningMode = BluetoothLEScanningMode.Active
};

watcher.Received += (_, eventArgs) =>
{
    foreach (var manufacturer in eventArgs.Advertisement.ManufacturerData)
    {
        if (manufacturer.CompanyId != 0x004C)
        {
            continue;
        }

        var payload = ReadBytes(manufacturer.Data);
        seenApplePackets++;

        var address = FormatBluetoothAddress(eventArgs.BluetoothAddress);
        var name = string.IsNullOrWhiteSpace(eventArgs.Advertisement.LocalName)
            ? "(no local name)"
            : eventArgs.Advertisement.LocalName;

        if (!AirPodsAdvertisementParser.TryParse(payload, out var info))
        {
            Console.WriteLine($"[{DateTimeOffset.Now:T}] Apple packet from {address} {name}: {Convert.ToHexString(payload)}");
            continue;
        }

        seenAirPodsPackets++;
        Console.WriteLine($"[{DateTimeOffset.Now:T}] AirPods candidate {address} {name}");
        Console.WriteLine($"  Raw:        {Convert.ToHexString(payload)}");
        Console.WriteLine($"  Model:      {info.Model} (0x{info.ModelId:X4}), color={info.Color}, state={info.ConnectionState}");
        Console.WriteLine($"  Battery:    L={FormatBattery(info.LeftPodBattery)} R={FormatBattery(info.RightPodBattery)} Case={FormatBattery(info.CaseBattery)}");
        Console.WriteLine($"  Charging:   L={info.LeftCharging} R={info.RightCharging} Case={info.CaseCharging}");
        Console.WriteLine($"  Ear/case:   LInEar={info.LeftPodInEar} RInEar={info.RightPodInEar} primaryLeft={info.PrimaryLeft} lid={info.LidState}");
        Console.WriteLine();
    }
};

watcher.Stopped += (_, eventArgs) =>
{
    Console.WriteLine($"Watcher stopped: {eventArgs.Error}");
};

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    done.Cancel();
};

watcher.Start();
try
{
    await Task.Delay(Timeout.InfiniteTimeSpan, done.Token);
}
catch (OperationCanceledException)
{
}
finally
{
    watcher.Stop();
}

Console.WriteLine();
Console.WriteLine($"Summary: Apple packets={seenApplePackets}, parsed AirPods packets={seenAirPodsPackets}");

static TimeSpan ParseDuration(string[] args)
{
    const int fallbackSeconds = 30;
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i] is "--seconds" or "-s" && int.TryParse(args[i + 1], out var seconds) && seconds > 0)
        {
            return TimeSpan.FromSeconds(seconds);
        }
    }

    return TimeSpan.FromSeconds(fallbackSeconds);
}

static byte[] ReadBytes(IBuffer buffer)
{
    var data = new byte[buffer.Length];
    using var reader = DataReader.FromBuffer(buffer);
    reader.ReadBytes(data);
    return data;
}

static string FormatBluetoothAddress(ulong address)
{
    Span<byte> bytes = stackalloc byte[8];
    BinaryPrimitives.WriteUInt64BigEndian(bytes, address);
    return Convert.ToHexString(bytes[^6..]).Chunk(2).Select(chunk => new string(chunk)).Aggregate((a, b) => $"{a}:{b}");
}

static string FormatBattery(int value) => value < 0 ? "n/a" : $"{value}%";

internal sealed record AirPodsAdvertisementInfo(
    ushort ModelId,
    string Model,
    string Color,
    int LeftPodBattery,
    int RightPodBattery,
    int CaseBattery,
    bool LeftCharging,
    bool RightCharging,
    bool CaseCharging,
    bool LeftPodInEar,
    bool RightPodInEar,
    bool PrimaryLeft,
    string LidState,
    string ConnectionState);

internal static class AirPodsAdvertisementParser
{
    public static bool TryParse(byte[] data, out AirPodsAdvertisementInfo info)
    {
        info = default!;

        if (data.Length < 11 || data[0] != 0x07)
        {
            return false;
        }

        // Pairing-mode packets use a different structure in LibrePods' current parser.
        if (data[2] == 0x00)
        {
            return false;
        }

        var modelId = (ushort)(data[4] | (data[3] << 8));
        var status = data[5];
        var podsBattery = data[6];
        var flagsAndCaseBattery = data[7];
        var lidIndicator = data[8];
        var colorId = data[9];
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
        var leftPodInEar = xorFactor ? (status & 0x08) != 0 : (status & 0x02) != 0;
        var rightPodInEar = xorFactor ? (status & 0x02) != 0 : (status & 0x08) != 0;

        var lidState = thisPodInCase
            ? (((lidIndicator >> 3) & 0x01) == 0 ? "open" : "closed")
            : "unknown";

        info = new AirPodsAdvertisementInfo(
            modelId,
            ModelName(modelId),
            ColorName(colorId),
            BatteryFromNibble(leftNibble),
            BatteryFromNibble(rightNibble),
            BatteryFromNibble(caseNibble),
            leftCharging,
            rightCharging,
            caseCharging,
            leftPodInEar,
            rightPodInEar,
            primaryLeft,
            lidState,
            ConnectionStateName(connectionState));

        return true;
    }

    private static int BatteryFromNibble(int value) => value == 15 ? -1 : value * 10;

    private static string ModelName(ushort modelId) => modelId switch
    {
        0x0220 => "AirPods 1",
        0x0F20 => "AirPods 2",
        0x1320 => "AirPods 3",
        0x1920 => "AirPods 4",
        0x1B20 => "AirPods 4 ANC",
        0x0A20 => "AirPods Max Lightning",
        0x1F20 => "AirPods Max USB-C",
        0x0E20 => "AirPods Pro",
        0x1420 => "AirPods Pro 2 Lightning",
        0x2420 => "AirPods Pro 2 USB-C",
        _ => "Unknown"
    };

    private static string ColorName(byte colorId) => colorId switch
    {
        0x00 => "White",
        0x01 => "Black",
        0x02 => "Red",
        0x03 => "Blue",
        0x04 => "Pink",
        0x05 => "Gray",
        0x06 => "Silver",
        0x07 => "Gold",
        0x08 => "Rose Gold",
        0x09 => "Space Gray",
        0x0A => "Dark Blue",
        0x0B => "Light Blue",
        0x0C => "Yellow",
        _ => "Unknown"
    };

    private static string ConnectionStateName(byte state) => state switch
    {
        0x00 => "Disconnected",
        0x04 => "Idle",
        0x05 => "Playing Music",
        0x06 => "On Call",
        0x07 => "Ringing",
        0x09 => "Hanging Up",
        _ => $"Unknown 0x{state:X2}"
    };
}
