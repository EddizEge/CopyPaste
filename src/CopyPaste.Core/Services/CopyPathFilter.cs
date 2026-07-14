using System.IO.Enumeration;
using CopyPaste.Core.Models;

namespace CopyPaste.Core.Services;

public static class CopyPathFilter
{
    public static bool ShouldIncludeFile(string relativePath, CopyJobOptions options) =>
        !ContainsExcludedSegment(Path.GetDirectoryName(relativePath), options)
        && options.FilePatterns.Any(pattern =>
            FileSystemName.MatchesSimpleExpression(pattern, Path.GetFileName(relativePath), ignoreCase: true));

    public static bool IsExcludedDirectory(string relativePath, CopyJobOptions options) =>
        ContainsExcludedSegment(relativePath, options);

    private static bool ContainsExcludedSegment(string? path, CopyJobOptions options)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        var segments = path.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        return segments.Any(segment =>
            options.ExcludedDirectories.Contains(segment, StringComparer.OrdinalIgnoreCase));
    }
}
