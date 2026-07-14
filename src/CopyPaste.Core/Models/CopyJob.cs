namespace CopyPaste.Core.Models;

public enum CopyJobStatus
{
    Ready,
    Running,
    Paused,
    Completed,
    CompletedWithWarnings,
    Cancelled,
    Failed
}

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
}

public sealed record RobocopyProgress(double? Percentage, string Message);

public sealed record RobocopyResult(int ExitCode, CopyJobStatus Status, string Summary)
{
    public bool IsSuccessful => ExitCode is >= 0 and < 8;
}
