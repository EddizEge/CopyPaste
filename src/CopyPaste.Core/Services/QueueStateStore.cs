using System.Text.Json;
using CopyPaste.Core.Models;

namespace CopyPaste.Core.Services;

public sealed class QueueStateStore
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    public QueueStateStore(string? rootDirectory = null)
    {
        var root = rootDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CopyPaste");
        Directory.CreateDirectory(root);
        _filePath = Path.Combine(root, "queue.json");
    }

    public async Task SaveAsync(IEnumerable<CopyJob> jobs)
    {
        var recoverable = jobs
            .Where(job => job.Status is not (CopyJobStatus.Completed or CopyJobStatus.CompletedWithWarnings))
            .ToList();
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (recoverable.Count == 0)
            {
                if (File.Exists(_filePath))
                    File.Delete(_filePath);
                return;
            }
            var temporaryPath = _filePath + ".tmp";
            await using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
                await JsonSerializer.SerializeAsync(stream, recoverable, _options).ConfigureAwait(false);
            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        finally { _gate.Release(); }
    }

    public async Task<IReadOnlyList<CopyJob>> LoadAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!File.Exists(_filePath))
                return [];
            await using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var jobs = await JsonSerializer.DeserializeAsync<List<CopyJob>>(stream, _options).ConfigureAwait(false) ?? [];
            foreach (var job in jobs.Where(job => job.Status == CopyJobStatus.Running))
            {
                job.Status = CopyJobStatus.Paused;
                job.Summary = "CopyPaste beklenmedik şekilde kapandı; yeniden başlatılabilir transfer kurtarıldı.";
            }
            return jobs;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return [];
        }
        finally { _gate.Release(); }
    }
}
