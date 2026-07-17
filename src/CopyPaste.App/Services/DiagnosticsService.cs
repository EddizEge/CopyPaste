using System.IO.Compression;
using System.Reflection;
using System.Text;

namespace CopyPaste.App.Services;

public sealed class DiagnosticsService
{
    private readonly string _root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CopyPaste");

    public bool HasCrashReport => File.Exists(Path.Combine(_root, "crash.log"));

    public async Task<string> CreatePackageAsync(CancellationToken cancellationToken = default)
    {
        var output = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            $"CopyPaste-Diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
        var temporary = Path.Combine(Path.GetTempPath(), "CopyPasteDiagnostics", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
            var systemInfo = $"CopyPaste: {version}{Environment.NewLine}" +
                             $"Windows: {Environment.OSVersion}{Environment.NewLine}" +
                             $"64-bit OS: {Environment.Is64BitOperatingSystem}{Environment.NewLine}" +
                             $"Generated: {DateTimeOffset.Now:O}{Environment.NewLine}";
            await File.WriteAllTextAsync(Path.Combine(temporary, "system.txt"), systemInfo,
                new UTF8Encoding(false), cancellationToken);
            foreach (var fileName in new[] { "crash.log", "settings.json", "queue.json" })
            {
                var source = Path.Combine(_root, fileName);
                if (File.Exists(source))
                    File.Copy(source, Path.Combine(temporary, fileName), overwrite: true);
            }
            var logs = Path.Combine(_root, "Logs");
            if (Directory.Exists(logs))
            {
                var targetLogs = Directory.CreateDirectory(Path.Combine(temporary, "Logs")).FullName;
                foreach (var log in Directory.EnumerateFiles(logs, "*.log")
                             .OrderByDescending(File.GetLastWriteTimeUtc).Take(3))
                    File.Copy(log, Path.Combine(targetLogs, Path.GetFileName(log)), overwrite: true);
            }
            if (File.Exists(output))
                File.Delete(output);
            ZipFile.CreateFromDirectory(temporary, output, CompressionLevel.Optimal, includeBaseDirectory: false);
            return output;
        }
        finally
        {
            if (Directory.Exists(temporary))
                Directory.Delete(temporary, recursive: true);
        }
    }
}
