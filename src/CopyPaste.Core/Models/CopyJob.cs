namespace CopyPaste.Core.Models;

public enum CopyJobStatus
{
    Ready,
    Running,
    Paused,
    Completed,
    CompletedWithWarnings,
    CompletedWithErrors,
    Cancelled,
    Failed,
    WaitingForNetwork
}

public enum QueueRecoveryReason
{
    None,
    UnexpectedShutdown,
    NetworkUnavailable
}

public sealed record CopyFailure(string Path, string Reason, int? ErrorCode = null);

public sealed class CopyJob
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string SourcePath { get; init; }
    public required string DestinationPath { get; init; }
    public string? DestinationRootPath { get; init; }
    public CopyRootMode RootMode { get; init; } = CopyRootMode.ContentsOnly;
    public required CopyProfile Profile { get; init; }
    public TransferPerformanceMode RequestedPerformanceMode { get; init; } = TransferPerformanceMode.Automatic;
    public TransferPerformanceMode ActivePerformanceMode { get; set; } = TransferPerformanceMode.Balanced;
    public int BandwidthLimitMbps { get; init; }
    public CompletionAction CompletionAction { get; init; }
    public bool UseBackupMode { get; init; }
    public CopyJobOptions Options { get; init; } = new();
    public CopyJobStatus Status { get; set; } = CopyJobStatus.Ready;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int? ExitCode { get; set; }
    public string? Summary { get; set; }
    public string? LogPath { get; set; }
    public int FailedItemCount { get; set; }
    public List<CopyFailure> Failures { get; set; } = [];
    public long EstimatedTotalBytes { get; set; }
    public int EstimatedFileCount { get; set; }
    public DateTimeOffset? LastCheckpointAt { get; set; }
    public long LastKnownBytesTransferred { get; set; }
    public int LastKnownCompletedFiles { get; set; }
    public QueueRecoveryReason RecoveryReason { get; set; }
    public int NetworkRetryAttempt { get; set; }
    public DateTimeOffset? NetworkWaitUntil { get; set; }
}

public sealed record RobocopyProgress(
    double? Percentage,
    string Message,
    long? BytesTransferred = null,
    double? BytesPerSecond = null,
    TimeSpan? EstimatedRemaining = null,
    int? CompletedFiles = null);

public sealed record RobocopyResult(
    int ExitCode,
    CopyJobStatus Status,
    string Summary,
    IReadOnlyList<CopyFailure>? Failures = null,
    int FailedItemCount = 0)
{
    public bool IsSuccessful => Status is CopyJobStatus.Completed or CopyJobStatus.CompletedWithWarnings;
    public bool IsFinished => Status is CopyJobStatus.Completed
        or CopyJobStatus.CompletedWithWarnings
        or CopyJobStatus.CompletedWithErrors;
}
