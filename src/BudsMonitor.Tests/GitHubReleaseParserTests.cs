using BudsMonitor.Application.Updates;

namespace BudsMonitor.Tests;

public sealed class GitHubReleaseParserTests
{
    private const string Pattern = "portable-win-x64";

    private static string Release(string tag, string body, bool draft = false, bool prerelease = false,
        bool withZip = true, bool withSha = true)
    {
        var assets = new List<string>();
        if (withZip)
        {
            assets.Add($$"""
                {"name":"BudsMonitor-{{tag}}-portable-win-x64.zip",
                 "browser_download_url":"https://example.test/{{tag}}/portable.zip"}
                """);
        }
        if (withSha)
        {
            assets.Add($$"""
                {"name":"BudsMonitor-{{tag}}-portable-win-x64.zip.sha256",
                 "browser_download_url":"https://example.test/{{tag}}/portable.zip.sha256"}
                """);
        }

        return $$"""
            {"tag_name":"{{tag}}","draft":{{(draft ? "true" : "false")}},
             "prerelease":{{(prerelease ? "true" : "false")}},"body":{{System.Text.Json.JsonSerializer.Serialize(body)}},
             "assets":[{{string.Join(",", assets)}}]}
            """;
    }

    private static string Releases(params string[] releases) => "[" + string.Join(",", releases) + "]";

    [Fact]
    public void Picks_newest_release_above_current()
    {
        var json = Releases(
            Release("v0.2.0", "Second"),
            Release("v0.1.1", "First"),
            Release("v0.1.0", "Base"));

        var info = GitHubReleaseParser.SelectUpdate(json, new Version(0, 1, 0), Pattern);

        Assert.NotNull(info);
        Assert.Equal(new Version(0, 2, 0), info!.Version);
        Assert.Equal("v0.2.0", info.Tag);
        Assert.Equal("https://example.test/v0.2.0/portable.zip", info.ZipUrl);
        Assert.Equal("https://example.test/v0.2.0/portable.zip.sha256", info.Sha256Url);
    }

    [Fact]
    public void Combines_notes_for_all_intervening_versions_newest_first()
    {
        var json = Releases(Release("v0.1.1", "First fix"), Release("v0.2.0", "Big feature"));
        var info = GitHubReleaseParser.SelectUpdate(json, new Version(0, 1, 0), Pattern);

        Assert.NotNull(info);
        var notes = info!.ReleaseNotes;
        Assert.Contains("## v0.2.0", notes);
        Assert.Contains("Big feature", notes);
        Assert.Contains("## v0.1.1", notes);
        Assert.True(notes.IndexOf("v0.2.0", StringComparison.Ordinal)
                    < notes.IndexOf("v0.1.1", StringComparison.Ordinal), "newest notes first");
    }

    [Fact]
    public void Returns_null_when_up_to_date()
    {
        var json = Releases(Release("v0.1.0", "Base"));
        Assert.Null(GitHubReleaseParser.SelectUpdate(json, new Version(0, 1, 0), Pattern));
        Assert.Null(GitHubReleaseParser.SelectUpdate(json, new Version(0, 2, 0), Pattern));
    }

    [Fact]
    public void Ignores_drafts_and_prereleases()
    {
        var json = Releases(
            Release("v0.3.0", "Draft", draft: true),
            Release("v0.2.5", "Pre", prerelease: true),
            Release("v0.2.0", "Stable"));

        var info = GitHubReleaseParser.SelectUpdate(json, new Version(0, 1, 0), Pattern);
        Assert.Equal(new Version(0, 2, 0), info!.Version);
    }

    [Fact]
    public void Respects_skipped_version()
    {
        var json = Releases(Release("v0.2.0", "Second"));
        Assert.Null(GitHubReleaseParser.SelectUpdate(json, new Version(0, 1, 0), Pattern, skippedVersion: "0.2.0"));
        Assert.Null(GitHubReleaseParser.SelectUpdate(json, new Version(0, 1, 0), Pattern, skippedVersion: "v0.2.0"));
    }

    [Fact]
    public void Returns_null_when_no_matching_zip_asset()
    {
        var json = Releases(Release("v0.2.0", "No zip", withZip: false));
        Assert.Null(GitHubReleaseParser.SelectUpdate(json, new Version(0, 1, 0), Pattern));
    }

    [Fact]
    public void Sha256_is_optional()
    {
        var json = Releases(Release("v0.2.0", "No sha", withSha: false));
        var info = GitHubReleaseParser.SelectUpdate(json, new Version(0, 1, 0), Pattern);
        Assert.NotNull(info);
        Assert.Equal("", info!.Sha256Url);
    }

    [Fact]
    public void Empty_or_non_array_json_returns_null()
    {
        Assert.Null(GitHubReleaseParser.SelectUpdate("[]", new Version(0, 1, 0), Pattern));
        Assert.Null(GitHubReleaseParser.SelectUpdate("{}", new Version(0, 1, 0), Pattern));
    }
}
