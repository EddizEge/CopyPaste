using CopyPaste.Core.Models;

namespace CopyPaste.Core.Services;

public sealed record CopyJobOptionsParseResult(CopyJobOptions? Options, string? Error)
{
    public bool IsValid => Options is not null;
}

public static class CopyJobOptionsParser
{
    public static CopyJobOptionsParseResult Parse(
        ExistingFileBehavior existingFiles,
        VerificationMode verification,
        string? filePatterns,
        string? excludedDirectories)
    {
        var patterns = Split(filePatterns, "*");
        foreach (var pattern in patterns)
        {
            if (!IsSafeFilePattern(pattern))
                return new(null, $"Geçersiz dosya filtresi: {pattern}");
        }

        var exclusions = Split(excludedDirectories, null);
        foreach (var exclusion in exclusions)
        {
            if (!IsSafeDirectoryName(exclusion))
                return new(null, $"Geçersiz hariç tutulan klasör adı: {exclusion}");
        }

        return new(new CopyJobOptions
        {
            ExistingFiles = existingFiles,
            Verification = verification,
            FilePatterns = patterns,
            ExcludedDirectories = exclusions
        }, null);
    }

    private static IReadOnlyList<string> Split(string? value, string? defaultValue)
    {
        var values = (value ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return values.Length == 0 && defaultValue is not null ? [defaultValue] : values;
    }

    private static bool IsSafeFilePattern(string value) =>
        value.Length is > 0 and <= 120
        && !value.StartsWith('/')
        && !value.StartsWith('-')
        && !Path.IsPathRooted(value)
        && value.IndexOfAny(['\\', '/', ':', '"', '<', '>', '|']) < 0
        && value.All(character => !char.IsControl(character));

    private static bool IsSafeDirectoryName(string value) =>
        value.Length is > 0 and <= 120
        && value is not "." and not ".."
        && !value.StartsWith('/')
        && !value.StartsWith('-')
        && !Path.IsPathRooted(value)
        && value.IndexOfAny(['\\', '/', ':', '"', '<', '>', '|', '*', '?']) < 0
        && value.All(character => !char.IsControl(character));
}
