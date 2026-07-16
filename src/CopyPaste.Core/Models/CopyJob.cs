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
    Failed
}

public sealed record CopyFailure(string Path, string Reason, int? ErrorCode = null);

public sealed class CopyJob
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string SourcePath { get; init; }
    public required string DestinationPath { get; init; }
    public required CopyProfile Profile { get; init; }
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
}

public sealed record RobocopyProgress(double? Percentage, string Message);

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
