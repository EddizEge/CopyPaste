using System.Net.Http.Headers;
using System.Security.Cryptography;
using CopyPaste.Core.Models;

namespace CopyPaste.Core.Services;

public sealed record UpdateDownloadProgress(long BytesReceived, long? TotalBytes)
{
    public double? Percentage => TotalBytes is > 0
        ? Math.Clamp(BytesReceived * 100d / TotalBytes.Value, 0, 100)
        : null;
}

public sealed record UpdateDownloadResult(bool Success, string? FilePath = null, string? Error = null);

public sealed class UpdateDownloadService
{
    private readonly HttpClient _httpClient;

    public UpdateDownloadService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("CopyPaste", "1.3"));
    }

    public async Task<UpdateDownloadResult> DownloadAsync(
        UpdateCheckResult update,
        string destinationDirectory,
        IProgress<UpdateDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!update.HasUpdate || update.DownloadUri is null || string.IsNullOrWhiteSpace(update.AssetName))
            return new(false, Error: "İndirilebilir güncelleme paketi bulunamadı.");
        if (!IsTrustedGitHubUri(update.DownloadUri))
            return new(false, Error: "Güncelleme adresi güvenilir bir GitHub bağlantısı değil.");
        if (!TryNormalizeDigest(update.Sha256Digest, out var expectedDigest))
            return new(false, Error: "Güncellemenin SHA-256 özeti GitHub Release üzerinde bulunamadı.");

        var safeName = Path.GetFileName(update.AssetName);
        if (!string.Equals(safeName, update.AssetName, StringComparison.Ordinal)
            || !(safeName.EndsWith(ProductInfo.InstallerAssetSuffix, StringComparison.OrdinalIgnoreCase)
                 || safeName.EndsWith(ProductInfo.PortableAssetSuffix, StringComparison.OrdinalIgnoreCase)))
        {
            return new(false, Error: "Güncelleme dosya adı güvenli değil.");
        }

        Directory.CreateDirectory(destinationDirectory);
        var destinationPath = Path.GetFullPath(Path.Combine(destinationDirectory, safeName));
        var root = Path.GetFullPath(destinationDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!destinationPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            return new(false, Error: "Güncelleme hedefi güvenli değil.");

        var temporaryPath = destinationPath + ".download";
        try
        {
            using var response = await _httpClient.GetAsync(
                update.DownloadUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var totalBytes = response.Content.Headers.ContentLength;
            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var target = new FileStream(
                temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 128 * 1024, useAsync: true);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[128 * 1024];
            long received = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                hash.AppendData(buffer, 0, read);
                received += read;
                progress?.Report(new(received, totalBytes));
            }
            await target.FlushAsync(cancellationToken).ConfigureAwait(false);
            var actualDigest = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            if (!CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(actualDigest), Convert.FromHexString(expectedDigest)))
            {
                return new(false, Error: "Güncelleme paketi SHA-256 doğrulamasından geçemedi.");
            }

            await target.DisposeAsync().ConfigureAwait(false);
            File.Move(temporaryPath, destinationPath, overwrite: true);
            return new(true, destinationPath);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new(false, Error: "Güncelleme indirmesi iptal edildi.");
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException)
        {
            return new(false, Error: "Güncelleme indirilemedi: " + ex.Message);
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            catch (IOException) { }
        }
    }

    private static bool TryNormalizeDigest(string? digest, out string normalized)
    {
        normalized = (digest ?? string.Empty).Trim();
        if (normalized.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[7..];
        normalized = normalized.ToLowerInvariant();
        return normalized.Length == 64 && normalized.All(Uri.IsHexDigit);
    }

    private static bool IsTrustedGitHubUri(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttps
        && (uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith(".github.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase));
}
