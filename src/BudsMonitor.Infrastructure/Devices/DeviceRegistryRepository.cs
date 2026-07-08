using System.Text.Json;
using BudsMonitor.Infrastructure.Json;
using BudsMonitor.Infrastructure.Storage;

namespace BudsMonitor.Infrastructure.Devices;

/// <summary>Loads and saves the device registry as JSON, tolerant of a missing or corrupt file.</summary>
public sealed class DeviceRegistryRepository
{
    private readonly string _filePath;

    public DeviceRegistryRepository(StoragePaths paths)
        : this(paths.DevicesFile)
    {
    }

    public DeviceRegistryRepository(string filePath)
    {
        _filePath = filePath;
    }

    public DeviceRegistryFile Load()
    {
        if (!File.Exists(_filePath))
        {
            return new DeviceRegistryFile();
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<DeviceRegistryFile>(json, StorageJson.Options)
                   ?? new DeviceRegistryFile();
        }
        catch (JsonException)
        {
            return new DeviceRegistryFile();
        }
    }

    public void Save(DeviceRegistryFile registry)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var json = JsonSerializer.Serialize(registry, StorageJson.Options);
        File.WriteAllText(_filePath, json);
    }
}
