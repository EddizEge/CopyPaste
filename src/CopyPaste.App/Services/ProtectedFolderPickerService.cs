using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using CopyPaste.Core.Services;

namespace CopyPaste.App.Services;

public static class ProtectedFolderPickerService
{
    public static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static bool LaunchElevatedSession(string? destinationPath, string? language = null)
    {
        var resultFile = Path.Combine(Path.GetTempPath(), $"CopyPaste-picker-{Guid.NewGuid():N}.txt");
        try
        {
            var executable = Environment.ProcessPath
                ?? throw new InvalidOperationException("Uygulama yolu belirlenemedi.");
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = true,
                Verb = "runas"
            };
            startInfo.ArgumentList.Add("--protected-session");
            startInfo.ArgumentList.Add(resultFile);
            if (!string.IsNullOrWhiteSpace(language))
            {
                startInfo.ArgumentList.Add("--language");
                startInfo.ArgumentList.Add(language);
            }
            if (!string.IsNullOrWhiteSpace(destinationPath))
            {
                startInfo.ArgumentList.Add("--destination");
                startInfo.ArgumentList.Add(destinationPath.Trim());
            }
            return Process.Start(startInfo) is not null;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            try { File.Delete(resultFile); }
            catch (IOException) { }
            return false;
        }
    }

    public static async Task<IReadOnlyList<string>> PickManyAsync(
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        var resultFile = Path.Combine(Path.GetTempPath(), $"CopyPaste-picker-{Guid.NewGuid():N}.txt");
        try
        {
            var executable = Environment.ProcessPath
                ?? throw new InvalidOperationException("Uygulama yolu belirlenemedi.");
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = true,
                Verb = "runas"
            };
            startInfo.ArgumentList.Add("--protected-folder-picker");
            startInfo.ArgumentList.Add(resultFile);
            if (!string.IsNullOrWhiteSpace(language))
            {
                startInfo.ArgumentList.Add("--language");
                startInfo.ArgumentList.Add(language);
            }
            using var process = Process.Start(startInfo);
            if (process is null)
                return [];
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return await ReadResultAsync(resultFile, cancellationToken).ConfigureAwait(false);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return [];
        }
        finally
        {
            try { File.Delete(resultFile); }
            catch (IOException) { }
        }
    }

    public static async Task<IReadOnlyList<string>> ReadResultAsync(
        string resultFile,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(resultFile))
            return [];
        var content = (await File.ReadAllTextAsync(resultFile, cancellationToken).ConfigureAwait(false)).Trim();
        if (string.IsNullOrWhiteSpace(content))
            return [];
        return ProtectedFolderSelectionSerializer.Parse(content);
    }
}
