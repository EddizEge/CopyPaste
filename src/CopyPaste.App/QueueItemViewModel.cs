using System.ComponentModel;
using System.Runtime.CompilerServices;
using CopyPaste.Core.Models;
using CopyPaste.Core.Services;

namespace CopyPaste.App;

public sealed class QueueItemViewModel : INotifyPropertyChanged
{
    private string _statusLabel = "Bekliyor";
    private string _detail;
    private double _progress;
    private bool _isSelected;

    public QueueItemViewModel(CopyJob job, CopyPreflightResult preflight)
    {
        Job = job;
        Preflight = preflight;
        _detail = $"{preflight.FileCount:N0} dosya • {FormatBytes(preflight.TotalBytes)} • " +
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
        StatusLabel = "Kopyalanıyor";
        if (percentage is { } value)
            Progress = value;
        if (!string.IsNullOrWhiteSpace(detail))
            Detail = detail;
    }

    public void SetVerifying(string? relativePath = null)
    {
        StatusLabel = "Doğrulanıyor";
        if (!string.IsNullOrWhiteSpace(relativePath))
            Detail = relativePath;
    }

    public void SetPaused()
    {
        Job.Status = CopyJobStatus.Paused;
        StatusLabel = "Duraklatıldı";
        Detail = "Devam ettirildiğinde yeniden başlatılabilir Robocopy modu kaldığı yerden sürdürecek.";
    }

    public void ResetForRetry()
    {
        Job.Status = CopyJobStatus.Ready;
        Job.ExitCode = null;
        Job.CompletedAt = null;
        Job.Summary = null;
        StatusLabel = "Bekliyor";
        Detail = $"{Preflight.FileCount:N0} dosya • {FormatBytes(Preflight.TotalBytes)} • " +
                 $"{Job.Profile.Name} • {OptionsLabel(Job.Options)}";
        Progress = 0;
    }

    public void SetResult(RobocopyResult result)
    {
        StatusLabel = result.Status switch
        {
            CopyJobStatus.Completed => "Tamamlandı",
            CopyJobStatus.CompletedWithWarnings => "Uyarılarla tamamlandı",
            CopyJobStatus.Cancelled => "İptal edildi",
            _ => "Başarısız"
        };
        Progress = result.IsSuccessful ? 100 : Progress;
        Detail = result.Summary;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

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

    private static string OptionsLabel(CopyJobOptions options)
    {
        var existing = options.ExistingFiles switch
        {
            ExistingFileBehavior.Skip => "mevcutları atla",
            ExistingFileBehavior.Overwrite => "üzerine yaz",
            _ => "güncelle"
        };
        var verification = options.Verification switch
        {
            VerificationMode.Sha256 => "SHA-256",
            VerificationMode.Size => "boyut doğrulama",
            _ => "doğrulama yok"
        };
        return $"{existing} • {verification}";
    }
}
