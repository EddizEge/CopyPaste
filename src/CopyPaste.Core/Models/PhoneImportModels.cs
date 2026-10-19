using CopyPaste.Core.Services;

namespace CopyPaste.Core.Models;

/// <summary>
/// A file or folder on a phone or other portable device. <see cref="Handle"/> is opaque and belongs to the
/// backend that produced the entry; only that backend may use it.
/// </summary>
public sealed record PortableDeviceEntry(
    string Name,
    bool IsFolder,
    long? ReportedSize,
    DateTimeOffset? Modified,
    DateTimeOffset? Created,
    object Handle);

/// <summary>A device as seen by one access backend. <see cref="Root"/> is null when the backend could not open it.</summary>
public sealed record PortableDeviceInfo(
    string Name,
    string Id,
    IPortableDeviceSource Source,
    PortableDeviceEntry? Root,
    int? ErrorHResult = null,
    string? ErrorMessage = null)
{
    public string BackendName => Source.BackendName;
}

public sealed record PortableDeviceBackendReport(
    string BackendName,
    IReadOnlyList<PortableDeviceInfo> Devices,
    int? ErrorHResult,
    string? ErrorMessage,
    TimeSpan Elapsed);

public sealed record PortableDeviceDiscovery(
    IReadOnlyList<PortableDeviceInfo> Devices,
    IReadOnlyList<PortableDeviceBackendReport> Reports);

public enum PortableDeviceOpenOutcome
{
    DcimFound,
    StorageWithoutDcim,
    StorageEmpty,
    Failed
}

/// <summary>
/// The device folder chosen after opening a device. <see cref="Path"/> runs from the device root to the default
/// folder (DCIM when found). <see cref="Attempts"/> records what each backend saw, for diagnostics.
/// </summary>
public sealed record PortableDeviceOpenResult(
    PortableDeviceInfo? Device,
    IReadOnlyList<PortableDeviceEntry> Path,
    PortableDeviceOpenOutcome Outcome,
    IReadOnlyList<string> Attempts,
    PhoneImportFailure? Error);

public enum PhoneImportErrorKind
{
    Other,
    DeviceNotFunctioning,
    DeviceDisconnected,
    AccessDenied,
    DestinationFull,
    PathTooLong,
    InvalidName,
    DestinationConflict,
    VerificationFailed,
    Cancelled
}

public sealed record PhoneImportFailure(
    string RelativePath,
    PhoneImportErrorKind Kind,
    int HResult,
    string Message);

public sealed record PhoneImportPlanItem(
    PortableDeviceEntry Entry,
    string RelativePath,
    bool AlreadyExists,
    bool Renamed);

public sealed record PhoneImportPlan(
    IPortableDeviceSource Source,
    PortableDeviceEntry SourceFolder,
    string SourceDisplayPath,
    string DestinationRoot,
    IReadOnlyList<PhoneImportPlanItem> Items,
    long ReportedTotalBytes,
    int UnknownSizeCount,
    int ExistingCount,
    long ReportedBytesToCopy,
    int RenamedCount,
    long? FreeBytes,
    IReadOnlyList<PhoneImportFailure> ScanFailures)
{
    public int FileCount => Items.Count;
    public int FilesToCopy => Items.Count - ExistingCount;
    public bool HasEnoughSpace => FreeBytes is not { } free || free >= ReportedBytesToCopy;
}

public sealed record PhoneImportScanProgress(int FoldersScanned, int FilesFound, string CurrentFolder);

public sealed record PhoneImportProgress(
    int ProcessedFiles,
    int TotalFiles,
    long ProcessedBytes,
    long TotalBytes,
    long CopiedBytes,
    string? CurrentItem,
    int CopiedFiles,
    int SkippedFiles,
    int FailedFiles);

public enum PhoneImportStatus
{
    Completed,
    CompletedWithErrors,
    Cancelled,
    StoppedDeviceDisconnected,
    StoppedDestinationFull
}

public sealed record PhoneImportResult(
    PhoneImportStatus Status,
    string BackendName,
    string DestinationRoot,
    int TotalCount,
    int CopiedCount,
    int SkippedExistingCount,
    int FailedCount,
    int NotProcessedCount,
    long CopiedBytes,
    IReadOnlyList<PhoneImportFailure> Failures,
    int SizeMismatchCount,
    IReadOnlyList<string> SizeMismatches,
    int RenamedCount,
    bool SuggestKeepOriginals,
    TimeSpan Elapsed);
