using CopyPaste.Core.Models;

namespace CopyPaste.Core.Services;

/// <summary>
/// Read-only access to phones and other WPD (MTP/PTP) devices. Implementations list devices and folders and open
/// files for reading; none of them may create, rename, change, or delete anything on the device.
/// </summary>
public interface IPortableDeviceSource
{
    /// <summary>Short, stable backend name shown in results and diagnostics ("WinRT", "Shell").</summary>
    string BackendName { get; }

    Task<IReadOnlyList<PortableDeviceInfo>> GetDevicesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<PortableDeviceEntry>> GetChildrenAsync(PortableDeviceEntry folder, CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(PortableDeviceEntry file, CancellationToken cancellationToken);
}
