using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace ColumnPadStudio.Services;

public sealed record GitHubReleaseInfo(
    Version Version,
    string DisplayVersion,
    Uri ReleasePage);

public sealed class GitHubReleaseUpdateService
{
    private static readonly Uri LatestReleaseApiUri = new(
        "https://api.github.com/repos/Awetspoon/ColumnPadStudio/releases/latest");

    private static readonly HttpClient SharedHttpClient = CreateSharedHttpClient();

    public static Uri ReleasesPageUri { get; } = new(
        "https://github.com/Awetspoon/ColumnPadStudio/releases/latest");

    private readonly HttpClient _httpClient;

    public GitHubReleaseUpdateService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? SharedHttpClient;
    }

    public async Task<GitHubReleaseInfo?> GetLatestReleaseAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApiUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("ColumnPadStudio", "1.0"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        using var response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        using var document = await JsonDocument
            .ParseAsync(responseStream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (!document.RootElement.TryGetProperty("tag_name", out var tagElement) ||
            !TryParseReleaseVersion(tagElement.GetString(), out var version))
        {
            return null;
        }

        var tagName = tagElement.GetString()!.Trim();
        var releasePage = ResolveReleasePage(document.RootElement);
        return new GitHubReleaseInfo(version, NormalizeDisplayVersion(tagName), releasePage);
    }

    public static bool TryParseReleaseVersion(string? tagName, out Version version)
    {
        version = new Version(0, 0, 0, 0);
        if (string.IsNullOrWhiteSpace(tagName))
            return false;

        var candidate = tagName.Trim();
        if (candidate.StartsWith('v') || candidate.StartsWith('V'))
            candidate = candidate[1..];

        var metadataIndex = candidate.IndexOfAny(['-', '+']);
        if (metadataIndex >= 0)
            candidate = candidate[..metadataIndex];

        if (!Version.TryParse(candidate, out var parsed))
            return false;

        version = NormalizeVersion(parsed);
        return true;
    }

    public static bool IsNewerRelease(Version latestVersion, Version currentVersion)
    {
        ArgumentNullException.ThrowIfNull(latestVersion);
        ArgumentNullException.ThrowIfNull(currentVersion);
        return NormalizeVersion(latestVersion) > NormalizeVersion(currentVersion);
    }

    private static Uri ResolveReleasePage(JsonElement release)
    {
        if (release.TryGetProperty("html_url", out var urlElement) &&
            Uri.TryCreate(urlElement.GetString(), UriKind.Absolute, out var parsedUri) &&
            parsedUri.Scheme == Uri.UriSchemeHttps &&
            string.Equals(parsedUri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            return parsedUri;
        }

        return ReleasesPageUri;
    }

    private static string NormalizeDisplayVersion(string tagName)
    {
        return tagName.StartsWith('v') || tagName.StartsWith('V')
            ? $"v{tagName[1..]}"
            : $"v{tagName}";
    }

    private static Version NormalizeVersion(Version version)
    {
        return new Version(
            version.Major,
            version.Minor,
            Math.Max(0, version.Build),
            Math.Max(0, version.Revision));
    }

    private static HttpClient CreateSharedHttpClient()
    {
        return new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(6)
        };
    }
}
