using System.Text.Json;
using CopyPaste.Core.Models;

namespace CopyPaste.Core.Services;

public sealed record QueueLoadResult(
    IReadOnlyList<CopyJob> Jobs,
    bool UsedBackup = false,
    bool MigratedLegacyFormat = false);

public sealed class QueueStateStore
{
    private const int CurrentSchemaVersion = 2;
    private readonly string _filePath;
    private readonly string _backupPath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    public QueueStateStore(string? rootDirectory = null)
    {
        var root = rootDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CopyPaste");
        Directory.CreateDirectory(root);
        _filePath = Path.Combine(root, "queue.json");
        _backupPath = _filePath + ".bak";
    }

    public async Task SaveAsync(IEnumerable<CopyJob> jobs)
    {
        var recoverable = jobs
            .Where(job => job.Status is not (CopyJobStatus.Completed or CopyJobStatus.CompletedWithWarnings))
            .ToList();
        var savedAt = DateTimeOffset.UtcNow;
        foreach (var job in recoverable)
            job.LastCheckpointAt = savedAt;

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var temporaryPath = _filePath + ".tmp";
            if (recoverable.Count == 0)
            {
                DeleteIfExists(temporaryPath);
                DeleteIfExists(_filePath);
                DeleteIfExists(_backupPath);
                return;
            }

            var checkpoint = new QueueCheckpoint(CurrentSchemaVersion, savedAt, recoverable);
            await using (var stream = new FileStream(
                             temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, checkpoint, _options).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(_filePath))
                File.Replace(temporaryPath, _filePath, _backupPath, ignoreMetadataErrors: true);
            else
                File.Move(temporaryPath, _filePath);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<CopyJob>> LoadAsync() =>
        (await LoadWithMetadataAsync().ConfigureAwait(false)).Jobs;

    public async Task<QueueLoadResult> LoadWithMetadataAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var primary = await TryLoadAsync(_filePath).ConfigureAwait(false);
            if (primary is not null)
                return Normalize(primary.Value.Jobs, usedBackup: false, primary.Value.Legacy);

            var backup = await TryLoadAsync(_backupPath).ConfigureAwait(false);
            if (backup is null)
                return new QueueLoadResult([]);
            try
            {
                File.Copy(_backupPath, _filePath, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
            return Normalize(backup.Value.Jobs, usedBackup: true, backup.Value.Legacy);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<(IReadOnlyList<CopyJob> Jobs, bool Legacy)?> TryLoadAsync(string path)
    {
        if (!File.Exists(path))
            return null;
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var document = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                var legacyJobs = document.RootElement.Deserialize<List<CopyJob>>(_options) ?? [];
                return (legacyJobs, true);
            }

            var checkpoint = document.RootElement.Deserialize<QueueCheckpoint>(_options);
            if (checkpoint is null
                || checkpoint.SchemaVersion is < 1 or > CurrentSchemaVersion
                || checkpoint.Jobs is null)
                return null;
            return (checkpoint.Jobs, false);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static QueueLoadResult Normalize(
        IReadOnlyList<CopyJob> jobs,
        bool usedBackup,
        bool migratedLegacyFormat)
    {
        foreach (var job in jobs.Where(job =>
                     job.Status is CopyJobStatus.Running or CopyJobStatus.WaitingForNetwork))
        {
            var wasWaitingForNetwork = job.Status == CopyJobStatus.WaitingForNetwork;
            job.Status = CopyJobStatus.Paused;
            job.RecoveryReason = wasWaitingForNetwork
                ? QueueRecoveryReason.NetworkUnavailable
                : QueueRecoveryReason.UnexpectedShutdown;
            job.Summary = wasWaitingForNetwork
                ? "Ağ bekleme durumundaki transfer yeniden başlatılmak üzere kurtarıldı."
                : "CopyPaste beklenmedik şekilde kapandı; yeniden başlatılabilir transfer kurtarıldı.";
        }
        return new QueueLoadResult(jobs, usedBackup, migratedLegacyFormat);
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private sealed record QueueCheckpoint(
        int SchemaVersion,
        DateTimeOffset SavedAt,
        IReadOnlyList<CopyJob> Jobs);
}
