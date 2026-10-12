namespace CopyPaste.Core.Services;

public sealed record DeletionSelectionSnapshot(IReadOnlyList<string> Paths, long Version);

/// <summary>
/// Holds the user's deletion targets and a version that changes on every mutation, so a preview
/// created from an earlier snapshot can be recognized as stale before it is shown or executed.
/// </summary>
public sealed class DeletionSelectionState
{
    private readonly List<string> _paths = [];

    public IReadOnlyList<string> Paths => _paths;
    public int Count => _paths.Count;
    public long Version { get; private set; }

    public bool TryAdd(string? path, out string candidate)
    {
        candidate = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
            return false;
        candidate = path.Trim().Trim('"');
        if (_paths.Contains(candidate, StringComparer.OrdinalIgnoreCase))
            return false;
        _paths.Add(candidate);
        Version++;
        return true;
    }

    public bool Remove(string path)
    {
        if (!_paths.Remove(path))
            return false;
        Version++;
        return true;
    }

    public void Clear()
    {
        if (_paths.Count == 0)
            return;
        _paths.Clear();
        Version++;
    }

    public DeletionSelectionSnapshot Snapshot() => new(_paths.ToArray(), Version);

    public bool IsCurrent(long version) => version == Version;
}
