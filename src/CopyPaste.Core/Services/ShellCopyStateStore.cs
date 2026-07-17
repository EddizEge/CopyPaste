using System.Text.Json;

namespace CopyPaste.Core.Services;

public sealed class ShellCopyStateStore
{
    private readonly string _filePath;

    public ShellCopyStateStore(string? rootDirectory = null)
    {
        var root = rootDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CopyPaste");
        Directory.CreateDirectory(root);
        _filePath = Path.Combine(root, "shell-copy.json");
    }

    public void SaveSource(string sourcePath) => SaveSources([sourcePath]);

    public void SaveSources(IEnumerable<string> sourcePaths)
    {
        var paths = sourcePaths.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var state = new ShellCopyState(paths, DateTimeOffset.Now);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(state));
    }

    public string? LoadSource() => LoadSources().FirstOrDefault();

    public IReadOnlyList<string> LoadSources()
    {
        if (!File.Exists(_filePath))
            return [];

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(_filePath));
            var root = document.RootElement;
            IEnumerable<string?> paths = root.TryGetProperty("SourcePaths", out var sourcePaths)
                && sourcePaths.ValueKind == JsonValueKind.Array
                ? sourcePaths.EnumerateArray().Select(item => item.GetString())
                : root.TryGetProperty("SourcePath", out var legacyPath)
                    ? [legacyPath.GetString()]
                    : [];
            return paths.Where(path => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                .Select(path => Path.GetFullPath(path!))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return [];
        }
    }

    private sealed record ShellCopyState(IReadOnlyList<string> SourcePaths, DateTimeOffset SavedAt);
}
