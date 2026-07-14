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

            var outputTask = ReadOutputAsync(process.StandardOutput, progress, logLine, cancellationToken);
            var errorTask = ReadOutputAsync(process.StandardError, progress, logLine, cancellationToken);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            await Task.WhenAll(outputTask, errorTask).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            return CreateResult(process.ExitCode);
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

    public static RobocopyResult CreateResult(int exitCode) => exitCode switch
    {
        0 => new(exitCode, CopyJobStatus.Completed, "Hedef zaten güncel; kopyalanacak dosya yok."),
        1 => new(exitCode, CopyJobStatus.Completed, "Tüm dosyalar başarıyla kopyalandı."),
        < 8 => new(exitCode, CopyJobStatus.CompletedWithWarnings, "Kopyalama tamamlandı; hedefte ek veya farklı dosyalar bulundu."),
        _ => new(exitCode, CopyJobStatus.Failed, "Bir veya daha fazla dosya kopyalanamadı.")
    };

    private static async Task ReadOutputAsync(
        StreamReader reader,
        IProgress<RobocopyProgress>? progress,
        Action<string>? logLine,
        CancellationToken cancellationToken)
    {
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            logLine?.Invoke(line);
            progress?.Report(new RobocopyProgress(ParsePercentage(line), line.Trim()));
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
}
