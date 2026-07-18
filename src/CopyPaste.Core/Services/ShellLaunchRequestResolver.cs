namespace CopyPaste.Core.Services;

public enum ShellLaunchMode
{
    Normal,
    Copy,
    Paste,
    Scheduled
}
public sealed record ShellLaunchRequest(
    ShellLaunchMode Mode,
    string? SourcePath,
    string? DestinationPath,
    string? Message,
    bool AutoStart,
    Guid? ScheduleId = null,
    CopyPaste.Core.Models.CopyJob? ScheduledJob = null,
    IReadOnlyList<string>? SourcePaths = null,
    bool UseBackupMode = false);

public static class ShellLaunchRequestResolver
{
    public static ShellLaunchRequest Resolve(string[] arguments, ShellCopyStateStore stateStore)
    {
        if (arguments.Length >= 3
            && arguments[1].Equals("--schedule", StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(arguments[2], out var scheduleId))
        {
            return new(ShellLaunchMode.Scheduled, null, null,
                "Zamanlanmış transfer hazırlanıyor.", true, scheduleId);
        }
        if (arguments.Length >= 3 && arguments[1].Equals("--copy", StringComparison.OrdinalIgnoreCase))
        {
            var sources = arguments.Skip(2).Select(NormalizeExistingDirectory)
                .Where(path => path is not null)
                .Select(path => path!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (sources.Count == 0)
                return new(ShellLaunchMode.Copy, null, null, "Seçilen kaynak klasör bulunamadı.", false);

            stateStore.SaveSources(sources);
            return new(
                ShellLaunchMode.Copy,
                sources[0],
                null,
                sources.Count == 1
                    ? "Kaynak klasör hatırlandı. Hedef klasörde sağ tıklayıp ‘CopyPaste: Buraya yapıştır’ seçeneğini kullanın."
                    : $"{sources.Count} kaynak klasör hatırlandı. Hedef klasörde CopyPaste ile yapıştırabilirsiniz.",
                false,
                SourcePaths: sources);
        }

        if (arguments.Length >= 3 && arguments[1].Equals("--paste", StringComparison.OrdinalIgnoreCase))
        {
            var destination = NormalizeExistingDirectory(arguments[2]);
            var sources = stateStore.LoadSources();
            var source = sources.FirstOrDefault();
            if (destination is null)
                return new(ShellLaunchMode.Paste, source, null, "Seçilen hedef klasör bulunamadı.", false);
            if (source is null)
                return new(ShellLaunchMode.Paste, null, destination, "Önce bir klasörde ‘CopyPaste: Kopyala’ seçeneğini kullanın.", false);

            return new(
                ShellLaunchMode.Paste,
                source,
                destination,
                "Explorer yapıştırma isteği alındı; transfer otomatik başlatılıyor.",
                true,
                SourcePaths: sources);
        }

        var normalSource = StartupPathResolver.Resolve(arguments);
        return new(ShellLaunchMode.Normal, normalSource, null, null, false);
    }

    private static string? NormalizeExistingDirectory(string path)
    {
        var normalized = path.Trim().Trim('"');
        return Directory.Exists(normalized) ? Path.GetFullPath(normalized) : null;
    }
}
