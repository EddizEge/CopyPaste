namespace CopyPaste.Core.Models;

public enum ScheduleKind
{
    Daily,
    Weekly,
    Once,
    WhenIdle
}

public sealed record ScheduledTransfer
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; init; }
    public required CopyJob Job { get; init; }
    public string TimeOfDay { get; init; } = "02:00";
    public ScheduleKind Kind { get; init; } = ScheduleKind.Daily;
    public DayOfWeek DayOfWeek { get; init; } = DayOfWeek.Monday;
    public DateOnly? RunDate { get; init; }
    public int IdleMinutes { get; init; } = 10;
    public bool Enabled { get; init; } = true;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
}
