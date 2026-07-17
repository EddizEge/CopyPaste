using CopyPaste.Core.Models;
using System.Globalization;
using System.Text;

namespace CopyPaste.Core.Services;

public sealed class FailedItemRetryService
{
    private readonly RobocopyRunner _runner;

    public FailedItemRetryService(RobocopyRunner? runner = null) =>
        _runner = runner ?? new RobocopyRunner();

    public async Task<RobocopyResult> RetryAsync(
        CopyJob originalJob,
        IProgress<RobocopyProgress>? progress = null,
        CancellationToken cancellationToken = default,
        Action<string>? logLine = null)
    {
        var retryJobs = CreateRetryJobs(originalJob);
        if (retryJobs.Count == 0)
            return new(16, CopyJobStatus.Failed, "Yeniden denenecek güvenli bir dosya yolu bulunamadı.");

        var remainingFailures = new List<CopyFailure>();
        for (var index = 0; index < retryJobs.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var retryJob = retryJobs[index];
            var sourceItem = retryJob.Options.FilePatterns.Count == 1
                ? Path.Combine(retryJob.SourcePath, retryJob.Options.FilePatterns[0])
                : retryJob.SourcePath;
            progress?.Report(new(null, $"[{index + 1}/{retryJobs.Count}] Yeniden deneniyor: {sourceItem}"));
            logLine?.Invoke($"--- Yalnızca hatalı öğe yeniden denemesi {index + 1}/{retryJobs.Count}: {sourceItem} ---");
            var nestedProgress = new Progress<RobocopyProgress>(value =>
                progress?.Report(new(value.Percentage, $"[{index + 1}/{retryJobs.Count}] {value.Message}")));
            var result = await _runner.RunAsync(retryJob, nestedProgress, cancellationToken, logLine)
                .ConfigureAwait(false);
            if (!result.IsSuccessful)
            {
                if (result.Failures is { Count: > 0 })
                    remainingFailures.AddRange(result.Failures);
                else
                    remainingFailures.Add(new(sourceItem, result.Summary));
            }
        }

        return remainingFailures.Count == 0
            ? new(1, CopyJobStatus.Completed,
                $"Daha önce kopyalanamayan {retryJobs.Count:N0} öğenin tamamı başarıyla kopyalandı.")
            : new(8, CopyJobStatus.CompletedWithErrors,
                $"Hatalı öğeler yeniden denendi; {remainingFailures.Count:N0} öğe hâlâ kopyalanamadı.",
                remainingFailures, remainingFailures.Count);
    }

    public static IReadOnlyList<CopyJob> CreateRetryJobs(CopyJob originalJob)
    {
        var sourceRoot = Path.GetFullPath(originalJob.SourcePath).TrimEnd(Path.DirectorySeparatorChar);
        var destinationRoot = Path.GetFullPath(originalJob.DestinationPath).TrimEnd(Path.DirectorySeparatorChar);
        var jobs = new List<CopyJob>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var failure in originalJob.Failures)
        {
            string failedPath;
            try { failedPath = Path.GetFullPath(failure.Path.Trim().Trim('"')); }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                continue;
            }
            if (!IsWithinRoot(failedPath, sourceRoot))
                continue;
            failedPath = ResolveExistingPath(failedPath, sourceRoot) ?? failedPath;
            if (!seen.Add(failedPath) || (!File.Exists(failedPath) && !Directory.Exists(failedPath)))
                continue;

            var relativePath = Path.GetRelativePath(sourceRoot, failedPath);
            if (Directory.Exists(failedPath))
            {
                jobs.Add(new CopyJob
                {
                    SourcePath = failedPath,
                    DestinationPath = Path.Combine(destinationRoot, relativePath),
                    Profile = originalJob.Profile,
                    Options = originalJob.Options with { FilePatterns = ["*"] }
                });
                continue;
            }

            var relativeDirectory = Path.GetDirectoryName(relativePath) ?? string.Empty;
            jobs.Add(new CopyJob
            {
                SourcePath = Path.GetDirectoryName(failedPath) ?? sourceRoot,
                DestinationPath = Path.Combine(destinationRoot, relativeDirectory),
                Profile = originalJob.Profile,
                Options = originalJob.Options with
                {
                    FilePatterns = [Path.GetFileName(failedPath)],
                    ExcludedDirectories = []
                }
            });
        }
        return jobs;
    }

    private static bool IsWithinRoot(string path, string root) =>
        path.Equals(root, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static string? ResolveExistingPath(string path, string sourceRoot)
    {
        if (File.Exists(path) || Directory.Exists(path))
            return path;

        var relativePath = Path.GetRelativePath(sourceRoot, path);
        if (relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || relativePath.Equals("..", StringComparison.Ordinal))
            return null;

        var current = sourceRoot;
        foreach (var segment in relativePath.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            if (!Directory.Exists(current))
                return null;

            var matches = Directory.EnumerateFileSystemEntries(current)
                .Where(entry => FoldName(Path.GetFileName(entry)) == FoldName(segment))
                .Take(2)
                .ToArray();
            if (matches.Length != 1)
                return null;
            current = matches[0];
        }
        return current;
    }

    private static string FoldName(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;
            builder.Append(character switch
            {
                'ı' or 'İ' => 'i',
                _ => char.ToLowerInvariant(character)
            });
        }
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
