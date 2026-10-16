using System.Runtime.InteropServices;
using System.Security.Cryptography;
using CopyPaste.Core.Models;

namespace CopyPaste.Core.Services;

public sealed class DeletionService
{
    private const int SecureErasePasses = 3;
    private const int BufferSize = 1024 * 1024;
    private readonly DeletionPlanner _planner;
    private readonly IRecycleBinSender _recycleBin;

    public DeletionService(DeletionPlanner? planner = null, IRecycleBinSender? recycleBin = null)
    {
        _planner = planner ?? new DeletionPlanner();
        _recycleBin = recycleBin ?? new ShellRecycleBinSender();
    }

    public Task<DeletionResult> ExecuteAsync(
        DeletionPlan plan,
        IProgress<DeletionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.IsValid)
            throw new InvalidOperationException("Only a valid deletion preview can be executed.");
        return Task.Run(() => Execute(plan, progress, cancellationToken), CancellationToken.None);
    }

    private DeletionResult Execute(
        DeletionPlan plan,
        IProgress<DeletionProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (plan.Mode == DeletionMode.RecycleBin)
            return ExecuteRecycleBin(plan, progress, cancellationToken);

        var results = new List<DeletionItemResult>();
        var completed = 0;
        long processedBytes = 0;
        foreach (var entry in plan.Entries)
        {
            if (cancellationToken.IsCancellationRequested)
                return new(plan.Mode, results, true);
            var partiallyOverwritten = false;
            try
            {
                if (!_planner.IsEntryUnchanged(entry, out var changedIssue))
                {
                    results.Add(new(entry.Path, false, false,
                        changedIssue?.Code ?? DeletionIssueCode.ChangedAfterPreview,
                        changedIssue?.Message));
                    completed++;
                    continue;
                }

                if (entry.Kind == DeletionEntryKind.File && plan.Mode == DeletionMode.SecureErase)
                {
                    var clearedReadOnly = ClearReadOnlyForOverwrite(entry);
                    try
                    {
                        Overwrite(entry, progress, completed, plan.Entries.Count,
                            processedBytes, plan.TotalBytes, ref partiallyOverwritten, cancellationToken);
                        processedBytes += entry.Length;
                        cancellationToken.ThrowIfCancellationRequested();
                        DeleteEntry(entry);
                    }
                    catch when (clearedReadOnly)
                    {
                        RestoreAttributes(entry);
                        throw;
                    }
                }
                else
                {
                    DeleteEntry(entry);
                    if (entry.Kind == DeletionEntryKind.File)
                        processedBytes += entry.Length;
                }
                results.Add(new(entry.Path, true, false));
            }
            catch (SecureEraseRefusedException ex)
            {
                results.Add(new(entry.Path, false, false, ex.Code, ex.Message));
            }
            catch (OperationCanceledException)
            {
                results.Add(new(entry.Path, false, partiallyOverwritten,
                    DeletionIssueCode.OperationFailed,
                    partiallyOverwritten
                        ? "Cancellation left this file partially or fully overwritten."
                        : "The operation was canceled before this item changed."));
                return new(plan.Mode, results, true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                results.Add(Failure(entry.Path, ex, partiallyOverwritten));
            }
            completed++;
            progress?.Report(new(entry.Path, completed, plan.Entries.Count,
                processedBytes, plan.TotalBytes, 0, 0));
        }
        return new(plan.Mode, results, false);
    }

    private DeletionResult ExecuteRecycleBin(
        DeletionPlan plan,
        IProgress<DeletionProgress>? progress,
        CancellationToken cancellationToken)
    {
        var refreshed = _planner.Create(plan.RequestedPaths, DeletionMode.RecycleBin, cancellationToken);
        if (!refreshed.IsValid || !PlansMatch(plan, refreshed))
        {
            var changedItems = refreshed.Issues.Count > 0
                ? refreshed.Issues.Select(issue => new DeletionItemResult(
                    issue.Path, false, false, issue.Code, issue.Message)).ToArray()
                : plan.RequestedPaths.Select(path => new DeletionItemResult(
                    path, false, false, DeletionIssueCode.ChangedAfterPreview,
                    "A target changed after the deletion preview.")).ToArray();
            return new(plan.Mode, changedItems, false);
        }

        var results = new List<DeletionItemResult>();
        var entriesByRoot = DeletionPlanner.GroupEntriesByRoot(plan.RequestedPaths, plan.Entries);
        long processedBytes = 0;
        for (var index = 0; index < plan.RequestedPaths.Count; index++)
        {
            var path = plan.RequestedPaths[index];
            if (cancellationToken.IsCancellationRequested)
                return new(plan.Mode, results, true);
            var rootEntries = entriesByRoot[path];
            DeletionItemResult item;
            // Re-read the Recycle Bin state just before this root so a setting changed meanwhile is honored.
            if (_planner.TryGetRecycleBinIssue(path, rootEntries, out var issue))
                item = new(path, false, false, issue.Code, issue.Message);
            else
            {
                RecycleBinSendResult sent;
                try
                {
                    sent = _recycleBin.Send(path, cancellationToken);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                    or COMException or InvalidOperationException)
                {
                    sent = new(RecycleBinSendStatus.Failed, ex.HResult, null, null, false);
                }

                // Canceled before the shell changed this root, as between roots.
                if (sent.Status == RecycleBinSendStatus.Canceled)
                    return new(plan.Mode, results, true);
                item = ToRecycleResult(path, sent);
                if (sent.Status == RecycleBinSendStatus.DeletedPermanently)
                {
                    // Never continue after an unconfirmed deletion; leave the remaining roots untouched.
                    results.Add(item);
                    results.AddRange(plan.RequestedPaths.Skip(index + 1).Select(remaining =>
                        new DeletionItemResult(remaining, false, false, DeletionIssueCode.RecycleNotPossible,
                            "Not processed: a previous item could not be confirmed in the Recycle Bin.")));
                    return new(plan.Mode, results, false);
                }
            }
            results.Add(item);
            processedBytes += rootEntries.Where(entry => entry.Kind == DeletionEntryKind.File)
                .Sum(entry => entry.Length);
            progress?.Report(new(path, index + 1, plan.RequestedPaths.Count,
                processedBytes, plan.TotalBytes, 0, 0));
        }
        return new(plan.Mode, results, false);
    }

    private static DeletionItemResult ToRecycleResult(string path, RecycleBinSendResult sent) => sent.Status switch
    {
        RecycleBinSendStatus.Recycled => new(path, true, false),
        RecycleBinSendStatus.NotRecyclable => new(path, false, false, DeletionIssueCode.RecycleNotPossible,
            sent.SomeContentRecycled
                ? "Windows could not move this item to the Recycle Bin; it was not deleted permanently, and part of its content is in the Recycle Bin."
                : "Windows could not move this item to the Recycle Bin; it was not deleted."),
        RecycleBinSendStatus.DeletedPermanently => new(path, false, false,
            DeletionIssueCode.DeletedWithoutRecycleBin,
            "The item, or part of it, was removed without a confirmed Recycle Bin copy; Windows may have deleted it permanently."),
        _ => RecycleFailure(path, sent.HResult)
    };

    private static DeletionItemResult RecycleFailure(string path, int hResult)
    {
        var code = GetRecycleFailureCode(hResult);
        return new(path, false, false, code, code == DeletionIssueCode.Missing
            ? "The target no longer exists."
            : $"Windows could not move this item to the Recycle Bin (0x{hResult:X8}); it is still in place.");
    }

    private static DeletionIssueCode GetRecycleFailureCode(int hResult) => unchecked((uint)hResult) switch
    {
        0x80070002 or 0x80070003 => DeletionIssueCode.Missing,
        0x80070005 or 0x80270002 or 0x80270021 => DeletionIssueCode.AccessDenied,
        0x80070020 or 0x80070021 or 0x80270027 => DeletionIssueCode.InUse,
        >= 0x80270080 and <= 0x8027008F => DeletionIssueCode.RecycleNotPossible,
        _ => DeletionIssueCode.OperationFailed
    };

    private static bool PlansMatch(DeletionPlan expected, DeletionPlan actual)
    {
        if (expected.Entries.Count != actual.Entries.Count)
            return false;
        var current = actual.Entries.ToDictionary(entry => entry.Path, StringComparer.OrdinalIgnoreCase);
        return expected.Entries.All(entry => current.TryGetValue(entry.Path, out var other)
            && entry.Kind == other.Kind
            && entry.Length == other.Length
            && entry.Attributes == other.Attributes
            && entry.LastWriteTimeUtc == other.LastWriteTimeUtc);
    }

    private static void Overwrite(
        DeletionEntry entry,
        IProgress<DeletionProgress>? progress,
        int completed,
        int totalItems,
        long processedBytes,
        long totalBytes,
        ref bool changed,
        CancellationToken cancellationToken)
    {
        using var stream = new FileStream(entry.Path, FileMode.Open, FileAccess.Write,
            FileShare.None, BufferSize, FileOptions.WriteThrough | FileOptions.SequentialScan);
        // Recheck on the exclusive handle: a hard link added after the preview must never be overwritten.
        EnsureSingleHardLink(entry.Path, DeletionPlanner.GetHardLinkCount(stream.SafeFileHandle));
        var buffer = new byte[BufferSize];
        for (var pass = 1; pass <= SecureErasePasses; pass++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            stream.Position = 0;
            long remaining = stream.Length;
            while (remaining > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var count = (int)Math.Min(buffer.Length, remaining);
                if (pass == 2)
                    Array.Clear(buffer, 0, count);
                else
                    RandomNumberGenerator.Fill(buffer.AsSpan(0, count));
                stream.Write(buffer, 0, count);
                changed = true;
                remaining -= count;
                progress?.Report(new(entry.Path, completed, totalItems,
                    processedBytes * SecureErasePasses
                    + (pass - 1L) * entry.Length
                    + (entry.Length - remaining),
                    totalBytes * SecureErasePasses, pass, SecureErasePasses));
            }
            stream.Flush(flushToDisk: true);
        }
        stream.SetLength(0);
        stream.Flush(flushToDisk: true);
    }

    private static void EnsureSingleHardLink(string path, uint? linkCount)
    {
        if (DeletionPlanner.TryGetSecureEraseLinkIssue(path, linkCount, out var issue))
            throw new SecureEraseRefusedException(issue.Code, issue.Message);
    }

    // Only the ReadOnly attribute is cleared for overwriting; ownership and ACLs are never changed.
    private static bool ClearReadOnlyForOverwrite(DeletionEntry entry)
    {
        if ((entry.Attributes & FileAttributes.ReadOnly) == 0)
            return false;
        File.SetAttributes(entry.Path, entry.Attributes & ~FileAttributes.ReadOnly);
        return true;
    }

    private static void RestoreAttributes(DeletionEntry entry)
    {
        try
        {
            if (File.Exists(entry.Path))
                File.SetAttributes(entry.Path, entry.Attributes);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void DeleteEntry(DeletionEntry entry)
    {
        if ((entry.Attributes & FileAttributes.ReadOnly) != 0
            && entry.Kind != DeletionEntryKind.ReparsePoint)
            File.SetAttributes(entry.Path, entry.Attributes & ~FileAttributes.ReadOnly);

        if (entry.Kind == DeletionEntryKind.Directory
            || (entry.Kind == DeletionEntryKind.ReparsePoint
                && (entry.Attributes & FileAttributes.Directory) != 0))
            Directory.Delete(entry.Path, recursive: false);
        else
            File.Delete(entry.Path);
    }

    private static DeletionItemResult Failure(string path, Exception exception, bool partiallyOverwritten)
    {
        var code = exception switch
        {
            UnauthorizedAccessException => DeletionIssueCode.AccessDenied,
            IOException when IsSharingViolation(exception.HResult) => DeletionIssueCode.InUse,
            _ => DeletionIssueCode.OperationFailed
        };
        return new(path, false, partiallyOverwritten, code, exception.Message);
    }

    private static bool IsSharingViolation(int hResult) =>
        (hResult & 0xFFFF) is 32 or 33;

    private sealed class SecureEraseRefusedException(DeletionIssueCode code, string message)
        : Exception(message)
    {
        public DeletionIssueCode Code { get; } = code;
    }
}
