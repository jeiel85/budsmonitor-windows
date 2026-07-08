using System.Text.Json;
using BudsMonitor.Infrastructure.Json;
using BudsMonitor.Infrastructure.Storage;

namespace BudsMonitor.Infrastructure.Cache;

/// <summary>
/// Loads and saves the battery cache as JSON. Returns an empty cache when the file is
/// missing or corrupt so a bad cache never blocks startup.
/// </summary>
public sealed class BatteryCacheRepository
{
    private readonly string _filePath;

    public BatteryCacheRepository(StoragePaths paths)
        : this(paths.BatteryCacheFile)
    {
    }

    public BatteryCacheRepository(string filePath)
    {
        _filePath = filePath;
    }

    public BatteryCacheFile Load()
    {
        if (!File.Exists(_filePath))
        {
            return new BatteryCacheFile();
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<BatteryCacheFile>(json, StorageJson.Options)
                   ?? new BatteryCacheFile();
        }
        catch (JsonException)
        {
            return new BatteryCacheFile();
        }
    }

    public void Save(BatteryCacheFile cache)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var json = JsonSerializer.Serialize(cache, StorageJson.Options);
        File.WriteAllText(_filePath, json);
    }
}
