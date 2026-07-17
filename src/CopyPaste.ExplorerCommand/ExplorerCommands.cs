using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace CopyPaste.ExplorerCommand;

[ComVisible(true)]
[Guid("4F492155-4C1B-49A0-A4EE-552AD797EDC1")]
[ClassInterface(ClassInterfaceType.None)]
public sealed class CopyCommand : ExplorerCommandBase
{
    protected override string Title => "CopyPaste ile kopyala";
    protected override string Action => "--copy";
}

[ComVisible(true)]
[Guid("EF89AE7B-8A91-4F05-80EF-C89D7E5B345A")]
[ClassInterface(ClassInterfaceType.None)]
public sealed class PasteCommand : ExplorerCommandBase
{
    protected override string Title => "CopyPaste ile buraya yapıştır";
    protected override string Action => "--paste";
}

[ComVisible(false)]
[ClassInterface(ClassInterfaceType.None)]
public abstract class ExplorerCommandBase : IExplorerCommand
{
    protected abstract string Title { get; }
    protected abstract string Action { get; }

    public int GetTitle(IShellItemArray? selection, out nint name)
    {
        name = Marshal.StringToCoTaskMemUni(Title);
        return 0;
    }

    public int GetIcon(IShellItemArray? selection, out nint icon)
    {
        var executable = FindExecutable();
        icon = Marshal.StringToCoTaskMemUni(string.IsNullOrWhiteSpace(executable) ? string.Empty : executable);
        return 0;
    }

    public int GetToolTip(IShellItemArray? selection, out nint tooltip)
    {
        tooltip = nint.Zero;
        return HResult.NotImplemented;
    }

    public int GetCanonicalName(out Guid commandName)
    {
        commandName = GetType().GUID;
        return 0;
    }

    public int GetState(IShellItemArray? selection, bool okToBeSlow, out ExplorerCommandState state)
    {
        state = selection is null ? ExplorerCommandState.Disabled : ExplorerCommandState.Enabled;
        return 0;
    }

    public int Invoke(IShellItemArray selection, IBindCtx? bindContext)
    {
        try
        {
            var paths = GetPaths(selection);
            if (paths.Count == 0)
                return HResult.InvalidArgument;
            var executable = FindExecutable();
            if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
                return HResult.FileNotFound;
            var startInfo = new ProcessStartInfo(executable) { UseShellExecute = false };
            startInfo.ArgumentList.Add(Action);
            foreach (var path in Action == "--paste" ? paths.Take(1) : paths)
                startInfo.ArgumentList.Add(path);
            Process.Start(startInfo);
            return 0;
        }
        catch (Exception ex)
        {
            return Marshal.GetHRForException(ex);
        }
    }

    public int GetFlags(out ExplorerCommandFlags flags)
    {
        flags = ExplorerCommandFlags.Default;
        return 0;
    }

    public int EnumSubCommands(out nint commands)
    {
        commands = nint.Zero;
        return HResult.NotImplemented;
    }

    private static IReadOnlyList<string> GetPaths(IShellItemArray selection)
    {
        var paths = new List<string>();
        selection.GetCount(out var count);
        for (uint index = 0; index < count; index++)
        {
            selection.GetItemAt(index, out var item);
            try
            {
                item.GetDisplayName(ShellDisplayName.FileSystemPath, out var pointer);
                try
                {
                    var path = Marshal.PtrToStringUni(pointer);
                    if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                        paths.Add(path);
                }
                finally { Marshal.FreeCoTaskMem(pointer); }
            }
            finally
            {
                if (Marshal.IsComObject(item))
                    Marshal.ReleaseComObject(item);
            }
        }
        return paths;
    }

    private static string? FindExecutable()
    {
        var assemblyDirectory = Path.GetDirectoryName(typeof(ExplorerCommandBase).Assembly.Location);
        if (!string.IsNullOrWhiteSpace(assemblyDirectory))
        {
            var alongside = Path.Combine(assemblyDirectory, "CopyPaste.App.exe");
            if (File.Exists(alongside))
                return alongside;
        }
        return null;
    }
}

[ComImport]
[Guid("A08CE4D0-FA25-44AB-B57C-C7B1C323E0B9")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IExplorerCommand
{
    [PreserveSig] int GetTitle(IShellItemArray? selection, out nint name);
    [PreserveSig] int GetIcon(IShellItemArray? selection, out nint icon);
    [PreserveSig] int GetToolTip(IShellItemArray? selection, out nint tooltip);
    [PreserveSig] int GetCanonicalName(out Guid commandName);
    [PreserveSig] int GetState(IShellItemArray? selection, [MarshalAs(UnmanagedType.Bool)] bool okToBeSlow,
        out ExplorerCommandState state);
    [PreserveSig] int Invoke(IShellItemArray selection, IBindCtx? bindContext);
    [PreserveSig] int GetFlags(out ExplorerCommandFlags flags);
    [PreserveSig] int EnumSubCommands(out nint commands);
}

[ComImport]
[Guid("B63EA76D-1F85-456F-A19C-48159EFA858B")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IShellItemArray
{
    void BindToHandler(IBindCtx? bindContext, ref Guid handlerId, ref Guid interfaceId, out nint result);
    void GetPropertyStore(int flags, ref Guid interfaceId, out nint propertyStore);
    void GetPropertyDescriptionList(nint propertyKey, ref Guid interfaceId, out nint descriptions);
    void GetAttributes(uint attributes, out uint result);
    void GetCount(out uint count);
    void GetItemAt(uint index, [MarshalAs(UnmanagedType.Interface)] out IShellItem item);
    void EnumItems(out nint enumerator);
}

[ComImport]
[Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IShellItem
{
    void BindToHandler(IBindCtx? bindContext, ref Guid handlerId, ref Guid interfaceId, out nint result);
    void GetParent([MarshalAs(UnmanagedType.Interface)] out IShellItem parent);
    void GetDisplayName(ShellDisplayName displayName, out nint name);
    void GetAttributes(uint attributes, out uint result);
    void Compare([MarshalAs(UnmanagedType.Interface)] IShellItem item, uint hint, out int order);
}

public enum ExplorerCommandState
{
    Enabled = 0,
    Disabled = 1,
    Hidden = 2
}

[Flags]
public enum ExplorerCommandFlags
{
    Default = 0
}

public enum ShellDisplayName : uint
{
    FileSystemPath = 0x80058000
}

internal static class HResult
{
    public const int NotImplemented = unchecked((int)0x80004001);
    public const int InvalidArgument = unchecked((int)0x80070057);
    public const int FileNotFound = unchecked((int)0x80070002);
}
