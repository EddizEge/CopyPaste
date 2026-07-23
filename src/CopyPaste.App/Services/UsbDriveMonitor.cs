using System.Runtime.InteropServices;
using CopyPaste.Core.Services;

namespace CopyPaste.App.Services;

public sealed class UsbDriveMonitor : IDisposable
{
    private readonly Action<UsbDriveIdentity> _driveArrived;
    private readonly object _gate = new();
    private HashSet<string> _knownVolumeIds = new(StringComparer.OrdinalIgnoreCase);
    private Timer? _timer;
    private bool _polling;

    public UsbDriveMonitor(Action<UsbDriveIdentity> driveArrived)
    {
        _driveArrived = driveArrived;
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_timer is not null)
                return;
            _knownVolumeIds = GetConnectedDrives()
                .Select(drive => drive.VolumeId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            _timer = new Timer(Poll, null, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3));
        }
    }

    public static IReadOnlyList<UsbDriveIdentity> GetConnectedDrives()
    {
        var result = new List<UsbDriveIdentity>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady || drive.DriveType is not (DriveType.Removable or DriveType.Fixed))
                    continue;
                if (!TryGetVolumeSerial(drive.RootDirectory.FullName, out var serial))
                    continue;
                result.Add(new UsbDriveIdentity(
                    drive.RootDirectory.FullName,
                    serial.ToString("X8"),
                    drive.VolumeLabel));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A drive can disappear while Windows is enumerating it.
            }
        }
        return result;
    }

    private void Poll(object? state)
    {
        lock (_gate)
        {
            if (_polling)
                return;
            _polling = true;
        }
        try
        {
            var drives = GetConnectedDrives();
            HashSet<string> previous;
            lock (_gate)
            {
                previous = _knownVolumeIds;
                _knownVolumeIds = drives.Select(drive => drive.VolumeId)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
            foreach (var drive in UsbDriveArrivalDetector.FindArrivals(previous, drives))
                _driveArrived(drive);
        }
        finally
        {
            lock (_gate)
                _polling = false;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _timer?.Dispose();
            _timer = null;
        }
    }

    private static bool TryGetVolumeSerial(string rootPath, out uint serialNumber) =>
        GetVolumeInformation(
            rootPath, null, 0, out serialNumber, out _, out _, null, 0);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumeInformation(
        string rootPathName,
        char[]? volumeNameBuffer,
        int volumeNameSize,
        out uint volumeSerialNumber,
        out uint maximumComponentLength,
        out uint fileSystemFlags,
        char[]? fileSystemNameBuffer,
        int fileSystemNameSize);
}
