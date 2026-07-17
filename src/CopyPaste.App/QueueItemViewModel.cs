using System.ComponentModel;
using System.Runtime.CompilerServices;
using CopyPaste.Core.Models;
using CopyPaste.Core.Services;

namespace CopyPaste.App;

public sealed class QueueItemViewModel : INotifyPropertyChanged
{
    private string _statusLabel;
    private string _detail;
    private double _progress;
    private bool _isSelected;
    private bool _english;

    public QueueItemViewModel(CopyJob job, CopyPreflightResult preflight, bool english = false)
    {
        Job = job;
        Preflight = preflight;
        _english = english;
        _statusLabel = T("Bekliyor", "Waiting");
        _detail = $"{preflight.FileCount:N0} {T("dosya", "files")} • {FormatBytes(preflight.TotalBytes)} • " +
                  $"{job.Profile.Name} • {OptionsLabel(job.Options)}";
    }

    public CopyJob Job { get; }
    public CopyPreflightResult Preflight { get; }
    public string Title => $"{DisplayName(Job.SourcePath)} → {DisplayName(Job.DestinationPath)}";
    public string Paths => $"{Job.SourcePath} → {Job.DestinationPath}";

    public string StatusLabel
    {
        get => _statusLabel;
        private set => SetField(ref _statusLabel, value);
    }

    public string Detail
    {
        get => _detail;
        private set => SetField(ref _detail, value);
    }

    public double Progress
    {
        get => _progress;
        private set => SetField(ref _progress, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    public void SetRunning(double? percentage = null, string? detail = null)
    {
        StatusLabel = T("Kopyalanıyor", "Copying");
        if (percentage is { } value)
            Progress = value;
        if (!string.IsNullOrWhiteSpace(detail))
            Detail = detail;
    }

    public void SetVerifying(string? relativePath = null)
    {
        // Progress<T> callbacks are posted to the UI queue. Ignore a late callback
        // after the final result has already changed the job state.
        if (Job.Status != CopyJobStatus.Running)
            return;
        StatusLabel = T("Doğrulanıyor", "Verifying");
        if (!string.IsNullOrWhiteSpace(relativePath))
            Detail = relativePath;
    }

    public void SetPaused()
    {
        Job.Status = CopyJobStatus.Paused;
        StatusLabel = T("Duraklatıldı", "Paused");
        Detail = T("Devam ettirildiğinde yeniden başlatılabilir Robocopy modu kaldığı yerden sürdürecek.",
            "Restartable Robocopy mode will continue when resumed.");
    }

    public void ResetForRetry()
    {
        Job.Status = CopyJobStatus.Ready;
        Job.ExitCode = null;
        Job.CompletedAt = null;
        Job.Summary = null;
        Job.FailedItemCount = 0;
        Job.Failures.Clear();
        StatusLabel = T("Bekliyor", "Waiting");
        Detail = $"{Preflight.FileCount:N0} {T("dosya", "files")} • {FormatBytes(Preflight.TotalBytes)} • " +
                 $"{Job.Profile.Name} • {OptionsLabel(Job.Options)}";
        Progress = 0;
    }

    public void SetResult(RobocopyResult result)
    {
        StatusLabel = result.Status switch
        {
            CopyJobStatus.Completed => T("Tamamlandı", "Completed"),
            CopyJobStatus.CompletedWithWarnings => T("Uyarılarla tamamlandı", "Completed with warnings"),
            CopyJobStatus.CompletedWithErrors => T("Hatalarla tamamlandı", "Completed with errors"),
            CopyJobStatus.Cancelled => T("İptal edildi", "Cancelled"),
            _ => T("Başarısız", "Failed")
        };
        Progress = result.IsFinished ? 100 : Progress;
        Detail = result.Summary;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void SetLanguage(bool english)
    {
        _english = english;
        StatusLabel = Job.Status switch
        {
            CopyJobStatus.Ready => T("Bekliyor", "Waiting"),
            CopyJobStatus.Running => T("Kopyalanıyor", "Copying"),
            CopyJobStatus.Paused => T("Duraklatıldı", "Paused"),
            CopyJobStatus.Completed => T("Tamamlandı", "Completed"),
            CopyJobStatus.CompletedWithWarnings => T("Uyarılarla tamamlandı", "Completed with warnings"),
            CopyJobStatus.CompletedWithErrors => T("Hatalarla tamamlandı", "Completed with errors"),
            CopyJobStatus.Cancelled => T("İptal edildi", "Cancelled"),
            _ => T("Başarısız", "Failed")
        };
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private static string DisplayName(string path) =>
        Path.GetFileName(Path.TrimEndingDirectorySeparator(path)) is { Length: > 0 } name ? name : path;

    public static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return $"{value:0.#} {units[unit]}";
    }

    private string OptionsLabel(CopyJobOptions options)
    {
        var existing = options.ExistingFiles switch
        {
            ExistingFileBehavior.Skip => T("mevcutları atla", "skip existing"),
            ExistingFileBehavior.Overwrite => T("üzerine yaz", "overwrite"),
            _ => T("güncelle", "update")
        };
        var verification = options.Verification switch
        {
            VerificationMode.Sha256 => "SHA-256",
            VerificationMode.Size => T("boyut doğrulama", "size verification"),
            _ => T("doğrulama yok", "no verification")
        };
        return $"{existing} • {verification}";
    }

    private string T(string turkish, string english) => _english ? english : turkish;
}
