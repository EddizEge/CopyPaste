using CopyPaste.Core.Models;

namespace CopyPaste.Core.Services;

public sealed record UsbDriveIdentity(string RootPath, string VolumeId, string VolumeLabel)
{
    public string DisplayName => string.IsNullOrWhiteSpace(VolumeLabel)
        ? $"{RootPath} ({VolumeId})"
        : $"{VolumeLabel} ({RootPath})";
}

public enum UsbScheduleTriggerDecision
{
    NotMatched,
    AcPowerRequired,
    Ready
}

public static class UsbScheduleMatcher
{
    public static bool Matches(ScheduledTransfer schedule, UsbDriveIdentity drive) =>
        schedule.Kind == ScheduleKind.UsbArrival
        && schedule.Enabled
        && !string.IsNullOrWhiteSpace(schedule.UsbVolumeId)
        && schedule.UsbVolumeId.Equals(drive.VolumeId, StringComparison.OrdinalIgnoreCase);

    public static UsbScheduleTriggerDecision Evaluate(
        ScheduledTransfer schedule,
        UsbDriveIdentity drive,
        bool isOnAcPower)
    {
        if (!Matches(schedule, drive))
            return UsbScheduleTriggerDecision.NotMatched;
        return schedule.RequireAcPower && !isOnAcPower
            ? UsbScheduleTriggerDecision.AcPowerRequired
            : UsbScheduleTriggerDecision.Ready;
    }
}
