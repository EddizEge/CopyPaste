using System.Text.Json;
using CopyPaste.Core.Models;

namespace CopyPaste.Core.Services;

public sealed class ScheduleStore
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    public ScheduleStore(string? rootDirectory = null)
    {
        var root = rootDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CopyPaste");
        Directory.CreateDirectory(root);
        _filePath = Path.Combine(root, "schedules.json");
    }

    public async Task<IReadOnlyList<ScheduledTransfer>> LoadAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!File.Exists(_filePath))
                return [];
            await using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return await JsonSerializer.DeserializeAsync<List<ScheduledTransfer>>(stream, _options).ConfigureAwait(false) ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return [];
        }
        finally { _gate.Release(); }
    }

    public async Task SaveAsync(ScheduledTransfer schedule)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var schedules = (await LoadUnsafeAsync().ConfigureAwait(false)).ToList();
            schedules.RemoveAll(item => item.Id == schedule.Id);
            schedules.Add(schedule);
            await WriteUnsafeAsync(schedules).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public async Task RemoveAsync(Guid id)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var schedules = (await LoadUnsafeAsync().ConfigureAwait(false)).Where(item => item.Id != id).ToList();
            await WriteUnsafeAsync(schedules).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public async Task<ScheduledTransfer?> FindAsync(Guid id) =>
        (await LoadAsync().ConfigureAwait(false)).FirstOrDefault(item => item.Id == id && item.Enabled);

    private async Task<IReadOnlyList<ScheduledTransfer>> LoadUnsafeAsync()
    {
        if (!File.Exists(_filePath))
            return [];
        await using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        try { return await JsonSerializer.DeserializeAsync<List<ScheduledTransfer>>(stream, _options).ConfigureAwait(false) ?? []; }
        catch (JsonException) { return []; }
    }

    private async Task WriteUnsafeAsync(IReadOnlyList<ScheduledTransfer> schedules)
    {
        var temporaryPath = _filePath + ".tmp";
        await using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            await JsonSerializer.SerializeAsync(stream, schedules, _options).ConfigureAwait(false);
        File.Move(temporaryPath, _filePath, overwrite: true);
    }
}
