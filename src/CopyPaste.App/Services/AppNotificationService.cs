using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace CopyPaste.App.Services;

public sealed class AppNotificationService : IDisposable
{
    private bool _registered;

    public bool IsAvailable { get; private set; }
    public event Action? ActivationRequested;

    public void Initialize()
    {
        try
        {
            if (!AppNotificationManager.IsSupported())
                return;

            AppNotificationManager.Default.NotificationInvoked += OnNotificationInvoked;
            AppNotificationManager.Default.Register();
            _registered = true;
            IsAvailable = true;
        }
        catch
        {
            IsAvailable = false;
        }
    }

    public bool ShowTransferSummary(int completedCount, int failedCount)
    {
        if (!IsAvailable)
            return false;

        try
        {
            var title = failedCount == 0 ? "Transferler tamamlandı" : "Transfer kuyruğu tamamlandı";
            var body = failedCount == 0
                ? $"{completedCount} iş başarıyla tamamlandı."
                : $"{completedCount} iş tamamlandı, {failedCount} iş başarısız oldu.";

            var notification = new AppNotificationBuilder()
                .AddArgument("action", "open")
                .AddText(title)
                .AddText(body)
                .BuildNotification();
            AppNotificationManager.Default.Show(notification);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool ShowTestNotification()
    {
        if (!IsAvailable)
            return false;

        try
        {
            var notification = new AppNotificationBuilder()
                .AddArgument("action", "open")
                .AddText("CopyPaste hazır")
                .AddText("Windows bildirim entegrasyonu düzgün çalışıyor.")
                .BuildNotification();
            AppNotificationManager.Default.Show(notification);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool ShowUpdateAvailable(string version)
    {
        if (!IsAvailable)
            return false;

        try
        {
            var notification = new AppNotificationBuilder()
                .AddArgument("action", "open")
                .AddText("CopyPaste güncellemesi hazır")
                .AddText($"{version} sürümü yayınlandı. İndirmek için CopyPaste'i açın.")
                .BuildNotification();
            AppNotificationManager.Default.Show(notification);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void OnNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args) =>
        ActivationRequested?.Invoke();

    public void Dispose()
    {
        if (!_registered)
            return;

        AppNotificationManager.Default.NotificationInvoked -= OnNotificationInvoked;
        AppNotificationManager.Default.Unregister();
        _registered = false;
    }
}
