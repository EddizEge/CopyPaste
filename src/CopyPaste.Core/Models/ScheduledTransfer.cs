namespace CopyPaste.Core.Models;

public sealed record ScheduledTransfer
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; init; }
    public required CopyJob Job { get; init; }
    public string TimeOfDay { get; init; } = "02:00";
    public bool Enabled { get; init; } = true;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
}
