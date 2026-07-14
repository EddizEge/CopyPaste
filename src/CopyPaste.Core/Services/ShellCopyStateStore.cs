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

    public void SaveSource(string sourcePath)
    {
        var state = new ShellCopyState(Path.GetFullPath(sourcePath), DateTimeOffset.Now);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(state));
    }

    public string? LoadSource()
    {
        if (!File.Exists(_filePath))
            return null;

        try
        {
            var state = JsonSerializer.Deserialize<ShellCopyState>(File.ReadAllText(_filePath));
            return state is not null && Directory.Exists(state.SourcePath) ? state.SourcePath : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private sealed record ShellCopyState(string SourcePath, DateTimeOffset SavedAt);
}
