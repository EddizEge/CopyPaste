using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using CopyPaste.Core.Models;

namespace CopyPaste.Core.Services;

public enum UpdateCheckStatus
{
    UpToDate,
    UpdateAvailable,
    RepositoryUnavailable,
    NetworkError,
    InvalidResponse
}

public sealed record UpdateCheckResult(
    UpdateCheckStatus Status,
    Version CurrentVersion,
    Version? LatestVersion = null,
    string? TagName = null,
    Uri? ReleasePageUri = null,
    Uri? DownloadUri = null,
    string? ReleaseNotes = null,
    string? Error = null)
{
    public bool HasUpdate => Status == UpdateCheckStatus.UpdateAvailable;
}

public sealed class GitHubUpdateService
{
    private readonly HttpClient _httpClient;
    private readonly string _owner;
    private readonly string _repository;

    public GitHubUpdateService(
        HttpClient? httpClient = null,
        string owner = ProductInfo.GitHubOwner,
        string repository = ProductInfo.GitHubRepository)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _owner = owner;
        _repository = repository;
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("CopyPaste", "1.1"));
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public async Task<UpdateCheckResult> CheckAsync(
        Version currentVersion,
        CancellationToken cancellationToken = default)
    {
        var endpoint = $"https://api.github.com/repos/{Uri.EscapeDataString(_owner)}/" +
                       $"{Uri.EscapeDataString(_repository)}/releases/latest";
        try
        {
            using var response = await _httpClient.GetAsync(endpoint, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new(UpdateCheckStatus.RepositoryUnavailable, currentVersion,
                    Error: "GitHub deposu veya yayınlanmış bir Release henüz bulunamadı.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return new(UpdateCheckStatus.NetworkError, currentVersion,
                    Error: $"GitHub yanıtı: {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return ParseRelease(document.RootElement, currentVersion);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(UpdateCheckStatus.NetworkError, currentVersion,
                Error: "GitHub güncelleme denetimi zaman aşımına uğradı.");
        }
        catch (HttpRequestException ex)
        {
            return new(UpdateCheckStatus.NetworkError, currentVersion, Error: ex.Message);
        }
        catch (JsonException ex)
        {
            return new(UpdateCheckStatus.InvalidResponse, currentVersion, Error: ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return new(UpdateCheckStatus.InvalidResponse, currentVersion, Error: ex.Message);
        }
    }

    public static bool TryParseVersion(string? tagName, out Version version)
    {
        var normalized = (tagName ?? string.Empty).Trim().TrimStart('v', 'V');
        var suffixIndex = normalized.IndexOfAny(['-', '+']);
        if (suffixIndex >= 0)
            normalized = normalized[..suffixIndex];
        if (!Version.TryParse(normalized, out var parsed))
        {
            version = new Version(0, 0, 0, 0);
            return false;
        }

        version = NormalizeVersion(parsed);
        return true;
    }

    private static UpdateCheckResult ParseRelease(JsonElement root, Version currentVersion)
    {
        if (!root.TryGetProperty("tag_name", out var tagElement)
            || !TryParseVersion(tagElement.GetString(), out var latestVersion))
        {
            return new(UpdateCheckStatus.InvalidResponse, currentVersion,
                Error: "GitHub Release sürüm etiketi okunamadı.");
        }

        var tagName = tagElement.GetString();
        var releasePage = TryGetUri(root, "html_url");
        var download = FindPortableAsset(root) ?? releasePage;
        var releaseNotes = root.TryGetProperty("body", out var body) ? body.GetString() : null;
        var status = latestVersion > NormalizeVersion(currentVersion)
            ? UpdateCheckStatus.UpdateAvailable
            : UpdateCheckStatus.UpToDate;
        return new(status, NormalizeVersion(currentVersion), latestVersion, tagName,
            releasePage, download, releaseNotes);
    }

    private static Uri? FindPortableAsset(JsonElement root)
    {
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
            if (name?.EndsWith(ProductInfo.PortableAssetSuffix, StringComparison.OrdinalIgnoreCase) != true)
                continue;
            var uri = TryGetUri(asset, "browser_download_url");
            if (uri is not null)
                return uri;
        }
        return null;
    }

    private static Uri? TryGetUri(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property)
            || !Uri.TryCreate(property.GetString(), UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !(uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
                 || uri.Host.EndsWith(".github.com", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        return uri;
    }

    private static Version NormalizeVersion(Version version) => new(
        Math.Max(0, version.Major),
        Math.Max(0, version.Minor),
        Math.Max(0, version.Build),
        Math.Max(0, version.Revision));
}
