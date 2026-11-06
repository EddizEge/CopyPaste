using System.Globalization;
using System.Text;
using CopyPaste.Core.Models;

namespace CopyPaste.Core.Services;

/// <summary>
/// Builds the plain-text list of files a phone import did not bring over, for the user to save next to their photos.
/// Every failed file is listed with its reason and HRESULT, then the files that were not processed and the
/// subfolders that couldn't be read. Device identifiers and serial numbers are never written.
/// </summary>
public static class PhoneImportReport
{
    private const string NewLine = "\r\n";

    /// <summary>Whether the result has anything a list could show.</summary>
    public static bool HasEntries(PhoneImportPlan? plan, PhoneImportResult result) =>
        result.FailedCount > 0 || result.NotProcessedCount > 0 || plan?.ScanFailures.Count > 0;

    /// <summary>File name without extension, e.g. <c>CopyPaste-telefon-aktarilamayanlar-20261106-1430</c>.</summary>
    public static string SuggestedFileName(bool english, DateTime localTime) =>
        (english ? "CopyPaste-phone-not-copied-" : "CopyPaste-telefon-aktarilamayanlar-")
        + localTime.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);

    /// <summary>UTF-8 with a byte order mark, so Notepad and older editors show Turkish characters correctly.</summary>
    public static byte[] Encode(string text)
    {
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        var preamble = encoding.GetPreamble();
        var body = encoding.GetBytes(text);
        var bytes = new byte[preamble.Length + body.Length];
        preamble.CopyTo(bytes, 0);
        body.CopyTo(bytes, preamble.Length);
        return bytes;
    }

    public static string Build(
        PhoneImportPlan? plan,
        PhoneImportResult result,
        bool english,
        string appVersion,
        DateTimeOffset createdAt,
        string? accessPathLabel = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        string T(string turkish, string englishText) => english ? englishText : turkish;
        var culture = CultureInfo.InvariantCulture;
        var scanFailures = plan?.ScanFailures ?? [];
        var text = new StringBuilder();
        void Line(string value = "") => text.Append(value).Append(NewLine);

        Line(T("CopyPaste — Telefondan aktarılamayan dosyalar", "CopyPaste — Files not copied from the phone"));
        Line($"{T("Tarih", "Date")}: {createdAt.ToString("yyyy-MM-dd HH:mm:ss zzz", culture)}");
        Line($"{T("Uygulama sürümü", "App version")}: {Clean(appVersion)}");
        if (plan is not null)
            Line($"{T("Kaynak", "Source")}: {Clean(plan.SourceDisplayPath)}");
        Line($"{T("Hedef", "Destination")}: {Clean(result.DestinationRoot)}");
        Line($"{T("Erişim yolu", "Access path")}: {Clean(accessPathLabel ?? result.BackendName)}");
        Line($"{T("Durum", "Status")}: {DescribeStatus(result.Status, english)}");
        Line(T(
            $"Sayılar: toplam {result.TotalCount} dosya, aktarılan {result.CopiedCount}, zaten var {result.SkippedExistingCount}, "
            + $"başarısız {result.FailedCount}, işlenmeyen {result.NotProcessedCount}, okunamayan alt klasör {scanFailures.Count}",
            $"Counts: {result.TotalCount} files in total, copied {result.CopiedCount}, already present {result.SkippedExistingCount}, "
            + $"failed {result.FailedCount}, not processed {result.NotProcessedCount}, unreadable subfolders {scanFailures.Count}"));
        if (plan is { IsRetry: true, ScannedFileCount: { } scanned })
            Line(T($"Bu bir yeniden denemedir: ilk taramada {scanned} dosya vardı, yalnız eksikler denendi.",
                $"This was a retry: the first scan found {scanned} files and only the missing ones were tried."));
        if (result.Failures.Count < result.FailedCount)
            Line(T($"Not: {result.FailedCount - result.Failures.Count} başarısız dosyanın ayrıntısı kaydedilmedi.",
                $"Note: details of {result.FailedCount - result.Failures.Count} failed files were not recorded."));
        Line(T("Sütunlar sekmeyle ayrılmıştır; Excel'e yapıştırılabilir.",
            "Columns are tab-separated and can be pasted into Excel."));

        Line();
        Line($"== {T("Başarısız dosyalar", "Failed files")} ({result.Failures.Count}) ==");
        if (result.Failures.Count == 0)
            Line(T("(yok)", "(none)"));
        else
        {
            Line(T("Göreli yol\tSebep\tHRESULT\tMesaj", "Relative path\tReason\tHRESULT\tMessage"));
            foreach (var failure in result.Failures)
                Line(FailureLine(failure, english));
        }

        Line();
        Line($"== {T("İşlenmeyen dosyalar", "Files not processed")} ({result.NotProcessedPaths.Count}) ==");
        if (result.NotProcessedPaths.Count == 0)
            Line(T("(yok)", "(none)"));
        foreach (var path in result.NotProcessedPaths)
            Line(Clean(path));

        Line();
        Line($"== {T("Okunamayan alt klasörler", "Subfolders that couldn't be read")} ({scanFailures.Count}) ==");
        if (scanFailures.Count == 0)
            Line(T("(yok)", "(none)"));
        else
        {
            Line(T("Göreli yol\tSebep\tHRESULT\tMesaj", "Relative path\tReason\tHRESULT\tMessage"));
            foreach (var failure in scanFailures)
                Line(FailureLine(failure, english));
        }
        return text.ToString();
    }

    public static string DescribeStatus(PhoneImportStatus status, bool english) => status switch
    {
        PhoneImportStatus.Completed => english ? "Completed" : "Tamamlandı",
        PhoneImportStatus.CompletedWithErrors => english ? "Completed with errors" : "Hatalarla tamamlandı",
        PhoneImportStatus.Cancelled => english ? "Cancelled" : "İptal edildi",
        PhoneImportStatus.StoppedDeviceDisconnected => english
            ? "Stopped: the connection to the phone was lost"
            : "Durdu: telefonla bağlantı koptu",
        PhoneImportStatus.StoppedDestinationFull => english
            ? "Stopped: the destination drive is full"
            : "Durdu: hedef sürücü doldu",
        _ => status.ToString()
    };

    private static string FailureLine(PhoneImportFailure failure, bool english) =>
        string.Join('\t',
            Clean(failure.RelativePath),
            PhoneImportErrors.Describe(failure.Kind, english),
            PhoneImportErrors.FormatHResult(failure.HResult),
            Clean(failure.Message));

    // One line per entry, no tab inside a column, and no device-interface path (it carries the serial number).
    private static string Clean(string? value)
    {
        var line = (value ?? string.Empty)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace("\t", " ", StringComparison.Ordinal)
            .Trim();
        return PhoneImportDiagnostics.MaskDeviceId(line);
    }
}
