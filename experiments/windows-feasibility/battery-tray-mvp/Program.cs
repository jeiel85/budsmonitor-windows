using System.Buffers.Binary;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage.Streams;

namespace LibrePods.WindowsFeasibility.BatteryTrayMvp;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new TrayContext());
    }
}

internal sealed class TrayContext : ApplicationContext
{
    private readonly BluetoothLEAdvertisementWatcher watcher = new()
    {
        ScanningMode = BluetoothLEScanningMode.Active
    };

    private readonly NotifyIcon trayIcon;

    public TrayContext()
    {
        trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Information,
            Text = "LibrePods: scanning for AirPods",
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };

        watcher.Received += OnAdvertisementReceived;
        watcher.Stopped += (_, eventArgs) =>
        {
            trayIcon.Text = TrimTooltip($"LibrePods: scanner stopped ({eventArgs.Error})");
        };
        watcher.Start();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        menu.Items.Add(new ToolStripMenuItem("Scanning for AirPods")
        {
            Enabled = false,
            Name = "status"
        });
        menu.Items.Add(new ToolStripSeparator());

        var rescanItem = new ToolStripMenuItem("Restart scan");
        rescanItem.Click += (_, _) =>
        {
            watcher.Stop();
            watcher.Start();
            trayIcon.Text = "LibrePods: scanning for AirPods";
            SetStatus("Scanning for AirPods");
        };
        menu.Items.Add(rescanItem);

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitThread();
        menu.Items.Add(exitItem);

        return menu;
    }

    private void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs eventArgs)
    {
        foreach (var manufacturer in eventArgs.Advertisement.ManufacturerData)
        {
            if (manufacturer.CompanyId != 0x004C)
            {
                continue;
            }

            var payload = ReadBytes(manufacturer.Data);
            if (!AirPodsAdvertisementParser.TryParse(payload, out var info))
            {
                continue;
            }

            var display = info with
            {
                Address = FormatBluetoothAddress(eventArgs.BluetoothAddress),
                Name = string.IsNullOrWhiteSpace(eventArgs.Advertisement.LocalName)
                    ? "AirPods"
                    : eventArgs.Advertisement.LocalName
            };

            trayIcon.Text = TrimTooltip($"LibrePods: {display.Model} {display.BatterySummary}");
            SetStatus($"{display.Name} | {display.Model} | {display.BatterySummary} | {display.ConnectionState}");
        }
    }

    private void SetStatus(string text)
    {
        if (trayIcon.ContextMenuStrip?.Items["status"] is ToolStripMenuItem statusItem)
        {
            statusItem.Text = text;
        }
    }

    protected override void ExitThreadCore()
    {
        watcher.Stop();
        trayIcon.Visible = false;
        trayIcon.Dispose();
        watcher.Received -= OnAdvertisementReceived;
        base.ExitThreadCore();
    }

    private static byte[] ReadBytes(IBuffer buffer)
    {
        var data = new byte[buffer.Length];
        using var reader = DataReader.FromBuffer(buffer);
        reader.ReadBytes(data);
        return data;
    }

    private static string FormatBluetoothAddress(ulong address)
    {
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, address);
        return Convert.ToHexString(bytes[^6..]).Chunk(2).Select(chunk => new string(chunk)).Aggregate((a, b) => $"{a}:{b}");
    }

    private static string TrimTooltip(string value) => value.Length <= 63 ? value : value[..63];
}

internal sealed record AirPodsAdvertisementInfo(
    string Name,
    string Address,
    ushort ModelId,
    string Model,
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
    string ConnectionState)
{
    public string BatterySummary => $"L={FormatBattery(LeftPodBattery)} R={FormatBattery(RightPodBattery)} C={FormatBattery(CaseBattery)}";

    private static string FormatBattery(int value) => value < 0 ? "n/a" : $"{value}%";
}

internal static class AirPodsAdvertisementParser
{
    public static bool TryParse(byte[] data, out AirPodsAdvertisementInfo info)
    {
        info = default!;

        if (data.Length < 11 || data[0] != 0x07 || data[2] == 0x00)
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
        var leftPodInEar = xorFactor ? (status & 0x08) != 0 : (status & 0x02) != 0;
        var rightPodInEar = xorFactor ? (status & 0x02) != 0 : (status & 0x08) != 0;

        var lidState = thisPodInCase
            ? (((lidIndicator >> 3) & 0x01) == 0 ? "open" : "closed")
            : "unknown";

        info = new AirPodsAdvertisementInfo(
            "AirPods",
            "",
            modelId,
            ModelName(modelId),
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
