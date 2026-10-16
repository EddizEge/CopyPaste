using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using CopyPaste.Core.Models;

namespace CopyPaste.Core.Services;

public interface IRecycleBinSender
{
    RecycleBinSendResult Send(string path, CancellationToken cancellationToken);
}

// Sends one selected root to the Recycle Bin with the shell IFileOperation engine, without any shell UI.
// Measured on Windows 11 with a throwaway probe on disposable files: a failure HRESULT from PreDeleteItem
// leaves that item in place but also cancels every later item of the same operation, so each root gets its
// own operation. A folder too large for the Recycle Bin falls back to child deletes without
// TSF_DELETE_RECYCLE_IF_POSSIBLE, which the sink refuses. A single file too large for the Recycle Bin still
// arrives with that flag and is then deleted permanently without another callback; the planner's capacity
// check prevents that case, and PostDeleteItem without a Recycle Bin item reports it honestly afterwards.
public sealed class ShellRecycleBinSender : IRecycleBinSender
{
    public const uint TransferSourceDeleteRecycleIfPossible = 0x80;

    private const uint FofSilent = 0x0004;
    private const uint FofNoConfirmation = 0x0010;
    private const uint FofAllowUndo = 0x0040;
    private const uint FofNoConfirmMkDir = 0x0200;
    private const uint FofNoErrorUi = 0x0400;
    private const uint FofxRecycleOnDelete = 0x00080000;
    // FOF_WANTNUKEWARNING is deliberately absent: it opens a confirmation dialog even with FOF_NOERRORUI.
    private const uint OperationFlags = FofAllowUndo | FofNoConfirmation | FofSilent | FofNoErrorUi
        | FofNoConfirmMkDir | FofxRecycleOnDelete;
    private const int SOk = 0;
    private const int EAbort = unchecked((int)0x80004004);
    private const int FileNotFound = unchecked((int)0x80070002);
    private const int CopyEngineUserCancelled = unchecked((int)0x80270000);
    private const uint SigdnFileSystemPath = 0x80058000;
    private static readonly Guid FileOperationClassId = new("3ad05575-8857-4850-9277-11b85bdb8e09");

    public static bool IsRecycleDeletionAllowed(uint transferSourceFlags) =>
        (transferSourceFlags & TransferSourceDeleteRecycleIfPossible) != 0;

    public static RecycleBinSendStatus Classify(
        bool sourceExists,
        bool deletionAllowed,
        bool recycledItemConfirmed,
        bool refused,
        bool canceled,
        bool permanentDeletionObserved)
    {
        if (permanentDeletionObserved)
            return RecycleBinSendStatus.DeletedPermanently;
        if (!sourceExists)
        {
            if (recycledItemConfirmed)
                return RecycleBinSendStatus.Recycled;
            return deletionAllowed ? RecycleBinSendStatus.DeletedPermanently : RecycleBinSendStatus.Failed;
        }
        if (canceled)
            return RecycleBinSendStatus.Canceled;
        return refused ? RecycleBinSendStatus.NotRecyclable : RecycleBinSendStatus.Failed;
    }

    public RecycleBinSendResult Send(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            return SendOnStaThread(path, cancellationToken);

        RecycleBinSendResult? result = null;
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = SendOnStaThread(path, cancellationToken);
            }
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
            }
        })
        {
            IsBackground = true,
            Name = "CopyPaste Recycle Bin"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        failure?.Throw();
        return result!;
    }

    private static RecycleBinSendResult SendOnStaThread(string path, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return new(RecycleBinSendStatus.Canceled, CopyEngineUserCancelled, null, null, false);
        if (!Path.Exists(path))
            return new(RecycleBinSendStatus.Failed, FileNotFound, null, null, false);

        var sink = new DeleteProgressSink(path, cancellationToken);
        IFileOperation? operation = null;
        IShellItem? item = null;
        int hr;
        try
        {
            var type = Type.GetTypeFromCLSID(FileOperationClassId, throwOnError: true)!;
            operation = (IFileOperation)Activator.CreateInstance(type)!;
            var shellItemId = typeof(IShellItem).GUID;
            hr = SHCreateItemFromParsingName(path, IntPtr.Zero, ref shellItemId, out item);
            if (hr >= 0)
                hr = operation.SetOperationFlags(OperationFlags);
            uint cookie = 0;
            var advised = false;
            if (hr >= 0)
            {
                hr = operation.Advise(sink, out cookie);
                advised = hr >= 0;
            }
            if (hr >= 0)
                hr = operation.DeleteItem(item, IntPtr.Zero);
            if (hr >= 0)
                hr = operation.PerformOperations();
            if (advised)
                operation.Unadvise(cookie);
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException)
        {
            hr = ex.HResult;
        }
        finally
        {
            if (item is not null)
                Marshal.FinalReleaseComObject(item);
            if (operation is not null)
                Marshal.FinalReleaseComObject(operation);
        }

        var recycledPath = sink.RecycledItemPath is { } candidate && IsRecycleBinItem(candidate)
            && Path.Exists(candidate)
                ? candidate
                : null;
        var sourceExists = Path.Exists(path);
        var status = Classify(sourceExists, sink.DeletionAllowed, recycledPath is not null,
            sink.Refused, sink.Canceled, sink.PermanentDeletionObserved);
        // A failure whose source vanished without our deletion is reported as missing, never as "still in place".
        var reportedHr = status == RecycleBinSendStatus.Failed && !sourceExists
            ? FileNotFound
            : sink.FailureHResult ?? (hr < 0 ? hr : SOk);
        return new(status, reportedHr, sink.FirstPreDeleteFlags, recycledPath, sink.SomeContentRecycled);
    }

    private static bool IsRecycleBinItem(string path) =>
        path.Contains(@"\$Recycle.Bin\", StringComparison.OrdinalIgnoreCase);

    private static bool PathEquals(string left, string right) =>
        string.Equals(Path.TrimEndingDirectorySeparator(left), Path.TrimEndingDirectorySeparator(right),
            StringComparison.OrdinalIgnoreCase);

    private static bool IsSameOrWithin(string path, string root) =>
        PathEquals(path, root)
        || path.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);

    private static string? GetFileSystemPath(IShellItem? item)
    {
        if (item is null || item.GetDisplayName(SigdnFileSystemPath, out var name) < 0)
            return null;
        try
        {
            return Marshal.PtrToStringUni(name);
        }
        finally
        {
            Marshal.FreeCoTaskMem(name);
        }
    }

    // Each callback argument entering managed code adds one reference to its wrapper; release exactly that one.
    private static void Release(IShellItem? item)
    {
        if (item is not null && Marshal.IsComObject(item))
            Marshal.ReleaseComObject(item);
    }

    [ComVisible(true)]
    private sealed class DeleteProgressSink(string root, CancellationToken cancellationToken)
        : IFileOperationProgressSink
    {
        public uint? FirstPreDeleteFlags { get; private set; }
        public bool DeletionAllowed { get; private set; }
        public bool Refused { get; private set; }
        public bool Canceled { get; private set; }
        public bool SomeContentRecycled { get; private set; }
        public bool PermanentDeletionObserved { get; private set; }
        public string? RecycledItemPath { get; private set; }
        public int? FailureHResult { get; private set; }

        public int PreDeleteItem(uint flags, IShellItem item)
        {
            try
            {
                FirstPreDeleteFlags ??= flags;
                if (cancellationToken.IsCancellationRequested)
                {
                    Canceled = true;
                    return CopyEngineUserCancelled;
                }
                var itemPath = GetFileSystemPath(item);
                if (!IsRecycleDeletionAllowed(flags) || itemPath is null || !IsSameOrWithin(itemPath, root))
                {
                    Refused = true;
                    return EAbort;
                }
                DeletionAllowed = true;
                return SOk;
            }
            finally
            {
                Release(item);
            }
        }

        public int PostDeleteItem(uint flags, IShellItem item, int hrDelete, IShellItem? newlyCreated)
        {
            try
            {
                if (hrDelete < 0)
                {
                    if (!Refused && !Canceled)
                        FailureHResult ??= hrDelete;
                    return SOk;
                }
                var itemPath = GetFileSystemPath(item);
                var createdPath = GetFileSystemPath(newlyCreated);
                if (itemPath is not null && PathEquals(itemPath, root))
                {
                    RecycledItemPath = createdPath;
                    return SOk;
                }
                if (createdPath is not null)
                {
                    SomeContentRecycled = true;
                    return SOk;
                }
                // A child removed without a Recycle Bin item: report it and stop the rest of this operation.
                PermanentDeletionObserved = true;
                return EAbort;
            }
            finally
            {
                Release(item);
                Release(newlyCreated);
            }
        }

        public int StartOperations() => SOk;
        public int FinishOperations(int hrResult) => SOk;
        public int PreRenameItem(uint flags, IntPtr item, IntPtr newName) => SOk;
        public int PostRenameItem(uint flags, IntPtr item, IntPtr newName, int hrRename, IntPtr created) => SOk;
        public int PreMoveItem(uint flags, IntPtr item, IntPtr destination, IntPtr newName) => SOk;
        public int PostMoveItem(uint flags, IntPtr item, IntPtr destination, IntPtr newName, int hrMove,
            IntPtr created) => SOk;
        public int PreCopyItem(uint flags, IntPtr item, IntPtr destination, IntPtr newName) => SOk;
        public int PostCopyItem(uint flags, IntPtr item, IntPtr destination, IntPtr newName, int hrCopy,
            IntPtr created) => SOk;
        public int PreNewItem(uint flags, IntPtr destination, IntPtr newName) => SOk;
        public int PostNewItem(uint flags, IntPtr destination, IntPtr newName, IntPtr templateName,
            uint attributes, int hrNew, IntPtr item) => SOk;
        public int UpdateProgress(uint workTotal, uint workSoFar) => SOk;
        public int ResetTimer() => SOk;
        public int PauseTimer() => SOk;
        public int ResumeTimer() => SOk;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(
        string path,
        IntPtr bindContext,
        ref Guid interfaceId,
        [MarshalAs(UnmanagedType.Interface, IidParameterIndex = 2)] out IShellItem item);

    [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        [PreserveSig] int BindToHandler(IntPtr bindContext, ref Guid handler, ref Guid interfaceId, out IntPtr result);
        [PreserveSig] int GetParent(out IntPtr parent);
        [PreserveSig] int GetDisplayName(uint sigdnName, out IntPtr name);
        [PreserveSig] int GetAttributes(uint mask, out uint attributes);
        [PreserveSig] int Compare(IntPtr other, uint hint, out int order);
    }

    [ComImport, Guid("04b0f1a7-9490-44bc-96e1-4296a31252e2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOperationProgressSink
    {
        [PreserveSig] int StartOperations();
        [PreserveSig] int FinishOperations(int hrResult);
        [PreserveSig] int PreRenameItem(uint flags, IntPtr item, IntPtr newName);
        [PreserveSig] int PostRenameItem(uint flags, IntPtr item, IntPtr newName, int hrRename, IntPtr created);
        [PreserveSig] int PreMoveItem(uint flags, IntPtr item, IntPtr destination, IntPtr newName);
        [PreserveSig] int PostMoveItem(uint flags, IntPtr item, IntPtr destination, IntPtr newName, int hrMove,
            IntPtr created);
        [PreserveSig] int PreCopyItem(uint flags, IntPtr item, IntPtr destination, IntPtr newName);
        [PreserveSig] int PostCopyItem(uint flags, IntPtr item, IntPtr destination, IntPtr newName, int hrCopy,
            IntPtr created);
        [PreserveSig] int PreDeleteItem(uint flags, IShellItem item);
        [PreserveSig] int PostDeleteItem(uint flags, IShellItem item, int hrDelete, IShellItem? newlyCreated);
        [PreserveSig] int PreNewItem(uint flags, IntPtr destination, IntPtr newName);
        [PreserveSig] int PostNewItem(uint flags, IntPtr destination, IntPtr newName, IntPtr templateName,
            uint attributes, int hrNew, IntPtr item);
        [PreserveSig] int UpdateProgress(uint workTotal, uint workSoFar);
        [PreserveSig] int ResetTimer();
        [PreserveSig] int PauseTimer();
        [PreserveSig] int ResumeTimer();
    }

    // Only the members used here are typed; the others keep their vtable slots.
    [ComImport, Guid("947aab5f-0a5c-4c13-b4d6-4bf7836fc9f8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOperation
    {
        [PreserveSig] int Advise(IFileOperationProgressSink sink, out uint cookie);
        [PreserveSig] int Unadvise(uint cookie);
        [PreserveSig] int SetOperationFlags(uint flags);
        [PreserveSig] int SetProgressMessage(IntPtr message);
        [PreserveSig] int SetProgressDialog(IntPtr dialog);
        [PreserveSig] int SetProperties(IntPtr properties);
        [PreserveSig] int SetOwnerWindow(IntPtr owner);
        [PreserveSig] int ApplyPropertiesToItem(IntPtr item);
        [PreserveSig] int ApplyPropertiesToItems(IntPtr items);
        [PreserveSig] int RenameItem(IntPtr item, IntPtr newName, IntPtr sink);
        [PreserveSig] int RenameItems(IntPtr items, IntPtr newName);
        [PreserveSig] int MoveItem(IntPtr item, IntPtr destination, IntPtr newName, IntPtr sink);
        [PreserveSig] int MoveItems(IntPtr items, IntPtr destination);
        [PreserveSig] int CopyItem(IntPtr item, IntPtr destination, IntPtr copyName, IntPtr sink);
        [PreserveSig] int CopyItems(IntPtr items, IntPtr destination);
        [PreserveSig] int DeleteItem(IShellItem item, IntPtr sink);
        [PreserveSig] int DeleteItems(IntPtr items);
        [PreserveSig] int NewItem(IntPtr destination, uint attributes, IntPtr name, IntPtr templateName, IntPtr sink);
        [PreserveSig] int PerformOperations();
        [PreserveSig] int GetAnyOperationsAborted([MarshalAs(UnmanagedType.Bool)] out bool aborted);
    }
}
