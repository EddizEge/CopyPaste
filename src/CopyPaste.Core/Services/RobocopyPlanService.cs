using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using CopyPaste.Core.Models;

namespace CopyPaste.Core.Services;

public enum CopyPlanAction
{
    Copy,
    Overwrite,
    Skip,
    Error
}

public sealed record CopyPlanItem(
    string Path,
    long Bytes,
    CopyPlanAction Action,
    string RobocopyStatus);

public sealed record CopyPlanResult(
    int TotalFileCount,
    int CopyFileCount,
    int OverwriteFileCount,
    int SkippedFileCount,
    int FailedFileCount,
    long TotalBytes,
    long BytesToCopy,
    long SkippedBytes,
    TimeSpan? EstimatedDuration,
    IReadOnlyList<CopyPlanItem> Items,
    bool IsTruncated,
    int ExitCode)
{
    public bool HasErrors => FailedFileCount > 0 || ExitCode >= 8;
    public bool HasChanges => CopyFileCount + OverwriteFileCount > 0;
}

public sealed partial class RobocopyPlanService
{
    private const int DefaultItemLimit = 500;

    public async Task<CopyPlanResult> PlanAsync(
        CopyJob job,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default,
        int itemLimit = DefaultItemLimit)
    {
        using var process = new Process { StartInfo = RobocopyCommandBuilder.Build(job, listOnly: true) };
        var analyzer = new PlanOutputAnalyzer(job, Math.Max(0, itemLimit), progress);

        try
        {
            if (!process.Start())
                throw new InvalidOperationException("Robocopy önizlemesi başlatılamadı.");

            using var registration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException) { }
            });

            var outputTask = ReadOutputAsync(process.StandardOutput, analyzer, cancellationToken);
            var errorTask = ReadOutputAsync(process.StandardError, analyzer, cancellationToken);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            await Task.WhenAll(outputTask, errorTask).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return analyzer.Build(process.ExitCode);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            throw new IOException("Robocopy önizlemesi çalıştırılamadı.", ex);
        }
    }

    public static CopyPlanResult Analyze(
        CopyJob job,
        IEnumerable<string> output,
        int exitCode = 0,
        int itemLimit = DefaultItemLimit)
    {
        var analyzer = new PlanOutputAnalyzer(job, Math.Max(0, itemLimit), null);
        foreach (var line in output)
            analyzer.Observe(line);
        return analyzer.Build(exitCode);
    }

    private static async Task ReadOutputAsync(
        StreamReader reader,
        PlanOutputAnalyzer analyzer,
        CancellationToken cancellationToken)
    {
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            analyzer.Observe(line);
    }

    private sealed class PlanOutputAnalyzer(CopyJob job, int itemLimit, IProgress<int>? progress)
    {
        private readonly object _gate = new();
        private readonly List<CopyPlanItem> _items = [];
        private readonly HashSet<string> _errorPaths = new(StringComparer.OrdinalIgnoreCase);
        private int _observedFiles;
        private int _newFiles;
        private int _totalFiles;
        private int _copiedFiles;
        private int _skippedFiles;
        private int _failedFiles;
        private long _totalBytes;
        private long _copiedBytes;
        private long _skippedBytes;
        private bool _truncated;

        public void Observe(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            lock (_gate)
                ObserveCore(line);
        }

        private void ObserveCore(string line)
        {
            var filesSummary = FilesSummaryRegex().Match(line);
            if (filesSummary.Success)
            {
                _totalFiles = ParseInt(filesSummary, "total");
                _copiedFiles = ParseInt(filesSummary, "copied");
                _skippedFiles = ParseInt(filesSummary, "skipped");
                _failedFiles = Math.Max(_failedFiles, ParseInt(filesSummary, "failed"));
                return;
            }

            var error = ErrorRegex().Match(line);
            if (error.Success)
            {
                var pathMatch = PathRegex().Match(error.Groups["operation"].Value);
                if (pathMatch.Success)
                {
                    var errorPath = pathMatch.Groups["path"].Value.Trim().TrimEnd('.');
                    if (_errorPaths.Add(errorPath))
                    {
                        _failedFiles++;
                        AddItem(new CopyPlanItem(errorPath, 0, CopyPlanAction.Error,
                            $"Robocopy error {error.Groups["code"].Value}"));
                    }
                }
                return;
            }

            var bytesSummary = BytesSummaryRegex().Match(line);
            if (bytesSummary.Success)
            {
                _totalBytes = ParseLong(bytesSummary, "total");
                _copiedBytes = ParseLong(bytesSummary, "copied");
                _skippedBytes = ParseLong(bytesSummary, "skipped");
                return;
            }

            var file = FileLineRegex().Match(line);
            if (!file.Success || !long.TryParse(file.Groups["bytes"].Value,
                    NumberStyles.None, CultureInfo.InvariantCulture, out var bytes))
                return;

            var status = file.Groups["status"].Value.Trim();
            var path = file.Groups["path"].Value.Trim();
            if (path.EndsWith(Path.DirectorySeparatorChar)
                || path.EndsWith(Path.AltDirectorySeparatorChar))
                return;
            var action = Classify(status, job.Options.ExistingFiles);
            if (action == CopyPlanAction.Copy)
                _newFiles++;
            _observedFiles++;
            progress?.Report(_observedFiles);

            AddItem(new CopyPlanItem(path, bytes, action, status));
        }

        public CopyPlanResult Build(int exitCode)
        {
            lock (_gate)
            {
                var copyFiles = Math.Min(_newFiles, _copiedFiles);
                var overwriteFiles = Math.Max(0, _copiedFiles - copyFiles);
                TimeSpan? estimatedDuration = job.BandwidthLimitMbps > 0 && _copiedBytes > 0
                    ? TimeSpan.FromSeconds(_copiedBytes / (job.BandwidthLimitMbps * 1024d * 1024d))
                    : null;
                return new CopyPlanResult(
                    _totalFiles,
                    copyFiles,
                    overwriteFiles,
                    _skippedFiles,
                    _failedFiles,
                    _totalBytes,
                    _copiedBytes,
                    _skippedBytes,
                    estimatedDuration,
                    _items.ToArray(),
                    _truncated,
                    exitCode);
            }
        }

        private static CopyPlanAction Classify(string status, ExistingFileBehavior behavior)
        {
            if (status.Contains("ERROR", StringComparison.OrdinalIgnoreCase))
                return CopyPlanAction.Error;
            if (status.Contains("EXTRA", StringComparison.OrdinalIgnoreCase))
                return CopyPlanAction.Skip;
            if (status.Contains("New File", StringComparison.OrdinalIgnoreCase)
                || status.Contains("Yeni Dosya", StringComparison.OrdinalIgnoreCase))
                return CopyPlanAction.Copy;
            if (behavior == ExistingFileBehavior.Skip)
                return CopyPlanAction.Skip;
            if (status.Equals("same", StringComparison.OrdinalIgnoreCase)
                || status.Equals("aynı", StringComparison.OrdinalIgnoreCase))
                return behavior == ExistingFileBehavior.Overwrite
                    ? CopyPlanAction.Overwrite
                    : CopyPlanAction.Skip;
            return CopyPlanAction.Overwrite;
        }

        private void AddItem(CopyPlanItem item)
        {
            if (_items.Count < itemLimit)
                _items.Add(item);
            else
                _truncated = true;
        }

        private static int ParseInt(Match match, string group) =>
            int.TryParse(CountCleanupRegex().Replace(match.Groups[group].Value, string.Empty),
                NumberStyles.None, CultureInfo.InvariantCulture, out var value)
                ? value : 0;

        private static long ParseLong(Match match, string group) =>
            long.TryParse(CountCleanupRegex().Replace(match.Groups[group].Value, string.Empty),
                NumberStyles.None, CultureInfo.InvariantCulture, out var value)
                ? value : 0;
    }

    [GeneratedRegex(@"^\s*(?:Files|Dosyalar)\s*:\s*(?<total>[\d.,]+)\s+(?<copied>[\d.,]+)\s+(?<skipped>[\d.,]+)\s+(?<mismatch>[\d.,]+)\s+(?<failed>[\d.,]+)\s+(?<extras>[\d.,]+)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FilesSummaryRegex();

    [GeneratedRegex(@"^\s*(?:Bytes|Bayt)\s*:\s*(?<total>[\d.,]+)\s+(?<copied>[\d.,]+)\s+(?<skipped>[\d.,]+)\s+(?<mismatch>[\d.,]+)\s+(?<failed>[\d.,]+)\s+(?<extras>[\d.,]+)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BytesSummaryRegex();

    [GeneratedRegex(@"^\s*(?<status>[^\d].*?\S)\s+(?<bytes>\d+)\s+(?<path>(?:[A-Za-z]:\\|\\\\).+?)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex FileLineRegex();

    [GeneratedRegex(@"\b(?:ERROR|HATA)\s+(?<code>\d+)\s+\(0x[0-9A-Fa-f]+\)\s+(?<operation>.*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ErrorRegex();

    [GeneratedRegex(@"(?<path>(?:[A-Za-z]:\\|\\\\).+)$", RegexOptions.CultureInvariant)]
    private static partial Regex PathRegex();

    [GeneratedRegex(@"[^0-9]", RegexOptions.CultureInvariant)]
    private static partial Regex CountCleanupRegex();
}
