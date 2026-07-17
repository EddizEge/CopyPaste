using CopyPaste.App.Services;
using CopyPaste.Core.Services;
using Microsoft.UI.Xaml;

namespace CopyPaste.App;

public partial class App : Application
{
    private Window? _window;
    private AppNotificationService? _notificationService;

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

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (Environment.GetCommandLineArgs().Any(argument =>
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
    }
}
