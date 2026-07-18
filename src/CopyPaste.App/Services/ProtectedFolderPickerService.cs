using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

namespace CopyPaste.App.Services;

public static class ProtectedFolderPickerService
{
    public static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static bool LaunchElevatedSession(string? destinationPath)
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

    public static async Task<string?> PickAsync(CancellationToken cancellationToken = default)
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
            using var process = Process.Start(startInfo);
            if (process is null)
                return null;
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            if (!File.Exists(resultFile))
                return null;
            var path = (await File.ReadAllTextAsync(resultFile, cancellationToken).ConfigureAwait(false)).Trim();
            return Path.IsPathFullyQualified(path) ? path : null;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return null;
        }
        finally
        {
            try { File.Delete(resultFile); }
            catch (IOException) { }
        }
    }
}
