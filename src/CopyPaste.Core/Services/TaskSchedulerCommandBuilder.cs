using System.Globalization;
using CopyPaste.Core.Models;

namespace CopyPaste.Core.Services;

public static class TaskSchedulerCommandBuilder
{
    public static IReadOnlyList<string> BuildCreate(ScheduledTransfer schedule, string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        if (schedule.Kind == ScheduleKind.UsbArrival)
            throw new ArgumentException("USB arrival schedules are handled by the CopyPaste drive monitor.", nameof(schedule));

        var arguments = new List<string>
        {
            "/Create", "/TN", TaskName(schedule.Id),
            "/TR", $"\"{executablePath}\" --schedule {schedule.Id:D}",
            "/RL", "LIMITED", "/F"
        };

        if (schedule.Kind == ScheduleKind.WhenIdle)
        {
            arguments.InsertRange(1,
                ["/SC", "ONIDLE", "/I", Math.Clamp(schedule.IdleMinutes, 1, 999).ToString(CultureInfo.InvariantCulture)]);
        }
        else
        {
            if (!TimeOnly.TryParseExact(schedule.TimeOfDay, "HH:mm", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var time))
                throw new ArgumentException("Zamanlama saati HH:mm biçiminde olmalıdır.", nameof(schedule));

            string[] trigger = schedule.Kind switch
            {
                ScheduleKind.Weekly =>
                [
                    "/SC", "WEEKLY", "/D", schedule.DayOfWeek.ToString()[..3].ToUpperInvariant()
                ],
                ScheduleKind.Once =>
                [
                    "/SC", "ONCE", "/SD", (schedule.RunDate ?? DateOnly.FromDateTime(DateTime.Today))
                        .ToString("MM/dd/yyyy", CultureInfo.InvariantCulture)
                ],
                _ => ["/SC", "DAILY"]
            };
            arguments.InsertRange(1, trigger);
            arguments.InsertRange(arguments.IndexOf("/RL"),
                ["/ST", time.ToString("HH:mm", CultureInfo.InvariantCulture)]);
        }

        return arguments;
    }

    public static IReadOnlyList<string> BuildSetEnabled(Guid id, bool enabled) =>
        ["/Change", "/TN", TaskName(id), enabled ? "/ENABLE" : "/DISABLE"];

    public static IReadOnlyList<string> BuildRunNow(Guid id) =>
        ["/Run", "/TN", TaskName(id)];

    public static IReadOnlyList<string> BuildDelete(Guid id) =>
        ["/Delete", "/TN", TaskName(id), "/F"];

    private static string TaskName(Guid id) => $@"CopyPaste\{id:D}";
}
