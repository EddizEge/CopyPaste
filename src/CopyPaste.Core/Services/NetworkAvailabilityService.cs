namespace CopyPaste.Core.Services;

public sealed record NetworkWaitProgress(TimeSpan Elapsed, TimeSpan Remaining, string Message);

public sealed class NetworkAvailabilityService
{
    public static bool IsNetworkPath(string path)
    {
        if (path.StartsWith(@"\\", StringComparison.Ordinal))
            return true;
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            return !string.IsNullOrWhiteSpace(root) && new DriveInfo(root).DriveType == DriveType.Network;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public async Task<bool> WaitForAvailabilityAsync(
        string sourcePath,
        string destinationPath,
        TimeSpan maximumWait,
        IProgress<NetworkWaitProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsNetworkPath(sourcePath) && !IsNetworkPath(destinationPath))
            return true;
        var started = DateTimeOffset.UtcNow;
        while (DateTimeOffset.UtcNow - started <= maximumWait)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Directory.Exists(sourcePath) && IsDestinationReachable(destinationPath))
                return true;
            var elapsed = DateTimeOffset.UtcNow - started;
            progress?.Report(new(elapsed, maximumWait - elapsed,
                "Ağ konumu kullanılamıyor; bağlantının geri gelmesi bekleniyor."));
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
        }
        return false;
    }

    private static bool IsDestinationReachable(string destinationPath)
    {
        var current = Path.GetFullPath(destinationPath);
        while (!Directory.Exists(current))
        {
            var parent = Directory.GetParent(current);
            if (parent is null)
                return false;
            current = parent.FullName;
        }
        return true;
    }
}
