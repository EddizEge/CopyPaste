using CopyPaste.App.Services;
using CopyPaste.Core.Services;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace CopyPaste.App;

public partial class App : Application
{
    private Window? _window;
    private AppNotificationService? _notificationService;
    private AppInstance? _mainInstance;

    public App()
    {
        UnhandledException += (_, args) =>
        {
            try
            {
                var directory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CopyPaste");
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "crash.log"), args.Exception.ToString());
            }
            catch { }
        };
        InitializeComponent();
    }

    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        var commandLine = Environment.GetCommandLineArgs();
        var protectedPickerIndex = Array.FindIndex(commandLine, argument =>
            argument.Equals("--protected-folder-picker", StringComparison.OrdinalIgnoreCase));
        if (protectedPickerIndex >= 0 && protectedPickerIndex + 1 < commandLine.Length)
        {
            _window = new ProtectedFolderPickerWindow(commandLine[protectedPickerIndex + 1]);
            _window.Activate();
            return;
        }

        if (commandLine.Any(argument =>
                argument.Equals("--uninstall-cleanup", StringComparison.OrdinalIgnoreCase)))
        {
            var scheduleStore = new ScheduleStore();
            var scheduler = new TaskSchedulerService();
            foreach (var schedule in await scheduleStore.LoadAsync())
            {
                try { await scheduler.RemoveAsync(schedule.Id); }
                catch (InvalidOperationException) { }
                await scheduleStore.RemoveAsync(schedule.Id);
            }
            Exit();
            return;
        }

        _mainInstance = AppInstance.FindOrRegisterForKey("CopyPaste.Main");
        if (!_mainInstance.IsCurrent)
        {
            await _mainInstance.RedirectActivationToAsync(AppInstance.GetCurrent().GetActivatedEventArgs());
            Process.GetCurrentProcess().Kill();
            return;
        }
        _mainInstance.Activated += (_, activation) =>
        {
            if (_window is { } window)
                window.DispatcherQueue.TryEnqueue(async () =>
                    await HandleRedirectedActivationAsync(activation));
        };

        _notificationService = new AppNotificationService();
        _notificationService.Initialize();

        var shellRequest = ShellLaunchRequestResolver.Resolve(
            Environment.GetCommandLineArgs(),
            new ShellCopyStateStore());
        if (shellRequest.ScheduleId is { } scheduleId)
        {
            var schedule = await new ScheduleStore().FindAsync(scheduleId);
            shellRequest = schedule is null
                ? shellRequest with
                {
                    AutoStart = false,
                    Message = "Zamanlanmış transfer bulunamadı veya devre dışı."
                }
                : shellRequest with
                {
                    SourcePath = schedule.Job.SourcePath,
                    DestinationPath = schedule.Job.DestinationPath,
                    ScheduledJob = schedule.Job,
                    Message = $"Zamanlanmış transfer başlatılıyor: {schedule.Name}"
                };
        }

        var mainWindow = new MainWindow(shellRequest, _notificationService);
        _window = mainWindow;
        _notificationService.ActivationRequested += () =>
            mainWindow.DispatcherQueue.TryEnqueue(mainWindow.RestoreFromTray);
        _window.Closed += (_, _) => _notificationService?.Dispose();
        _window.Activate();
        if (Environment.GetCommandLineArgs().Any(argument =>
                argument.Equals("--minimized", StringComparison.OrdinalIgnoreCase)))
            mainWindow.MinimizeToTray();
    }

    private async Task HandleRedirectedActivationAsync(AppActivationArguments activation)
    {
        if (_window is not MainWindow mainWindow)
            return;
        var arguments = GetCommandLineArguments(activation);
        var request = ShellLaunchRequestResolver.Resolve(arguments, new ShellCopyStateStore());
        if (request.ScheduleId is { } scheduleId)
        {
            var schedule = await new ScheduleStore().FindAsync(scheduleId);
            if (schedule is not null)
            {
                request = request with
                {
                    SourcePath = schedule.Job.SourcePath,
                    DestinationPath = schedule.Job.DestinationRootPath ?? schedule.Job.DestinationPath,
                    ScheduledJob = schedule.Job,
                    AutoStart = true
                };
            }
        }
        await mainWindow.HandleShellRequestAsync(request);
    }

    private static string[] GetCommandLineArguments(AppActivationArguments activation)
    {
        if (activation.Data is not ILaunchActivatedEventArgs launch
            || string.IsNullOrWhiteSpace(launch.Arguments))
            return [Environment.ProcessPath ?? "CopyPaste.App.exe"];
        var count = 0;
        var pointer = CommandLineToArgvW(launch.Arguments, out count);
        if (pointer == IntPtr.Zero || count == 0)
            return [Environment.ProcessPath ?? "CopyPaste.App.exe"];
        try
        {
            var result = new List<string> { Environment.ProcessPath ?? "CopyPaste.App.exe" };
            for (var index = 0; index < count; index++)
                result.Add(Marshal.PtrToStringUni(Marshal.ReadIntPtr(pointer, index * IntPtr.Size)) ?? string.Empty);
            return result.ToArray();
        }
        finally { LocalFree(pointer); }
    }

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern IntPtr CommandLineToArgvW([MarshalAs(UnmanagedType.LPWStr)] string commandLine, out int count);
    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
