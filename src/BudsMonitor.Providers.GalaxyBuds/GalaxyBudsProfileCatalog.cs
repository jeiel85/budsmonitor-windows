using System.Reflection;
using System.Text.Json;

namespace BudsMonitor.Providers.GalaxyBuds;

/// <summary>
/// Loads the Galaxy Buds model profiles (embedded JSON) and matches a device name to the
/// most specific profile. Matching is longest-pattern-wins so nested names
/// ("Galaxy Buds2 Pro" ⊃ "Galaxy Buds") resolve to the correct model.
/// </summary>
public sealed class GalaxyBudsProfileCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly Lazy<GalaxyBudsProfileCatalog> LazyDefault = new(LoadEmbedded);

    public GalaxyBudsProfileCatalog(IReadOnlyList<GalaxyBudsProfile> profiles) => Profiles = profiles;

    /// <summary>Catalog loaded from the embedded profile file.</summary>
    public static GalaxyBudsProfileCatalog Default => LazyDefault.Value;

    public IReadOnlyList<GalaxyBudsProfile> Profiles { get; }

    /// <summary>Returns the best (most specific) Galaxy Buds match for a device name, or null.</summary>
    public GalaxyBudsMatch? Match(string? deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return null;
        }

        GalaxyBudsProfile? best = null;
        var bestLength = 0;

        foreach (var profile in Profiles)
        {
            foreach (var pattern in profile.NamePatterns)
            {
                if (pattern.Length > bestLength
                    && deviceName.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    best = profile;
                    bestLength = pattern.Length;
                }
            }
        }

        return best is null ? null : new GalaxyBudsMatch(best.Model, best.DisplayName);
    }

    private static GalaxyBudsProfileCatalog LoadEmbedded()
    {
        var assembly = typeof(GalaxyBudsProfileCatalog).Assembly;
        var resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("galaxy-buds-profiles.json", StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            return new GalaxyBudsProfileCatalog([]);
        }

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        var file = JsonSerializer.Deserialize<GalaxyBudsProfilesFile>(stream, JsonOptions);
        return new GalaxyBudsProfileCatalog(file?.Profiles ?? []);
    }
}
