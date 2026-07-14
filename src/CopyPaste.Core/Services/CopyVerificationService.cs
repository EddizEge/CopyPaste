using System.Security.Cryptography;
using CopyPaste.Core.Models;

namespace CopyPaste.Core.Services;

public sealed record CopyVerificationProgress(int CheckedFiles, string RelativePath);

public sealed record CopyVerificationResult(
    int CheckedFiles,
    int MissingFiles,
    int SizeMismatches,
    int HashMismatches,
    int ReadErrors)
{
    public bool IsSuccessful => MissingFiles == 0
        && SizeMismatches == 0
        && HashMismatches == 0
        && ReadErrors == 0;

    public string Summary => IsSuccessful
        ? $"Doğrulama başarılı: {CheckedFiles:N0} dosya kontrol edildi."
        : $"Doğrulama başarısız: {MissingFiles:N0} eksik, {SizeMismatches:N0} boyut farkı, " +
          $"{HashMismatches:N0} hash farkı, {ReadErrors:N0} okuma hatası.";
}

public sealed class CopyVerificationService
{
    public async Task<CopyVerificationResult> VerifyAsync(
        CopyJob job,
        IProgress<CopyVerificationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (job.Options.Verification == VerificationMode.None)
            return new(0, 0, 0, 0, 0);

        var checkedFiles = 0;
        var missingFiles = 0;
        var sizeMismatches = 0;
        var hashMismatches = 0;
        var readErrors = 0;
        var enumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = false,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(job.SourcePath, "*", enumerationOptions);
            foreach (var sourceFile in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relativePath = Path.GetRelativePath(job.SourcePath, sourceFile);
                if (!CopyPathFilter.ShouldIncludeFile(relativePath, job.Options))
                    continue;

                checkedFiles++;
                progress?.Report(new CopyVerificationProgress(checkedFiles, relativePath));
                var destinationFile = Path.Combine(job.DestinationPath, relativePath);
                if (!File.Exists(destinationFile))
                {
                    missingFiles++;
                    continue;
                }

                // "Mevcutları atla" intentionally preserves destination content.
                // In that mode verification guarantees presence without treating differences as corruption.
                if (job.Options.ExistingFiles == ExistingFileBehavior.Skip)
                    continue;

                try
                {
                    var sourceLength = new FileInfo(sourceFile).Length;
                    var destinationLength = new FileInfo(destinationFile).Length;
                    if (sourceLength != destinationLength)
                    {
                        sizeMismatches++;
                        continue;
                    }

                    if (job.Options.Verification == VerificationMode.Sha256
                        && !await HashesMatchAsync(sourceFile, destinationFile, cancellationToken).ConfigureAwait(false))
                    {
                        hashMismatches++;
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    readErrors++;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            readErrors++;
        }

        return new(checkedFiles, missingFiles, sizeMismatches, hashMismatches, readErrors);
    }

    private static async Task<bool> HashesMatchAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        var sourceHash = await HashFileAsync(sourcePath, cancellationToken).ConfigureAwait(false);
        var destinationHash = await HashFileAsync(destinationPath, cancellationToken).ConfigureAwait(false);
        return CryptographicOperations.FixedTimeEquals(sourceHash, destinationHash);
    }

    private static async Task<byte[]> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var algorithm = SHA256.Create();
        return await algorithm.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
    }
}
