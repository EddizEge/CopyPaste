namespace CopyPaste.Core.Models;

public enum DeletionMode
{
    RecycleBin,
    Permanent,
    SecureErase
}

public enum DeletionEntryKind
{
    File,
    Directory,
    ReparsePoint
}

public enum DeletionIssueCode
{
    InvalidPath,
    Missing,
    ProtectedPath,
    VolumeRoot,
    AccessDenied,
    InUse,
    ReparsePoint,
    UnsupportedStorage,
    MultipleHardLinks,
    EnumerationFailed,
    ChangedAfterPreview,
    OperationFailed
}

public sealed record DeletionIssue(string Path, DeletionIssueCode Code, string Message);

public sealed record DeletionEntry(
    string Path,
    DeletionEntryKind Kind,
    long Length,
    FileAttributes Attributes,
    DateTimeOffset LastWriteTimeUtc);

public sealed record DeletionPlan(
    DeletionMode Mode,
    IReadOnlyList<string> RequestedPaths,
    IReadOnlyList<DeletionEntry> Entries,
    IReadOnlyList<DeletionIssue> Issues,
    long TotalBytes,
    int FileCount,
    int DirectoryCount,
    IReadOnlyList<string> Warnings)
{
    public bool IsValid => RequestedPaths.Count > 0 && Entries.Count > 0 && Issues.Count == 0;
}

public sealed record DeletionProgress(
    string Path,
    int CompletedItems,
    int TotalItems,
    long ProcessedBytes,
    long TotalBytes,
    int Pass,
    int TotalPasses);

public sealed record DeletionItemResult(
    string Path,
    bool Succeeded,
    bool PartiallyOverwritten,
    DeletionIssueCode? ErrorCode = null,
    string? ErrorMessage = null);

public sealed record DeletionResult(
    DeletionMode Mode,
    IReadOnlyList<DeletionItemResult> Items,
    bool WasCanceled)
{
    public int SucceededCount => Items.Count(item => item.Succeeded);
    public int FailedCount => Items.Count(item => !item.Succeeded);
    public bool Succeeded => !WasCanceled && FailedCount == 0;
}

public sealed record DeletionHistoryEntry(
    Guid Id,
    DateTimeOffset CompletedAt,
    DeletionMode Mode,
    int RequestedTargetCount,
    int SucceededCount,
    int FailedCount,
    long PlannedBytes,
    bool WasCanceled);
