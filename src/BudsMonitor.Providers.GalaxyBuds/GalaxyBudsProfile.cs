namespace BudsMonitor.Providers.GalaxyBuds;

/// <summary>Root of the embedded galaxy-buds-profiles.json file.</summary>
public sealed record GalaxyBudsProfilesFile
{
    public int Version { get; init; } = 1;
    public IReadOnlyList<GalaxyBudsProfile> Profiles { get; init; } = [];
}

/// <summary>
/// A known Galaxy Buds family. Recognition is by device name: any of <see cref="NamePatterns"/>
/// appearing (case-insensitive) in the advertised/paired name identifies the model. The most
/// specific (longest) matching pattern wins, so "Galaxy Buds2 Pro" beats "Galaxy Buds".
/// </summary>
public sealed record GalaxyBudsProfile
{
    public required string Model { get; init; }
    public required string DisplayName { get; init; }
    public IReadOnlyList<string> NamePatterns { get; init; } = [];
}

/// <summary>Result of classifying a device name as a Galaxy Buds model.</summary>
public sealed record GalaxyBudsMatch(string Model, string DisplayName);
