using CopyPaste.Core.Models;

namespace CopyPaste.Core.Services;

public static class CopyDestinationResolver
{
    public static string Resolve(string sourcePath, string destinationRootPath, CopyRootMode mode)
    {
        var source = Path.GetFullPath(sourcePath.Trim());
        var destinationRoot = Path.GetFullPath(destinationRootPath.Trim());
        if (mode == CopyRootMode.ContentsOnly)
            return destinationRoot;

        var folderName = Path.GetFileName(Path.TrimEndingDirectorySeparator(source));
        if (string.IsNullOrWhiteSpace(folderName))
            folderName = new DirectoryInfo(source).Name;
        if (string.IsNullOrWhiteSpace(folderName))
            throw new ArgumentException("Seçilen kaynak klasörün adı belirlenemedi.", nameof(sourcePath));
        return Path.Combine(destinationRoot, folderName);
    }
}
