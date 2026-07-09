using System.Text;
using System.Text.Json;

namespace BudsMonitor.Application.Updates;

/// <summary>
/// Pure parsing of the GitHub "list releases" JSON. Kept separate from the HTTP layer so the
/// version-selection and asset-matching rules are unit-testable without a network call.
/// </summary>
public static class GitHubReleaseParser
{
    /// <summary>
    /// Returns the newest published release strictly newer than <paramref name="currentVersion"/>,
    /// or null if none applies (already up to date, only drafts/prereleases, the newest matches
    /// <paramref name="skippedVersion"/>, or no matching portable asset is attached). Release
    /// notes combine every intervening version, newest first.
    /// </summary>
    /// <param name="zipAssetContains">Substring the portable .zip asset name must contain, e.g. "portable-win-x64".</param>
    public static UpdateInfo? SelectUpdate(
        string releasesJson,
        Version currentVersion,
        string zipAssetContains,
        string? skippedVersion = null)
    {
        using var doc = JsonDocument.Parse(releasesJson);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var newer = new List<(Version version, string tag, string body, JsonElement element)>();
        foreach (var release in root.EnumerateArray())
        {
            if (IsTrue(release, "draft") || IsTrue(release, "prerelease"))
            {
                continue;
            }

            var tag = GetString(release, "tag_name");
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var version) || version <= currentVersion)
            {
                continue;
            }

            newer.Add((version, tag, GetString(release, "body"), release));
        }

        if (newer.Count == 0)
        {
            return null;
        }

        newer.Sort((a, b) => b.version.CompareTo(a.version));
        var latest = newer[0];

        if (!string.IsNullOrWhiteSpace(skippedVersion)
            && string.Equals(latest.tag.TrimStart('v', 'V'), skippedVersion.TrimStart('v', 'V'),
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var (zipUrl, sha256Url) = SelectAssets(latest.element, zipAssetContains);
        if (string.IsNullOrEmpty(zipUrl))
        {
            return null;
        }

        var notes = new StringBuilder();
        foreach (var (_, tag, body, _) in newer)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                continue;
            }

            if (notes.Length > 0)
            {
                notes.AppendLine();
            }

            notes.AppendLine($"## {tag}");
            notes.AppendLine(body.Trim());
        }

        return new UpdateInfo(latest.version, latest.tag, zipUrl, sha256Url, notes.ToString().TrimEnd());
    }

    private static (string zipUrl, string sha256Url) SelectAssets(JsonElement release, string zipAssetContains)
    {
        string zipUrl = "", sha256Url = "";
        if (!release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
        {
            return (zipUrl, sha256Url);
        }

        foreach (var asset in assets.EnumerateArray())
        {
            var name = GetString(asset, "name");
            var url = GetString(asset, "browser_download_url");
            if (name.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase))
            {
                sha256Url = url;
            }
            else if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                     && name.Contains(zipAssetContains, StringComparison.OrdinalIgnoreCase))
            {
                zipUrl = url;
            }
        }

        return (zipUrl, sha256Url);
    }

    private static bool IsTrue(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;

    private static string GetString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
}
