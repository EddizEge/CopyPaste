using System.Globalization;
using System.Text.RegularExpressions;
using CopyPaste.Core.Models;

namespace CopyPaste.Core.Services;

public sealed record RobocopyExecutionSummary(
    long CopiedFileCount,
    int FailedItemCount,
    IReadOnlyList<CopyFailure> Failures);

public sealed partial class RobocopyOutputAnalyzer
{
    private readonly object _gate = new();
    private readonly Dictionary<string, CopyFailure> _failures =
        new(StringComparer.OrdinalIgnoreCase);
    private string? _pendingPath;
    private long _copiedFileCount;
    private int _failedItemCount;

    public void Observe(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        lock (_gate)
        {
            if (TryReadFileSummary(line, out var copied, out var failed))
            {
                _copiedFileCount = copied;
                _failedItemCount = Math.Max(_failedItemCount, failed);
                _pendingPath = null;
                return;
            }

            var error = ErrorRegex().Match(line);
            if (error.Success)
            {
                int? errorCode = int.TryParse(error.Groups["code"].Value, out var parsedCode)
                    ? parsedCode
                    : null;
                var operation = error.Groups["operation"].Value.Trim();
                var pathMatch = PathRegex().Match(operation);
                if (pathMatch.Success)
                {
                    var path = pathMatch.Groups["path"].Value.Trim().TrimEnd('.');
                    _failures[path] = new CopyFailure(path, $"Robocopy hata kodu {errorCode}", errorCode);
                    _pendingPath = path;
                }
                return;
            }

            if (_pendingPath is null || IsRetryStatus(line))
                return;

            var reason = line.Trim();
            if (reason.Length > 0 && _failures.TryGetValue(_pendingPath, out var failure))
                _failures[_pendingPath] = failure with { Reason = reason };
            _pendingPath = null;
        }
    }

    public RobocopyExecutionSummary BuildSummary()
    {
        lock (_gate)
        {
            var failures = _failures.Values.ToList();
            var missingDetails = Math.Max(0, _failedItemCount - failures.Count);
            if (missingDetails > 0)
            {
                failures.Add(new CopyFailure(
                    $"{missingDetails:N0} ek öğe",
                    "Dosya yolu Robocopy özetinde yer almadı; ayrıntılar için transfer günlüğünü açın."));
            }

            return new RobocopyExecutionSummary(
                _copiedFileCount,
                Math.Max(_failedItemCount, failures.Count),
                failures);
        }
    }

    public static RobocopyExecutionSummary Analyze(IEnumerable<string> lines)
    {
        var analyzer = new RobocopyOutputAnalyzer();
        foreach (var line in lines)
            analyzer.Observe(line);
        return analyzer.BuildSummary();
    }

    private static bool TryReadFileSummary(string line, out long copied, out int failed)
    {
        copied = 0;
        failed = 0;
        var match = FileSummaryRegex().Match(line);
        if (!match.Success)
            return false;

        copied = ParseCount(match.Groups["copied"].Value);
        failed = (int)Math.Min(int.MaxValue, ParseCount(match.Groups["failed"].Value));
        return true;
    }

    private static long ParseCount(string value) =>
        long.TryParse(CountCleanupRegex().Replace(value, string.Empty),
            NumberStyles.None, CultureInfo.InvariantCulture, out var result)
            ? result
            : 0;

    private static bool IsRetryStatus(string line) =>
        line.Contains("retry", StringComparison.OrdinalIgnoreCase)
        || line.Contains("waiting", StringComparison.OrdinalIgnoreCase)
        || line.Contains("yeniden", StringComparison.OrdinalIgnoreCase)
        || line.Contains("beklen", StringComparison.OrdinalIgnoreCase)
        || line.Contains("limit", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"\b(?:ERROR|HATA)\s+(?<code>\d+)\s+\(0x[0-9A-Fa-f]+\)\s+(?<operation>.*)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ErrorRegex();

    [GeneratedRegex(@"(?<path>(?:[A-Za-z]:\\|\\\\).+)$", RegexOptions.CultureInvariant)]
    private static partial Regex PathRegex();

    [GeneratedRegex(@"^\s*(?:Files?|Dosyalar?|Dosya)\s*:\s*[\d.,]+\s+(?<copied>[\d.,]+)\s+[\d.,]+\s+[\d.,]+\s+(?<failed>[\d.,]+)\s+[\d.,]+\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FileSummaryRegex();

    [GeneratedRegex(@"[^0-9]", RegexOptions.CultureInvariant)]
    private static partial Regex CountCleanupRegex();
}
