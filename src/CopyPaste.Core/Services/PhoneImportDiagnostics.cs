using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using CopyPaste.Core.Models;

namespace CopyPaste.Core.Services;

/// <summary>
/// Collects what the phone import saw so a user can paste one block of text into a support conversation. Nothing
/// is sent anywhere; the text is built only when the user asks to copy it.
/// </summary>
public sealed class PhoneImportDiagnostics
{
    private const int MaxErrors = 20;
    private readonly List<string> _errors = [];

    public string AppVersion { get; set; } = "?";
    public string? Distribution { get; set; }
    public string BackendPreference { get; set; } = "Auto";
    public PortableDeviceDiscovery? Discovery { get; set; }
    public PortableDeviceOpenResult? LastOpen { get; set; }
    public PhoneImportPlan? LastPlan { get; set; }
    public PhoneImportResult? LastResult { get; set; }
    public IReadOnlyList<string> MediaImportLines { get; set; } = [];
    public IReadOnlyList<string> ExtraLines { get; set; } = [];

    public void RecordError(string context, Exception exception) =>
        RecordError(context, exception.HResult, exception.GetType().Name + ": " + exception.Message);

    public void RecordError(string context, int hresult, string message)
    {
        if (_errors.Count >= MaxErrors)
            _errors.RemoveAt(0);
        _errors.Add($"{DateTimeOffset.Now:HH:mm:ss} {context}: {PhoneImportErrors.FormatHResult(hresult)} "
                    + $"{PhoneImportErrors.ClassifyHResult(hresult)} — {OneLine(message)}");
    }

    public string Build(bool english)
    {
        string T(string turkish, string englishText) => english ? englishText : turkish;
        var culture = CultureInfo.InvariantCulture;
        var text = new StringBuilder();
        text.AppendLine(T("CopyPaste — Telefondan aktar tanılaması", "CopyPaste — Import from phone diagnostics"));
        text.AppendLine($"{T("Zaman", "Time")}: {DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz", culture)}");
        text.AppendLine($"{T("Uygulama", "App")}: {AppVersion}{(Distribution is null ? string.Empty : " (" + Distribution + ")")}"
                        + $", {RuntimeInformation.ProcessArchitecture}, .NET {Environment.Version}");
        text.AppendLine($"Windows: {RuntimeInformation.OSDescription} ({Environment.OSVersion.Version})");
        text.AppendLine($"{T("Erişim yolu tercihi", "Access path preference")}: {BackendPreference}");

        text.AppendLine();
        text.AppendLine(T("Arka uçların gördüğü aygıtlar:", "Devices seen by each backend:"));
        if (Discovery is null)
            text.AppendLine("  " + T("Henüz aygıt aranmadı.", "No device search yet."));
        else
        {
            foreach (var report in Discovery.Reports)
            {
                var status = report.ErrorHResult is { } hresult
                    ? $"{T("HATA", "ERROR")} {PhoneImportErrors.FormatHResult(hresult)} {OneLine(report.ErrorMessage)}"
                    : $"{report.Devices.Count} {T("aygıt", "device(s)")}";
                text.AppendLine($"  [{report.BackendName}] {status} ({report.Elapsed.TotalMilliseconds.ToString("N0", culture)} ms)");
                foreach (var device in report.Devices)
                {
                    var opened = device.Root is null
                        ? $" — {T("açılamadı", "not opened")} {PhoneImportErrors.FormatHResult(device.ErrorHResult ?? 0)} {OneLine(device.ErrorMessage)}"
                        : string.Empty;
                    text.AppendLine($"    • \"{device.Name}\" | {device.Id}{opened}");
                }
            }
        }

        text.AppendLine();
        text.AppendLine("Windows.Media.Import:");
        if (MediaImportLines.Count == 0)
            text.AppendLine("  " + T("bilgi yok", "no information"));
        foreach (var line in MediaImportLines)
            text.AppendLine("  " + line);

        text.AppendLine();
        if (LastOpen is { } open)
        {
            var dcim = open.Outcome == PortableDeviceOpenOutcome.DcimFound ? T("bulundu", "found") : T("bulunamadı", "not found");
            text.AppendLine($"{T("Açılan aygıt", "Opened device")}: \"{open.Device?.Name}\" [{open.Device?.BackendName}] — "
                            + $"DCIM {dcim}; {T("sonuç", "outcome")} {open.Outcome}");
            if (open.Path.Count > 0)
                text.AppendLine($"  {T("Klasör", "Folder")}: {PortableDeviceCatalog.DescribePath(open.Path, open.Device?.Name)}");
            foreach (var attempt in open.Attempts)
                text.AppendLine("  - " + OneLine(attempt));
        }
        else
            text.AppendLine($"{T("Açılan aygıt", "Opened device")}: —");

        if (LastPlan is { } plan)
        {
            text.AppendLine($"{T("Son tarama", "Last scan")} [{plan.Source.BackendName}]: {plan.SourceDisplayPath} → {plan.DestinationRoot}");
            text.AppendLine($"  {T("dosya", "files")} {plan.FileCount}, {T("bildirilen", "reported")} {FormatBytes(plan.ReportedTotalBytes)}, "
                            + $"{T("boyutu bilinmeyen", "unknown size")} {plan.UnknownSizeCount}, {T("zaten var", "already present")} {plan.ExistingCount}, "
                            + $"{T("yeniden adlandırılan", "renamed")} {plan.RenamedCount}, {T("boş alan", "free space")} "
                            + (plan.FreeBytes is { } free ? FormatBytes(free) : "?")
                            + $", {T("taranamayan klasör", "unscanned folders")} {plan.ScanFailures.Count}");
        }
        if (LastResult is { } result)
        {
            text.AppendLine($"{T("Son aktarma", "Last import")} [{result.BackendName}]: {result.Status}, "
                            + $"{T("aktarılan", "copied")} {result.CopiedCount}, {T("atlanan (zaten var)", "skipped (already present)")} {result.SkippedExistingCount}, "
                            + $"{T("başarısız", "failed")} {result.FailedCount}, {T("işlenmeyen", "not processed")} {result.NotProcessedCount}, "
                            + $"{FormatBytes(result.CopiedBytes)}, {result.Elapsed.TotalSeconds.ToString("N0", culture)} s, "
                            + $"{T("boyutu farklı", "size differs")} {result.SizeMismatchCount}");
            foreach (var failure in result.Failures)
                text.AppendLine($"  ✗ {failure.RelativePath} — {PhoneImportErrors.FormatHResult(failure.HResult)} {failure.Kind} — {OneLine(failure.Message)}");
        }

        text.AppendLine();
        text.AppendLine(T("İlk hatalar:", "First errors:"));
        if (_errors.Count == 0)
            text.AppendLine("  " + T("yok", "none"));
        foreach (var error in _errors)
            text.AppendLine("  " + error);
        foreach (var line in ExtraLines)
            text.AppendLine(line);
        return text.ToString().TrimEnd();
    }

    public static string FormatBytes(long value)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = value;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }
        return unit == 0
            ? $"{value} B"
            : size.ToString(size >= 100 ? "N0" : "N1", CultureInfo.InvariantCulture) + " " + units[unit];
    }

    private static string OneLine(string? value) =>
        (value ?? string.Empty).Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Trim();
}
