using System.Security.Cryptography;
using CopyPaste.Core.Models;

namespace CopyPaste.Core.Services;

public enum CopyDifferenceKind
{
    Missing,
    SizeMismatch,
    HashMismatch,
    ReadError
}

public sealed record CopyDifference(string SourcePath, string RelativePath, CopyDifferenceKind Kind, string Detail);

public sealed record CopyComparisonResult(
    int CheckedFiles,
    int IdenticalFiles,
    int MissingFiles,
    int SizeMismatches,
    int HashMismatches,
    int ReadErrors,
    IReadOnlyList<CopyDifference> Differences)
{
    public bool NeedsRepair => MissingFiles + SizeMismatches + HashMismatches > 0;
}

public sealed class CopyComparisonService
{
    public Task<CopyComparisonResult> CompareAsync(
        CopyJob job,
        IProgress<CopyVerificationProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Compare(job, progress, cancellationToken), cancellationToken);

    private static CopyComparisonResult Compare(
        CopyJob job,
        IProgress<CopyVerificationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var checkedFiles = 0;
        var identical = 0;
        var missing = 0;
        var sizeMismatch = 0;
        var hashMismatch = 0;
        var readErrors = 0;
        var differences = new List<CopyDifference>();
        var enumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        foreach (var sourceFile in Directory.EnumerateFiles(job.SourcePath, "*", enumerationOptions))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = Path.GetRelativePath(job.SourcePath, sourceFile);
            if (!CopyPathFilter.ShouldIncludeFile(relativePath, job.Options))
                continue;
            checkedFiles++;
            progress?.Report(new(checkedFiles, relativePath));
            var destinationFile = Path.Combine(job.DestinationPath, relativePath);
            if (!File.Exists(destinationFile))
            {
                missing++;
                differences.Add(new(sourceFile, relativePath, CopyDifferenceKind.Missing, "Hedefte bulunmuyor"));
                continue;
            }
            try
            {
                var sourceLength = new FileInfo(sourceFile).Length;
                var destinationLength = new FileInfo(destinationFile).Length;
                if (sourceLength != destinationLength)
                {
                    sizeMismatch++;
                    differences.Add(new(sourceFile, relativePath, CopyDifferenceKind.SizeMismatch,
                        $"Boyut farklı: {sourceLength:N0} / {destinationLength:N0} bayt"));
                    continue;
                }
                if (job.Options.Verification == VerificationMode.Sha256
                    && !HashesMatch(sourceFile, destinationFile, cancellationToken))
                {
                    hashMismatch++;
                    differences.Add(new(sourceFile, relativePath, CopyDifferenceKind.HashMismatch, "SHA-256 farklı"));
                    continue;
                }
                identical++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                readErrors++;
                differences.Add(new(sourceFile, relativePath, CopyDifferenceKind.ReadError, ex.Message));
            }
        }
        return new(checkedFiles, identical, missing, sizeMismatch, hashMismatch, readErrors, differences);
    }

    private static bool HashesMatch(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        using var algorithm = SHA256.Create();
        using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var destination = new FileStream(destinationPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var sourceHash = algorithm.ComputeHash(source);
        cancellationToken.ThrowIfCancellationRequested();
        var destinationHash = algorithm.ComputeHash(destination);
        return CryptographicOperations.FixedTimeEquals(sourceHash, destinationHash);
    }
}

public static class CopyRepairService
{
    public static IReadOnlyList<CopyJob> CreateRepairJobs(CopyJob original, IEnumerable<CopyDifference> differences) =>
        differences
            .Where(item => item.Kind is CopyDifferenceKind.Missing
                or CopyDifferenceKind.SizeMismatch or CopyDifferenceKind.HashMismatch)
            .GroupBy(item => Path.GetDirectoryName(item.RelativePath) ?? string.Empty,
                StringComparer.OrdinalIgnoreCase)
            .Select(group => new CopyJob
            {
                SourcePath = Path.Combine(original.SourcePath, group.Key),
                DestinationPath = Path.Combine(original.DestinationPath, group.Key),
                DestinationRootPath = original.DestinationRootPath,
                RootMode = original.RootMode,
                Profile = original.Profile,
                RequestedPerformanceMode = original.RequestedPerformanceMode,
                ActivePerformanceMode = original.ActivePerformanceMode,
                BandwidthLimitMbps = original.BandwidthLimitMbps,
                UseBackupMode = original.UseBackupMode,
                Options = original.Options with
                {
                    ExistingFiles = ExistingFileBehavior.Overwrite,
                    FilePatterns = group.Select(item => Path.GetFileName(item.RelativePath))
                        .Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                    ExcludedDirectories = []
                }
            })
            .ToList();
}
