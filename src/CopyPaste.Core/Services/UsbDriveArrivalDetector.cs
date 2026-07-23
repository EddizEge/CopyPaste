namespace CopyPaste.Core.Services;

public static class UsbDriveArrivalDetector
{
    public static IReadOnlyList<UsbDriveIdentity> FindArrivals(
        IEnumerable<string> previousVolumeIds,
        IEnumerable<UsbDriveIdentity> currentDrives)
    {
        ArgumentNullException.ThrowIfNull(previousVolumeIds);
        ArgumentNullException.ThrowIfNull(currentDrives);

        var previous = previousVolumeIds
            .Where(volumeId => !string.IsNullOrWhiteSpace(volumeId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return currentDrives
            .Where(drive => !string.IsNullOrWhiteSpace(drive.VolumeId)
                            && seen.Add(drive.VolumeId)
                            && !previous.Contains(drive.VolumeId))
            .ToArray();
    }
}
