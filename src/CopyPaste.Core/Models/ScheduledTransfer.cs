namespace CopyPaste.Core.Models;

public enum ScheduleKind
{
    Daily,
    Weekly,
    Once,
    WhenIdle,
    UsbArrival
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
    public string? UsbVolumeId { get; init; }
    public string? UsbVolumeLabel { get; init; }
    public bool RequireAcPower { get; init; }
    public bool Enabled { get; init; } = true;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
}
