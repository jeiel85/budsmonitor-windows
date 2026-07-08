using System.Text.Json;

namespace BudsMonitor.Infrastructure.Json;

/// <summary>Shared System.Text.Json options for all persisted files (camelCase, indented).</summary>
public static class StorageJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };
}
