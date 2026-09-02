using System.Text.Json;
using CopyPaste.Core.Models;

namespace CopyPaste.Core.Services;

public sealed class DeletionHistoryStore
{
    private const int MaximumEntries = 50;
    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    public DeletionHistoryStore(string? rootDirectory = null)
    {
        var root = rootDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CopyPaste");
        Directory.CreateDirectory(root);
        _filePath = Path.Combine(root, "deletion-history.json");
    }

    public async Task<IReadOnlyList<DeletionHistoryEntry>> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return await LoadUnsafeAsync(cancellationToken).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }

    public async Task AddAsync(
        DeletionHistoryEntry entry,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var entries = (await LoadUnsafeAsync(cancellationToken).ConfigureAwait(false)).ToList();
            entries.RemoveAll(existing => existing.Id == entry.Id);
            entries.Insert(0, entry);
            if (entries.Count > MaximumEntries)
                entries.RemoveRange(MaximumEntries, entries.Count - MaximumEntries);
            var temporaryPath = _filePath + ".tmp";
            await using (var stream = new FileStream(
                temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
                await JsonSerializer.SerializeAsync(stream, entries, _options, cancellationToken)
                    .ConfigureAwait(false);
            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        finally { _gate.Release(); }
    }

    private async Task<IReadOnlyList<DeletionHistoryEntry>> LoadUnsafeAsync(
        CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
            return [];
        try
        {
            await using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return await JsonSerializer.DeserializeAsync<List<DeletionHistoryEntry>>(
                stream, _options, cancellationToken).ConfigureAwait(false) ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return [];
        }
    }
}
