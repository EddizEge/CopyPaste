using System.Runtime.InteropServices;
using WinRT.Interop;

namespace CopyPaste.App.Services;

public sealed class TrayIconService : IDisposable
{
    private const uint CallbackMessage = 0x8000 + 128;
    private const uint NimAdd = 0x00000000;
    private const uint NimDelete = 0x00000002;
    private const uint NimSetVersion = 0x00000004;
    private const uint NifMessage = 0x00000001;
    private const uint NifIcon = 0x00000002;
    private const uint NifTip = 0x00000004;
    private const uint NotifyIconVersion4 = 4;
    private const int GwlWndProc = -4;
    private const int SwHide = 0;
    private const int SwShow = 5;
    private const uint WmLButtonUp = 0x0202;
    private const uint WmLButtonDblClk = 0x0203;
    private const uint WmContextMenu = 0x007B;
    private const int IdiApplication = 32512;

    private readonly Microsoft.UI.Xaml.Window _window;
    private readonly nint _windowHandle;
    private readonly WindowProcedure _windowProcedure;
    private readonly nint _previousWindowProcedure;
    private NotifyIconData _iconData;
    private bool _iconVisible;
    private bool _disposed;

    public TrayIconService(Microsoft.UI.Xaml.Window window)
    {
        _window = window;
        _windowHandle = WindowNative.GetWindowHandle(window);
        _windowProcedure = WindowProc;
        _previousWindowProcedure = SetWindowLongPtr(
            _windowHandle,
            GwlWndProc,
            Marshal.GetFunctionPointerForDelegate(_windowProcedure));

        _iconData = new NotifyIconData
        {
            cbSize = (uint)Marshal.SizeOf<NotifyIconData>(),
            hWnd = _windowHandle,
            uID = 1,
            uFlags = NifMessage | NifIcon | NifTip,
            uCallbackMessage = CallbackMessage,
            hIcon = LoadApplicationIcon(),
            szTip = "CopyPaste — pencereyi açmak için tıklayın",
            uVersionOrTimeout = NotifyIconVersion4,
            szInfo = string.Empty,
            szInfoTitle = string.Empty
        };
    }

    public bool HideToTray()
    {
        if (!EnsureIcon())
            return false;

        ShowWindow(_windowHandle, SwHide);
        return true;
    }

    public void Restore()
    {
        ShowWindow(_windowHandle, SwShow);
        SetForegroundWindow(_windowHandle);
        _window.Activate();
        RemoveIcon();
    }

    private bool EnsureIcon()
    {
        if (_iconVisible)
            return true;

        if (!ShellNotifyIcon(NimAdd, ref _iconData))
            return false;

        _iconVisible = true;
        ShellNotifyIcon(NimSetVersion, ref _iconData);
        return true;
    }

    private static nint LoadApplicationIcon()
    {
        var executable = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(executable))
        {
            var large = new nint[1];
            var small = new nint[1];
            if (ExtractIconEx(executable, 0, large, small, 1) > 0)
                return small[0] != nint.Zero ? small[0] : large[0];
        }
        return LoadIcon(nint.Zero, new nint(IdiApplication));
    }

    private void RemoveIcon()
    {
        if (!_iconVisible)
            return;

        ShellNotifyIcon(NimDelete, ref _iconData);
        _iconVisible = false;
    }

    private nint WindowProc(nint hwnd, uint message, nint wParam, nint lParam)
    {
        if (message == CallbackMessage)
        {
            var mouseMessage = (uint)(lParam.ToInt64() & 0xFFFF);
            if (mouseMessage is WmLButtonUp or WmLButtonDblClk or WmContextMenu)
                Restore();
        }

        return CallWindowProc(_previousWindowProcedure, hwnd, message, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        RemoveIcon();
        if (_previousWindowProcedure != nint.Zero)
            SetWindowLongPtr(_windowHandle, GwlWndProc, _previousWindowProcedure);
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint cbSize;
        public nint hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public nint hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uVersionOrTimeout;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public nint hBalloonIcon;
    }

    private delegate nint WindowProcedure(nint hwnd, uint message, nint wParam, nint lParam);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "Shell_NotifyIconW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShellNotifyIcon(uint message, ref NotifyIconData data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "LoadIconW")]
    private static extern nint LoadIcon(nint instance, nint iconName);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(string file, int iconIndex, nint[] largeIcons, nint[] smallIcons, uint icons);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(nint hwnd, int index, nint newLong);

    [DllImport("user32.dll", EntryPoint = "CallWindowProcW")]
    private static extern nint CallWindowProc(nint previous, nint hwnd, uint message, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint hwnd, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint hwnd);
}
