using System.Runtime.InteropServices;
using CopyPaste.Core.Models;

namespace CopyPaste.App.Services;

public static class SystemActivityService
{
    public static TransferPerformanceMode Resolve(TransferPerformanceMode requested)
    {
        if (requested != TransferPerformanceMode.Automatic)
            return requested;
        if (IsForegroundWindowFullscreen())
            return TransferPerformanceMode.LowResource;
        return GetIdleTime() >= TimeSpan.FromMinutes(2)
            ? TransferPerformanceMode.FullSpeed
            : TransferPerformanceMode.Balanced;
    }

    public static TimeSpan GetIdleTime()
    {
        var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        return GetLastInputInfo(ref info)
            ? TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - info.Time))
            : TimeSpan.Zero;
    }

    private static bool IsForegroundWindowFullscreen()
    {
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero || !GetWindowRect(window, out var windowRect))
            return false;
        var monitor = MonitorFromWindow(window, 2);
        var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
            return false;
        const int tolerance = 2;
        return Math.Abs(windowRect.Left - info.Monitor.Left) <= tolerance
               && Math.Abs(windowRect.Top - info.Monitor.Top) <= tolerance
               && Math.Abs(windowRect.Right - info.Monitor.Right) <= tolerance
               && Math.Abs(windowRect.Bottom - info.Monitor.Bottom) <= tolerance;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo { public uint Size; public uint Time; }
    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left; public int Top; public int Right; public int Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo { public uint Size; public Rect Monitor; public Rect Work; public uint Flags; }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref LastInputInfo info);
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
}
