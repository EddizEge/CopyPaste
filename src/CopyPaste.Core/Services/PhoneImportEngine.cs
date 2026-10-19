using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using CopyPaste.Core.Models;

namespace CopyPaste.Core.Services;

public sealed class PhoneImportOptions
{
    public TimeSpan ProgressInterval { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// How long one device open or read may make no progress before the device counts as unresponsive. iPhones in
    /// the "Automatic" format convert videos before the first byte arrives, so this is deliberately generous.
    /// </summary>
    public TimeSpan StallTimeout { get; init; } = TimeSpan.FromMinutes(5);

    public TimeSpan ProbeTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public int BufferSize { get; init; } = 1024 * 1024;
    public int MaxReportedFailures { get; init; } = 20;

    // Test hook: runs after the temporary file is closed and before it is verified and renamed.
    internal Action<string, string>? AfterTemporaryFileWritten { get; init; }
}

/// <summary>
/// Copies a device folder into a local folder. The device is only listed and read. Destination files are never
/// overwritten: an existing name is skipped, each file is written under a temporary name, verified with SHA-256
/// against the bytes read from the device, and only then renamed without overwrite.
/// </summary>
public sealed class PhoneImportEngine(PhoneImportOptions? options = null)
{
    public const string TemporaryMarker = ".cptmp-";

    private readonly PhoneImportOptions _options = options ?? new PhoneImportOptions();

    public async Task<PhoneImportPlan> PlanAsync(
        IPortableDeviceSource source,
        PortableDeviceEntry sourceFolder,
        string sourceDisplayPath,
        string destinationRoot,
        IProgress<PhoneImportScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(sourceFolder);
        var destination = NormalizeDestination(destinationRoot);
        var items = new List<PhoneImportPlanItem>();
        var scanFailures = new List<PhoneImportFailure>();
        var pending = new Stack<(PortableDeviceEntry Folder, string RelativePath, bool Renamed)>();
        pending.Push((sourceFolder, string.Empty, false));
        var foldersScanned = 0;
        var lastReport = Stopwatch.StartNew();

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (folder, relativePath, folderRenamed) = pending.Pop();
            IReadOnlyList<PortableDeviceEntry> children;
            try
            {
                children = await WaitForDeviceAsync(source.GetChildrenAsync(folder, cancellationToken),
                    cancellationToken, onAbandon: null).ConfigureAwait(false);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                var kind = PhoneImportErrors.Classify(ex);
                if (relativePath.Length == 0 || PhoneImportErrors.StopsImport(kind))
                    throw new PhoneImportException(kind, ex);
                scanFailures.Add(new PhoneImportFailure(relativePath, kind, ex.HResult, ex.Message));
                continue;
            }

            foldersScanned++;
            var subfolders = new List<(PortableDeviceEntry, string, bool)>();
            foreach (var (entry, name, renamed) in PhoneImportPathSanitizer.AssignNames(children))
            {
                var childPath = relativePath.Length == 0 ? name : relativePath + Path.DirectorySeparatorChar + name;
                if (entry.IsFolder)
                    subfolders.Add((entry, childPath, folderRenamed || renamed));
                else
                    items.Add(new PhoneImportPlanItem(entry, childPath,
                        File.Exists(Path.Combine(destination, childPath)), folderRenamed || renamed));
            }
            for (var index = subfolders.Count - 1; index >= 0; index--)
                pending.Push(subfolders[index]);

            if (progress is not null && (pending.Count == 0 || lastReport.Elapsed >= _options.ProgressInterval))
            {
                progress.Report(new PhoneImportScanProgress(foldersScanned, items.Count, relativePath));
                lastReport.Restart();
            }
        }

        items.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.RelativePath, right.RelativePath));
        return new PhoneImportPlan(
            source,
            sourceFolder,
            sourceDisplayPath,
            destination,
            items,
            items.Sum(item => Math.Max(0, item.Entry.ReportedSize ?? 0)),
            items.Count(item => item.Entry.ReportedSize is null),
            items.Count(item => item.AlreadyExists),
            items.Where(item => !item.AlreadyExists).Sum(item => Math.Max(0, item.Entry.ReportedSize ?? 0)),
            items.Count(item => item.Renamed),
            DestinationSpace.GetFreeBytes(destination),
            scanFailures);
    }

    public async Task<PhoneImportResult> ImportAsync(
        PhoneImportPlan plan,
        IProgress<PhoneImportProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var elapsed = Stopwatch.StartNew();
        Directory.CreateDirectory(plan.DestinationRoot);
        var tracker = new ProgressTracker(plan, progress, _options.ProgressInterval);
        var failures = new List<PhoneImportFailure>();
        var sizeMismatches = new List<string>();
        var copied = 0;
        var skipped = 0;
        var failed = 0;
        var notProcessed = 0;
        var sizeMismatchCount = 0;
        var suggestKeepOriginals = false;
        long copiedBytes = 0;
        PhoneImportStatus? stopStatus = null;
        var buffer = new byte[Math.Max(4096, _options.BufferSize)];

        for (var index = 0; index < plan.Items.Count; index++)
        {
            var item = plan.Items[index];
            if (cancellationToken.IsCancellationRequested)
            {
                stopStatus = PhoneImportStatus.Cancelled;
                notProcessed = plan.Items.Count - index;
                break;
            }

            tracker.BeginItem(item);
            var outcome = await ImportItemAsync(plan, item, buffer, tracker, cancellationToken).ConfigureAwait(false);
            if (outcome.BufferAbandoned)
                buffer = new byte[buffer.Length];

            if (outcome.Kind == ItemOutcomeKind.Failed && outcome.Failure!.Kind == PhoneImportErrorKind.DeviceDisconnected
                && !cancellationToken.IsCancellationRequested
                && await IsDeviceReachableAsync(plan, cancellationToken).ConfigureAwait(false))
            {
                // The error looked like a disconnect, but the device still answers: only this item failed.
                outcome = outcome with { Failure = outcome.Failure with { Kind = PhoneImportErrorKind.Other } };
            }
            if (outcome.Kind == ItemOutcomeKind.Failed && cancellationToken.IsCancellationRequested)
                outcome = new ItemOutcome(ItemOutcomeKind.Cancelled, 0, null, outcome.BufferAbandoned);

            switch (outcome.Kind)
            {
                case ItemOutcomeKind.Copied:
                    copied++;
                    copiedBytes += outcome.Bytes;
                    if (item.Entry.ReportedSize is { } reported && reported != outcome.Bytes)
                    {
                        sizeMismatchCount++;
                        if (sizeMismatches.Count < _options.MaxReportedFailures)
                            sizeMismatches.Add(item.RelativePath);
                    }
                    break;
                case ItemOutcomeKind.SkippedExisting:
                    skipped++;
                    break;
                case ItemOutcomeKind.Cancelled:
                    stopStatus = PhoneImportStatus.Cancelled;
                    notProcessed = plan.Items.Count - index;
                    break;
                default:
                    failed++;
                    var failure = outcome.Failure!;
                    if (failures.Count < _options.MaxReportedFailures)
                        failures.Add(failure);
                    if (failure.Kind == PhoneImportErrorKind.DeviceNotFunctioning)
                        suggestKeepOriginals = true;
                    if (PhoneImportErrors.StopsImport(failure.Kind))
                    {
                        stopStatus = failure.Kind == PhoneImportErrorKind.DestinationFull
                            ? PhoneImportStatus.StoppedDestinationFull
                            : PhoneImportStatus.StoppedDeviceDisconnected;
                        notProcessed = plan.Items.Count - index - 1;
                    }
                    break;
            }
            tracker.EndItem(item, outcome.Bytes, copied, skipped, failed);
            if (stopStatus is not null)
                break;
        }

        tracker.Report(force: true);
        var status = stopStatus ?? (failed > 0 ? PhoneImportStatus.CompletedWithErrors : PhoneImportStatus.Completed);
        return new PhoneImportResult(
            status,
            plan.Source.BackendName,
            plan.DestinationRoot,
            plan.Items.Count,
            copied,
            skipped,
            failed,
            notProcessed,
            copiedBytes,
            failures,
            sizeMismatchCount,
            sizeMismatches,
            plan.RenamedCount,
            suggestKeepOriginals,
            elapsed.Elapsed);
    }

    /// <summary>Returns the full destination path, or throws when it is not a usable local or network folder path.</summary>
    public static string NormalizeDestination(string? destinationRoot)
    {
        if (TransferPathClassifier.Classify(destinationRoot) != TransferPathKind.FileSystem)
            throw new ArgumentException("The destination must be a full file-system folder path.", nameof(destinationRoot));
        var full = Path.GetFullPath(destinationRoot!.Trim().Trim('"').Trim());
        var root = Path.GetPathRoot(full);
        return string.Equals(full, root, StringComparison.OrdinalIgnoreCase)
            ? full
            : Path.TrimEndingDirectorySeparator(full);
    }

    internal static string TemporaryName(string finalName)
    {
        var random = Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
        var name = "." + finalName + TemporaryMarker + random;
        return name.Length <= PhoneImportPathSanitizer.MaxComponentLength ? name : TemporaryMarker + random;
    }

    private async Task<ItemOutcome> ImportItemAsync(
        PhoneImportPlan plan,
        PhoneImportPlanItem item,
        byte[] buffer,
        ProgressTracker tracker,
        CancellationToken cancellationToken)
    {
        string? temporaryPath = null;
        Stream? source = null;
        Task? pendingRead = null;
        var bufferAbandoned = false;
        long total = 0;
        try
        {
            var finalPath = Path.GetFullPath(Path.Combine(plan.DestinationRoot, item.RelativePath));
            var rootPrefix = Path.EndsInDirectorySeparator(plan.DestinationRoot)
                ? plan.DestinationRoot
                : plan.DestinationRoot + Path.DirectorySeparatorChar;
            if (!finalPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                return Failed(item, PhoneImportErrorKind.InvalidName, unchecked((int)0x8007007B),
                    "The device name resolves outside the destination folder.");
            if (File.Exists(finalPath))
                return new ItemOutcome(ItemOutcomeKind.SkippedExisting, 0, null, false);
            if (Directory.Exists(finalPath))
                return Failed(item, PhoneImportErrorKind.DestinationConflict, unchecked((int)0x800700B7),
                    "A folder with this name already exists.");

            source = await WaitForDeviceAsync(plan.Source.OpenReadAsync(item.Entry, cancellationToken),
                cancellationToken, onAbandon: null).ConfigureAwait(false);
            var directory = Path.GetDirectoryName(finalPath)!;
            Directory.CreateDirectory(directory);

            temporaryPath = Path.Combine(directory, TemporaryName(Path.GetFileName(finalPath)));
            byte[] expectedHash;
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                await using (var target = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                                 FileShare.None, bufferSize: 0, FileOptions.Asynchronous))
                {
                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var readTask = source.ReadAsync(buffer.AsMemory(), cancellationToken).AsTask();
                        var read = await WaitForDeviceAsync(readTask, cancellationToken, () =>
                        {
                            bufferAbandoned = true;
                            pendingRead = readTask;
                        }).ConfigureAwait(false);
                        if (read == 0)
                            break;
                        hash.AppendData(buffer, 0, read);
                        await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                        total += read;
                        tracker.ReportItemBytes(total);
                    }
                    await target.FlushAsync(cancellationToken).ConfigureAwait(false);
                    target.Flush(flushToDisk: true);
                }
                expectedHash = hash.GetHashAndReset();
            }

            _options.AfterTemporaryFileWritten?.Invoke(temporaryPath, finalPath);
            byte[] writtenHash;
            await using (var check = new FileStream(temporaryPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                             bufferSize: 0, FileOptions.Asynchronous | FileOptions.SequentialScan))
                writtenHash = await SHA256.HashDataAsync(check, cancellationToken).ConfigureAwait(false);
            if (!CryptographicOperations.FixedTimeEquals(expectedHash, writtenHash))
                throw new PhoneImportVerificationException("SHA-256 of the written file differs from the device data.");

            cancellationToken.ThrowIfCancellationRequested();
            ApplyDeviceTimestamps(temporaryPath, item.Entry);
            try
            {
                File.Move(temporaryPath, finalPath, overwrite: false);
                temporaryPath = null;
            }
            catch (IOException) when (File.Exists(finalPath) || Directory.Exists(finalPath))
            {
                // The name appeared while this file was copied; keep the existing file untouched.
                return new ItemOutcome(ItemOutcomeKind.SkippedExisting, 0, null, bufferAbandoned);
            }
            return new ItemOutcome(ItemOutcomeKind.Copied, total, null, bufferAbandoned);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ItemOutcome(ItemOutcomeKind.Cancelled, 0, null, bufferAbandoned);
        }
        catch (Exception ex)
        {
            var kind = PhoneImportErrors.Classify(ex);
            if (kind == PhoneImportErrorKind.Cancelled)
                kind = PhoneImportErrorKind.Other;
            return new ItemOutcome(ItemOutcomeKind.Failed, 0,
                new PhoneImportFailure(item.RelativePath, kind, ex.HResult, ex.Message), bufferAbandoned);
        }
        finally
        {
            if (source is not null)
            {
                if (pendingRead is { IsCompleted: false })
                {
                    var abandoned = source;
                    _ = pendingRead.ContinueWith(_ => abandoned.Dispose(), TaskScheduler.Default);
                }
                else
                    await source.DisposeAsync().ConfigureAwait(false);
            }
            if (temporaryPath is not null)
                TryDelete(temporaryPath);
        }
    }

    private async Task<T> WaitForDeviceAsync<T>(Task<T> task, CancellationToken cancellationToken, Action? onAbandon)
    {
        try
        {
            return await task.WaitAsync(_options.StallTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when ((ex is TimeoutException || ex is OperationCanceledException) && !task.IsCompleted)
        {
            // The device call is still running and may finish later: never reuse its buffer, and close
            // whatever it eventually returns.
            onAbandon?.Invoke();
            _ = task.ContinueWith(static completed =>
            {
                if (completed.Status == TaskStatus.RanToCompletion && completed.Result is IDisposable disposable)
                    disposable.Dispose();
                _ = completed.Exception;
            }, TaskScheduler.Default);
            if (ex is TimeoutException)
                throw new PhoneImportStallException(
                    $"The device did not respond within {_options.StallTimeout.TotalSeconds:N0} seconds.");
            throw;
        }
    }

    private async Task<bool> IsDeviceReachableAsync(PhoneImportPlan plan, CancellationToken cancellationToken)
    {
        using var probe = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        probe.CancelAfter(_options.ProbeTimeout);
        try
        {
            await plan.Source.GetChildrenAsync(plan.SourceFolder, probe.Token)
                .WaitAsync(_options.ProbeTimeout, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception)
        {
            // Unreachable, or the user cancelled; the caller checks the token before deciding to stop.
            return false;
        }
    }

    private static void ApplyDeviceTimestamps(string path, PortableDeviceEntry entry)
    {
        var modified = Plausible(entry.Modified);
        var created = Plausible(entry.Created) ?? modified;
        try
        {
            if (modified is { } lastWrite)
                File.SetLastWriteTimeUtc(path, lastWrite.UtcDateTime);
            if (created is { } creation)
                File.SetCreationTimeUtc(path, creation.UtcDateTime);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentOutOfRangeException)
        {
            // The content is already verified; a date the volume refuses is not a reason to drop the file.
        }
    }

    // Devices sometimes report 1970 or far-future dates for unknown values; those count as unknown.
    private static DateTimeOffset? Plausible(DateTimeOffset? value) =>
        value is { } date && date.Year >= 1980 && date <= DateTimeOffset.UtcNow.AddDays(2) ? date : null;

    private static ItemOutcome Failed(PhoneImportPlanItem item, PhoneImportErrorKind kind, int hresult, string message) =>
        new(ItemOutcomeKind.Failed, 0, new PhoneImportFailure(item.RelativePath, kind, hresult, message), false);

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private enum ItemOutcomeKind
    {
        Copied,
        SkippedExisting,
        Failed,
        Cancelled
    }

    private sealed record ItemOutcome(ItemOutcomeKind Kind, long Bytes, PhoneImportFailure? Failure, bool BufferAbandoned);

    private sealed class ProgressTracker(
        PhoneImportPlan plan,
        IProgress<PhoneImportProgress>? progress,
        TimeSpan interval)
    {
        private readonly Stopwatch _sinceReport = Stopwatch.StartNew();
        private bool _reportedOnce;
        private int _processed;
        private long _finishedBytes;
        private long _currentBytes;
        private long _copiedBytes;
        private long? _currentReported;
        private string? _currentItem;
        private int _copied;
        private int _skipped;
        private int _failed;

        public void BeginItem(PhoneImportPlanItem item)
        {
            _currentItem = item.RelativePath;
            _currentReported = item.Entry.ReportedSize;
            _currentBytes = 0;
            Report(force: false);
        }

        public void ReportItemBytes(long bytes)
        {
            _currentBytes = bytes;
            Report(force: false);
        }

        public void EndItem(PhoneImportPlanItem item, long copiedBytes, int copied, int skipped, int failed)
        {
            _processed++;
            _finishedBytes += Math.Max(0, item.Entry.ReportedSize ?? copiedBytes);
            _copiedBytes += copiedBytes;
            _currentBytes = 0;
            _copied = copied;
            _skipped = skipped;
            _failed = failed;
            Report(force: false);
        }

        public void Report(bool force)
        {
            if (progress is null || (!force && _reportedOnce && _sinceReport.Elapsed < interval))
                return;
            var current = _currentReported is { } reported ? Math.Min(_currentBytes, Math.Max(0, reported)) : _currentBytes;
            var processedBytes = _finishedBytes + current;
            if (plan.ReportedTotalBytes > 0)
                processedBytes = Math.Min(processedBytes, plan.ReportedTotalBytes);
            progress.Report(new PhoneImportProgress(
                _processed,
                plan.Items.Count,
                processedBytes,
                plan.ReportedTotalBytes,
                _copiedBytes + _currentBytes,
                force ? null : _currentItem,
                _copied,
                _skipped,
                _failed));
            _reportedOnce = true;
            _sinceReport.Restart();
        }
    }
}

public sealed class PhoneImportException : Exception
{
    public PhoneImportException(PhoneImportErrorKind kind, Exception inner)
        : base(inner.Message, inner)
    {
        Kind = kind;
        HResult = inner.HResult;
    }

    public PhoneImportErrorKind Kind { get; }
}

/// <summary>Free space for a destination that may not exist yet; walks up to the nearest existing folder.</summary>
public static class DestinationSpace
{
    public static long? GetFreeBytes(string destination)
    {
        try
        {
            var probe = Path.GetFullPath(destination);
            while (!Directory.Exists(probe))
            {
                var parent = Path.GetDirectoryName(probe);
                if (string.IsNullOrEmpty(parent) || parent == probe)
                    return null;
                probe = parent;
            }
            var directory = Path.EndsInDirectorySeparator(probe) ? probe : probe + Path.DirectorySeparatorChar;
            return GetDiskFreeSpaceEx(directory, out var available, out _, out _) ? (long)available : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException
            or NotSupportedException)
        {
            return null;
        }
    }

    [DllImport("kernel32.dll", EntryPoint = "GetDiskFreeSpaceExW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetDiskFreeSpaceEx(
        string directoryName,
        out ulong freeBytesAvailableToCaller,
        out ulong totalNumberOfBytes,
        out ulong totalNumberOfFreeBytes);
}
