using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using CopyPaste.Core.Models;

namespace CopyPaste.Core.Services;

public sealed record TransferReportResult(string HtmlPath, string CsvPath);

public sealed class TransferReportService
{
    public async Task<TransferReportResult> ExportAsync(
        CopyJob job,
        string? outputDirectory = null,
        CancellationToken cancellationToken = default)
    {
        outputDirectory ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CopyPaste Reports");
        Directory.CreateDirectory(outputDirectory);
        var timestamp = (job.CompletedAt ?? DateTimeOffset.Now).ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var baseName = $"CopyPaste-{timestamp}-{job.Id:N}";
        var htmlPath = Path.Combine(outputDirectory, baseName + ".html");
        var csvPath = Path.Combine(outputDirectory, baseName + "-failures.csv");
        var html = BuildHtml(job);
        var csv = BuildCsv(job.Failures);
        await File.WriteAllTextAsync(htmlPath, html, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(csvPath, csv, new UTF8Encoding(true), cancellationToken).ConfigureAwait(false);
        return new(htmlPath, csvPath);
    }

    public static string BuildHtml(CopyJob job)
    {
        var e = HtmlEncoder.Default;
        var failures = job.Failures.Count == 0
            ? "<p class=\"muted\">Kopyalanamayan öğe yok.</p>"
            : "<table><thead><tr><th>Yol</th><th>Neden</th><th>Kod</th></tr></thead><tbody>" +
              string.Join(string.Empty, job.Failures.Select(failure =>
                  $"<tr><td>{e.Encode(failure.Path)}</td><td>{e.Encode(failure.Reason)}</td>" +
                  $"<td>{(failure.ErrorCode?.ToString(CultureInfo.InvariantCulture) ?? "—")}</td></tr>")) +
              "</tbody></table>";
        return $$"""
<!doctype html><html lang="tr"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width">
<title>CopyPaste transfer raporu</title><style>
body{font-family:Segoe UI,Arial,sans-serif;background:#0b0d12;color:#ececf3;margin:0;padding:36px}main{max-width:1050px;margin:auto}
.card{background:#151820;border:1px solid #303541;border-radius:16px;padding:22px;margin:16px 0}.accent{color:#8a72ff}.muted{color:#9ca3b2}
table{width:100%;border-collapse:collapse}th,td{text-align:left;padding:11px;border-bottom:1px solid #303541;vertical-align:top}th{color:#b9a9ff}
code{word-break:break-all;color:#d8d4ff}h1,h2{margin-top:0}</style></head><body><main>
<h1>CopyPaste <span class="accent">transfer raporu</span></h1>
<section class="card"><h2>{{e.Encode(job.Status.ToString())}}</h2><p>{{e.Encode(job.Summary ?? "Özet bulunmuyor.")}}</p>
<p class="muted">{{e.Encode((job.CompletedAt ?? job.CreatedAt).ToString("G", CultureInfo.CurrentCulture))}}</p></section>
<section class="card"><p><strong>Kaynak</strong><br><code>{{e.Encode(job.SourcePath)}}</code></p>
<p><strong>Hedef</strong><br><code>{{e.Encode(job.DestinationPath)}}</code></p>
<p><strong>Profil</strong><br>{{e.Encode(job.Profile.Name)}} · {{job.Profile.ThreadCount}} iş parçacığı</p></section>
<section class="card"><h2>Kopyalanamayan öğeler ({{Math.Max(job.FailedItemCount, job.Failures.Count)}})</h2>{{failures}}</section>
</main></body></html>
""";
    }

    public static string BuildCsv(IEnumerable<CopyFailure> failures)
    {
        static string Quote(string? value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";
        var lines = new List<string> { "Path,Reason,ErrorCode" };
        lines.AddRange(failures.Select(failure =>
            $"{Quote(failure.Path)},{Quote(failure.Reason)},{Quote(failure.ErrorCode?.ToString(CultureInfo.InvariantCulture))}"));
        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }
}
