using CopyPaste.Core.Models;

namespace CopyPaste.Core.Services;

public sealed record CopyPreflightResult(
    int FileCount,
    int DirectoryCount,
    long TotalBytes,
    int ExistingFileCount,
    long? AvailableBytes,
    bool HasEnoughSpace,
    IReadOnlyList<string> Warnings);

public sealed class CopyPreflightAnalyzer
{
    public Task<CopyPreflightResult> AnalyzeAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken = default) =>
        AnalyzeAsync(sourcePath, destinationPath, new CopyJobOptions(), cancellationToken);

    public Task<CopyPreflightResult> AnalyzeAsync(
        string sourcePath,
        string destinationPath,
        CopyJobOptions options,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Analyze(sourcePath, destinationPath, options, cancellationToken), cancellationToken);

    private static CopyPreflightResult Analyze(
        string sourcePath,
        string destinationPath,
        CopyJobOptions jobOptions,
        CancellationToken cancellationToken)
    {
        var fileCount = 0;
        var directoryCount = 0;
        var existingFileCount = 0;
        long totalBytes = 0;
        var warnings = new List<string>();

        var enumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = false,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        try
        {
            foreach (var directory in Directory.EnumerateDirectories(sourcePath, "*", enumerationOptions))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relativeDirectory = Path.GetRelativePath(sourcePath, directory);
                if (CopyPathFilter.IsExcludedDirectory(relativeDirectory, jobOptions))
                    continue;
                directoryCount++;
            }

            foreach (var file in Directory.EnumerateFiles(sourcePath, "*", enumerationOptions))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relativePath = Path.GetRelativePath(sourcePath, file);
                if (!CopyPathFilter.ShouldIncludeFile(relativePath, jobOptions))
                    continue;
                fileCount++;
                var info = new FileInfo(file);
                totalBytes += info.Length;

                if (File.Exists(Path.Combine(destinationPath, relativePath)))
                    existingFileCount++;
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            warnings.Add("Bazı klasörler için erişim izni yok: " + ex.Message);
        }
        catch (IOException ex)
        {
            warnings.Add("Dosya taraması tamamlanamadı: " + ex.Message);
        }

        var root = Path.GetPathRoot(Path.GetFullPath(destinationPath));
        long? availableBytes = string.IsNullOrEmpty(root) ? null : new DriveInfo(root).AvailableFreeSpace;
        var requiredBytes = Math.Max(0, totalBytes - GetReusableBytes(
            sourcePath, destinationPath, jobOptions, enumerationOptions, cancellationToken));
        var hasEnoughSpace = availableBytes is null || availableBytes.Value >= requiredBytes;

        if (!hasEnoughSpace)
            warnings.Add("Hedef sürücüde yeterli boş alan yok.");
        if (existingFileCount > 0)
        {
            var behavior = jobOptions.ExistingFiles switch
            {
                ExistingFileBehavior.Skip => "bu dosyalar korunarak atlanacak",
                ExistingFileBehavior.Overwrite => "bu dosyaların üzerine yeniden yazılacak",
                _ => "Robocopy yalnızca gerekli olanları güncelleyecek"
            };
            warnings.Add($"Hedefte {existingFileCount} dosya zaten bulunuyor; {behavior}.");
        }

        return new CopyPreflightResult(
            fileCount,
            directoryCount,
            totalBytes,
            existingFileCount,
            availableBytes,
            hasEnoughSpace,
            warnings);
    }

    private static long GetReusableBytes(
        string sourcePath,
        string destinationPath,
        CopyJobOptions jobOptions,
        EnumerationOptions enumerationOptions,
        CancellationToken cancellationToken)
    {
        long reusableBytes = 0;
        try
        {
            foreach (var sourceFile in Directory.EnumerateFiles(sourcePath, "*", enumerationOptions))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relativePath = Path.GetRelativePath(sourcePath, sourceFile);
                if (!CopyPathFilter.ShouldIncludeFile(relativePath, jobOptions))
                    continue;
                var destinationFile = Path.Combine(destinationPath, relativePath);
                if (!File.Exists(destinationFile))
                    continue;

                var sourceInfo = new FileInfo(sourceFile);
                var destinationInfo = new FileInfo(destinationFile);
                if (sourceInfo.Length == destinationInfo.Length)
                    reusableBytes += sourceInfo.Length;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The main scan already reports access warnings. Conservative space math is safe here.
        }

        return reusableBytes;
    }
}
