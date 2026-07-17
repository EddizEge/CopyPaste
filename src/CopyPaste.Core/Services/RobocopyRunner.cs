using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using CopyPaste.Core.Models;

namespace CopyPaste.Core.Services;

public sealed partial class RobocopyRunner
{
    public async Task<RobocopyResult> RunAsync(
        CopyJob job,
        IProgress<RobocopyProgress>? progress = null,
        CancellationToken cancellationToken = default,
        Action<string>? logLine = null)
    {
        using var process = new Process { StartInfo = RobocopyCommandBuilder.Build(job) };
        job.Status = CopyJobStatus.Running;
        job.StartedAt ??= DateTimeOffset.Now;
        var outputAnalyzer = new RobocopyOutputAnalyzer();
        var progressTracker = new RobocopyProgressTracker(job.EstimatedTotalBytes);

        try
        {
            if (!process.Start())
                throw new InvalidOperationException("Robocopy işlemi başlatılamadı.");

            using var registration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException) { }
            });

            void ObserveLine(string line)
            {
                outputAnalyzer.Observe(line);
                logLine?.Invoke(line);
            }

            var outputTask = ReadOutputAsync(process.StandardOutput, progress, ObserveLine, progressTracker, cancellationToken);
            var errorTask = ReadOutputAsync(process.StandardError, progress, ObserveLine, null, cancellationToken);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            await Task.WhenAll(outputTask, errorTask).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            return CreateResult(process.ExitCode, outputAnalyzer.BuildSummary());
        }
        catch (OperationCanceledException)
        {
            job.Status = CopyJobStatus.Cancelled;
            return new RobocopyResult(-1, CopyJobStatus.Cancelled, "Kopyalama kullanıcı tarafından iptal edildi.");
        }
        catch (Exception ex)
        {
            job.Status = CopyJobStatus.Failed;
            return new RobocopyResult(16, CopyJobStatus.Failed, ex.Message);
        }
    }

    public static RobocopyResult CreateResult(
        int exitCode,
        RobocopyExecutionSummary? execution = null) => exitCode switch
    {
        0 => new(exitCode, CopyJobStatus.Completed, "Hedef zaten güncel; kopyalanacak dosya yok."),
        1 => new(exitCode, CopyJobStatus.Completed, "Tüm dosyalar başarıyla kopyalandı."),
        < 8 => new(exitCode, CopyJobStatus.CompletedWithWarnings, "Kopyalama tamamlandı; hedefte ek veya farklı dosyalar bulundu."),
        < 16 => CreatePartialResult(exitCode, execution),
        _ => new(exitCode, CopyJobStatus.Failed,
            "Robocopy ciddi bir hata nedeniyle transferi tamamlayamadı.",
            execution?.Failures,
            execution?.FailedItemCount ?? 0)
    };

    private static RobocopyResult CreatePartialResult(
        int exitCode,
        RobocopyExecutionSummary? execution)
    {
        var failedCount = execution?.FailedItemCount ?? 0;
        var countText = failedCount > 0 ? $"{failedCount:N0} öğe" : "Bazı öğeler";
        return new RobocopyResult(
            exitCode,
            CopyJobStatus.CompletedWithErrors,
            $"İşlem hatalarla birlikte tamamlandı. {countText} kopyalanamadı; diğer dosyalar başarıyla işlendi.",
            execution?.Failures,
            failedCount);
    }

    private static async Task ReadOutputAsync(
        StreamReader reader,
        IProgress<RobocopyProgress>? progress,
        Action<string>? logLine,
        RobocopyProgressTracker? tracker,
        CancellationToken cancellationToken)
    {
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            logLine?.Invoke(line);
            progress?.Report(tracker?.CreateProgress(line) ?? new RobocopyProgress(ParsePercentage(line), line.Trim()));
        }
    }

    private static double? ParsePercentage(string line)
    {
        var match = PercentageRegex().Match(line);
        if (!match.Success)
            return null;

        var normalized = match.Groups[1].Value.Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? Math.Clamp(value, 0, 100)
            : null;
    }

    [GeneratedRegex(@"^\s*(\d+(?:[\.,]\d+)?)%", RegexOptions.CultureInvariant)]
    private static partial Regex PercentageRegex();

    [GeneratedRegex(@"^\s*(?:New File|Newer|Older|Same|Yeni Dosya|Yeni)\s+(\d+)\s+(.+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FileHeaderRegex();

    private sealed class RobocopyProgressTracker(long totalBytes)
    {
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private long _completedBytes;
        private long _currentFileBytes;
        private int _completedFiles;

        public RobocopyProgress CreateProgress(string line)
        {
            var header = FileHeaderRegex().Match(line);
            if (header.Success && long.TryParse(header.Groups[1].Value, out var fileBytes))
                _currentFileBytes = Math.Max(0, fileBytes);

            var percentage = ParsePercentage(line);
            var currentBytes = percentage is { } value
                ? (long)(_currentFileBytes * value / 100d)
                : 0;
            var transferred = Math.Max(0, _completedBytes + currentBytes);
            if (percentage is >= 100 && _currentFileBytes > 0)
            {
                _completedBytes += _currentFileBytes;
                transferred = _completedBytes;
                _currentFileBytes = 0;
                _completedFiles++;
            }

            var elapsedSeconds = _stopwatch.Elapsed.TotalSeconds;
            double? speed = elapsedSeconds > 0 && transferred > 0 ? transferred / elapsedSeconds : null;
            TimeSpan? remaining = speed is > 0 && totalBytes > transferred
                ? TimeSpan.FromSeconds((totalBytes - transferred) / speed.Value)
                : null;
            var overallPercentage = totalBytes > 0
                ? Math.Clamp(transferred * 100d / totalBytes, 0, 100)
                : percentage;
            return new(overallPercentage, line.Trim(), transferred, speed, remaining, _completedFiles);
        }
    }
}
