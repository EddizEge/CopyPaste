namespace CopyPaste.Core.Services;

public enum ShellLaunchMode
{
    Normal,
    Copy,
    Paste
}
public sealed record ShellLaunchRequest(
    ShellLaunchMode Mode,
    string? SourcePath,
    string? DestinationPath,
    string? Message,
    bool AutoStart);

public static class ShellLaunchRequestResolver
{
    public static ShellLaunchRequest Resolve(string[] arguments, ShellCopyStateStore stateStore)
    {
        if (arguments.Length >= 3 && arguments[1].Equals("--copy", StringComparison.OrdinalIgnoreCase))
        {
            var source = NormalizeExistingDirectory(arguments[2]);
            if (source is null)
                return new(ShellLaunchMode.Copy, null, null, "Seçilen kaynak klasör bulunamadı.", false);

            stateStore.SaveSource(source);
            return new(
                ShellLaunchMode.Copy,
                source,
                null,
                "Kaynak klasör hatırlandı. Hedef klasörde sağ tıklayıp ‘CopyPaste: Buraya yapıştır’ seçeneğini kullanın.",
                false);
        }

        if (arguments.Length >= 3 && arguments[1].Equals("--paste", StringComparison.OrdinalIgnoreCase))
        {
            var destination = NormalizeExistingDirectory(arguments[2]);
            var source = stateStore.LoadSource();
            if (destination is null)
                return new(ShellLaunchMode.Paste, source, null, "Seçilen hedef klasör bulunamadı.", false);
            if (source is null)
                return new(ShellLaunchMode.Paste, null, destination, "Önce bir klasörde ‘CopyPaste: Kopyala’ seçeneğini kullanın.", false);

            return new(
                ShellLaunchMode.Paste,
                source,
                destination,
                "Explorer yapıştırma isteği alındı; transfer otomatik başlatılıyor.",
                true);
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
