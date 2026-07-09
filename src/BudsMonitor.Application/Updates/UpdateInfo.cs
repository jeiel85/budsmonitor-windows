namespace BudsMonitor.Application.Updates;

/// <summary>
/// A newer release available from GitHub. <see cref="ZipUrl"/> is the portable bundle to
/// download; <see cref="Sha256Url"/> is its checksum file (empty if the release has none).
/// </summary>
public sealed record UpdateInfo(
    Version Version,
    string Tag,
    string ZipUrl,
    string Sha256Url,
    string ReleaseNotes);
