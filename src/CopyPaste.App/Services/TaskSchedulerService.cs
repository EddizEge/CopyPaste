using System.Diagnostics;
using CopyPaste.Core.Models;
using CopyPaste.Core.Services;

namespace CopyPaste.App.Services;

public sealed class TaskSchedulerService
{
    public async Task RegisterAsync(ScheduledTransfer schedule, CancellationToken cancellationToken = default)
    {
        if (schedule.Kind == ScheduleKind.UsbArrival)
            return;
        var executable = Environment.ProcessPath
            ?? throw new InvalidOperationException("Uygulama yolu belirlenemedi.");
        var arguments = TaskSchedulerCommandBuilder.BuildCreate(schedule, executable);
        await RunAsync(arguments, cancellationToken).ConfigureAwait(false);
        if (!schedule.Enabled)
            await SetEnabledAsync(schedule.Id, false, cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveAsync(Guid id, CancellationToken cancellationToken = default) =>
        await RunAsync(TaskSchedulerCommandBuilder.BuildDelete(id), cancellationToken).ConfigureAwait(false);

    public async Task SetEnabledAsync(Guid id, bool enabled, CancellationToken cancellationToken = default) =>
        await RunAsync(TaskSchedulerCommandBuilder.BuildSetEnabled(id, enabled), cancellationToken)
            .ConfigureAwait(false);

    public async Task RunNowAsync(Guid id, CancellationToken cancellationToken = default) =>
        await RunAsync(TaskSchedulerCommandBuilder.BuildRunNow(id), cancellationToken).ConfigureAwait(false);

    public static void RunDirect(Guid id)
    {
        var executable = Environment.ProcessPath
            ?? throw new InvalidOperationException("Uygulama yolu belirlenemedi.");
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("--schedule");
        startInfo.ArgumentList.Add(id.ToString("D"));
        _ = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Zamanlanmış görev başlatılamadı.");
    }

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
