using System.Net.Http;
using System.Security.Cryptography;

namespace BudsMonitor.Application.Updates;

/// <summary>Classifies update-check failures so the UI can route messaging by cause.</summary>
public enum UpdateCheckErrorKind
{
    Unknown,
    Network,
    Timeout,
    RateLimit,
    ApiError,
}

public sealed class UpdateCheckException : Exception
{
    public UpdateCheckErrorKind Kind { get; }
    public int? StatusCode { get; }
    public string? RetryAtLocal { get; }

    public UpdateCheckException(string message, UpdateCheckErrorKind kind,
        int? statusCode = null, string? retryAtLocal = null, Exception? inner = null)
        : base(message, inner)
    {
        Kind = kind;
        StatusCode = statusCode;
        RetryAtLocal = retryAtLocal;
    }
}

/// <summary>
/// Checks GitHub Releases for a newer BudsMonitor build and downloads the portable bundle.
/// This is the app's only network use — it runs only when the user leaves update checking on
/// or presses "check now". No telemetry, no analytics.
/// </summary>
public sealed class UpdateService
{
    public const string Repo = "jeiel85/budsmonitor-windows";
    public const string ReleasesPage = "https://github.com/jeiel85/budsmonitor-windows/releases";
    private const string ApiListUrl = "https://api.github.com/repos/jeiel85/budsmonitor-windows/releases?per_page=30";
    private const string ZipAssetContains = "portable-win-x64";

    private readonly HttpClient _http;

    public UpdateService(HttpClient? http = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.Add("User-Agent", "BudsMonitor-Updater");
        }
    }

    /// <summary>Returns a newer release, or null if up to date. Throws <see cref="UpdateCheckException"/> on failure.</summary>
    public async Task<UpdateInfo?> CheckForUpdateAsync(
        Version currentVersion, string? skippedVersion = null, CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response;
        string json;
        try
        {
            response = await _http.GetAsync(ApiListUrl, cancellationToken);
            json = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new UpdateCheckException(ex.Message, UpdateCheckErrorKind.Timeout, inner: ex);
        }
        catch (HttpRequestException ex)
        {
            throw new UpdateCheckException(ex.Message, UpdateCheckErrorKind.Network, inner: ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var code = (int)response.StatusCode;
            var rateLimited = code == 403 &&
                (json.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
                 (response.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining)
                  && remaining.FirstOrDefault() == "0"));

            if (rateLimited)
            {
                string? retryAt = null;
                if (response.Headers.TryGetValues("X-RateLimit-Reset", out var reset)
                    && long.TryParse(reset.FirstOrDefault(), out var epoch))
                {
                    retryAt = DateTimeOffset.FromUnixTimeSeconds(epoch).ToLocalTime().ToString("HH:mm");
                }

                throw new UpdateCheckException("GitHub API rate limit exceeded (unauthenticated 60/h).",
                    UpdateCheckErrorKind.RateLimit, code, retryAt);
            }

            throw new UpdateCheckException($"GitHub API returned HTTP {code}.",
                UpdateCheckErrorKind.ApiError, code);
        }

        return GitHubReleaseParser.SelectUpdate(json, currentVersion, ZipAssetContains, skippedVersion);
    }

    /// <summary>Downloads the update ZIP to a temp file and verifies its SHA256 (if provided). Returns the path.</summary>
    public async Task<string> DownloadZipAsync(
        string zipUrl, string sha256Url, IProgress<int>? progress, CancellationToken cancellationToken = default)
    {
        var tempZip = Path.Combine(Path.GetTempPath(), $"BudsMonitor_update_{Guid.NewGuid():N}.zip");

        using (var response = await _http.GetAsync(zipUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
        {
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? 0;

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var destination = new FileStream(
                tempZip, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

            var buffer = new byte[81920];
            long readTotal = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                readTotal += read;
                if (total > 0)
                {
                    progress?.Report((int)(readTotal * 100 / total));
                }
            }
        }

        if (!string.IsNullOrEmpty(sha256Url))
        {
            var expectedRaw = await _http.GetStringAsync(sha256Url, cancellationToken);
            var expected = expectedRaw
                .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()?.Trim().ToLowerInvariant();

            await using var fs = File.OpenRead(tempZip);
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(fs, cancellationToken)).ToLowerInvariant();

            if (!string.IsNullOrEmpty(expected) && actual != expected)
            {
                try { File.Delete(tempZip); } catch { /* best effort */ }
                throw new InvalidOperationException("SHA256 verification failed for the downloaded update.");
            }
        }

        return tempZip;
    }
}
