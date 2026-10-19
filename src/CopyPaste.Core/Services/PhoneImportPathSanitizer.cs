using CopyPaste.Core.Models;

namespace CopyPaste.Core.Services;

/// <summary>
/// Turns device file and folder names into safe NTFS names. Phones allow characters, trailing dots, reserved
/// device names, and case-only differences that Windows does not; every child of a folder still gets its own
/// destination name, and the same device listing always maps to the same names so re-runs skip finished files.
/// </summary>
public static class PhoneImportPathSanitizer
{
    public const int MaxComponentLength = 255;
    private const int MaxPreservedExtensionLength = 16;

    private static readonly HashSet<char> InvalidCharacters = [.. Path.GetInvalidFileNameChars()];

    private static readonly HashSet<string> ReservedNames = new(
        new[] { "CON", "PRN", "AUX", "NUL" }
            .Concat(Enumerable.Range(0, 10).Select(index => "COM" + index))
            .Concat(Enumerable.Range(0, 10).Select(index => "LPT" + index))
            .Concat(new[] { "COM¹", "COM²", "COM³", "LPT¹", "LPT²", "LPT³" }),
        StringComparer.OrdinalIgnoreCase);

    public static string SanitizeName(string? name)
    {
        var characters = (name ?? string.Empty)
            .Select(character => InvalidCharacters.Contains(character) ? '_' : character)
            .ToArray();
        // NTFS silently drops trailing dots and spaces, so "IMG." and "IMG" would collide.
        var value = new string(characters).TrimEnd(' ', '.');
        if (value.Length == 0)
            value = "_";
        var stem = value.Split('.')[0].TrimEnd(' ');
        if (ReservedNames.Contains(stem))
            value = "_" + value;
        return Truncate(value, MaxComponentLength);
    }

    /// <summary>Assigns unique, case-insensitively distinct destination names to one folder's children.</summary>
    public static IReadOnlyList<(PortableDeviceEntry Entry, string Name, bool Renamed)> AssignNames(
        IEnumerable<PortableDeviceEntry> children)
    {
        var ordered = children.OrderBy(child => child.Name, StringComparer.Ordinal).ToArray();
        var sanitized = ordered.Select(child => SanitizeName(child.Name)).ToArray();
        var everyCandidate = new HashSet<string>(sanitized, StringComparer.OrdinalIgnoreCase);
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new string?[ordered.Length];

        // Names that need no change keep them first, so a later rename never takes a real file's name.
        for (var index = 0; index < ordered.Length; index++)
        {
            if (string.Equals(sanitized[index], ordered[index].Name, StringComparison.Ordinal)
                && used.Add(sanitized[index]))
                names[index] = sanitized[index];
        }
        for (var index = 0; index < ordered.Length; index++)
        {
            if (names[index] is not null)
                continue;
            var candidate = sanitized[index];
            for (var suffix = 2; used.Contains(candidate)
                                 || (suffix > 2 && everyCandidate.Contains(candidate)); suffix++)
                candidate = WithSuffix(sanitized[index], suffix);
            used.Add(candidate);
            names[index] = candidate;
        }

        return ordered
            .Select((entry, index) => (entry, names[index]!,
                !string.Equals(names[index], entry.Name, StringComparison.Ordinal)))
            .ToArray();
    }

    internal static string Truncate(string name, int maxLength)
    {
        if (name.Length <= maxLength)
            return name;
        var extension = Path.GetExtension(name);
        if (extension.Length > MaxPreservedExtensionLength || extension.Length >= maxLength)
            extension = string.Empty;
        var stem = name[..(maxLength - extension.Length)];
        if (char.IsHighSurrogate(stem[^1]))
            stem = stem[..^1];
        stem = stem.TrimEnd(' ', '.');
        return (stem.Length == 0 ? "_" : stem) + extension;
    }

    private static string WithSuffix(string name, int suffix)
    {
        var extension = Path.GetExtension(name);
        var stem = extension.Length > 0 && extension.Length < name.Length ? name[..^extension.Length] : name;
        if (stem.Length == name.Length)
            extension = string.Empty;
        var tail = $" ({suffix})" + extension;
        return Truncate(stem, MaxComponentLength - tail.Length) + tail;
    }
}
