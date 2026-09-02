using System.Security.Cryptography;
using CopyPaste.Core.Models;
using Microsoft.VisualBasic.FileIO;

namespace CopyPaste.Core.Services;

public sealed class DeletionService
{
    private const int SecureErasePasses = 3;
    private const int BufferSize = 1024 * 1024;
    private readonly DeletionPlanner _planner;

    public DeletionService(DeletionPlanner? planner = null) =>
        _planner = planner ?? new DeletionPlanner();

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
        var results = new List<DeletionItemResult>();
        var completed = 0;
        long processedBytes = 0;

        if (plan.Mode == DeletionMode.RecycleBin)
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
            foreach (var path in plan.RequestedPaths)
            {
                if (cancellationToken.IsCancellationRequested)
                    return new(plan.Mode, results, true);
                try
                {
                    DeleteToRecycleBin(path);
                    results.Add(new(path, true, false));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                    or OperationCanceledException or InvalidOperationException)
                {
                    results.Add(Failure(path, ex, false));
                }
                completed++;
                progress?.Report(new(path, completed, plan.RequestedPaths.Count,
                    processedBytes, plan.TotalBytes, 0, 0));
            }
            return new(plan.Mode, results, false);
        }

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
                    Overwrite(entry, progress, completed, plan.Entries.Count,
                        processedBytes, plan.TotalBytes, ref partiallyOverwritten, cancellationToken);
                    processedBytes += entry.Length;
                    cancellationToken.ThrowIfCancellationRequested();
                }

                DeleteEntry(entry);
                if (entry.Kind == DeletionEntryKind.File && plan.Mode != DeletionMode.SecureErase)
                    processedBytes += entry.Length;
                results.Add(new(entry.Path, true, false));
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

    private static void DeleteToRecycleBin(string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.Directory) != 0)
            FileSystem.DeleteDirectory(path, UIOption.OnlyErrorDialogs,
                RecycleOption.SendToRecycleBin, UICancelOption.ThrowException);
        else
            FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs,
                RecycleOption.SendToRecycleBin, UICancelOption.ThrowException);
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
}
