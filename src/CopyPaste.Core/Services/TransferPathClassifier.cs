namespace CopyPaste.Core.Services;

public enum TransferPathKind
{
    Empty,
    FileSystem,
    PortableDevice,
    ShellNamespace,
    NotRooted
}

/// <summary>
/// Classifies typed, picked, or dropped paths before they reach Robocopy. Phones and other
/// WPD/MTP/PTP devices appear only inside the Windows shell namespace and have no file-system
/// path that Robocopy or the Win32 file APIs can open.
/// </summary>
public static class TransferPathClassifier
{
    // Device interface class used by Windows Portable Devices (GUID_DEVINTERFACE_WPD).
    private const string WpdInterfaceGuid = "{6ac27878-a6fa-4155-ba85-f98f491d4f33}";

    private static readonly string[] PortableDeviceMarkers =
    [
        @"\\?\usb#",
        @"\\?\swd#",
        @"\\.\usb#",
        @"\\.\swd#",
        "wpdbusenum",
        WpdInterfaceGuid
    ];

    private static readonly string[] ShellDisplayRoots =
    [
        "Bu bilgisayar",
        "This PC"
    ];

    public static TransferPathKind Classify(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return TransferPathKind.Empty;

        var value = path.Trim().Trim('"').Trim();
        if (value.Length == 0)
            return TransferPathKind.Empty;

        if (ContainsPortableDeviceMarker(value))
            return TransferPathKind.PortableDevice;

        if (value.StartsWith("::", StringComparison.Ordinal)
            || value.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
            return TransferPathKind.ShellNamespace;

        if (TryClassifyShellDisplayPath(value, out var displayKind))
            return displayKind;

        return Path.IsPathFullyQualified(value)
            ? TransferPathKind.FileSystem
            : TransferPathKind.NotRooted;
    }

    public static bool IsFileSystem(string? path) => Classify(path) == TransferPathKind.FileSystem;

    private static bool ContainsPortableDeviceMarker(string value) =>
        PortableDeviceMarkers.Any(marker => value.Contains(marker, StringComparison.OrdinalIgnoreCase));

    // Explorer's address bar can show "This PC\Apple iPhone\Internal Storage\DCIM". A drive under
    // the same root ("This PC\Local Disk (C:)") is not a phone, but it is still not a usable path.
    private static bool TryClassifyShellDisplayPath(string value, out TransferPathKind kind)
    {
        kind = TransferPathKind.FileSystem;
        var segments = value.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || !ShellDisplayRoots.Any(root =>
                string.Equals(segments[0].Trim(), root, StringComparison.OrdinalIgnoreCase)))
            return false;

        kind = segments.Length > 1 && !LooksLikeDriveDisplayName(segments[1])
            ? TransferPathKind.PortableDevice
            : TransferPathKind.ShellNamespace;
        return true;
    }

    private static bool LooksLikeDriveDisplayName(string segment)
    {
        var trimmed = segment.Trim();
        return trimmed.Length >= 4
               && trimmed.EndsWith(":)", StringComparison.Ordinal)
               && trimmed[^4] == '('
               && char.IsAsciiLetter(trimmed[^3]);
    }
}
