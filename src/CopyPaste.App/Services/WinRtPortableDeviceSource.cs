using CopyPaste.Core.Models;
using CopyPaste.Core.Services;
using Windows.Devices.Enumeration;
using Windows.Devices.Portable;
using Windows.Storage;

namespace CopyPaste.App.Services;

/// <summary>
/// Primary device backend: Windows.Devices.Portable.StorageDevice. A thin read-only adapter; planning, skipping,
/// verification, and error rules live in the Core import engine. Devices whose storage has a file-system path
/// (USB sticks, card readers) are left out because the normal Robocopy transfer already handles them.
/// </summary>
public sealed class WinRtPortableDeviceSource : IPortableDeviceSource
{
    public const string Name = "WinRT";
    private const uint PageSize = 1000;

    public string BackendName => Name;

    public int SkippedDrivesWithPath { get; private set; }

    public async Task<IReadOnlyList<PortableDeviceInfo>> GetDevicesAsync(CancellationToken cancellationToken)
    {
        var interfaces = await DeviceInformation.FindAllAsync(StorageDevice.GetDeviceSelector())
            .AsTask(cancellationToken).ConfigureAwait(false);
        var devices = new List<PortableDeviceInfo>();
        var skipped = 0;
        foreach (var device in interfaces)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var folder = StorageDevice.FromId(device.Id);
                if (!string.IsNullOrEmpty(folder.Path))
                {
                    skipped++;
                    continue;
                }
                var name = string.IsNullOrWhiteSpace(device.Name) ? folder.DisplayName : device.Name;
                devices.Add(new PortableDeviceInfo(name, device.Id, this, CreateFolderEntry(folder, name)));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                devices.Add(new PortableDeviceInfo(device.Name, device.Id, this, null, ex.HResult, ex.Message));
            }
        }
        SkippedDrivesWithPath = skipped;
        return devices;
    }

    /// <summary>A folder chosen in the Windows folder picker that has no file-system path (a phone folder).</summary>
    public PortableDeviceInfo CreatePickedFolderDevice(StorageFolder folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        var name = string.IsNullOrWhiteSpace(folder.DisplayName) ? folder.Name : folder.DisplayName;
        return new PortableDeviceInfo(name, "picker:" + folder.Name, this, CreateFolderEntry(folder, name));
    }

    public static PortableDeviceEntry CreateFolderEntry(StorageFolder folder, string? name = null) =>
        new(name ?? folder.Name, true, null, null, folder.DateCreated, folder);

    public async Task<IReadOnlyList<PortableDeviceEntry>> GetChildrenAsync(
        PortableDeviceEntry folder,
        CancellationToken cancellationToken)
    {
        var storageFolder = Own<StorageFolder>(folder);
        var entries = new List<PortableDeviceEntry>();
        for (uint start = 0; ; start += PageSize)
        {
            var page = await storageFolder.GetItemsAsync(start, PageSize).AsTask(cancellationToken).ConfigureAwait(false);
            foreach (var item in page)
            {
                cancellationToken.ThrowIfCancellationRequested();
                switch (item)
                {
                    case StorageFolder child:
                        entries.Add(CreateFolderEntry(child));
                        break;
                    case StorageFile file:
                        entries.Add(await CreateFileEntryAsync(file, cancellationToken).ConfigureAwait(false));
                        break;
                }
            }
            if (page.Count < PageSize)
                break;
        }
        return entries;
    }

    public async Task<Stream> OpenReadAsync(PortableDeviceEntry file, CancellationToken cancellationToken) =>
        await Own<StorageFile>(file).OpenStreamForReadAsync().WaitAsync(cancellationToken).ConfigureAwait(false);

    // StorageFile.Name keeps the extension; DisplayName would drop it.
    private static async Task<PortableDeviceEntry> CreateFileEntryAsync(StorageFile file, CancellationToken cancellationToken)
    {
        long? size = null;
        DateTimeOffset? modified = null;
        try
        {
            var properties = await file.GetBasicPropertiesAsync().AsTask(cancellationToken).ConfigureAwait(false);
            size = checked((long)properties.Size);
            modified = properties.DateModified;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Size and date stay unknown; the file is still listed and copied.
        }
        return new PortableDeviceEntry(file.Name, false, size, modified, file.DateCreated, file);
    }

    private static T Own<T>(PortableDeviceEntry entry) where T : class =>
        entry.Handle as T
        ?? throw new ArgumentException("The entry does not belong to the WinRT device source.", nameof(entry));
}
