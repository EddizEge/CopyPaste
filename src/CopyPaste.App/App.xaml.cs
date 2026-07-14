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

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _notificationService = new AppNotificationService();
        _notificationService.Initialize();

        var shellRequest = ShellLaunchRequestResolver.Resolve(
            Environment.GetCommandLineArgs(),
            new ShellCopyStateStore());

        var mainWindow = new MainWindow(shellRequest, _notificationService);
        _window = mainWindow;
        _notificationService.ActivationRequested += () =>
            mainWindow.DispatcherQueue.TryEnqueue(mainWindow.RestoreFromTray);
        _window.Closed += (_, _) => _notificationService?.Dispose();
        _window.Activate();
    }
}
