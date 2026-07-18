using System.Diagnostics;
using System.Globalization;
using CopyPaste.Core.Models;

namespace CopyPaste.App.Services;

public sealed class TaskSchedulerService
{
    public async Task RegisterAsync(ScheduledTransfer schedule, CancellationToken cancellationToken = default)
    {
        var executable = Environment.ProcessPath
            ?? throw new InvalidOperationException("Uygulama yolu belirlenemedi.");
        var arguments = BuildCreateArguments(schedule, executable);
        await RunAsync(arguments, cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveAsync(Guid id, CancellationToken cancellationToken = default) =>
        await RunAsync(["/Delete", "/TN", TaskName(id), "/F"], cancellationToken).ConfigureAwait(false);

    public static IReadOnlyList<string> BuildCreateArguments(ScheduledTransfer schedule, string executablePath)
    {
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
            return arguments;
        }

        if (!TimeOnly.TryParseExact(schedule.TimeOfDay, "HH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var time))
            throw new ArgumentException("Zamanlama saati HH:mm biçiminde olmalıdır.", nameof(schedule));

        string[] trigger = schedule.Kind switch
        {
            ScheduleKind.Weekly => new[]
            {
                "/SC", "WEEKLY", "/D", schedule.DayOfWeek.ToString()[..3].ToUpperInvariant()
            },
            ScheduleKind.Once => new[]
            {
                "/SC", "ONCE", "/SD", (schedule.RunDate ?? DateOnly.FromDateTime(DateTime.Today))
                    .ToString("MM/dd/yyyy", CultureInfo.InvariantCulture)
            },
            _ => ["/SC", "DAILY"]
        };
        arguments.InsertRange(1, trigger);
        arguments.InsertRange(arguments.IndexOf("/RL"), ["/ST", time.ToString("HH:mm", CultureInfo.InvariantCulture)]);
        return arguments;
    }

    private static string TaskName(Guid id) => $@"CopyPaste\{id:D}";

    private static async Task RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "schtasks.exe"),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Windows Görev Zamanlayıcı başlatılamadı.");
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var output = await outputTask.ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);
        if (process.ExitCode != 0)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? output : error);
    }
}
