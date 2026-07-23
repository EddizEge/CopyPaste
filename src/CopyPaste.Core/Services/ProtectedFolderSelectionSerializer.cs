using System.Text.Json;

namespace CopyPaste.Core.Services;

public static class ProtectedFolderSelectionSerializer
{
    public static string Serialize(IEnumerable<string> paths) => JsonSerializer.Serialize(
        Normalize(paths).Order(StringComparer.OrdinalIgnoreCase));

    public static IReadOnlyList<string> Parse(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return [];
        try
        {
            return Normalize(JsonSerializer.Deserialize<string[]>(content) ?? []);
        }
        catch (JsonException)
        {
            return Normalize([content]);
        }
    }

    private static IReadOnlyList<string> Normalize(IEnumerable<string> paths)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path.Trim()))
                    continue;
                var fullPath = Path.GetFullPath(path.Trim());
                if (seen.Add(fullPath))
                    result.Add(fullPath);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                // Invalid picker data is ignored instead of becoming a command argument.
            }
        }
        return result;
    }
}
