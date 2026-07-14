using System.Text.Json;
using CopyPaste.Core.Models;

namespace CopyPaste.Core.Services;

public sealed class HistoryStore
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };
    private readonly SemaphoreSlim _gate = new(1, 1);

    public HistoryStore(string? rootDirectory = null)
    {
        var root = rootDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CopyPaste");
        Directory.CreateDirectory(root);
        _filePath = Path.Combine(root, "history.json");
    }

    public async Task<IReadOnlyList<CopyJob>> LoadAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try { return await LoadUnsafeAsync().ConfigureAwait(false); }
        finally { _gate.Release(); }
    }

    public async Task AddAsync(CopyJob job)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var jobs = (await LoadUnsafeAsync().ConfigureAwait(false)).ToList();
            jobs.RemoveAll(existing => existing.Id == job.Id);
            jobs.Insert(0, job);
            if (jobs.Count > 100)
                jobs.RemoveRange(100, jobs.Count - 100);
            await WriteUnsafeAsync(jobs).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public async Task ClearAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (File.Exists(_filePath))
                File.Delete(_filePath);
        }
        finally { _gate.Release(); }
    }

    private async Task<IReadOnlyList<CopyJob>> LoadUnsafeAsync()
    {
        if (!File.Exists(_filePath))
            return [];
        try
        {
            await using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return await JsonSerializer.DeserializeAsync<List<CopyJob>>(stream, _options).ConfigureAwait(false) ?? [];
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return [];
        }
    }

    private async Task WriteUnsafeAsync(IReadOnlyList<CopyJob> jobs)
    {
        var temporaryPath = _filePath + ".tmp";
        await using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            await JsonSerializer.SerializeAsync(stream, jobs, _options).ConfigureAwait(false);
        File.Move(temporaryPath, _filePath, overwrite: true);
    }
}
