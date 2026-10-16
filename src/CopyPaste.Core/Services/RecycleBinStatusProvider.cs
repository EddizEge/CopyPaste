using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using System.Text;
using CopyPaste.Core.Models;
using Microsoft.Win32;

namespace CopyPaste.Core.Services;

public interface IRecycleBinStatusProvider
{
    RecycleBinVolumeStatus GetStatus(string path);
}

// Reads the per-user Recycle Bin settings Explorer uses; the registry is opened read-only and never written.
public sealed class WindowsRecycleBinStatusProvider : IRecycleBinStatusProvider
{
    private const string VolumeSettingsKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\BitBucket\Volume\";
    private const string ExplorerPolicyKey = @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer";

    public static WindowsRecycleBinStatusProvider Instance { get; } = new();

    public RecycleBinVolumeStatus GetStatus(string path)
    {
        var volumeRoot = TryGetVolumeRoot(path);
        var volumeGuid = volumeRoot is null ? null : TryGetVolumeGuid(volumeRoot);
        bool? nukeOnDelete = null;
        long? maxCapacityBytes = null;
        if (volumeGuid is not null)
        {
            var nuke = ReadDword(Registry.CurrentUser, VolumeSettingsKey + volumeGuid, "NukeOnDelete");
            nukeOnDelete = nuke is null ? null : nuke == 1;
            var capacityMegabytes = ReadDword(Registry.CurrentUser, VolumeSettingsKey + volumeGuid, "MaxCapacity");
            maxCapacityBytes = capacityMegabytes is null ? null : (long)capacityMegabytes.Value * 1024 * 1024;
        }
        return new(ReadPolicy(), nukeOnDelete, maxCapacityBytes, TryGetRecycleDirectory(volumeRoot));
    }

    private static bool? ReadPolicy()
    {
        bool? disabled = false;
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            try
            {
                using var key = hive.OpenSubKey(ExplorerPolicyKey, writable: false);
                if (key?.GetValue("NoRecycleFiles") is int value && value == 1)
                    return true;
            }
            catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
            {
                disabled = null;
            }
        }
        return disabled;
    }

    private static uint? ReadDword(RegistryKey hive, string keyPath, string name)
    {
        try
        {
            using var key = hive.OpenSubKey(keyPath, writable: false);
            return key?.GetValue(name) is int value ? unchecked((uint)value) : null;
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }

    private static string? TryGetVolumeRoot(string path)
    {
        var buffer = new StringBuilder(1024);
        return GetVolumePathName(path, buffer, buffer.Capacity) ? buffer.ToString() : null;
    }

    private static string? TryGetVolumeGuid(string volumeRoot)
    {
        var buffer = new StringBuilder(64);
        if (!GetVolumeNameForVolumeMountPoint(Path.TrimEndingDirectorySeparator(volumeRoot) + Path.DirectorySeparatorChar,
                buffer, buffer.Capacity))
            return null;
        // "\\?\Volume{GUID}\" -> "{GUID}"
        var name = buffer.ToString();
        var start = name.IndexOf('{');
        var end = name.IndexOf('}');
        return start >= 0 && end > start ? name[start..(end + 1)] : null;
    }

    private static string? TryGetRecycleDirectory(string? volumeRoot)
    {
        if (volumeRoot is null)
            return null;
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return identity.User is { } sid ? Path.Combine(volumeRoot, "$Recycle.Bin", sid.Value) : null;
        }
        catch (SecurityException)
        {
            return null;
        }
    }

    [DllImport("kernel32.dll", EntryPoint = "GetVolumePathNameW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumePathName(string fileName, StringBuilder volumePathName, int bufferLength);

    [DllImport("kernel32.dll", EntryPoint = "GetVolumeNameForVolumeMountPointW", CharSet = CharSet.Unicode,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumeNameForVolumeMountPoint(string mountPoint, StringBuilder volumeName, int bufferLength);
}
