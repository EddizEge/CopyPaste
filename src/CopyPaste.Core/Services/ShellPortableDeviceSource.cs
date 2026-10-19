using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using CopyPaste.Core.Models;
using ComFileTime = System.Runtime.InteropServices.ComTypes.FILETIME;

namespace CopyPaste.Core.Services;

/// <summary>
/// Fallback device backend through the Windows shell namespace ("This PC" → device → storage → folders), the same
/// view File Explorer uses. Shell items are apartment-bound, so every COM call, including stream reads, runs on one
/// dedicated STA thread owned by this instance. The backend only enumerates, reads properties, and opens read
/// streams; it never binds a handler that can modify the device. Because the same calls work on ordinary folders,
/// tests exercise it against local temporary directories.
/// </summary>
public sealed class ShellPortableDeviceSource : IPortableDeviceSource, IDisposable
{
    public const string Name = "Shell";

    private const uint SigdnNormalDisplay = 0;
    private const uint SigdnDesktopAbsoluteParsing = 0x80028000;
    private const uint SfgaoStream = 0x00400000;
    private const uint SfgaoFolder = 0x20000000;
    private const uint SfgaoFileSystem = 0x40000000;
    private const int ReadChunkSize = 1024 * 1024;

    private static readonly Guid ComputerFolderId = new("0AC0837C-BBF8-452A-850D-79D08E667CA7");
    private static readonly Guid EnumItemsHandlerId = new("94F60519-2850-4924-AA5A-D15E84868039");
    private static readonly Guid StreamHandlerId = new("1CEBB3AB-7C10-499A-A417-92CA16C4CB83");
    private static readonly Guid ShellItemInterfaceId = new("43826D1E-E718-42EE-BC55-A1E261C37BFE");
    private static readonly Guid EnumShellItemsInterfaceId = new("70629033-E363-4A28-A567-0DB78006E6D7");
    private static readonly Guid StreamInterfaceId = new("0000000C-0000-0000-C000-000000000046");
    private static readonly PropertyKey FileNameKey = new(new Guid("41CF5AE0-F75A-4806-BD87-59C7D9248EB9"), 100);
    private static readonly PropertyKey SizeKey = new(new Guid("B725F130-47EF-101A-A5F1-02608C9EEBAC"), 12);
    private static readonly PropertyKey DateModifiedKey = new(new Guid("B725F130-47EF-101A-A5F1-02608C9EEBAC"), 14);
    private static readonly PropertyKey DateCreatedKey = new(new Guid("B725F130-47EF-101A-A5F1-02608C9EEBAC"), 15);

    private readonly StaThread _sta = new("CopyPaste phone import (shell)");
    // Touched only on the STA thread; released there when the source is disposed.
    private readonly List<object> _comObjects = [];
    private bool _disposed;

    public string BackendName => Name;

    public Task<IReadOnlyList<PortableDeviceInfo>> GetDevicesAsync(CancellationToken cancellationToken) =>
        _sta.InvokeAsync<IReadOnlyList<PortableDeviceInfo>>(() =>
        {
            var folderId = ComputerFolderId;
            var interfaceId = ShellItemInterfaceId;
            ThrowOnFailure(SHGetKnownFolderItem(ref folderId, 0, IntPtr.Zero, ref interfaceId, out var computerObject));
            var computer = Track((IShellItem)computerObject);
            var devices = new List<PortableDeviceInfo>();
            foreach (var child in EnumerateChildren(computer, cancellationToken))
            {
                var parsingName = GetDisplayName(child, SigdnDesktopAbsoluteParsing);
                if ((GetAttributes(child, SfgaoFileSystem) & SfgaoFileSystem) != 0
                    || TransferPathClassifier.Classify(parsingName) != TransferPathKind.PortableDevice)
                    continue;
                var name = GetDisplayName(child, SigdnNormalDisplay);
                devices.Add(new PortableDeviceInfo(name, parsingName, this,
                    new PortableDeviceEntry(name, true, null, null, null, new ShellHandle(this, child))));
            }
            return devices;
        }, cancellationToken);

    /// <summary>Opens a shell parsing name (a device path or an ordinary folder path) as a folder entry.</summary>
    public Task<PortableDeviceEntry> GetFolderAsync(string parsingName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parsingName);
        return _sta.InvokeAsync(() =>
        {
            var interfaceId = ShellItemInterfaceId;
            ThrowOnFailure(SHCreateItemFromParsingName(parsingName, IntPtr.Zero, ref interfaceId, out var itemObject));
            var item = Track((IShellItem)itemObject);
            return new PortableDeviceEntry(GetDisplayName(item, SigdnNormalDisplay), true, null,
                GetDate(item, DateModifiedKey), GetDate(item, DateCreatedKey), new ShellHandle(this, item));
        }, cancellationToken);
    }

    public Task<IReadOnlyList<PortableDeviceEntry>> GetChildrenAsync(
        PortableDeviceEntry folder,
        CancellationToken cancellationToken)
    {
        var parent = OwnItem(folder);
        return _sta.InvokeAsync<IReadOnlyList<PortableDeviceEntry>>(() =>
        {
            var entries = new List<PortableDeviceEntry>();
            foreach (var child in EnumerateChildren(parent, cancellationToken))
            {
                var attributes = GetAttributes(child, SfgaoFolder | SfgaoStream);
                // Zip files and similar containers are both folder and stream; they are copied as files.
                var isFolder = (attributes & SfgaoFolder) != 0 && (attributes & SfgaoStream) == 0;
                long? size = isFolder ? null : GetSize(child);
                entries.Add(new PortableDeviceEntry(GetFileName(child), isFolder, size,
                    GetDate(child, DateModifiedKey), GetDate(child, DateCreatedKey), new ShellHandle(this, child)));
            }
            return entries;
        }, cancellationToken);
    }

    public Task<Stream> OpenReadAsync(PortableDeviceEntry file, CancellationToken cancellationToken)
    {
        var item = OwnItem(file);
        return _sta.InvokeAsync<Stream>(() =>
        {
            var handlerId = StreamHandlerId;
            var interfaceId = StreamInterfaceId;
            ThrowOnFailure(item.BindToHandler(IntPtr.Zero, ref handlerId, ref interfaceId, out var streamObject));
            return new ShellReadStream(this, Track((ISequentialStreamRead)streamObject));
        }, cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _sta.Post(() =>
        {
            foreach (var comObject in _comObjects)
            {
                try
                {
                    Marshal.FinalReleaseComObject(comObject);
                }
                catch (ArgumentException)
                {
                }
            }
            _comObjects.Clear();
        });
        _sta.Dispose();
    }

    private IShellItem OwnItem(PortableDeviceEntry entry)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return entry.Handle is ShellHandle handle && ReferenceEquals(handle.Owner, this)
            ? handle.Item
            : throw new ArgumentException("The entry does not belong to this shell device source.", nameof(entry));
    }

    private T Track<T>(T comObject) where T : class
    {
        _comObjects.Add(comObject);
        return comObject;
    }

    private IEnumerable<IShellItem> EnumerateChildren(IShellItem parent, CancellationToken cancellationToken)
    {
        var handlerId = EnumItemsHandlerId;
        var interfaceId = EnumShellItemsInterfaceId;
        ThrowOnFailure(parent.BindToHandler(IntPtr.Zero, ref handlerId, ref interfaceId, out var enumeratorObject));
        var enumerator = (IEnumShellItems)enumeratorObject;
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var hresult = enumerator.Next(1, out var child, out var fetched);
                ThrowOnFailure(hresult);
                if (hresult != 0 || fetched == 0 || child is null)
                    yield break;
                yield return Track(child);
            }
        }
        finally
        {
            Marshal.FinalReleaseComObject(enumerator);
        }
    }

    // PKEY_FileName keeps the extension even when Explorer hides known extensions (SIGDN_NORMALDISPLAY does not).
    private static string GetFileName(IShellItem item)
    {
        if (item is IShellItem2 item2)
        {
            var key = FileNameKey;
            if (item2.GetString(ref key, out var pointer) >= 0 && pointer != IntPtr.Zero)
            {
                try
                {
                    var value = Marshal.PtrToStringUni(pointer);
                    if (!string.IsNullOrEmpty(value))
                        return value;
                }
                finally
                {
                    Marshal.FreeCoTaskMem(pointer);
                }
            }
        }
        return GetDisplayName(item, SigdnNormalDisplay);
    }

    private static long? GetSize(IShellItem item)
    {
        var key = SizeKey;
        return item is IShellItem2 item2 && item2.GetUInt64(ref key, out var size) >= 0 && size <= long.MaxValue
            ? (long)size
            : null;
    }

    private static DateTimeOffset? GetDate(IShellItem item, PropertyKey propertyKey)
    {
        var key = propertyKey;
        if (item is not IShellItem2 item2 || item2.GetFileTime(ref key, out var fileTime) < 0)
            return null;
        var ticks = ((long)fileTime.dwHighDateTime << 32) | (uint)fileTime.dwLowDateTime;
        if (ticks <= 0)
            return null;
        try
        {
            return DateTimeOffset.FromFileTime(ticks).ToUniversalTime();
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static string GetDisplayName(IShellItem item, uint kind)
    {
        if (item.GetDisplayName(kind, out var pointer) < 0 || pointer == IntPtr.Zero)
            return string.Empty;
        try
        {
            return Marshal.PtrToStringUni(pointer) ?? string.Empty;
        }
        finally
        {
            Marshal.FreeCoTaskMem(pointer);
        }
    }

    private static uint GetAttributes(IShellItem item, uint mask) =>
        item.GetAttributes(mask, out var attributes) >= 0 ? attributes : 0;

    private static void ThrowOnFailure(int hresult)
    {
        if (hresult < 0)
            Marshal.ThrowExceptionForHR(hresult, new IntPtr(-1));
    }

    private sealed record ShellHandle(ShellPortableDeviceSource Owner, IShellItem Item);

    /// <summary>Read-only, forward-only stream over a shell IStream; every read runs on the owner's STA thread.</summary>
    private sealed class ShellReadStream(ShellPortableDeviceSource owner, ISequentialStreamRead stream) : Stream
    {
        private readonly byte[] _chunk = new byte[ReadChunkSize];
        private int _disposed;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            if (owner._sta.IsCurrentThread)
            {
                var read = ReadOnSta(Math.Min(buffer.Length, _chunk.Length));
                _chunk.AsSpan(0, read).CopyTo(buffer);
                return read;
            }
            var copy = new byte[buffer.Length];
            var count = ReadAsync(copy.AsMemory(), CancellationToken.None).AsTask().GetAwaiter().GetResult();
            copy.AsSpan(0, count).CopyTo(buffer);
            return count;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            if (buffer.Length == 0)
                return 0;
            var count = Math.Min(buffer.Length, _chunk.Length);
            var read = await owner._sta.InvokeAsync(() => ReadOnSta(count), cancellationToken).ConfigureAwait(false);
            _chunk.AsSpan(0, read).CopyTo(buffer.Span);
            return read;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                owner._sta.Post(() =>
                {
                    owner._comObjects.Remove(stream);
                    try
                    {
                        Marshal.FinalReleaseComObject(stream);
                    }
                    catch (ArgumentException)
                    {
                    }
                });
            }
            base.Dispose(disposing);
        }

        private int ReadOnSta(int count)
        {
            ThrowOnFailure(stream.Read(_chunk, (uint)count, out var read));
            return (int)Math.Min(read, (uint)count);
        }
    }

    /// <summary>One STA thread with a work queue; the managed wait in the queue pumps COM messages.</summary>
    private sealed class StaThread : IDisposable
    {
        private readonly BlockingCollection<Action> _work = new();
        private readonly Thread _thread;

        public StaThread(string name)
        {
            _thread = new Thread(Run) { IsBackground = true, Name = name };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
        }

        public bool IsCurrentThread => Thread.CurrentThread == _thread;

        public Task<T> InvokeAsync<T>(Func<T> work, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled<T>(cancellationToken);
            var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!TryAdd(() =>
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        completion.TrySetCanceled(cancellationToken);
                        return;
                    }
                    try
                    {
                        completion.TrySetResult(work());
                    }
                    catch (Exception ex)
                    {
                        completion.TrySetException(ex);
                    }
                }))
                return Task.FromException<T>(new ObjectDisposedException(nameof(ShellPortableDeviceSource)));
            return completion.Task;
        }

        public void Post(Action work) => TryAdd(() =>
        {
            try
            {
                work();
            }
            catch (Exception)
            {
                // Cleanup work must never stop the queue.
            }
        });

        public void Dispose() => _work.CompleteAdding();

        private bool TryAdd(Action work)
        {
            try
            {
                return _work.TryAdd(work);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private void Run()
        {
            foreach (var work in _work.GetConsumingEnumerable())
                work();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct PropertyKey(Guid formatId, uint propertyId)
    {
        public readonly Guid FormatId = formatId;
        public readonly uint PropertyId = propertyId;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHGetKnownFolderItem(
        ref Guid folderId,
        uint flags,
        IntPtr token,
        ref Guid interfaceId,
        [MarshalAs(UnmanagedType.Interface)] out object item);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(
        string path,
        IntPtr bindContext,
        ref Guid interfaceId,
        [MarshalAs(UnmanagedType.Interface)] out object item);

    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        [PreserveSig]
        int BindToHandler(IntPtr bindContext, ref Guid handlerId, ref Guid interfaceId,
            [MarshalAs(UnmanagedType.Interface)] out object handler);

        [PreserveSig]
        int GetParent([MarshalAs(UnmanagedType.Interface)] out IShellItem parent);

        [PreserveSig]
        int GetDisplayName(uint kind, out IntPtr name);

        [PreserveSig]
        int GetAttributes(uint mask, out uint attributes);

        [PreserveSig]
        int Compare([MarshalAs(UnmanagedType.Interface)] IShellItem other, uint hint, out int order);
    }

    [ComImport, Guid("7E9FB0D3-919F-4307-AB2E-9B1860310C93"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem2 : IShellItem
    {
        [PreserveSig]
        new int BindToHandler(IntPtr bindContext, ref Guid handlerId, ref Guid interfaceId,
            [MarshalAs(UnmanagedType.Interface)] out object handler);

        [PreserveSig]
        new int GetParent([MarshalAs(UnmanagedType.Interface)] out IShellItem parent);

        [PreserveSig]
        new int GetDisplayName(uint kind, out IntPtr name);

        [PreserveSig]
        new int GetAttributes(uint mask, out uint attributes);

        [PreserveSig]
        new int Compare([MarshalAs(UnmanagedType.Interface)] IShellItem other, uint hint, out int order);

        [PreserveSig]
        int GetPropertyStore(int flags, ref Guid interfaceId, out IntPtr store);

        [PreserveSig]
        int GetPropertyStoreWithCreateObject(int flags, IntPtr createObject, ref Guid interfaceId, out IntPtr store);

        [PreserveSig]
        int GetPropertyStoreForKeys(IntPtr keys, uint keyCount, int flags, ref Guid interfaceId, out IntPtr store);

        [PreserveSig]
        int GetPropertyDescriptionList(ref PropertyKey keyType, ref Guid interfaceId, out IntPtr list);

        [PreserveSig]
        int Update(IntPtr bindContext);

        [PreserveSig]
        int GetProperty(ref PropertyKey key, IntPtr value);

        [PreserveSig]
        int GetCLSID(ref PropertyKey key, out Guid classId);

        [PreserveSig]
        int GetFileTime(ref PropertyKey key, out ComFileTime value);

        [PreserveSig]
        int GetInt32(ref PropertyKey key, out int value);

        [PreserveSig]
        int GetString(ref PropertyKey key, out IntPtr value);

        [PreserveSig]
        int GetUInt32(ref PropertyKey key, out uint value);

        [PreserveSig]
        int GetUInt64(ref PropertyKey key, out ulong value);

        [PreserveSig]
        int GetBool(ref PropertyKey key, out int value);
    }

    [ComImport, Guid("70629033-E363-4A28-A567-0DB78006E6D7"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IEnumShellItems
    {
        [PreserveSig]
        int Next(uint count, [MarshalAs(UnmanagedType.Interface)] out IShellItem item, out uint fetched);

        [PreserveSig]
        int Skip(uint count);

        [PreserveSig]
        int Reset();

        [PreserveSig]
        int Clone([MarshalAs(UnmanagedType.Interface)] out IEnumShellItems enumerator);
    }

    // IStream, declared only up to ISequentialStream::Read: this backend never calls Write, Seek, or SetSize.
    [ComImport, Guid("0000000C-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISequentialStreamRead
    {
        [PreserveSig]
        int Read([Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] buffer, uint count, out uint read);
    }
}
