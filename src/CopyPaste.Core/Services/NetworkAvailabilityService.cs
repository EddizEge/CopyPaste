namespace CopyPaste.Core.Services;

public sealed record NetworkWaitProgress(
    TimeSpan Elapsed,
    TimeSpan Remaining,
    string Message,
    IReadOnlyList<string>? UnavailablePaths = null);

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
        CancellationToken cancellationToken = default,
        Func<TimeSpan, CancellationToken, Task>? waitAsync = null,
        Func<string, bool>? sourceAvailable = null,
        Func<string, bool>? destinationAvailable = null,
        Func<DateTimeOffset>? utcNow = null)
    {
        if (!IsNetworkPath(sourcePath) && !IsNetworkPath(destinationPath))
            return true;
        sourceAvailable ??= Directory.Exists;
        destinationAvailable ??= IsDestinationReachable;
        utcNow ??= static () => DateTimeOffset.UtcNow;
        var started = utcNow();
        while (utcNow() - started <= maximumWait)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var isSourceAvailable = sourceAvailable(sourcePath);
            var isDestinationAvailable = destinationAvailable(destinationPath);
            if (isSourceAvailable && isDestinationAvailable)
                return true;
            var elapsed = utcNow() - started;
            var unavailablePaths = new List<string>();
            if (!isSourceAvailable)
                unavailablePaths.Add(sourcePath);
            if (!isDestinationAvailable)
                unavailablePaths.Add(destinationPath);
            progress?.Report(new(elapsed, TimeSpan.FromTicks(Math.Max(0, (maximumWait - elapsed).Ticks)),
                "Ağ konumu kullanılamıyor; bağlantının geri gelmesi bekleniyor.", unavailablePaths));
            var delay = TimeSpan.FromSeconds(5);
            await (waitAsync is null
                    ? Task.Delay(delay, cancellationToken)
                    : waitAsync(delay, cancellationToken))
                .ConfigureAwait(false);
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
