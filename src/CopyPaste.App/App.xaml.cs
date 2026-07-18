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
        var protectedSessionIndex = Array.FindIndex(commandLine, argument =>
            argument.Equals("--protected-session", StringComparison.OrdinalIgnoreCase));
        if (protectedSessionIndex >= 0 && protectedSessionIndex + 1 < commandLine.Length)
        {
            var resultFile = commandLine[protectedSessionIndex + 1];
            var destination = GetArgumentValue(commandLine, "--destination");
            var pickerWindow = new ProtectedFolderPickerWindow(resultFile);
            _window = pickerWindow;
            pickerWindow.Closed += (_, _) =>
            {
                try
                {
                    if (!File.Exists(resultFile))
                        return;
                    var source = File.ReadAllText(resultFile).Trim();
                    if (!Path.IsPathFullyQualified(source))
                        return;
                    var executable = Environment.ProcessPath
                        ?? throw new InvalidOperationException("Uygulama yolu belirlenemedi.");
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = executable,
                        UseShellExecute = false
                    };
                    startInfo.ArgumentList.Add("--elevated-source");
                    startInfo.ArgumentList.Add(source);
                    if (!string.IsNullOrWhiteSpace(destination))
                    {
                        startInfo.ArgumentList.Add("--destination");
                        startInfo.ArgumentList.Add(destination);
                    }
                    Process.Start(startInfo);
                }
                finally
                {
                    try { File.Delete(resultFile); }
                    catch (IOException) { }
                    Exit();
                }
            };
            pickerWindow.Activate();
            return;
        }

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

        var elevatedSource = GetArgumentValue(commandLine, "--elevated-source");
        if (string.IsNullOrWhiteSpace(elevatedSource))
        {
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
        }

        _notificationService = new AppNotificationService();
        _notificationService.Initialize();

        var shellRequest = string.IsNullOrWhiteSpace(elevatedSource)
            ? ShellLaunchRequestResolver.Resolve(commandLine, new ShellCopyStateStore())
            : new ShellLaunchRequest(
                ShellLaunchMode.Normal,
                elevatedSource,
                GetArgumentValue(commandLine, "--destination"),
                "Korumalı kaynak yönetici yetkisiyle açıldı. Sahiplik ve klasör izinleri değiştirilmedi.",
                false,
                UseBackupMode: true);
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

    private static string? GetArgumentValue(string[] arguments, string name)
    {
        var index = Array.FindIndex(arguments, argument =>
            argument.Equals(name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
    }

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern IntPtr CommandLineToArgvW([MarshalAs(UnmanagedType.LPWStr)] string commandLine, out int count);
    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
