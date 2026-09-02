using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using CopyPaste.Core.Models;

namespace CopyPaste.Core.Services;

public sealed class DeletionPlanner
{
    private readonly string _windowsDirectory;
    private readonly string _applicationDirectory;

    public DeletionPlanner(string? windowsDirectory = null, string? applicationDirectory = null)
    {
        _windowsDirectory = NormalizeDirectory(windowsDirectory
            ?? Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        _applicationDirectory = NormalizeDirectory(applicationDirectory ?? AppContext.BaseDirectory);
    }

    public Task<DeletionPlan> CreateAsync(
        IEnumerable<string> paths,
        DeletionMode mode,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Create(paths, mode, cancellationToken), cancellationToken);

    public DeletionPlan Create(
        IEnumerable<string> paths,
        DeletionMode mode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var requested = new List<string>();
        var issues = new List<DeletionIssue>();
        foreach (var candidate in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryNormalize(candidate, out var normalized, out var message))
            {
                issues.Add(new(candidate ?? string.Empty, DeletionIssueCode.InvalidPath, message));
                continue;
            }
            if (!requested.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                requested.Add(normalized);
        }

        requested = requested
            .Where(path => !requested.Any(other => !PathEquals(path, other) && IsWithin(path, other)))
            .ToList();

        var entries = new List<DeletionEntry>();
        foreach (var path in requested)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryGetProtectedReason(path, out var protectedCode, out var protectedMessage))
            {
                issues.Add(new(path, protectedCode, protectedMessage));
                continue;
            }
            Enumerate(path, entries, issues, cancellationToken);
        }

        if (mode == DeletionMode.SecureErase)
            ValidateSecureErase(entries, issues);
        else if (mode == DeletionMode.RecycleBin)
            ValidateRecycleBin(requested, issues);

        var files = entries.Where(entry => entry.Kind == DeletionEntryKind.File).ToArray();
        var warnings = BuildWarnings(mode);
        return new(
            mode,
            requested,
            entries,
            issues,
            files.Sum(entry => entry.Length),
            files.Length,
            entries.Count(entry => entry.Kind != DeletionEntryKind.File),
            warnings);
    }

    public bool IsEntryUnchanged(DeletionEntry entry, out DeletionIssue? issue)
    {
        issue = null;
        if (!TryNormalize(entry.Path, out var normalized, out var message))
        {
            issue = new(entry.Path, DeletionIssueCode.ChangedAfterPreview, message);
            return false;
        }
        if (TryGetProtectedReason(normalized, out _, out var protectedMessage))
        {
            issue = new(normalized, DeletionIssueCode.ChangedAfterPreview, protectedMessage);
            return false;
        }
        try
        {
            var attributes = File.GetAttributes(normalized);
            var kind = GetKind(attributes);
            var length = kind == DeletionEntryKind.File ? new FileInfo(normalized).Length : 0;
            var lastWrite = kind == DeletionEntryKind.Directory
                ? new DirectoryInfo(normalized).LastWriteTimeUtc
                : new FileInfo(normalized).LastWriteTimeUtc;
            var fileChanged = kind == DeletionEntryKind.File
                && (attributes != entry.Attributes || length != entry.Length
                    || new DateTimeOffset(lastWrite, TimeSpan.Zero) != entry.LastWriteTimeUtc);
            if (kind != entry.Kind || fileChanged)
            {
                issue = new(normalized, DeletionIssueCode.ChangedAfterPreview,
                    "The item changed after the deletion preview.");
                return false;
            }
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            issue = new(normalized, DeletionIssueCode.ChangedAfterPreview, ex.Message);
            return false;
        }
    }

    private void Enumerate(
        string path,
        ICollection<DeletionEntry> entries,
        ICollection<DeletionIssue> issues,
        CancellationToken cancellationToken)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            var kind = GetKind(attributes);
            if (kind == DeletionEntryKind.Directory)
            {
                foreach (var child in Directory.EnumerateFileSystemEntries(path))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (TryGetProtectedReason(child, out var code, out var message))
                    {
                        issues.Add(new(child, code, message));
                        continue;
                    }
                    Enumerate(child, entries, issues, cancellationToken);
                }
            }

            var info = kind == DeletionEntryKind.Directory
                ? (FileSystemInfo)new DirectoryInfo(path)
                : new FileInfo(path);
            var length = kind == DeletionEntryKind.File ? ((FileInfo)info).Length : 0;
            entries.Add(new(path, kind, length, attributes,
                new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero)));
        }
        catch (FileNotFoundException ex)
        {
            issues.Add(new(path, DeletionIssueCode.Missing, ex.Message));
        }
        catch (DirectoryNotFoundException ex)
        {
            issues.Add(new(path, DeletionIssueCode.Missing, ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            issues.Add(new(path, DeletionIssueCode.AccessDenied, ex.Message));
        }
        catch (IOException ex)
        {
            issues.Add(new(path, DeletionIssueCode.EnumerationFailed, ex.Message));
        }
    }

    private static void ValidateSecureErase(
        IEnumerable<DeletionEntry> entries,
        ICollection<DeletionIssue> issues)
    {
        foreach (var entry in entries.Where(entry => entry.Kind != DeletionEntryKind.Directory))
        {
            if (entry.Kind == DeletionEntryKind.ReparsePoint)
            {
                issues.Add(new(entry.Path, DeletionIssueCode.ReparsePoint,
                    "Secure erasure does not follow or overwrite reparse points."));
                continue;
            }
            if ((entry.Attributes & (FileAttributes.SparseFile | FileAttributes.Compressed)) != 0)
            {
                issues.Add(new(entry.Path, DeletionIssueCode.UnsupportedStorage,
                    "Sparse and compressed files cannot be securely overwritten by this operation."));
                continue;
            }
            if (!TryGetDrive(entry.Path, out var drive)
                || drive.DriveType != DriveType.Fixed
                || !drive.IsReady
                || !drive.DriveFormat.Equals("NTFS", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new(entry.Path, DeletionIssueCode.UnsupportedStorage,
                    "Secure erasure is limited to regular files on local fixed NTFS volumes."));
                continue;
            }
            if (GetHardLinkCount(entry.Path) is > 1)
                issues.Add(new(entry.Path, DeletionIssueCode.MultipleHardLinks,
                    "The file has multiple hard links; overwriting it would alter another path."));
        }
    }

    private static void ValidateRecycleBin(
        IEnumerable<string> paths,
        ICollection<DeletionIssue> issues)
    {
        foreach (var path in paths)
        {
            if (!TryGetDrive(path, out var drive) || drive.DriveType != DriveType.Fixed || !drive.IsReady)
                issues.Add(new(path, DeletionIssueCode.UnsupportedStorage,
                    "Recycle Bin deletion is limited to ready local fixed volumes."));
        }
    }

    private bool TryGetProtectedReason(
        string path,
        out DeletionIssueCode code,
        out string message)
    {
        var root = Path.GetPathRoot(path);
        if (!string.IsNullOrWhiteSpace(root) && PathEquals(path, root))
        {
            code = DeletionIssueCode.VolumeRoot;
            message = "Volume roots cannot be deleted.";
            return true;
        }
        if (IsWithinOrEqual(path, _windowsDirectory))
        {
            code = DeletionIssueCode.ProtectedPath;
            message = "Windows and system directories are protected from this utility.";
            return true;
        }
        if (IsWithinOrEqual(path, _applicationDirectory))
        {
            code = DeletionIssueCode.ProtectedPath;
            message = "The running CopyPaste application directory is protected from this utility.";
            return true;
        }
        code = default;
        message = string.Empty;
        return false;
    }

    private static DeletionEntryKind GetKind(FileAttributes attributes)
    {
        if ((attributes & FileAttributes.ReparsePoint) != 0)
            return DeletionEntryKind.ReparsePoint;
        return (attributes & FileAttributes.Directory) != 0
            ? DeletionEntryKind.Directory
            : DeletionEntryKind.File;
    }

    private static IReadOnlyList<string> BuildWarnings(DeletionMode mode) => mode switch
    {
        DeletionMode.RecycleBin =>
        ["Items are expected to be recoverable from the Recycle Bin until it is emptied."],
        DeletionMode.Permanent =>
        ["Permanent deletion bypasses the Recycle Bin and cannot be undone by CopyPaste."],
        DeletionMode.SecureErase =>
        [
            "Secure erasure overwrites eligible file content before deletion and cannot be canceled safely once a file starts changing.",
            "Copies, backups, snapshots, SSD wear-leveling, TRIM, controller caches, and remapped sectors can retain recoverable data; no universal guarantee is possible."
        ],
        _ => []
    };

    private static bool TryNormalize(string? path, out string normalized, out string message)
    {
        normalized = string.Empty;
        message = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            message = "A deletion path is required.";
            return false;
        }
        try
        {
            var candidate = path.Trim().Trim('"');
            if (!Path.IsPathFullyQualified(candidate))
            {
                message = "Deletion paths must be fully qualified.";
                return false;
            }
            normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            message = ex.Message;
            return false;
        }
    }

    private static string NormalizeDirectory(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static bool PathEquals(string left, string right) =>
        string.Equals(Path.TrimEndingDirectorySeparator(left),
            Path.TrimEndingDirectorySeparator(right), StringComparison.OrdinalIgnoreCase);

    private static bool IsWithinOrEqual(string path, string parent) =>
        PathEquals(path, parent) || IsWithin(path, parent);

    private static bool IsWithin(string path, string parent)
    {
        var prefix = Path.TrimEndingDirectorySeparator(parent) + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetDrive(string path, out DriveInfo drive)
    {
        drive = null!;
        try
        {
            var root = Path.GetPathRoot(path);
            if (string.IsNullOrWhiteSpace(root) || root.StartsWith("\\\\", StringComparison.Ordinal))
                return false;
            drive = new DriveInfo(root);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static uint? GetHardLinkCount(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            return GetFileInformationByHandle(stream.SafeFileHandle, out var info)
                ? info.NumberOfLinks
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle handle,
        out ByHandleFileInformation information);

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }
}
