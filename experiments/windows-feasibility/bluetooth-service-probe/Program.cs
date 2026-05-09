using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;

var aacpUuid = Guid.Parse("74ec2172-0bad-4d01-8f77-997b2be0722a");

Console.WriteLine("LibrePods Windows Bluetooth Service Probe");
Console.WriteLine($"AACP UUID: {aacpUuid}");
Console.WriteLine();

await PrintDevices("Paired/known Bluetooth devices", BluetoothDevice.GetDeviceSelector());
await PrintDevices("Paired/known Bluetooth LE devices", BluetoothLEDevice.GetDeviceSelector());
await PrintDevices("RFCOMM services matching AACP UUID", RfcommDeviceService.GetDeviceSelector(RfcommServiceId.FromUuid(aacpUuid)));
await PrintDevices("GATT services matching AACP UUID", GattDeviceService.GetDeviceSelectorFromUuid(aacpUuid));

Console.WriteLine();
Console.WriteLine("Interpretation:");
Console.WriteLine("- If RFCOMM/GATT AACP entries are empty, Windows may not expose the AirPods control channel through normal high-level APIs.");
Console.WriteLine("- That does not prove AACP is impossible, but it means the next step is a native socket/SDP PoC.");

static async Task PrintDevices(string title, string selector)
{
    Console.WriteLine($"== {title} ==");
    try
    {
        var devices = await DeviceInformation.FindAllAsync(selector);
        if (devices.Count == 0)
        {
            Console.WriteLine("  (none)");
            Console.WriteLine();
            return;
        }

        foreach (var device in devices)
        {
            Console.WriteLine($"  Name:      {device.Name}");
            Console.WriteLine($"  Id:        {device.Id}");
            Console.WriteLine($"  Kind:      {device.Kind}");
            Console.WriteLine($"  Enabled:   {device.IsEnabled}");
            Console.WriteLine($"  Default:   {device.IsDefault}");
            Console.WriteLine();
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  Error: {ex.GetType().Name}: {ex.Message}");
        Console.WriteLine();
    }
}
